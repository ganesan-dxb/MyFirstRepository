using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using StudentApp.Shared.Models;
using StudentApp.Web.Services;

namespace StudentApp.Web.Pages.Students;

public class DetailsModel : PageModel
{
    private readonly IStudentApiClient _api;

    public DetailsModel(IStudentApiClient api) => _api = api;

    public Student? Student { get; set; }

    public async Task OnGetAsync(int id)
    {
        Student = await _api.GetStudentAsync(id);
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        await _api.DeleteStudentAsync(id);
        TempData["Success"] = "Student deleted.";
        return RedirectToPage("Index");
    }
}
