using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using CharRecognitionLib;
using System.ComponentModel.DataAnnotations;

namespace WebUI.Controllers
{
    [Route("api/image-data")]
    [ApiController]
    public class ImageData_Controller : ControllerBase
    {
        #region Add Image

        public class Add_Image_Input
        {
            [Required]
            public string Label { get; set; }

            [Required]
            public string Type { get; set; }

            [Required]
            public int Height { get; set; }

            [Required]
            public int Width { get; set; }

            [Required]
            public int[] Bytes { get; set; }
            // The use of "byte[] Bytes" here does not work
        }


        public class Add_Image_Output
        {
            public bool Success { get; set; }
        }


        [HttpPost("add_image")]
        public ActionResult<Add_Image_Output> AddImage(Add_Image_Input input)
        {
            // Convert "input.Bytes" to byte[]
            var bytes = new byte[input.Bytes.Length];

            for (int i = 0; i < bytes.Length; i++)
                bytes[i] = (byte)input.Bytes[i];

            bool success = ImageData.Add_Image(input.Label, bytes,
                input.Type, input.Height, input.Width);

            var output = new Add_Image_Output();
            output.Success = success;
            return output;
        }

        #endregion



        #region Get Images


        public class Get_Images_Input
        {
            [Required]
            public string Prefix { get; set; }

            [Required]
            public string Label { get; set; }

            [Required]
            public int ImageNumber { get; set; }
        }


        public class Get_Images_Output
        {
            public bool Success { get; set; }
            public string Base64_Data { get; set; }

            public string Type { get; set; }
            public int Height { get; set; }
            public int Width { get; set; }
            public int MaxImagesPerFile { get; set; }
        }


        [HttpPost("get_images")]
        public ActionResult<Get_Images_Output> GetImages(Get_Images_Input input)
        {
            var bytes = ImageData.Get_Label_Bytes(input.Prefix, input.Label, input.ImageNumber);

            if (bytes is null)
            {
                // Case: no data found
                var output = new Get_Images_Output();
                output.Success = false;
                output.Base64_Data = "";
                return output;
            }
            else
            {
                // Standard case
                var output = new Get_Images_Output();
                output.Success = true;
                output.Base64_Data = Convert.ToBase64String(bytes);
                output.MaxImagesPerFile = ImageData.MaxImagesPerFile;

                if (input.Prefix == ImageData.Standard_Image_Location)
                {
                    // Standardized images have the same attributes
                    output.Type = "B";
                    output.Height = 8;
                    output.Width = 8;
                }
                else
                {
                    (output.Type, output.Height, output.Width, _) = 
                        ImageData.Get_Label_Stats(input.Label);
                }
                
                return output;
            }
        }

        #endregion



        #region Get Images By Number


        public class Get_Images_By_Number_Input
        {
            [Required]
            public string Prefix { get; set; }

            [Required]
            public string Label { get; set; }

            public List<int> SortedImageNumbers { get; set; }
        }


        public class Get_Images_By_Number_Output
        {
            public bool Success { get; set; }
            public string Base64_Data { get; set; }

            public string Type { get; set; }
            public int Height { get; set; }
            public int Width { get; set; }
        }


        [HttpPost("get_images_by_number")]
        public ActionResult<Get_Images_By_Number_Output> GetImagesByNumber(Get_Images_By_Number_Input input)
        {
            var bytes = ImageData.Get_Label_Bytes_By_Numbers(input.Prefix, input.Label, 
                input.SortedImageNumbers.ToArray());

            if (bytes is null)
            {
                // Case: no data found
                var output = new Get_Images_By_Number_Output();
                output.Success = false;
                output.Base64_Data = "";
                return output;
            }
            else
            {
                // Standard case
                var output = new Get_Images_By_Number_Output();
                output.Success = true;
                output.Base64_Data = Convert.ToBase64String(bytes);

                if (input.Prefix == ImageData.Standard_Image_Location)
                {
                    // Standardized images have the same attributes
                    output.Type = "B";
                    output.Height = 8;
                    output.Width = 8;
                }
                else
                {
                    (output.Type, output.Height, output.Width, _) =
                        ImageData.Get_Label_Stats(input.Label);
                }

                return output;
            }
        }

        #endregion
    }
}
