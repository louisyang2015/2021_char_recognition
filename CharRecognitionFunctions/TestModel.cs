using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Collections.Generic;

using CharRecognitionLib;

namespace CharRecognitionFunctions
{
    public static class TestModel
    {
        class Input
        {
            public string label = "";
            public int start_file_number = 0, end_file_number = 0;
        }

        class Output
        {
            public bool success;
            public int correct, incorrect, unknown;
            public List<int> misclassified;
        }


        // For testing, it's easier to constantly reload models to have
        // the most updated model
        // static RecognitionModel recog;


        [FunctionName("TestModel")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            ////////////////////////////////////////////////
            // Extract Input
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var data = JsonConvert.DeserializeObject<Input>(requestBody);

            string label = data.label;
            int start_file_number = data.start_file_number;
            int end_file_number = data.end_file_number;


            ////////////////////////////////////////////////
            // Test model using files specified in the input

            // Reloads the model for every run
            var ti = Util.Download_From_Storage("char-recognition", "template_indices/all_labels.bin");
            var tc = Util.Download_From_Storage("char-recognition", "template_collections/all_labels.bin");

            var recog = new RecognitionModel(ti, tc);
                        
            int images_per_file = ImageData.MaxImagesPerFile;

            bool success = true;
            int correct = 0;
            int incorrect = 0;
            int unknown = 0;
            var misclassifieds = new List<int>();
            int image_number = start_file_number;

            // Buffer to hold image being recognized
            byte[,] image = new byte[8, 8];
            int image_length = 8; // image is 8 bytes long

            try
            {
                for (int i = start_file_number; i <= end_file_number; i += images_per_file)
                {
                    // Read the i-th file into images
                    var bytes = ImageData.Get_Label_Bytes("standard/", label, i);

                    if (bytes == null)
                    {
                        success = false;
                        break;
                    }

                    int offset = 0;

                    while (offset + image_length - 1 < bytes.Length)
                    {
                        // Extract image from file -> "image"
                        BlackAndWhite_Image.Decode_from_bytes_to_BW_Image(bytes, offset, image);

                        // Recognize an image
                        var label2 = recog.Recognize_BW_Image(image);

                        if (label2 == null)
                        {
                            unknown++;

                            if (misclassifieds.Count < 100)
                                misclassifieds.Add(image_number);
                        }
                        else if (label == label2)
                            correct++;
                        else
                        {
                            incorrect++;

                            if (misclassifieds.Count < 100)
                                misclassifieds.Add(image_number);
                        }

                        // For the next loop:
                        offset += image_length;
                        image_number++;
                    }
                }
            }
            catch
            {
                success = false;
            }


            ////////////////////////////////////////////////
            // Format output

            var output = new Output();
            output.success = success;

            if (success)
            {
                output.correct = correct;
                output.incorrect = incorrect;
                output.unknown = unknown;
                output.misclassified = misclassifieds;
            }

            var json = JsonConvert.SerializeObject(output);

            return new OkObjectResult(json);
        }
    }
}
