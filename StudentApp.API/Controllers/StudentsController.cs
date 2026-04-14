using Microsoft.AspNetCore.Mvc;
using StudentApp.API.Services;
using StudentApp.Shared.Models;

namespace StudentApp.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class StudentsController : ControllerBase
{
    private readonly IStudentService _studentService;

    public StudentsController(IStudentService studentService)
    {
        _studentService = studentService;
    }

    // GET /api/students
    // Returns all students. Cached in Redis for 60 seconds.
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<Student>>), 200)]
    public async Task<IActionResult> GetAll()
    {
        var (students, fromCache) = await _studentService.GetAllAsync();
        var response = ApiResponse<IEnumerable<Student>>.Ok(students, fromCache);
        return Ok(response);
    }

    // GET /api/students/5
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(ApiResponse<Student>), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetById(int id)
    {
        var student = await _studentService.GetByIdAsync(id);
        if (student is null)
            return NotFound(ApiResponse<Student>.Fail($"Student {id} not found => Running in Linux WSL ubuntu "));

        return Ok(ApiResponse<Student>.Ok(student));
    }

    // POST /api/students
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<Student>), 201)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> Create([FromBody] CreateStudentDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<Student>.Fail("Invalid data"));

        try
        {
            var student = await _studentService.CreateAsync(dto);
            return CreatedAtAction(nameof(GetById), new { id = student.Id },
                ApiResponse<Student>.Ok(student));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse<Student>.Fail(ex.Message));
        }
    }

    // PUT /api/students/5
    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(ApiResponse<Student>), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateStudentDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<Student>.Fail("Invalid data"));

        var student = await _studentService.UpdateAsync(id, dto);
        if (student is null)
            return NotFound(ApiResponse<Student>.Fail($"Student {id} not found"));

        return Ok(ApiResponse<Student>.Ok(student));
    }

    // DELETE /api/students/5
    [HttpDelete("{id:int}")]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Delete(int id)
    {
        var deleted = await _studentService.DeleteAsync(id);
        if (!deleted)
            return NotFound(ApiResponse<bool>.Fail($"Student {id} not found"));

        return NoContent();
    }

    // GET /api/students/cache-status
    // Shows cache info — useful for learning
    [HttpGet("cache-status")]
    public async Task<IActionResult> CacheStatus([FromServices] ICacheService cache)
    {
        var cached = await cache.GetAsync<object>("students:all");
        return Ok(new
        {
            CacheKey = "students:all",
            IsCached = cached is not null,
            Message = cached is not null
                ? "Student list is currently in Redis cache"
                : "Student list is NOT in cache — next GET /students will hit SQL Server"
        });
    }
}
