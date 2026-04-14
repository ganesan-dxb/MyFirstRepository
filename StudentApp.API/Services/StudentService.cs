using Dapper;
using StudentApp.API.Data;
using StudentApp.Shared.Models;

namespace StudentApp.API.Services;

public interface IStudentService
{
    Task<(IEnumerable<Student> students, bool fromCache)> GetAllAsync();
    Task<Student?> GetByIdAsync(int id);
    Task<Student> CreateAsync(CreateStudentDto dto);
    Task<Student?> UpdateAsync(int id, UpdateStudentDto dto);
    Task<bool> DeleteAsync(int id);
}

public class StudentService : IStudentService
{
    private readonly IDbConnectionFactory _db;
    private readonly ICacheService _cache;
    private const string AllStudentsCacheKey = "students:all";

    public StudentService(IDbConnectionFactory db, ICacheService cache)
    {
        _db = db;
        _cache = cache;
    }

    public async Task<(IEnumerable<Student> students, bool fromCache)> GetAllAsync()
    {
        // 1. Try Redis cache first
        var cached = await _cache.GetAsync<List<Student>>(AllStudentsCacheKey);
        if (cached is not null)
            return (cached, true);

        // 2. Cache miss — query SQL Server with Dapper
        using var connection = _db.CreateConnection();

        var students = (await connection.QueryAsync<Student>("""
            SELECT Id, FirstName, LastName, Email, DateOfBirth, Grade, CreatedAt
            FROM   Students
            ORDER  BY LastName, FirstName
            """)).ToList();

        // 3. Store result in Redis for 60 seconds
        await _cache.SetAsync(AllStudentsCacheKey, students, TimeSpan.FromSeconds(60));

        return (students, false);
    }

    public async Task<Student?> GetByIdAsync(int id)
    {
        var cacheKey = $"students:{id}";

        var cached = await _cache.GetAsync<Student>(cacheKey);
        if (cached is not null) return cached;

        using var connection = _db.CreateConnection();

        // Dapper maps query columns -> Student properties automatically
        var student = await connection.QuerySingleOrDefaultAsync<Student>("""
            SELECT Id, FirstName, LastName, Email, DateOfBirth, Grade, CreatedAt
            FROM   Students
            WHERE  Id = @Id
            """, new { Id = id });

        if (student is not null)
        {
            student.FirstName = student.FirstName.Trim() + " From WSL Ubuntu";
            await _cache.SetAsync(cacheKey, student, TimeSpan.FromSeconds(120));
        }

        return student;
    }

    public async Task<Student> CreateAsync(CreateStudentDto dto)
    {
        using var connection = _db.CreateConnection();

        // INSERT ... OUTPUT INSERTED.* returns the newly created row including Id and CreatedAt
        var student = await connection.QuerySingleAsync<Student>("""
            INSERT INTO Students (FirstName, LastName, Email, DateOfBirth, Grade, CreatedAt)
            OUTPUT INSERTED.*
            VALUES (@FirstName, @LastName, @Email, @DateOfBirth, @Grade, GETUTCDATE())
            """,
            new
            {
                dto.FirstName,
                dto.LastName,
                dto.Email,
                dto.DateOfBirth,
                dto.Grade
            });

        // Invalidate the list cache so next GET fetches fresh data
        await _cache.RemoveAsync(AllStudentsCacheKey);

        return student;
    }

    public async Task<Student?> UpdateAsync(int id, UpdateStudentDto dto)
    {
        using var connection = _db.CreateConnection();

        var student = await connection.QuerySingleOrDefaultAsync<Student>("""
            UPDATE Students
            SET    FirstName   = @FirstName,
                   LastName    = @LastName,
                   Email       = @Email,
                   DateOfBirth = @DateOfBirth,
                   Grade       = @Grade
            OUTPUT INSERTED.*
            WHERE  Id = @Id
            """,
            new
            {
                dto.FirstName,
                dto.LastName,
                dto.Email,
                dto.DateOfBirth,
                dto.Grade,
                Id = id
            });

        if (student is not null)
        {
            // Invalidate both caches
            await _cache.RemoveAsync($"students:{id}");
            await _cache.RemoveAsync(AllStudentsCacheKey);
        }

        return student;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        using var connection = _db.CreateConnection();

        var rows = await connection.ExecuteAsync(
            "DELETE FROM Students WHERE Id = @Id",
            new { Id = id });

        if (rows > 0)
        {
            await _cache.RemoveAsync($"students:{id}");
            await _cache.RemoveAsync(AllStudentsCacheKey);
            return true;
        }

        return false;
    }
}
