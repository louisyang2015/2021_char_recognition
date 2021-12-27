using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

using CharRecognitionLib;



namespace CharRecognitionFunctions
{
    public static class UpdateTemplateCollection
    {
        class Input
        {
            public string Label = "";
        }

        class Output
        {
            public bool Success;
            public int Num_templates;
            public string Error;
        }



        [FunctionName("UpdateTemplateCollection")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            ////////////////////////////////////////////////
            // Extract Input
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var input = JsonConvert.DeserializeObject<Input>(requestBody);

            string label = input.Label;

            ////////////////////////////////////////////////

            bool success = true;
            string error = "";
            int num_templates = 0;

            try
            {
                // Read training data from storage
                var bytes = Util.Download_From_Storage("char-recognition",
                    "template_collections/all_candidates.bin");

                if (bytes == null)
                    throw new Exception(@"The file ""all_candidates.bin"" is missing.");

                var tc = TemplateCollection.FromBytes(bytes);

                // Train one label
                var trained_templates = Training.Train_One_Label(tc, label);

                if (trained_templates == null)
                    throw new Exception("No data for training. Training failed.\n");

                if (trained_templates.Count == 0)
                    throw new Exception("Training failed to produce any templates.\n");

                num_templates = trained_templates.Count;

                // Put "trained_templates" into a TemplateCollection
                var trained_tc = new TemplateCollection(8, 8);
                trained_tc.Add(trained_templates, label);

                // Save "trained_tc" to storage under
                // "template_collections/label.bin"
                var blob_name = "template_collections/" + label + ".bin";

                Util.Upload_To_Storage("char-recognition", blob_name,
                    trained_tc.ToBytes().ToArray());

            }
            catch(Exception ex)
            {
                success = false;
                error = "Error while updating the templates. " + ex.Message;
            }

            ////////////////////////////////////////////////
            // Format output

            var output = new Output();
            output.Success = success;
            output.Error = error;
            output.Num_templates = num_templates;

            var json = JsonConvert.SerializeObject(output);

            return new OkObjectResult(json);
        }
    }
}
