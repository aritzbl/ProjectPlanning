using System.Net;
using System.Net.Http.Headers;
// using Microsoft.AspNetCore.Mvc; // <- remover, no se usa en servicios
using System.Text.RegularExpressions;
using System.Text.Json;

namespace ProjectPlanning.Web.Services
{
    public class BonitaLoginService
    {
        private readonly HttpClient _http;
        private readonly IConfiguration _config;
        private readonly ILogger<BonitaLoginService> _logger;

        public BonitaLoginService(HttpClient http, IConfiguration config, ILogger<BonitaLoginService> logger)
        {
            _http = http;
            _config = config;
            _logger = logger;
            
            var baseUrl = _config["Bonita:BaseUrl"] ?? "http://localhost:8080/bonita/";
            _http.BaseAddress = new Uri(baseUrl);
            _logger.LogInformation("Bonita BaseUrl configurada: {BaseUrl}", baseUrl);
        }

        public async Task<(string sessionId, string apiToken, string userId, List<string> roles)> LoginAsync(string username, string password)
        {
            try
            {
                _logger.LogInformation("Intentando login en Bonita para usuario: {Username}", username);

                var content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    { "username", username },
                    { "password", password },
                    { "redirect", "false" }
                });

                var response = await _http.PostAsync("loginservice", content);
                _logger.LogInformation("Respuesta de Bonita - Status: {StatusCode}", response.StatusCode);

                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Bonita login failed: {StatusCode} - {Error}", response.StatusCode, error);
                    throw new Exception($"Bonita authentication failed: {response.StatusCode}");
                }

                string? jsessionId = null;
                string? apiToken = null;

                if (response.Headers.TryGetValues("Set-Cookie", out var cookies))
                {
                    var cookieString = string.Join("; ", cookies);
                    var sessionMatch = Regex.Match(cookieString, @"JSESSIONID=([^;,\s]+)");
                    if (sessionMatch.Success) jsessionId = sessionMatch.Groups[1].Value;

                    var tokenMatch = Regex.Match(cookieString, @"X-Bonita-API-Token=([^;,\s]+)");
                    if (tokenMatch.Success) apiToken = tokenMatch.Groups[1].Value;
                }

                if (string.IsNullOrEmpty(jsessionId) || string.IsNullOrEmpty(apiToken))
                {
                    throw new Exception("Error al obtener sesión Bonita: No se encontraron las cookies necesarias");
                }

                var (userId, roles) = await GetUserProfileAsync(jsessionId, apiToken, username);
                _logger.LogInformation("Login OK - Usuario: {Username}, UserId: {UserId}, Roles: {Roles}", username, userId, string.Join(",", roles));

                return (jsessionId, apiToken, userId, roles);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "HTTP error durante login a Bonita");
                throw new Exception($"Error de conexión con Bonita BPM. Verifica que esté corriendo en {_http.BaseAddress}", ex);
            }
        }

        private async Task<(string userId, List<string> roles)> GetUserProfileAsync(string sessionId, string apiToken, string username)
        {
            // 1) Usuario por username
            var userReq = new HttpRequestMessage(HttpMethod.Get, $"API/identity/user?p=0&c=1&f=userName={Uri.EscapeDataString(username)}");
            userReq.Headers.Add("Cookie", $"JSESSIONID={sessionId}");
            userReq.Headers.Add("X-Bonita-API-Token", apiToken);

            var userRes = await _http.SendAsync(userReq);
            if (!userRes.IsSuccessStatusCode)
            {
                _logger.LogWarning("No se pudo obtener el usuario: {Status}", userRes.StatusCode);
                return ("", new List<string>());
            }

            var usersJson = await userRes.Content.ReadAsStringAsync();
            using var usersDoc = JsonDocument.Parse(usersJson);
            if (usersDoc.RootElement.ValueKind != JsonValueKind.Array || usersDoc.RootElement.GetArrayLength() == 0)
            {
                _logger.LogWarning("Usuario no encontrado en Bonita");
                return ("", new List<string>());
            }

            var userElem = usersDoc.RootElement[0];
            var userId = userElem.TryGetProperty("id", out var idProp) ? idProp.GetString() ?? "" : "";
            if (string.IsNullOrEmpty(userId))
            {
                _logger.LogWarning("No se pudo leer el id del usuario");
                return ("", new List<string>());
            }

            // 2) Memberships con expansión del rol (d=role_id)
            var memReq = new HttpRequestMessage(HttpMethod.Get, $"API/identity/membership?p=0&c=100&f=user_id={Uri.EscapeDataString(userId)}&d=role_id");
            memReq.Headers.Add("Cookie", $"JSESSIONID={sessionId}");
            memReq.Headers.Add("X-Bonita-API-Token", apiToken);

            var memRes = await _http.SendAsync(memReq);
            if (!memRes.IsSuccessStatusCode)
            {
                _logger.LogWarning("No se pudieron obtener memberships: {Status}", memRes.StatusCode);
                return (userId, new List<string>());
            }

            var roles = new List<string>();
            var memJson = await memRes.Content.ReadAsStringAsync();
            using var memDoc = JsonDocument.Parse(memJson);

            if (memDoc.RootElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var m in memDoc.RootElement.EnumerateArray())
                {
                    if (m.TryGetProperty("role_id", out var roleObj) && roleObj.ValueKind == JsonValueKind.Object)
                    {
                        if (roleObj.TryGetProperty("name", out var nameProp))
                        {
                            var roleName = nameProp.GetString();
                            if (!string.IsNullOrWhiteSpace(roleName))
                                roles.Add(roleName);
                        }
                    }
                }
            }

            return (userId, roles.Distinct(StringComparer.OrdinalIgnoreCase).ToList());
        }

        public async Task<HttpResponseMessage> GetAsync(string endpoint, string sessionId, string apiToken)
        {
            var req = new HttpRequestMessage(HttpMethod.Get, endpoint);
            req.Headers.Add("Cookie", $"JSESSIONID={sessionId}");
            req.Headers.Add("X-Bonita-API-Token", apiToken);
            return await _http.SendAsync(req);
        }
    }
}