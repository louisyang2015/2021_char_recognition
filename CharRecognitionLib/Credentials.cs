using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CharRecognitionLib
{
    class Credentials
    {
        // Azure storage connection string
        public static string Azure_Blob_Storage = "DefaultEndpointsProtocol=https;AccountName=FAKE;AccountKey=FAKE==;EndpointSuffix=core.windows.net";

        // Azure function endpoints
        public static string Azure_Standardize_Image = "https://FAKE.azurewebsites.net/api/StandardizeImages?code=FAKE==";
        public static string Azure_Test_Model = "https://FAKE.azurewebsites.net/api/TestModel/?code=FAKE==";
        public static string Azure_Update_Template_Collection = "https://FAKE.azurewebsites.net/api/UpdateTemplateCollection/?code=FAKE==";
    }
}
