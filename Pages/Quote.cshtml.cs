using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WiseLabels.Models;

namespace WiseLabels.Pages
{
    public class QuoteModel : PageModel
    {
        public bool Testing { get; set; }
        public QuoteRequest? SavedQuoteRequest { get; set; }
        public void OnGet()
        {
        }
    }
}
