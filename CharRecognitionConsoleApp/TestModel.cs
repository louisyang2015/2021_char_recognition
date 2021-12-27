using CharRecognitionLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CharRecognitionConsoleApp
{
    internal class TestModel
    {
        #region Input from user

        string label;
        int start_file_number, end_file_number;

        string get_string_input(string prompt)
        {
            Console.Write(prompt);
            return Console.ReadLine();
        }

        int get_integer_input(string prompt)
        {
            while (true)
            {
                Console.Write(prompt);
                var number_str = Console.ReadLine();

                int result;
                bool success = int.TryParse(number_str, out result);

                if (success)
                    return result;
            }
        }

        void get_input()
        {
            label = get_string_input("Enter label: ");
            start_file_number = get_integer_input("Enter start file number: ");
            end_file_number = get_integer_input("Enter end file number: ");
        }

        #endregion


        #region Misidentified Image Display
        /// <summary>
        /// Prints an image and its best match template.
        /// </summary>
        void print_misidentified_image(byte[,] image, Template template)
        {
            int height = image.GetUpperBound(0) + 1;
            int width = image.GetUpperBound(1) + 1;

            // Header
            Console.WriteLine("Image".PadRight(width + 4) 
                + "Best Match Template");

            // It's assumed the image and template has the same number of rows
            // Print the two images, one row at a time.

            for (int r = 0; r < height; r++)
            {
                // Print "image"
                for (int c = 0; c < width; c++)
                {
                    byte bw_value = image[r, c];

                    // The black and white image stores values differently
                    // from the template.
                    // 0 is background, and anything else is foreground

                    if (bw_value == 0)
                        print_image_value(255);
                    else
                        print_image_value(0);
                }

                // Print spaces
                Console.Write("".PadRight(4));

                // Print "template"
                for (int c = 0; c < width; c++)
                {
                    print_image_value(template[r, c]);
                }

                Console.WriteLine();
            }


            Console.WriteLine();
        }


        /// <summary>
        /// Prints a value such that: 0 prints as '*', 1 ~ 9 
        /// are printed as they are, and anything larger than 9
        /// is printed as '_'.
        /// </summary>
        void print_image_value(int value)
        {
            if (value == 0)
                Console.Write('*');
            else if (value > 0 && value <= 9)
                Console.Write(value.ToString());
            else
                Console.Write('_');
        }

        #endregion


        public void Start()
        {
            get_input();


            try
            {
                var ti_bytes = Util.Download_From_Storage("char-recognition", "template_indices/all_labels.bin");
                var tc_bytes = Util.Download_From_Storage("char-recognition", "template_collections/all_labels.bin");
                var recog = new RecognitionModel(ti_bytes, tc_bytes);

                var template_collection = TemplateCollection.FromBytes(tc_bytes);


                int images_per_file = ImageData.MaxImagesPerFile;

                // Buffer to hold image being recognized
                byte[,] image = new byte[8, 8];
                int image_length = 8; // image is 8 bytes long

                int image_number = start_file_number;


                for (int i = start_file_number; i <= end_file_number; i += images_per_file)
                {
                    // Read the i-th file into images
                    var bytes = ImageData.Get_Label_Bytes("standard/", label, i);

                    if (bytes == null)
                    {
                        Console.WriteLine("No data to test.");
                        break;
                    }

                    int offset = 0;

                    while (offset + image_length - 1 < bytes.Length)
                    {
                        // Extract image from file -> "image"
                        BlackAndWhite_Image.Decode_from_bytes_to_BW_Image(bytes, offset, image);

                        // Recognize an image
                        var label2 = recog.Recognize_BW_Image(image);

                        if (label2 != null && label2 != label 
                            && recog.LatestBestMatch != null)
                        {
                            Console.WriteLine($"Image #{image_number} is incorrectly identified.");
                            var t = template_collection.GetTemplate(recog.LatestBestMatch.Value);
                            print_misidentified_image(image, t);
                        }

                        // For the next loop:
                        offset += image_length;
                        image_number++;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Excpetion: " + ex.Message);
            }

            Console.WriteLine();
        }
    }
}
