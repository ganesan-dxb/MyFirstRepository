using Microsoft.AspNetCore.Mvc.RazorPages;

namespace StudentApp.Web.Pages.Students;

// The form submission is now handled entirely by JavaScript (fetch API).
// This page model just serves the page — no OnPost needed.
public class CreateModel : PageModel
{
    private readonly IConfiguration _configuration;
    public CreateModel(IConfiguration configuration) => _configuration = configuration;

    // Expose config to the Razor page for the JS URLs
    public IConfiguration Configuration => _configuration;

    public void OnGet() { }
}
