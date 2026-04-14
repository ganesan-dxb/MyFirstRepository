using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using StudentApp.Shared.Models;
using StudentApp.Web.Services;

namespace StudentApp.Web.Pages.Students;

public class EditModel : PageModel
{
    private readonly IStudentApiClient _api;

    public EditModel(IStudentApiClient api) => _api = api;

    [BindProperty]
    public UpdateStudentDto Input { get; set; } = new();

    [BindProperty]
    public int StudentId { get; set; }

    public bool StudentNotFound { get; set; }

    public async Task OnGetAsync(int id)
    {
        StudentId = id;
        var student = await _api.GetStudentAsync(id);
        if (student is null)
        {
            StudentNotFound = true;
            return;
        }

        // Pre-fill the form with existing data
        Input = new UpdateStudentDto
        {
            FirstName = student.FirstName,
            LastName = student.LastName,
            Email = student.Email,
            DateOfBirth = student.DateOfBirth,
            Grade = student.Grade
        };
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
            return Page();

        var student = await _api.UpdateStudentAsync(StudentId, Input);
        if (student is null)
        {
            ModelState.AddModelError(string.Empty, "Failed to update student.");
            return Page();
        }

        TempData["Success"] = $"Student '{student.FirstName} {student.LastName}' updated successfully!";
        return RedirectToPage("Index");
    }
}
