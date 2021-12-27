using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CharRecognitionLib;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;


namespace WebUI.Pages
{
    public class MLModel : PageModel
    {
        public string[] Labels { get; set; }
        public string Host { get; set; }

        public void OnGet()
        {
            Labels = ImageData.Get_All_Labels();
            Host = HttpContext.Request.Host.Value;
        }
    }
}
