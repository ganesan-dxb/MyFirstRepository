using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using StudentApp.Shared.Models;
using StudentApp.Web.Services;

namespace StudentApp.Web.Pages.Students;

public class IndexModel : PageModel
{
    private readonly IStudentApiClient _api;

    public IndexModel(IStudentApiClient api) => _api = api;

    public List<Student> Students { get; set; } = [];
    public bool FromCache { get; set; }

    public async Task OnGetAsync()
    {
        var (students, fromCache) = await _api.GetAllStudentsAsync();
        Students = students;
        FromCache = fromCache;
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        var deleted = await _api.DeleteStudentAsync(id);
        TempData["Success"] = deleted
            ? "Student deleted successfully."
            : "Could not delete student.";
        return RedirectToPage();
    }
}
