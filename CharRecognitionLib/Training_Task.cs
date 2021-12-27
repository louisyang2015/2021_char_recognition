using Azure.Storage.Blobs;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CharRecognitionLib
{
    public class New_Training_Data
    {
        public string Label { get; set; }

        // Image Numbers
        public int[] New_data { get; set; }
    }


    /// <summary>
    /// Routines for training the recognition model.
    /// </summary>
    public class Training
    {
        /// <summary>
        /// Return all TemplateCandidates objects, as a 
        /// (label, TemplateCandidates) dictionary.
        /// </summary>
        public static Dictionary<string, TemplateCandidates> Get_All_Candidate_IDs()
        {
            var labels = ImageData.Get_All_Labels();
            var return_val = new Dictionary<string, TemplateCandidates>();

            foreach (var label in labels)
            {
                var candidate = new TemplateCandidates(label);
                return_val.Add(label, candidate);
            }

            return return_val;
        }


        /// <summary>
        /// Merge the images mentioned by various candidate numbers into
        /// a single template collection file.
        /// </summary>
        public static void Create_candidate_template_collection(
            TemplateCandidates[] candidates)
        {
            // Template attributes:
            const int image_bytes = 8; // image size in bytes
            const int image_size = 8; // image width and height

            var tc = new TemplateCollection(image_size, image_size);

            foreach (var candidate in candidates)
            {
                // Read candidate numbers
                var ids = candidate.GetIds();
                if (ids.Count == 0)
                    continue;

                ids.Sort();

                // Extract candidate images
                var label = candidate.Label;
                var bytes = ImageData.Get_Label_Bytes_By_Numbers("standard/", label, ids.ToArray());

                // Build up a set of templates
                var templates = new Template[ids.Count];

                for (int i = 0; i < templates.Length; i++)
                    templates[i] = new Template(bytes,
                        i * image_bytes,
                        image_size, image_size);

                tc.Add(templates, label);
            }

            // Save template collection to storage
            var blob_name = "template_collections/all_candidates.bin";

            Util.Upload_To_Storage("char-recognition", blob_name,
                tc.ToBytes().ToArray());
        }


        /// <summary>
        /// Trains the recognition model for one set of templates. 
        /// Returns null if there is no training data for "label".
        /// </summary>
        public static List<Template> Train_One_Label(
            TemplateCollection training_data, string label)
        {
            // Summary:
            // The training process disables neighborhood pixels to
            // prevent mis-identification.
            // In addition, the number of templates representing
            // "label" will be reduced if possible.

            // Split training_data into two sets: label, and non-label
            var label_data = new List<Template>();
            var non_label_data = new List<Template>();

            training_data.Split_templates_into_two_groups(label,
                label_data, non_label_data);

            if (label_data.Count == 0) return null;


            // For the label data, adjust neighbor weights so  
            // that non-label images will not match against the
            // label images.
            foreach(var t in label_data)
                Template.Train_to_reject(t, non_label_data);


            // check
            //int count = 0;
            //int total_diff = 0;

            //foreach (var t in label_data)
            //{
            //    foreach (var t2 in non_label_data)
            //    {
            //        int diff = t2.Diff(t);
            //        if (diff < 255)
            //        {
            //            Console.WriteLine("match found");
            //            count++;
            //            total_diff += diff;
            //        }
            //    }
            //}


            // Eliminate any duplicate templates in the label data
            var return_val = new List<Template>();

            // Keep track of what remaining IDs have to be checked
            var candidates = new HashSet<int>(label_data.Count);

            for (int id = 0; id < label_data.Count; id++)
                candidates.Add(id);

            for (int id = 0; id < label_data.Count; id++)
            {
                // Special case: "id" has already been removed from "candidates"
                if (candidates.Contains(id) == false)
                    continue; // move onto next "id"

                return_val.Add(label_data[id]);

                // The current "label_data[]" always gets added.
                // The future "label_data[]" might be duplicates of the
                // current one.

                // Currently on "label_data[id]"
                // Check "label_data[id]" against "label_data[(id+1)..end]"
                // for duplicates.
                for (int id2 = id + 1; id2 < label_data.Count; id2++)
                {
                    // Special case: "id" has already been removed from "candidates"
                    if (candidates.Contains(id2) == false)
                        continue; // move onto next "id2"

                    // Look for mutual match between "label_data[id]"
                    // and "label_data[id2]"
                    int diff1 = label_data[id].Diff(label_data[id2]);

                    if (diff1 < 255)
                    {
                        int diff2 = label_data[id2].Diff(label_data[id]);

                        if (diff2 < 25)
                            // "label_data[id2]" is redundant
                            candidates.Remove(id2);
                    }
                }
            }

            return return_val;
        }
    }



    public class Training_Task
    {
        // Each training can involve new data
        New_Training_Data[] new_data;

        // A Done flag to report training completion
        bool done = true;
        public bool Done { get { return done; } }


        // Messages to report back to the user
        List<string> messages = new List<string>();

        // Reading the messages removes them
        public string[] ReadMessages()
        {
            lock (this)
            {
                var return_val = messages.ToArray();
                messages.Clear();

                return return_val;
            }
        }

        void add_message(string message)
        {
            lock (this)
            {
                messages.Add(message);
            }
        }


        HttpClient http_client;

        public Training_Task(HttpClient http_client)
        {
            this.http_client = http_client;
        }


        // Record which label has data
        List<string> labels_with_data = new List<string>();


        /// <summary>
        /// Add the "new_data" to the model, increasing the number
        /// of templates underlying the model. Returns a 
        /// "data_added" flag.
        /// </summary>
        bool add_new_data()
        {
            bool data_added = false;

            // A label -> TemplateCandidates lookup
            var candidates = new Dictionary<string, TemplateCandidates>();

            // Go through "new_data". Add new data to TemplateCandidates
            foreach (var data in new_data)
            {
                var candidate = new TemplateCandidates(data.Label);

                bool data_added2 = candidate.Add(data.New_data);

                if (data_added2)
                {
                    data_added = true;
                    candidate.Save_Async();

                    add_message($"New data added for \"{data.Label}\", "
                        + $"which now has {candidate.Count} images for training.");
                }
                else
                {
                    add_message($"No new data added for \"{data.Label}\", "
                        + $"which now has {candidate.Count} images for training.");
                }

                candidates.Add(data.Label, candidate);
            }

            // One idea is to exit early if there is no new data added.
            // For now always trigger full retrain for easier debug.

            // Case: early exit if no data has been added, or the
            // data that got added was already in "TemplateCandidates"
            //if (data_added == false) return data_added;

            // Code arrives here if new data has been added

            // Get all candidates into memory, as the "candidates" dictionary.
            var labels = ImageData.Get_All_Labels();

            foreach (var label in labels)
            {
                if (candidates.ContainsKey(label) == false)
                {
                    var candidate = new TemplateCandidates(label);
                    candidates.Add(label, candidate);

                    add_message($"The label \"{label}\" has "
                        + $"{candidate.Count} images for training.");
                }
            }

            // Record which label has data (for training)
            foreach (var candidate in candidates.Values)
            {
                if (candidate.Count > 0)
                    labels_with_data.Add(candidate.Label);
            }

            // Put all training data (in the ) into a single
            // "template_collections/all_candidates.bin" file.
            Training.Create_candidate_template_collection(candidates.Values.ToArray());

            // Print out total number of training images.
            int sum = 0;
            foreach (var candidate in candidates.Values)
                sum += candidate.Count;

            add_message($"There are a total of {sum} images being used for training.");

            return data_added; // return true
        }


        /// <summary>
        /// Call serverless functions to train, one function
        /// per label.
        /// </summary>
        void train_model_using_serverless()
        {
            // One Azure function is used to handle each label.
            // Each Azure function is called by a separate thread.
            var thread_list = new List<UpdateTemplateCollection_Thread>();

            foreach (var label in labels_with_data)
            {
                var t = new UpdateTemplateCollection_Thread(http_client);
                t.Start(label);
                thread_list.Add(t);
            }

            foreach (var t in thread_list)
            {
                (string label, var output) = t.Wait();

                if (output.Success)
                {
                    add_message($"Successfully train the model for \"{label}\", "
                        + $"which now consists of {output.Num_templates} templates.");
                }
                else
                {
                    add_message($"Failed to train the model for \"{label}\".");
                }
            }
        }


        /// <summary>
        /// Merge the various template collections into a single one.
        /// </summary>
        void merge_template_collections()
        {
            var labels = ImageData.Get_All_Labels();

            // Build a list of all the individual template collections
            var tc_list = new List<TemplateCollection>();

            foreach (var label in labels)
            {
                string blob_name = "template_collections/" + label + ".bin";
                var bytes = Util.Download_From_Storage("char-recognition", blob_name);

                if (bytes != null)
                {
                    var tc = TemplateCollection.FromBytes(bytes);
                    tc_list.Add(tc);
                }
            }

            // Merge "tc_list" into a single template collection
            // and save to Azure
            var all_templates = TemplateCollection.Merge(tc_list.ToArray());

            Util.Upload_To_Storage("char-recognition",
                "template_collections/all_labels.bin",
                all_templates.ToBytes().ToArray());

            // Build an index and save to Azure
            var t_index = new TemplateIndex(all_templates.GetAllTemplates());

            Util.Upload_To_Storage("char-recognition",
                "template_indices/all_labels.bin",
                t_index.ToBytes());

            add_message($"New model created. It contains {all_templates.Count} templates.");
        }


        void train_model()
        {
            // One idea is to exit early if there is no new data.
            // But for now, always retrain model for easier debug.
            //if (new_data == null)
            //{
            //    done = true;
            //    return;
            //}

            labels_with_data.Clear();

            bool data_added = add_new_data();

            // One idea is to exit early if there is no new data.
            // But for now, always retrain model for easier debug.

            //if (data_added == false)
            //{
            //    add_message("No data has been added to the model. No model "
            //        + "update occurred.");
            //    done = true;

            //    return;
            //}
            // Code arrives here if "data_added" is true.

            // The "add_new_data()" has already updated the
            // "template_collections/all_candidates.bin" file.
            train_model_using_serverless();

            merge_template_collections();

            done = true;
        }


        /// <summary>
        /// Start the training process in a new thread.
        /// </summary>
        public void Start(New_Training_Data[] new_data)
        {
            if (done == false)
                // Training on-going, ignore new training request
                return;

            this.new_data = new_data;

            done = false;

            var thread = new Thread(train_model);
            thread.Start();
        }
    }



    public class TemplateCandidates
    {
        public readonly string Label;

        HashSet<int> image_id_set = new HashSet<int>();

        /// <summary>
        /// Number of IDs in this Candidate object.
        /// </summary>
        public int Count
        {
            get { return image_id_set.Count; }
        }


        public TemplateCandidates(string label)
        {
            Label = label;

            // Read template candidates from Azure

            string blob_name = "candidate_numbers/" + Label + ".bin";
            var bytes = Util.Download_From_Storage("char-recognition", blob_name);

            if (bytes != null)
            {
                var reader = new BytesReader(bytes);
                image_id_set = reader.Read_Int_HashSet();
            }
        }


        /// <summary>
        /// Return "data_added" flag. This flag can be "false"
        /// if all data in "image_ids" already exist in the internal
        /// "image_id_set".
        /// </summary>
        public bool Add(int[] image_ids)
        {
            bool data_added = false;

            foreach (var id in image_ids)
            {
                bool added = image_id_set.Add(id);

                if (added) data_added = true;
            }

            return data_added;
        }


        /// <summary>
        /// Save to Azure
        /// </summary>
        public void Save_Async()
        {
            var writer = new BytesWriter();
            writer.Add_Int_HashSet(image_id_set);

            string blob_name = "candidate_numbers/" + Label + ".bin";
            Util.Upload_To_Storage_Async("char-recognition", blob_name, 
                writer.BytesList.ToArray());
        }


        public List<int> GetIds()
        {
            var result = new List<int>(image_id_set.Count);

            foreach (var value in image_id_set)
                result.Add(value);

            return result;
        }
    }



    public class UpdateTemplateCollection_Thread
    {
        // Makes an API call to
        // CharRecognitionFunctions.UpdateTemplateCollection
        // using a background thread

        class Input
        {
            public string Label = "";
        }

        public class Output
        {
            public bool Success;
            public int Num_templates;
            public string Error;
        }

        Input input;
        Output output;

        Thread make_call_thread;

        HttpClient client;


        public UpdateTemplateCollection_Thread(HttpClient client)
        {
            this.client = client;
        }


        public void Start(string label)
        {
            lock (this)
            {
                if (make_call_thread != null)
                    return; // silent fail; assume it's extra "Start()"

                input = new Input() { Label = label };

                make_call_thread = new Thread(make_call);
                make_call_thread.Start();
            }
        }


        public void make_call()
        {
            string input_json;

            lock (this)
            {
                input_json = JsonConvert.SerializeObject(input);
            }

            var url = Credentials.Azure_Update_Template_Collection;

            var random = new Random();
            int retry = 0;

            try
            {
                do
                {
                    var response = client.PostAsync(url,
                        new StringContent(input_json, Encoding.UTF8, "application/json"));

                    response.Wait();

                    var output_json = response.Result.Content.ReadAsStringAsync().Result;

                    output = JsonConvert.DeserializeObject<Output>(output_json);

                    if (output.Success == false)
                    {
                        Thread.Sleep(random.Next(100, 300));
                        retry++;
                    }

                } while (output.Success == false && retry < 10);
            }
            catch (Exception ex)
            {
                output = new Output();
                output.Success = false;
                output.Error = ex.ToString();
            }
        }


        /// <summary>
        /// Returns the (input) label, and the result of the API call.
        /// </summary>
        public (string, Output) Wait()
        {
            if (make_call_thread != null)
                make_call_thread.Join();

            lock (this)
            {
                make_call_thread = null;
                return (input.Label, output);
            }
        }
    }

}
