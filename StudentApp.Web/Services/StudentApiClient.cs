using System.Net.Http.Json;
using StudentApp.Shared.Messaging;
using StudentApp.Shared.Models;

namespace StudentApp.Web.Services;

public interface IStudentApiClient
{
    Task<(List<Student> students, bool fromCache)> GetAllStudentsAsync();
    Task<Student?> GetStudentAsync(int id);
    Task<(Guid correlationId, string statusUrl, string message)> SubmitRegistrationAsync(CreateStudentDto dto);
    Task<RegistrationStatus?> GetRegistrationStatusAsync(Guid correlationId);
    Task<Student?> UpdateStudentAsync(int id, UpdateStudentDto dto);
    Task<bool> DeleteStudentAsync(int id);
}

public class StudentApiClient : IStudentApiClient
{
    private readonly HttpClient _http;
    private readonly ILogger<StudentApiClient> _logger;

    public StudentApiClient(HttpClient http, ILogger<StudentApiClient> logger)
    {
        _http = http;
        _logger = logger;
    }

    public async Task<(List<Student> students, bool fromCache)> GetAllStudentsAsync()
    {
        try
        {
            var response = await _http.GetFromJsonAsync<ApiResponse<List<Student>>>("api/students");
            return (response?.Data ?? [], response?.FromCache ?? false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get students");
            return ([], false);
        }
    }

    public async Task<Student?> GetStudentAsync(int id)
    {
        try
        {
            var response = await _http.GetFromJsonAsync<ApiResponse<Student>>($"api/students/{id}");
            return response?.Data;
        }
        catch (Exception ex) { _logger.LogError(ex, "Failed to get student {Id}", id); return null; }
    }

    // Submit async registration — returns correlationId for tracking
    public async Task<(Guid correlationId, string statusUrl, string message)> SubmitRegistrationAsync(CreateStudentDto dto)
    {
        try
        {
            var result = await _http.PostAsJsonAsync("api/registration", dto);
            var body = await result.Content.ReadFromJsonAsync<RegistrationSubmittedResponse>();
            return (body!.CorrelationId, body.StatusUrl, body.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to submit registration");
            return (Guid.Empty, string.Empty, "Failed to submit registration");
        }
    }

    // Polling fallback — called every 3s if SignalR is not connected
    public async Task<RegistrationStatus?> GetRegistrationStatusAsync(Guid correlationId)
    {
        try
        {
            return await _http.GetFromJsonAsync<RegistrationStatus>(
                $"api/registration/status/{correlationId}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get registration status");
            return null;
        }
    }

    public async Task<Student?> UpdateStudentAsync(int id, UpdateStudentDto dto)
    {
        try
        {
            var result = await _http.PutAsJsonAsync($"api/students/{id}", dto);
            if (!result.IsSuccessStatusCode) return null;
            var response = await result.Content.ReadFromJsonAsync<ApiResponse<Student>>();
            return response?.Data;
        }
        catch (Exception ex) { _logger.LogError(ex, "Failed to update student {Id}", id); return null; }
    }

    public async Task<bool> DeleteStudentAsync(int id)
    {
        try
        {
            var result = await _http.DeleteAsync($"api/students/{id}");
            return result.IsSuccessStatusCode;
        }
        catch (Exception ex) { _logger.LogError(ex, "Failed to delete student {Id}", id); return false; }
    }
}

// Matches the 202 response body from RegistrationController
public class RegistrationSubmittedResponse
{
    public Guid   CorrelationId { get; set; }
    public string Message       { get; set; } = string.Empty;
    public string StatusUrl     { get; set; } = string.Empty;
}
