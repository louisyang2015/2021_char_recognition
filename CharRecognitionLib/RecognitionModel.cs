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
    public class RecognitionModel
    {
        TemplateIndex template_index;
        TemplateCollection template_collection;

        // Record the latest best match for debug purpose
        int? latest_best_match = null;

        /// <summary>
        /// Template ID of the latest best match.
        /// </summary>
        public int? LatestBestMatch
        {
            get { return latest_best_match; }
        }


        /// <summary>
        /// A data structure to hold template matching results.
        /// </summary>
        class TemplateMatchResult : IComparable<TemplateMatchResult>
        {
            public readonly int Id; // template ID
            public readonly int Diff_Score; // Template.Diff score
            public readonly int Pixel_Diff; // pixel count difference score

            public int CompareTo(TemplateMatchResult r)
            {
                if (Diff_Score != r.Diff_Score)
                    return Diff_Score - r.Diff_Score;
                else
                    return Pixel_Diff - r.Pixel_Diff;
            }

            public TemplateMatchResult(int id, int diff, int p_count_diff)
            {
                Id = id;
                Diff_Score = diff;
                Pixel_Diff = p_count_diff;
            }

            public bool IsTie(TemplateMatchResult r)
            {
                if ((Diff_Score == r.Diff_Score)
                    && (Pixel_Diff == r.Pixel_Diff))
                    return true;
                else
                    return false;
            }
        }


        /// <summary>
        /// The "RecognitionModel" is constructed using the byte[] representation
        /// of "TemplateIndex" and "TemplateCollection".
        /// </summary>
        public RecognitionModel(byte[] template_index,
            byte[] template_collection)
        {
            // Allow for a "null" case because in the very beginning there 
            // will be no template at all.
            if (template_index != null && template_collection != null)
            {
                this.template_index = new TemplateIndex(template_index);
                this.template_collection = TemplateCollection.FromBytes(template_collection, 0);
            }
        }


        /// <summary>
        /// Returns the label of a black and white "bw_image". 
        /// Returns null if unable to recognize the image. 
        /// The image is assumed to have been
        /// standardized to the right size already.
        /// </summary>
        public string Recognize_BW_Image(byte[,] bw_image)
        {
            latest_best_match = null;

            // Allow for a "null" case because in the very beginning there 
            // will be no template at all.
            if (template_index == null || template_collection == null)
                return null; // In the beginning, unable to recognize anything

            // Error check
            int height = bw_image.GetUpperBound(0) + 1;
            int width = bw_image.GetUpperBound(1) + 1;

            if (height != template_collection.Height
                || width != template_collection.Width)
                throw new Exception("Attempted to recognized an image that "
                    + "has not gone through standardization.");

            // Convert image into a "Template" object
            var template = new Template(bw_image);
            template.Set_4_neighbors_to_1();

            int[] ids = template_index.Search(template);

            // Case: no match found
            if (ids.Length == 0) return null;

            // Score the results
            var results = new List<TemplateMatchResult>();

            foreach (int id in ids)
            {
                // The index.search() is using template.Diff(index_templates),
                // so Diff in the other direction first
                var t2 = template_collection.GetTemplate(id);
                int diff = t2.Diff(template);

                // Case: no match
                if (diff >= 255) continue;

                if (diff < 255)
                {
                    // Diff in the other direction, to get the maximum
                    // diff value.
                    int diff2 = template.Diff(t2);

                    // Case: no match
                    if (diff2 >= 255) continue;

                    if (diff2 > diff) diff = diff2;
                }

                // Get the pixel count difference score
                int p_count_diff = template.Foreground_Pixel_Count - t2.Foreground_Pixel_Count;

                if (p_count_diff < 0)
                    p_count_diff = -1 * p_count_diff;

                results.Add(new TemplateMatchResult(id, diff, p_count_diff));
            }


            // Case: no match found 
            if (results.Count == 0)
                return null;

            // Code arrives here meaning "results.Count" is at least 1
            latest_best_match = results[0].Id;

            // Case: only one match
            if (results.Count == 1)
                return template_collection.GetLabel(results[0].Id);

            // Sort results
            results.Sort();
            string label = template_collection.GetLabel(results[0].Id);

            // Check for tie consistency
            for (int i = 0; i < results.Count - 1; i++)
            {
                var r1 = results[i];
                var r2 = results[i + 1];

                if (r1.IsTie(r2))
                {
                    string label2 = template_collection.GetLabel(r2.Id);
                    if (label != label2)
                        return null; 
                        // Results with same score produces different label.
                        // Therefore the recognition result is unknown.
                }
                else
                {
                    // No tie, tie consistency check is over
                    break;
                }
            }

            return label;
        }

    }


    /// <summary>
    /// Test "RecognitionModel" on the data, by invoking the serverless 
    /// function "Credentials.Azure_Test_Model" via multiple threads.
    /// </summary>
    public class Test_Model_Task
    {
        HttpClient client;

        public Test_Model_Task(HttpClient client)
        {
            this.client = client;
        }


        // The range of work:
        string[] labels; // The labels to work on
        bool all_images;

        int start_file_number_user, end_file_number_user;
        int start_file_number, end_file_number;
        // The "_user" values are values coming from user input
        // These are then checked against the available file numbers
        // to arrive at "start_file_number" and "start_file_number"

        // When "all_images" is true, the "end_file_number"
        // has to be re-adapted for each image.

        // The current work:
        int current_label_index = 0; // current label that we are working on
        int current_file_number = 0;  // the next file number to work on

        // Cumulative test results
        public class TestResult
        {
            public int correct = 0, incorrect = 0, unknown = 0;
            public List<int> misclassified = new List<int>();

            public void AddResult(int correct, int incorrect, int unknown, 
                int[] misclassified)
            {
                this.correct += correct;
                this.incorrect += incorrect;
                this.unknown += unknown;
                
                if (this.misclassified.Count < 100)
                {
                    foreach (var i in misclassified)
                    {
                        this.misclassified.Add(i);
                        if (this.misclassified.Count >= 100)
                            break;
                    }
                }
            }


            public TestResult Clone()
            {
                var r = new TestResult();
                r.correct = correct;
                r.incorrect = incorrect;
                r.unknown = unknown;

                r.misclassified = new List<int>();
                foreach (var i in misclassified)
                    r.misclassified.Add(i);

                return r;
            }
        }

        Dictionary<string, TestResult> test_results = new Dictionary<string, TestResult>();
        // The test result for "label" is at test_results["label"]

        public Dictionary<string, TestResult> TestResults
        {
            get { return test_results; }
        }


        public readonly static object lock_object = new object();

        bool done = true; // "false" means a test is happening

        public bool Done { get { return done; } }


        /// <summary>
        /// Update the (start_file_number, end_file_number) range, 
        /// based on a combination of user input and the given label.
        /// </summary>
        void determine_file_number_range(string label)
        {
            // The (start_file_number, end_file_number) range is
            // by default set by user input.
            start_file_number = start_file_number_user;
            end_file_number = end_file_number_user;

            // However, this range might exceed the actual file numbers
            // possible.
            (_, _, _, int max_file_number) = ImageData.Get_Label_Stats(label);

            if (start_file_number < 0) start_file_number = 0;
            if (start_file_number > max_file_number) start_file_number = max_file_number;
            if (end_file_number < 0) end_file_number = 0;
            if (end_file_number > max_file_number) end_file_number = max_file_number;
        }


        /// <summary>
        /// Returns a (label, file_number1, file_number2), 
        /// which represents the workload to be done.
        /// </summary>
        (string, int, int) get_work()
        {
            // First call returns ("0", 0, k*32)
            // Next call returns ("0", (k+1)*32, 2*k*32)
            // and so on...

            int batch_size = 32; // the "k" mentioned above

            lock (Test_Model_Task.lock_object)
            {
                // Case: nothing more to work on
                if (current_label_index >= labels.Length) 
                    return (null, 0, 0);

                string label = labels[current_label_index];
                int file_number1 = current_file_number;

                // Decide on the ending file number
                int delta = ImageData.MaxImagesPerFile;
                int file_number2 = current_file_number + (batch_size - 1) * delta;

                if (file_number2 > end_file_number)
                    file_number2 = end_file_number;

                // Increment the state variables for the next call to this function
                current_file_number = file_number2 + delta;

                if (current_file_number > end_file_number)
                {
                    current_label_index++;

                    // When "all_images" is true, the "end_file_number"
                    // has to be re-adapted for each image.
                    if (all_images)
                    {
                        if (current_label_index < labels.Length)
                        {
                            current_file_number = 0;

                            var label2 = labels[current_label_index];
                            (_, _, _, end_file_number) = ImageData.Get_Label_Stats(label2);
                        }
                    }
                    else
                    {
                        // For the situation where the user has specified a 
                        // start and end file number, these limites are
                        // subjected to a range check.
                        if (current_label_index < labels.Length)
                        {
                            var label2 = labels[current_label_index];
                            determine_file_number_range(label2);

                            current_file_number = start_file_number;
                        }
                    }
                }

                return (label, file_number1, file_number2);
            }
        }

        record FunctionInput (string label, int start_file_number, int end_file_number);
        record FunctionOutput(bool success, int correct, int incorrect, int unknown,
            int[] misclassified);


        void do_work()
        {
            var random = new Random();

            while (true)
            {
                // Figure out the next task
                (var label, int file_number1, int file_number2) = get_work();

                if (label == null) break;

                ///////////////////////////////////////////////////////
                // Call serverless function to accomplish task
                var input = new FunctionInput(label, file_number1, file_number2);
                var json = JsonConvert.SerializeObject(input);

                // endpoint URL comes from "Credentials.cs"
                string url = Credentials.Azure_Test_Model;

                // Azure functions often return error 500
                // So retry is needed
                FunctionOutput output;

                int retry = 0;
                try
                {
                    do
                    {
                        var response = client.PostAsync(url,
                        new StringContent(json, Encoding.UTF8, "application/json"));

                        response.Wait();

                        json = response.Result.Content.ReadAsStringAsync().Result;

                        output = JsonConvert.DeserializeObject<FunctionOutput>(json);

                        if (output.success == false)
                        {
                            Thread.Sleep(random.Next(100, 300));
                            retry++;
                        }
                    } while (output.success == false && retry < 30);
                }
                catch
                {
                    output = new FunctionOutput(false, 0, 0, 0, new int[0]);
                }

                lock(Test_Model_Task.lock_object)
                {
                    test_results[label].AddResult(output.correct, 
                        output.incorrect, output.unknown, output.misclassified);
                }
            }
        }

        void manager_thread()
        {
            // Create threads
            var threads = new Thread[20];

            for (int i = 0; i < threads.Length; i++)
            {
                threads[i] = new Thread(do_work);
                threads[i].Start();

                // Separate thread starting times so to make the debug
                // experience more consistent.
                Thread.Sleep(50);
            }

            // Wait for threads to end
            for (int i = 0; i < threads.Length; i++)
                threads[i].Join();

            done = true;
        }


        /// <summary>
        /// Start a new round of testing. All variables will be 
        /// reset / reallocated as necessary.
        /// </summary>
        public void Start(bool all_labels, string label, bool all_images,
            int start_file_number, int end_file_number)
        {
            // Exit if busy
            if (done == false) return;

            // Setup variables
            if (all_labels)
                labels = ImageData.Get_All_Labels();
            else
                labels = new string[] { label };

            this.all_images = all_images;

            if (all_images)
            {
                this.start_file_number = 0;
                (_, _, _, this.end_file_number) = ImageData.Get_Label_Stats(labels[0]);
            }
            else
            {
                this.start_file_number_user = start_file_number;
                this.end_file_number_user = end_file_number;

                determine_file_number_range(labels[0]);
            }

            current_label_index = 0;
            current_file_number = 0;

            // Allocate "test_results"
            test_results.Clear();

            foreach (var label2 in labels)
                test_results.Add(label2, new TestResult());

            // Start management thread so the current thread can exit
            done = false;
            var thread = new Thread(manager_thread);
            thread.Start();
        }
    }
}
