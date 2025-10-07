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
        Task<List<BonitaProcess>> GetAvailableProcessesAsync();
    }

    public class BonitaApiService : IBonitaApiService
    {
        private readonly HttpClient _httpClient;
        private readonly BonitaConfig _config;
        private readonly ILogger<BonitaApiService> _logger;
        private string? _sessionId;
        private string? _apiToken;

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
                await AuthenticateAsync();

                var processId = await GetProcessDefinitionIdAsync();

                var processInstance = CreateProcessInstance(project, processId);
                var json = JsonSerializer.Serialize(processInstance, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync($"API/bpm/process/{processId}/instantiation", content);

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
            if (!string.IsNullOrEmpty(_sessionId) && !string.IsNullOrEmpty(_apiToken))
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

                    if (response.Headers.TryGetValues("Set-Cookie", out var cookieHeaders))
                    {
                        var allCookies = string.Join("; ", cookieHeaders);
                        _logger.LogDebug("Received cookies: {Cookies}", allCookies);


                        var cookieString = string.Join(",", cookieHeaders);

                        var sessionMatch = System.Text.RegularExpressions.Regex.Match(cookieString, @"JSESSIONID=([^;,]+)");
                        if (sessionMatch.Success)
                        {
                            _sessionId = sessionMatch.Groups[1].Value;
                        }


                        var tokenMatch = System.Text.RegularExpressions.Regex.Match(cookieString, @"X-Bonita-API-Token=([^;,]+)");
                        if (tokenMatch.Success)
                        {
                            _apiToken = tokenMatch.Groups[1].Value;
                        }

                        _httpClient.DefaultRequestHeaders.Remove("Cookie");
                        _httpClient.DefaultRequestHeaders.Remove("X-Bonita-API-Token");
                        
                        if (!string.IsNullOrEmpty(_sessionId))
                        {
                            _httpClient.DefaultRequestHeaders.Add("Cookie", $"JSESSIONID={_sessionId}");
                        }
                        
                        if (!string.IsNullOrEmpty(_apiToken))
                        {
                            _httpClient.DefaultRequestHeaders.Add("X-Bonita-API-Token", _apiToken);
                        }

                        _logger.LogInformation("✅ Successfully authenticated with Bonita 7.9. SessionId: {SessionId}, ApiToken: {ApiToken}", 
                            _sessionId, _apiToken);
                    }
                    else
                    {
                        _logger.LogWarning("⚠️ No Set-Cookie headers found in authentication response");
                    }
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

        public async Task<String> GetProcessDefinitionIdAsync()
        {
            var processName = _config.ProcessDefinitionId; 

            await AuthenticateAsync();

            var response = await _httpClient.GetAsync($"API/bpm/process?p=0&c=10&f=name={processName}");

            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            var list = JsonSerializer.Deserialize<List<JsonElement>>(json);

            if (list != null && list.Count > 0)
            {
                return list[0].GetProperty("id").GetString() ?? throw new Exception("Process ID is null");
            }

            throw new Exception($"Process '{processName}' not found in Bonita.");
        }

        public async Task<List<BonitaProcess>> GetAvailableProcessesAsync()
        {
            try
            {
                await AuthenticateAsync();

                var response = await _httpClient.GetAsync("API/bpm/process?p=0&c=100");

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    var processes = JsonSerializer.Deserialize<List<BonitaProcess>>(responseContent, new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                    });

                    _logger.LogInformation("Found {Count} available processes", processes?.Count ?? 0);
                    return processes ?? new List<BonitaProcess>();
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Failed to get processes. Status: {StatusCode}, Error: {Error}",
                        response.StatusCode, errorContent);
                    return new List<BonitaProcess>();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting available processes");
                return new List<BonitaProcess>();
            }
        }

        private BonitaProcessInstance CreateProcessInstance(Project project, String processId)
        {
            return new BonitaProcessInstance
            {
                ProcessDefinitionId = processId.ToString(),
                Variables = new List<BonitaVariable>
                {
                    new() { Name = "projectName", Value = project.Name },
                    new() { Name = "startDate", Value = project.StartDate.ToString("yyyy-MM-dd") },
                    new() { Name = "endDate", Value = project.EndDate.ToString("yyyy-MM-dd") },
                    new() { Name = "resources", Value = project.Resources },
                }
            };
        }
    }

    public class BonitaProcess
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;
    }
}
