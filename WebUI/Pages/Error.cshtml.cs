using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace WebUI.Pages
{
    public class ErrorModel : PageModel
    {
        public string ExceptionMessage { get; set; }

        public void OnGet()
        {
            ExceptionMessage = "Unknown";

            var feature = HttpContext.Features.Get<IExceptionHandlerPathFeature>();
            if (feature != null)
            {
                ExceptionMessage = feature.Error.Message;
            }

        }
    }
}
