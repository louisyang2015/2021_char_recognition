using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using CharRecognitionLib;


namespace CharRecognitionConsoleApp
{
    class Program
    {
        static void Main(string[] args)
        {
            // test_template();

            while (true)
            {
                Console.WriteLine("Main menu:");
                Console.WriteLine("    1. Upload MNIST Data");
                Console.WriteLine("    2. Standardize one image file");
                Console.WriteLine("    3. Standardize ALL image files");
                Console.WriteLine(@"    4. Generate ""template_collections/all_candidates.bin""");
                Console.WriteLine("    5. Retrain model for one label");
                Console.WriteLine("    6. Test model");
                Console.WriteLine("    7. Print training image numbers");
                Console.WriteLine("    8. Quit");

                int user_input = get_user_choice("? ", 1, 8);

                if (user_input == 1)
                {
                    Console.WriteLine();
                    upload_mnist_data();
                }

                else if (user_input == 2)
                {
                    Console.WriteLine();
                    standardize_image();
                }

                else if (user_input == 3)
                {
                    Console.WriteLine();
                    var task = new Standardize_All_Images_Task();
                    task.Start();
                }

                else if (user_input == 4)
                    generate_all_template_candidates();

                else if (user_input == 5)
                    retrain_one_label();

                else if (user_input == 6)
                {
                    var test = new TestModel();
                    test.Start();
                }

                else if (user_input == 7)
                    print_candidate_numbers();

                else if (user_input == 8)
                    return;
            }
        }


        /// <summary>
        /// Get user input between "low" and "high".
        /// </summary>
        static int get_user_choice(string prompt, int low, int high)
        {
            while (true)
            {
                Console.Write(prompt);
                string user_choice_str = Console.ReadLine();

                int user_choice = 0;
                if (int.TryParse(user_choice_str, out user_choice))
                {
                    if (low <= user_choice && user_choice <= high)
                        return user_choice;
                }
            }
        }


        /// <summary>
        /// Upload MNIST data from disk to Azure storage.
        /// </summary>
        static void upload_mnist_data()
        {
            Console.Write("Enter the directory containing the four unzipped MNIST files: ");
            string data_dir = Console.ReadLine();

            ////////////////////////////////////////////////////////
            // Check files exist:

            string[] files = { "t10k-images.idx3-ubyte", "t10k-labels.idx1-ubyte",
                "train-images.idx3-ubyte", "train-labels.idx1-ubyte" };

            foreach (var f in files)
            {
                string full_path = data_dir + '/' + f;
                if (File.Exists(full_path) == false)
                {
                    Console.WriteLine("The file \"{0}\" does not exist", full_path);
                    return;
                }
            }


            /////////////////////////////////////////////////////
            // Read from disk into memory (variable "data")
            Console.WriteLine("Reading data from disk...");

            string[] image_files = {
                data_dir + '/' + files[0], data_dir + '/' + files[2]
            };

            string[] label_files = {
                data_dir + '/' + files[1], data_dir + '/' + files[3]
            };

            var data = new List<byte>[10];

            // data[4] will be a list of "byte" for images of the digit "4"

            for (int i = 0; i < data.Length; i++)
                data[i] = new List<byte>();

            for (int i = 0; i < image_files.Length; i++)
            {
                var image_bytes = File.ReadAllBytes(image_files[i]);
                var label_bytes = File.ReadAllBytes(label_files[i]);

                // The label data starts at index 8
                // The image data starts at index 16. Each image is 28 x 28
                int label_index = 8;
                int image_index = 16;

                while (label_index < label_bytes.Length)
                {
                    // Check that label is 0 ~ 9
                    int label = label_bytes[label_index];
                    if (label < 0 || label > 9)
                        throw new Exception("MNIST data corruption. Encountered a label that is not between 0 and 9.");

                    // Add "image_bytes[image_index...]" to data[label]
                    data[label].AddRange(image_bytes[image_index..(image_index + 28 * 28)]);

                    label_index++;
                    image_index += 28 * 28;
                }
            }


            /////////////////////////////////////////////////////
            // Upload from "data" to Azure storage

            for (int label = 0; label < data.Length; label++)
            {
                string label_str = label.ToString();

                Console.WriteLine("Uploading " + label_str + "...");

                ImageData.Add_Images(label_str, data[label].ToArray(), "G", 28, 28);
            }
        }


        /// <summary>
        /// Standardize one set of image.
        /// </summary>
        static void standardize_image()
        {
            Console.Write("Enter label: ");
            string label = Console.ReadLine();

            string image_number_str;
            int image_number;

            do
            {
                Console.Write("Enter image number: ");
                image_number_str = Console.ReadLine();
            } while (int.TryParse(image_number_str, out image_number) == false);

            ImageData.StandardizeImages(label, image_number);
        }
    

        /// <summary>
        /// Generate "template_collections/all_candidates.bin"
        /// </summary>
        static void generate_all_template_candidates()
        {
            var candidates = Training.Get_All_Candidate_IDs();
            Training.Create_candidate_template_collection(candidates.Values.ToArray());

            Console.WriteLine(@"""template_collections/all_candidates.bin"" has been updated.\n");
        }


        /// <summary>
        /// Trains a model using data from just one label.
        /// </summary>
        static void retrain_one_label()
        {
            Console.Write("Enter label: ");
            string label = Console.ReadLine();

            var bytes = Util.Download_From_Storage("char-recognition",
                "template_collections/all_candidates.bin");

            var tc = TemplateCollection.FromBytes(bytes);

            var trained_templates = Training.Train_One_Label(tc, label);

            if (trained_templates == null)
            {
                Console.WriteLine("No data for training. Training failed.\n");
                return;
            }

            if (trained_templates.Count == 0)
            {
                Console.WriteLine("Training failed to produce any templates.\n");
                return;
            }

            // Put "trained_templates" into a TemplateCollection
            var trained_tc = new TemplateCollection(8, 8);
            trained_tc.Add(trained_templates, label);

            // Save "trained_tc" to storage under
            // "template_collections/label.bin"
            var blob_name = "template_collections/" + label + ".bin";

            Util.Upload_To_Storage("char-recognition", blob_name,
                trained_tc.ToBytes().ToArray());

            // The whole model consist of this one label, so this is 
            // also the "all_labels" collection
            Util.Upload_To_Storage("char-recognition",
                "template_collections/all_labels.bin",
                trained_tc.ToBytes().ToArray());

            // Build an index and save to Azure
            var trained_tc_index = new TemplateIndex(trained_tc.GetAllTemplates());

            Util.Upload_To_Storage("char-recognition",
                "template_indices/all_labels.bin",
                trained_tc_index.ToBytes());

            Console.WriteLine("The model has been retrained to "
                + $"consist of only '{label}'. \n");
        }


        /// <summary>
        /// Ask the user for a label and print what images in that 
        /// label's data set is being used for training.
        /// </summary>
        static void print_candidate_numbers()
        {
            Console.Write("Enter label name: ");
            string label = Console.ReadLine();

            var c = new TemplateCandidates(label);

            if (c.Count == 0)
                Console.WriteLine($"Label \"{label}\" has no training data.");
            else
            {
                Console.Write($"Label \"{label}\" training data: ");

                var ids = c.GetIds();

                foreach (var id in ids)
                    Console.Write(id.ToString() + " ");

                Console.WriteLine();
            }
        }
    }
}
