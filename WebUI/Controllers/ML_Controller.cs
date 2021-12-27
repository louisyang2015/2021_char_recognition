using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

using CharRecognitionLib;
using System.Threading;

namespace WebUI.Controllers
{
    [Route("api/ml")]
    [ApiController]
    public class ML_Controller : ControllerBase
    {
        #region Test Data

        public class Test_Data_Input
        {
            
            [Required]
            public string Label { get; set; }

            [Required]
            public string Data { get; set; }

            public string Start { get; set; }
            public string End { get; set; }
        }

        public class Test_Data_Output
        {
            public bool Received { get; set; }
            public string Error { get; set; }
        }

        
        [HttpPost("test_data")]
        public ActionResult<Test_Data_Output> TestData(Test_Data_Input input)
        {
            var output = new Test_Data_Output();
            output.Received = true;

            var test_task = App.test_model_task;
            bool error = false;

            // Case: Busy
            if (test_task.Done == false)
            {
                
                output.Error = "Test on going.";
                return output;
            }

            // Start new test
            // Look over input data
            bool all_labels = false;

            if (input.Label == "All Labels")
                all_labels = true;

            bool all_images = false;

            if (input.Data == "All Data")
                all_images = true;

            int start_file_number = 0;
            int end_file_number = 0;

            if (all_images == false)
            {
                bool success = int.TryParse(input.Start, out start_file_number);
                
                // The number from "input.Start" is actually an image number.
                // This needs to be rounded off to become a file number.
                // For example, image number 38 is part of the file that
                // includes image number 32 ~ 63.
                int files_per_image = ImageData.MaxImagesPerFile;

                if (success)
                    start_file_number = start_file_number / files_per_image * files_per_image;
                else
                {
                    error = true;
                    output.Error = "Starting image number is not a number.";
                }

                success = int.TryParse(input.End, out end_file_number);
                if (success)
                    end_file_number = end_file_number / files_per_image * files_per_image;
                else
                {
                    error = true;
                    output.Error = "Ending image number is not a number.";
                }
            }

            if (error == false)
            {
                test_task.Start(all_labels, input.Label, all_images, 
                    start_file_number, end_file_number);
            }

            return output;
        }


        #endregion


        #region Get Test Results

        public class Get_Test_Results_Input
        {
        }

        public class TestResult
        {
            public string Label { get; set; }
            public int Correct { get; set; }
            public int Incorrect { get; set; }
            public int Unknown { get; set; }
            public List<int> Misclassifieds { get; set; }

            public TestResult()
            {
                Misclassifieds = new List<int>();
            }
        }

        public class Get_Test_Results_Output
        {
            public bool Done { get; set; }

            // Test Results: each label (string) has:
            // correct (int), incorrect (int), unknown (int),
            // misclassified_list (List<int>)
            public List<TestResult> Results { get; set; }

            public Get_Test_Results_Output()
            {
                Results = new List<TestResult>();
            }
        }


        [HttpPost("get_test_results")]
        public ActionResult<Get_Test_Results_Output> GetTestResults(Get_Test_Results_Input input)
        {
            var output = new Get_Test_Results_Output();
            output.Results = new List<TestResult>();

            // Extract test result from "App.test_model_task.TestResults"
            lock (Test_Model_Task.lock_object)
            {
                output.Done = App.test_model_task.Done;

                // Copy from the "App.test_model_task.TestResults"
                // dictionary to "output.Results"
                foreach (var kv in App.test_model_task.TestResults)
                {
                    var result = new TestResult();
                    result.Label = kv.Key;
                    result.Correct = kv.Value.correct;
                    result.Incorrect = kv.Value.incorrect;
                    result.Unknown = kv.Value.unknown;

                    foreach (var i in kv.Value.misclassified)
                        result.Misclassifieds.Add(i);

                    output.Results.Add(result);
                }
            }
            
            return output;
        }


        #endregion


        #region Start Retrain

        public class Start_Retrain_Input
        {
            [Required]
            public New_Training_Data[] New_data { get; set; }
            // New_Training_Data from "CharRecognitionLib"
        }

        public class Start_Retrain_Output
        {
            public bool Received { get; set; }

            public string Error { get; set; }
        }


        [HttpPost("start_retrain")]
        public ActionResult<Start_Retrain_Output> StartRetrain(Start_Retrain_Input input)
        {
            var output = new Start_Retrain_Output();

            output.Received = true;

            var train_task = App.training_task;

            // Case: Busy
            if (train_task.Done == false)
            {
                output.Error = "Test on going.";
                return output;
            }

            train_task.Start(input.New_data);

            var monitor_thread = new Thread(monitor_training_end);
            monitor_thread.Start();

            return output;
        }


        /// <summary>
        /// When the training ended, reload the application's
        /// recognition object.
        /// </summary>
        void monitor_training_end()
        {
            var train_task = App.training_task;

            while (true)
            {
                // Wait for "train_task.Done"
                while (train_task.Done == false)
                {
                    Thread.Sleep(1000);
                }

                // Wait one more second
                Thread.Sleep(1000);

                // Check for "train_task.Done"
                if (train_task.Done)
                {
                    App.Refresh_Recognition_Model();
                }
            }
        }


        #endregion


        #region Get Training Progress

        public class Get_Training_Progress_Input
        {
        }


        public class Get_Training_Progress_Output
        {
            public bool Done { get; set; }

            public string[] Messages { get; set; }
        }


        [HttpPost("get_training_progress")]
        public ActionResult<Get_Training_Progress_Output> GetTrainingProgress(Get_Training_Progress_Input input)
        {
            var output = new Get_Training_Progress_Output();
            output.Done = App.training_task.Done;
            output.Messages = App.training_task.ReadMessages();            

            return output;
        }

        #endregion

    }
}
