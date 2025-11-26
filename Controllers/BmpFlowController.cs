using Microsoft.AspNetCore.Mvc;
using ProjectPlanning.Web.Services;
using ProjectPlanning.Web.Data;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace ProjectPlanning.Web.Controllers
{
    // Controlador MVC para las vistas
    public class BmpFlowViewController : Controller
    {
        public IActionResult Index()
        {
            return View("~/Views/BmpFlow/Index.cshtml");
        }
    }

    // Controlador API para las operaciones
    [ApiController]
    [Route("api/[controller]")]
    public class BmpFlowController : ControllerBase
    {
        private readonly IBonitaApiService _bonitaService;
        private readonly ApplicationDbContext _context;
        private readonly ILogger<BmpFlowController> _logger;

        public BmpFlowController(IBonitaApiService bonitaService, ApplicationDbContext context, ILogger<BmpFlowController> logger)
        {
            _bonitaService = bonitaService;
            _context = context;
            _logger = logger;
        }

        // POST: Confirmar envío de avances por email
        [HttpPost("confirm-send-progress/{projectId}")]
        public async Task<IActionResult> ConfirmSendProgress(int projectId, [FromBody] JsonElement body)
        {
            try
            {
                var project = await _context.Projects.FindAsync(projectId);
                if (project == null)
                    return NotFound(new { message = "Proyecto no encontrado" });

                if (string.IsNullOrEmpty(project.ProcessInstanceId))
                    return BadRequest(new { message = "El proyecto no tiene un proceso asociado en Bonita" });

                // Avanzar automáticamente a "Recibe email de avances"
                var success = await AdvanceToReceiveEmailTask(project.ProcessInstanceId);
                
                if (success)
                {
                    _logger.LogInformation("✅ Proceso avanzado a 'Recibe email de avances' para proyecto {ProjectId}", projectId);
                    return Ok(new { message = "Avances confirmados y proceso avanzado exitosamente" });
                }
                else
                {
                    return BadRequest(new { message = "Error al avanzar el proceso en Bonita" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error confirmando envío de avances para proyecto {ProjectId}", projectId);
                return StatusCode(500, new { message = "Error interno del servidor" });
            }
        }

        // POST: Evaluar compuerta de decisión (acepta/rechaza avances)
        [HttpPost("evaluate-progress-gate/{projectId}")]
        public async Task<IActionResult> EvaluateProgressGate(int projectId, [FromBody] JsonElement body)
        {
            try
            {
                if (!body.TryGetProperty("progressAccepted", out var progressAcceptedElement))
                    return BadRequest(new { message = "El campo 'progressAccepted' es requerido" });

                var progressAccepted = progressAcceptedElement.GetBoolean();
                var project = await _context.Projects.FindAsync(projectId);
                
                if (project == null)
                    return NotFound(new { message = "Proyecto no encontrado" });

                if (progressAccepted)
                {
                    // Avanzar a "Recibir observaciones"
                    var success = await AdvanceToReceiveObservationsTask(project.ProcessInstanceId!);
                    if (success)
                    {
                        return Ok(new { 
                            message = "Avances aceptados. Proceso avanzado a recibir observaciones",
                            action = "show_observations_popup"
                        });
                    }
                }
                else
                {
                    // Finalizar proceso
                    var success = await FinalizeBpmProcess(project.ProcessInstanceId!);
                    if (success)
                    {
                        project.ProcessStatus = "COMPLETED";
                        await _context.SaveChangesAsync();
                        return Ok(new { 
                            message = "Avances rechazados. Proceso finalizado",
                            action = "process_ended"
                        });
                    }
                }

                return BadRequest(new { message = "Error al procesar la decisión en Bonita" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error evaluando compuerta para proyecto {ProjectId}", projectId);
                return StatusCode(500, new { message = "Error interno del servidor" });
            }
        }

        // POST: Confirmar que se revisaron las observaciones
        [HttpPost("confirm-observations-reviewed/{projectId}")]
        public async Task<IActionResult> ConfirmObservationsReviewed(int projectId)
        {
            try
            {
                var project = await _context.Projects.FindAsync(projectId);
                if (project == null)
                    return NotFound(new { message = "Proyecto no encontrado" });

                // Avanzar automáticamente a "Resolver observaciones"
                var success = await AdvanceToResolveObservationsTask(project.ProcessInstanceId!);
                
                if (success)
                {
                    return Ok(new { 
                        message = "Avanzado a resolver observaciones",
                        action = "resolve_observations"
                    });
                }

                return BadRequest(new { message = "Error al avanzar el proceso en Bonita" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error confirmando revisión de observaciones para proyecto {ProjectId}", projectId);
                return StatusCode(500, new { message = "Error interno del servidor" });
            }
        }

        // POST: Confirmar resolución de observaciones y finalizar proceso
        [HttpPost("resolve-observations-complete/{projectId}")]
        public async Task<IActionResult> ResolveObservationsComplete(int projectId)
        {
            try
            {
                var project = await _context.Projects.FindAsync(projectId);
                if (project == null)
                    return NotFound(new { message = "Proyecto no encontrado" });

                // Completar la tarea "Resolver observaciones" para finalizar el proceso
                var success = await CompleteResolveObservationsTask(project.ProcessInstanceId!);
                
                if (success)
                {
                    project.ProcessStatus = "COMPLETED";
                    await _context.SaveChangesAsync();
                    
                    return Ok(new { 
                        message = "Observaciones resueltas. Proceso completado exitosamente",
                        action = "process_completed"
                    });
                }

                return BadRequest(new { message = "Error al finalizar el proceso en Bonita" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error finalizando proceso para proyecto {ProjectId}", projectId);
                return StatusCode(500, new { message = "Error interno del servidor" });
            }
        }

        // Métodos privados para manejar las tareas de Bonita
        private async Task<bool> AdvanceToReceiveEmailTask(string processInstanceId)
        {
            try
            {
                // Completar la tarea actual "Envía email de avances" para avanzar automáticamente a "Recibe email de avances"
                var taskId = await GetActiveTaskId(processInstanceId);
                if (!string.IsNullOrEmpty(taskId))
                {
                    return await CompleteTaskInBonita(taskId);
                }
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error avanzando a tarea 'Recibe email de avances'");
                return false;
            }
        }

        private async Task<bool> AdvanceToReceiveObservationsTask(string processInstanceId)
        {
            try
            {
                // Completar "Recibe email de avances" con la variable hizoObservaciones = true
                var taskId = await GetActiveTaskId(processInstanceId);
                if (!string.IsNullOrEmpty(taskId))
                {
                    var decisionVariables = new Dictionary<string, object>
                    {
                        { "hizoObservaciones", true }
                    };
                    return await _bonitaService.CompleteTaskWithDecisionAsync(taskId, decisionVariables);
                }
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error avanzando a tarea 'Recibir observaciones'");
                return false;
            }
        }

        private async Task<bool> AdvanceToResolveObservationsTask(string processInstanceId)
        {
            try
            {
                // Completar la tarea "Recibir observaciones" para avanzar a "Resolver observaciones"
                var taskId = await GetActiveTaskId(processInstanceId);
                if (!string.IsNullOrEmpty(taskId))
                {
                    return await CompleteTaskInBonita(taskId);
                }
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error avanzando a tarea 'Resolver observaciones'");
                return false;
            }
        }

        private async Task<bool> FinalizeBpmProcess(string processInstanceId)
        {
            try
            {
                // Completar la tarea actual con hizoObservaciones = false para finalizar el proceso
                var taskId = await GetActiveTaskId(processInstanceId);
                if (!string.IsNullOrEmpty(taskId))
                {
                    var decisionVariables = new Dictionary<string, object>
                    {
                        { "hizoObservaciones", false }
                    };
                    return await _bonitaService.CompleteTaskWithDecisionAsync(taskId, decisionVariables);
                }
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error finalizando proceso BPM");
                return false;
            }
        }

        private async Task<bool> CompleteResolveObservationsTask(string processInstanceId)
        {
            try
            {
                // Completar la tarea "Resolver observaciones" para finalizar el proceso completamente
                var taskId = await GetActiveTaskId(processInstanceId);
                if (!string.IsNullOrEmpty(taskId))
                {
                    return await CompleteTaskInBonita(taskId);
                }
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error completando tarea 'Resolver observaciones'");
                return false;
            }
        }

        private async Task<string?> GetActiveTaskId(string processInstanceId, string? taskName = null)
        {
            return await _bonitaService.GetActiveTaskIdAsync(processInstanceId, taskName);
        }

        private async Task<bool> CompleteTaskInBonita(string taskId)
        {
            return await _bonitaService.CompleteTaskAsync(taskId);
        }
    }
}