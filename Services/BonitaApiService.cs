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
        Task CompleteFirstTaskAsync(string caseId);
        Task StartMonitoringProcessesForActiveProjectsAsync(List<Project> activeProjects);
        Task PublishProjectTaskAsync(string caseId);
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
            _httpClient.BaseAddress = new Uri(_config.BaseUrl);
        }

        // ...existing code...
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
                var processId = await GetProcessDefinitionIdAsync("NotificarONGs");

                var processInstance = CreateProcessInstance(project, processId);
                var json = JsonSerializer.Serialize(processInstance, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                _logger.LogInformation("DEBUG Bonita instantiation JSON: {Json}", json);

                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync($"API/bpm/process/{processId}/instantiation", content);

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    _logger.LogInformation("📦 [Bonita Raw Response] {Response}", responseContent);

                    var processResponse = JsonSerializer.Deserialize<BonitaProcessInstanceResponse>(
                        responseContent,
                        new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }
                    );

                    var caseId = processResponse?.CaseId.ToString() ?? "Unknown";
                    _logger.LogInformation("✅ Proceso iniciado correctamente con ID: {CaseId}", caseId);

                    if (caseId != "Unknown")
                    {
                        // 1) Inicializamos variables de caso (idProyecto, projectName, pedidosJSON, etc.)
                        await InitializeCaseVariablesForProjectAsync(caseId, project);

                        // 2) Ejecutamos la tarea "Publicar proyecto"
                        await PublishProjectTaskAsync(caseId);

                        // 3) Ejecutamos la siguiente tarea humana (donde está el conector CrearPedido_API)
                        await CompleteNextTaskForCaseAsync(caseId);
                    }

                    return caseId;
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("❌ Falló la creación de la instancia del proceso. Status: {StatusCode}, Error: {Error}",
                        response.StatusCode, errorContent);
                    throw new Exception($"Failed to create process instance: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "⚠️ Error al iniciar la instancia de proceso para el proyecto: {ProjectName}", project.Name);
                throw;
            }
        }



        private async Task AuthenticateAsync()
        {
            if (!string.IsNullOrEmpty(_sessionId) && !string.IsNullOrEmpty(_apiToken))
                return;

            var loginData = new Dictionary<string, string>
            {
                { "username", _config.Username },
                { "password", _config.Password },
                { "redirect", "false" }
            };

            var content = new FormUrlEncodedContent(loginData);
            var response = await _httpClient.PostAsync("loginservice", content);

            var cookies = response.Headers.GetValues("Set-Cookie");
            var cookieString = string.Join(",", cookies);

            _sessionId = RegexHelper(cookieString, "JSESSIONID");
            _apiToken = RegexHelper(cookieString, "X-Bonita-API-Token");

            _httpClient.DefaultRequestHeaders.Remove("Cookie");
            _httpClient.DefaultRequestHeaders.Remove("X-Bonita-API-Token");

            _httpClient.DefaultRequestHeaders.Add("Cookie", $"JSESSIONID={_sessionId}");
            _httpClient.DefaultRequestHeaders.Add("X-Bonita-API-Token", _apiToken);
        }

        private string? RegexHelper(string cookieString, string key)
        {
            var match = System.Text.RegularExpressions.Regex.Match(cookieString, $"{key}=([^;,]+)");
            return match.Success ? match.Groups[1].Value : null;
        }

        public async Task<string> GetProcessDefinitionIdAsync(string processName)
        {
            await AuthenticateAsync();

            var response = await _httpClient.GetAsync($"API/bpm/process?p=0&c=10&f=name={processName}");
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            var list = JsonSerializer.Deserialize<List<JsonElement>>(json);

            if (list == null || list.Count == 0)
                throw new Exception($"Process '{processName}' not found in Bonita.");

            // ELEGIR LA VERSIÓN MÁS ALTA
            JsonElement best = list[0];
            foreach (var p in list)
            {
                string vBest = best.GetProperty("version").GetString() ?? "0";
                string vCur = p.GetProperty("version").GetString() ?? "0";

                // si la versión actual es mayor, reemplazar
                if (string.Compare(vCur, vBest, StringComparison.Ordinal) > 0)
                    best = p;
            }

            return best.GetProperty("id").GetString()
                ?? throw new Exception("Process ID is null");
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
                    var processes = JsonSerializer.Deserialize<List<BonitaProcess>>(responseContent,
                        new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

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

        private BonitaProcessInstance CreateProcessInstance(Project project, string processId)
        {
            return new BonitaProcessInstance
            {
                ProcessDefinitionId = processId,
                Variables = BonitaProjectMapper.MapFromProject(project)
            };
        }


        public async Task CompleteFirstTaskAsync(string caseId)
        {
            await AuthenticateAsync();

            try
            {
                for (int i = 0; i < 5; i++)
                {
                    _logger.LogInformation("🔍 Buscando tareas (rootCaseId={CaseId}) (Intento {Try}/5)", caseId, i + 1);

                    var response = await _httpClient.GetAsync($"API/bpm/task?f=rootCaseId={caseId}&p=0&c=10");
                    var rawText = await response.Content.ReadAsStringAsync();
                    _logger.LogInformation("📨 Respuesta de Bonita (Intento {Try}): {Response}", i + 1, rawText);

                    if (response.IsSuccessStatusCode)
                    {
                        var tasks = JsonSerializer.Deserialize<List<JsonElement>>(rawText);

                        if (tasks != null)
                        {
                            var task = tasks.FirstOrDefault(t =>
                                t.TryGetProperty("type", out var type) && type.GetString() == "USER_TASK" &&
                                t.TryGetProperty("state", out var state) && state.GetString() == "ready");

                            if (task.ValueKind != JsonValueKind.Undefined)
                            {
                                var taskId = task.GetProperty("id").GetString();
                                _logger.LogInformation("🟢 Tarea encontrada para el caseId={CaseId}: {TaskId}", caseId, taskId);

                                var assignBody = new StringContent(
                                    JsonSerializer.Serialize(new { assigned_id = _config.UserId }),
                                    Encoding.UTF8,
                                    "application/json"
                                );

                                var assignResponse = await _httpClient.PutAsync($"API/bpm/userTask/{taskId}", assignBody);
                                assignResponse.EnsureSuccessStatusCode();
                                _logger.LogInformation("✅ Tarea {TaskId} asignada correctamente a usuario {UserId}.", taskId, _config.UserId);

                                var execResponse = await _httpClient.PostAsync(
                                    $"API/bpm/userTask/{taskId}/execution",
                                    new StringContent("{}", Encoding.UTF8, "application/json")
                                );
                                execResponse.EnsureSuccessStatusCode();
                                _logger.LogInformation("🏁 Tarea {TaskId} completada automáticamente.", taskId);

                                return;
                            }
                        }
                    }

                    _logger.LogWarning("⚠️ No se encontró tarea lista aún para el caseId={CaseId}. Reintentando...", caseId);
                    await Task.Delay(1000);
                }

                _logger.LogError("❌ No se pudo encontrar tarea humana para el CaseId={CaseId} luego de varios intentos.", caseId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error al intentar completar la primera tarea automáticamente (CaseId: {CaseId})", caseId);
                throw;
            }
        }

        public async Task StartMonitoringProcessesForActiveProjectsAsync(List<Project> activeProjects)
        {
            if (activeProjects == null || activeProjects.Count == 0)
            {
                _logger.LogInformation("No hay proyectos activos para iniciar el proceso de monitoreo.");
                return;
            }

            try
            {
                await AuthenticateAsync();

                // ID del proceso Monitoreo y Control
                var processId = await GetProcessDefinitionIdAsync("Monitoreo"); 

                foreach (var project in activeProjects)
                {
                    var processInstance = new BonitaProcessInstance
                    {
                        ProcessDefinitionId = processId,
                        Variables = new List<BonitaVariable>
                        {
                            new() { Name = "creatorEmail", Value = project.CreatorEmail },
                            new() { Name = "projectId", Value = project.Id }
                        }
                    };

                    var json = JsonSerializer.Serialize(processInstance, new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                    });

                    var content = new StringContent(json, Encoding.UTF8, "application/json");

                    var response = await _httpClient.PostAsync($"API/bpm/process/{processId}/instantiation", content);
                    if (response.IsSuccessStatusCode)
                    {
                        _logger.LogInformation("✅ Proceso de monitoreo iniciado para proyecto {ProjectId} ({ProjectName})", project.Id, project.Name);
                    }
                    else
                    {
                        var errorContent = await response.Content.ReadAsStringAsync();
                        _logger.LogError("❌ Falló al iniciar proceso de monitoreo para proyecto {ProjectId}: {Error}", project.Id, errorContent);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "⚠️ Error iniciando procesos de monitoreo para proyectos activos.");
                throw;
            }
        }

        public async Task CompleteNextTaskForCaseAsync(string caseId)
        {
            await AuthenticateAsync();

            // 1) Buscar la próxima tarea humana READY del caseId
            var resp = await _httpClient.GetAsync(
                $"API/bpm/humanTask?p=0&c=1&f=caseId={caseId}&f=state=ready");

            resp.EnsureSuccessStatusCode();
            var json = await resp.Content.ReadAsStringAsync();
            var tasks = JsonSerializer.Deserialize<List<JsonElement>>(json) ?? new();

            if (!tasks.Any())
            {
                _logger.LogInformation("⚠️ No hay tareas READY para el caseId {CaseId}", caseId);
                return;
            }

            var task = tasks[0];
            var taskId = task.GetProperty("id").GetString();
            var name = task.GetProperty("name").GetString();

            _logger.LogInformation("🟢 Completando tarea {Name} (ID={TaskId})", name, taskId);

            // 2) Asignar la tarea
            var assignBody = new { assigned_id = _config.UserId };
            var assignContent = new StringContent(
                JsonSerializer.Serialize(assignBody),
                Encoding.UTF8, "application/json");

            var assignResp = await _httpClient.PutAsync($"API/bpm/humanTask/{taskId}", assignContent);
            assignResp.EnsureSuccessStatusCode();

            // 3) Ejecutar la tarea
            var execContent = new StringContent("{}", Encoding.UTF8, "application/json");
            var execResp = await _httpClient.PostAsync(
                $"API/bpm/userTask/{taskId}/execution",
                execContent);

            execResp.EnsureSuccessStatusCode();

            _logger.LogInformation("🏁 Tarea {TaskId} completada correctamente.", taskId);
        }

        
        public async Task PublishProjectTaskAsync(string caseId)
        {
            await AuthenticateAsync();

            // Vamos a reintentar varias veces, por si la tarea todavía no está lista
            const int maxRetries = 10;
            const int delayMs = 1000;

            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                // 1) Pedimos las tareas READY del proceso usando rootCaseId
                var url = $"API/bpm/humanTask?p=0&c=10&f=rootCaseId={caseId}&f=state=ready";
                var resp = await _httpClient.GetAsync(url);

                var json = await resp.Content.ReadAsStringAsync();
                _logger.LogInformation(
                    "🔥 (Intento {Attempt}/{Max}) Tareas READY para rootCaseId={CaseId}: {Json}",
                    attempt, maxRetries, caseId, json);

                resp.EnsureSuccessStatusCode();

                var tasks = JsonSerializer.Deserialize<List<JsonElement>>(json) ?? new();

                if (!tasks.Any())
                {
                    _logger.LogWarning(
                        "⚠️ (Intento {Attempt}/{Max}) No hay tareas READY aún para rootCaseId={CaseId}.",
                        attempt, maxRetries, caseId);

                    if (attempt < maxRetries)
                    {
                        await Task.Delay(delayMs);
                        continue;
                    }
                    else
                    {
                        _logger.LogError(
                            "❌ No se encontraron tareas READY para rootCaseId={CaseId} después de {Max} intentos.",
                            caseId, maxRetries);
                        return;
                    }
                }

                // 2) Buscar la tarea cuyo displayName contenga 'Publicar'
                var task = tasks.FirstOrDefault(t =>
                    t.TryGetProperty("displayName", out var displayNameProp) &&
                    displayNameProp.GetString()!
                        .Contains("Publicar", StringComparison.OrdinalIgnoreCase));

                
                if (task.ValueKind == JsonValueKind.Undefined)
                {
                    task = tasks[0];

                    var dn = task.TryGetProperty("displayName", out var dnProp)
                        ? dnProp.GetString()
                        : "(sin displayName)";

                    _logger.LogWarning(
                        "⚠️ No se encontró tarea con 'Publicar' en displayName. " +
                        "Se ejecutará la primera tarea READY: {DisplayName}",
                        dn);
                }

                var taskId = task.GetProperty("id").GetString();
                var displayName = task.GetProperty("displayName").GetString();

                _logger.LogInformation(
                    "🟢 Ejecutando tarea {DisplayName} (ID={TaskId}) para rootCaseId {CaseId}",
                    displayName, taskId, caseId);  

                // 3) Asignar la tarea al usuario configurado
                var assignBody = new { assigned_id = _config.UserId };
                var assignContent = new StringContent(
                    JsonSerializer.Serialize(assignBody),
                    Encoding.UTF8,
                    "application/json");

                var assignResp = await _httpClient.PutAsync($"API/bpm/humanTask/{taskId}", assignContent);
                assignResp.EnsureSuccessStatusCode();
                _logger.LogInformation(
                    "✅ Tarea {TaskId} asignada correctamente al usuario {UserId}.",
                    taskId, _config.UserId);

                // 4) Ejecutar la tarea
                var execContent = new StringContent("{}", Encoding.UTF8, "application/json");
                var execResp = await _httpClient.PostAsync(
                    $"API/bpm/userTask/{taskId}/execution",
                    execContent);
                execResp.EnsureSuccessStatusCode();

                _logger.LogInformation(
                    "🏁 Tarea '{DisplayName}' completada correctamente (TaskId={TaskId}).",
                    displayName, taskId);

                return;
            }
        }

        private async Task SetCaseVariableAsync(string caseId, string name, object value, string javaType)
        {
            await AuthenticateAsync(); 

            var payload = new
            {
                type = javaType,
                value = value
            };

            var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var url = $"API/bpm/caseVariable/{caseId}/{name}";
            _logger.LogInformation("DEBUG SetCaseVariable: {Url} Body={Body}", url, json);

            var resp = await _httpClient.PutAsync(url, content);
            if (!resp.IsSuccessStatusCode)
            {
                var err = await resp.Content.ReadAsStringAsync();
                _logger.LogError("❌ Error al setear variable de caso {Name} para caseId={CaseId}. Status={Status}. Body={Body}",
                    name, caseId, resp.StatusCode, err);
                resp.EnsureSuccessStatusCode();
            }
        }

        private async Task InitializeCaseVariablesForProjectAsync(string caseId, Project project)
        {
            await SetCaseVariableAsync(caseId, "idProyecto", project.Id, "java.lang.Long");
            await SetCaseVariableAsync(caseId, "projectName", project.Name, "java.lang.String");
            await SetCaseVariableAsync(caseId, "responsableONG", project.CreatorEmail, "java.lang.String");

            var recursos = project.Resources ?? new List<Resource>();

            var pedidosPayload = recursos.Select(r => new
            {
                
                titulo = r.Name,                 
                descripcion = r.Name,            
                contacto = r.ContactEmail,       
                estado = string.IsNullOrEmpty(r.State) ? "pending" : r.State
            });

            var pedidosJson = JsonSerializer.Serialize(pedidosPayload);

            await SetCaseVariableAsync(caseId, "pedidosJSON", pedidosJson, "java.lang.String");

            var tituloPedido = project.Name;
            var descripcionPedido = $"Proyecto {project.Name} creado desde la aplicación .NET";

            var vencimientoPedido = project.EndDate.ToString("yyyy-MM-dd");

            await SetCaseVariableAsync(caseId, "tituloPedido", tituloPedido, "java.lang.String");
            await SetCaseVariableAsync(caseId, "descripcionPedido", descripcionPedido, "java.lang.String");
            await SetCaseVariableAsync(caseId, "vencimientoPedido", vencimientoPedido, "java.lang.String");

            await SetCaseVariableAsync(caseId, "etapald", 1, "java.lang.Integer");
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