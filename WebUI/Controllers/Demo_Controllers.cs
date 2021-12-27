using CharRecognitionLib;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace WebUI.Controllers
{
    [Route("api/demo")]
    [ApiController]
    public class Demo_Controllers : ControllerBase
    {
        #region Identify Image

        public class Id_Image_Input
        {
            [Required]
            public int[] Bytes { get; set; }
        }

        public class Id_Image_Output
        {
            public bool Success { get; set; }
            public string Label { get; set; }
        }

        [HttpPost("id_image")]
        public ActionResult<Id_Image_Output> Id_Image(Id_Image_Input input)
        {
            // Convert "input.Bytes" to byte[]
            var bytes = new byte[input.Bytes.Length];

            for (int i = 0; i < bytes.Length; i++)
                bytes[i] = (byte)input.Bytes[i];

            // Currently "bytes" is encoded as one bit per pixel
            // Decode "bytes" into a byte[,] image
            var bw_image = new BlackAndWhite_Image("B", 16, 16, bytes, 0);
            bw_image.Standardize();            


            string label = App.Recognition_Model.Recognize_BW_Image(bw_image.Get2DBytes());

            if (label == null) label = "unknown";


            var output = new Id_Image_Output();
            output.Success = true;
            output.Label = label;
            return output;
        }

        #endregion
    }
}
