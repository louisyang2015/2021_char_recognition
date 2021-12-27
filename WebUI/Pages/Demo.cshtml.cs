using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace WebUI.Pages
{
    public class DemoModel : PageModel
    {
        public string Host { get; set; }

        public void OnGet()
        {
            Host = HttpContext.Request.Host.Value;
        }
    }
}
