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
    public static class StandardizeImages
    {
        [FunctionName("StandardizeImages")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            dynamic data = JsonConvert.DeserializeObject(requestBody);

            string label = data.label;
            string file_numbers = data.file_numbers;

            // The file numbers are a comma separated list

            var tokens = file_numbers.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            foreach (var file_number in tokens)
            {
                var i = int.Parse(file_number);
                ImageData.StandardizeImages(label, i);
            }


            string responseMessage = "OK";

            return new OkObjectResult(responseMessage);
        }
    }
}
