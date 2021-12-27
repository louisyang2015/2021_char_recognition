using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

using CharRecognitionLib;

namespace WebUI
{
    public class App
    {
        // Application wide objects
        public static Test_Model_Task test_model_task;

        public static Training_Task training_task;


        
        static RecognitionModel recognition_model;

        public static RecognitionModel Recognition_Model
        {
            get { return recognition_model; }
        }


        static App()
        {
            HttpClient http_client = new HttpClient();

            test_model_task = new Test_Model_Task(http_client);
            training_task = new Training_Task(http_client);

            Refresh_Recognition_Model();
        }


        public static void Refresh_Recognition_Model()
        {
            var ti = Util.Download_From_Storage("char-recognition", 
                "template_indices/all_labels.bin");
            var tc = Util.Download_From_Storage("char-recognition", 
                "template_collections/all_labels.bin");

            recognition_model = new RecognitionModel(ti, tc);
        }
    }
}
