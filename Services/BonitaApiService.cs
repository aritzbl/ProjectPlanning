using System.Text;
using System.Text.Json;
using ProjectPlanning.Web.Models;
using ProjectPlanning.DTOs;

namespace ProjectPlanning.Web.Services
{
    public interface IBonitaApiService
    {
        Task<string> StartProcessInstanceAsync(Project project);
        Task<bool> IsBonitaAvailableAsync();
    }

    public class BonitaApiService : IBonitaApiService
    {
        private readonly HttpClient _httpClient;
        private readonly BonitaConfig _config;
        private readonly ILogger<BonitaApiService> _logger;
        private string? _sessionId;

        public BonitaApiService(HttpClient httpClient, IConfiguration configuration, ILogger<BonitaApiService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
            _config = configuration.GetSection("Bonita").Get<BonitaConfig>() ?? new BonitaConfig();

            _httpClient.BaseAddress = new Uri(_config.BaseUrl); //base url lo busca desde el appsettings.json
        }

       public async Task<bool> IsBonitaAvailableAsync()
{
    try
    {
        var content = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("username", _config.Username),
            new KeyValuePair<string, string>("password", _config.Password),
            new KeyValuePair<string, string>("redirect", "false")
        });

        var response = await _httpClient.PostAsync("loginservice", content);

        return response.IsSuccessStatusCode;
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error checking Bonita availability");
        return false;
    }
}


        public async Task<string> StartProcessInstanceAsync(Project project)
        {
            try
            {
                // con esto se autentica, deberia estar funcionando
                await AuthenticateAsync();

                var processInstance = CreateProcessInstance(project);
                var json = JsonSerializer.Serialize(processInstance, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync($"API/bpm/process/{_config.ProcessDefinitionId}/instantiation", content);

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    var processResponse = JsonSerializer.Deserialize<BonitaProcessInstanceResponse>(responseContent, new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                    });

                    _logger.LogInformation("Process instance created successfully with ID: {ProcessId}", processResponse?.Id);
                    return processResponse?.Id ?? "Unknown";
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Failed to create process instance. Status: {StatusCode}, Error: {Error}",
                        response.StatusCode, errorContent);
                    throw new Exception($"Failed to create process instance: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting process instance for project: {ProjectName}", project.Name);
                throw;
            }
        }

        private async Task AuthenticateAsync()
{
    if (!string.IsNullOrEmpty(_sessionId))
        return;

    try
    {
        var loginData = new Dictionary<string, string>
        {
            { "username", _config.Username },
            { "password", _config.Password },
            { "redirect", "false" }
        };

        var content = new FormUrlEncodedContent(loginData);

        var response = await _httpClient.PostAsync("loginservice", content);

        if (response.IsSuccessStatusCode)
        {
            // Extraer cookie de sesión
            var cookies = response.Headers.GetValues("Set-Cookie").FirstOrDefault();
            if (cookies != null)
            {
                var sessionCookie = cookies.Split(';').FirstOrDefault(c => c.StartsWith("JSESSIONID="));
                if (sessionCookie != null)
                {
                    _sessionId = sessionCookie.Split('=')[1];
                    _httpClient.DefaultRequestHeaders.Remove("Cookie");
                    _httpClient.DefaultRequestHeaders.Add("Cookie", $"JSESSIONID={_sessionId}");
                }
            }

            _logger.LogInformation("✅ Successfully authenticated with Bonita 7.9");
        }
        else
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            _logger.LogError("❌ Authentication failed. Status: {StatusCode}, Error: {Error}", 
                response.StatusCode, errorContent);
            throw new Exception($"Authentication failed: {response.StatusCode}");
        }
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "⚠️ Error during Bonita authentication");
        throw;
    }
}


        private BonitaProcessInstance CreateProcessInstance(Project project)
        {
            return new BonitaProcessInstance
            {
                ProcessDefinitionId = _config.ProcessDefinitionId,
                Variables = new List<BonitaVariable>
                {
                    new() { Name = "projectName", Value = project.Name },
                    new() { Name = "startDate", Value = project.StartDate.ToString("yyyy-MM-dd") },
                    new() { Name = "endDate", Value = project.EndDate.ToString("yyyy-MM-dd") },
                    new() { Name = "coverageType", Value = project.CoverageType },
                    new() { Name = "coverageDescription", Value = project.CoverageDescription }
                }
            };
        }
    }
}
