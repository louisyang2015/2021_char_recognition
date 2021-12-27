using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace WebUI.Pages
{
    public class AddDataModel : PageModel
    {
        public string Host { get; set; }

        public void OnGet()
        {
            Host = HttpContext.Request.Host.Value;
        }
    }
}
