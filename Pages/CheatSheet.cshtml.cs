using Markdig;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace WiseLabels.Pages;

public class CheatSheetModel : PageModel
{
    private readonly IWebHostEnvironment _env;

    public CheatSheetModel(IWebHostEnvironment env)
    {
        _env = env;
    }

    public string HtmlContent { get; set; } = "";

    public void OnGet()
    {
        var path = Path.Combine(_env.ContentRootPath, "Documentation", "cheat-sheet.md");
        if (!System.IO.File.Exists(path))
        {
            HtmlContent = "<p class=\"text-muted\">Reference guide not found.</p>";
            return;
        }

        var markdown = System.IO.File.ReadAllText(path);
        var pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();
        HtmlContent = Markdown.ToHtml(markdown, pipeline);
    }
}
