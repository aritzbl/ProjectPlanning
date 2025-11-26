using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProjectPlanning.Web.Data;
using ProjectPlanning.Web.Models;
using ProjectPlanning.Web.DTOs;
using System.Text.Json;

namespace ProjectPlanning.Controllers
{
    public class ObservationController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<ObservationController> _logger;

        public ObservationController(ApplicationDbContext context, ILogger<ObservationController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // Vista para ONG_Offering_Resources (agregar observaciones)
        public IActionResult Index()
        {
            return View();
        }

        // Vista para ONG_Offering_Projects (ver y resolver observaciones)
        public IActionResult ManageObservations()
        {
            return View();
        }

        // GET: Proyectos que pueden recibir observaciones (15+ días desde StartDate) - Para Offering_Resources
        [HttpGet]
        [Route("api/projects/eligible-for-observations")]
        public async Task<IActionResult> GetProjectsEligibleForObservations([FromQuery] string? email)
        {
            if (string.IsNullOrEmpty(email))
                return BadRequest(new { message = "Email es requerido" });

            var nowUtc = DateTime.UtcNow;
            var fifteenDaysAgo = nowUtc.AddDays(-15);

            var projects = await _context.Projects
                .Include(p => p.Observations)
                .Include(p => p.Resources)
                .Where(p => p.StartDate <= fifteenDaysAgo
                         && (p.ActualEndDate == null || p.ActualEndDate > nowUtc) // Solo proyectos activos
                         && p.Resources.Any(r => !string.IsNullOrEmpty(r.ContactEmail) 
                                              && r.ContactEmail == email
                                              && r.State == "accepted")) // Solo proyectos donde el usuario ofreció recursos
                .OrderBy(p => p.StartDate)
                .ToListAsync();

            // Obtener las últimas acciones del usuario para cada proyecto
            var projectIds = projects.Select(p => p.Id).ToList();
            var lastActions = await _context.ObservationActions
                .Where(a => projectIds.Contains(a.ProjectId) && a.UserEmail == email)
                .GroupBy(a => a.ProjectId)
                .Select(g => new
                {
                    ProjectId = g.Key,
                    LastActionDate = g.Max(a => a.ActionDate)
                })
                .ToListAsync();

            var eligibleProjects = projects.Select(p =>
            {
                var lastAction = lastActions.FirstOrDefault(a => a.ProjectId == p.Id);
                var daysSinceLastAction = lastAction != null
                    ? (int)(nowUtc - lastAction.LastActionDate).TotalDays
                    : 999; // Si no hay acción previa, está disponible

                var lastObservation = p.Observations.OrderByDescending(o => o.CreatedAt).FirstOrDefault();
                
                return new ProjectEligibleForObservationsDto
                {
                    Id = p.Id,
                    Name = p.Name ?? "",
                    StartDate = p.StartDate,
                    EndDate = p.EndDate,
                    DaysSinceStart = (int)(nowUtc - p.StartDate).TotalDays,
                    CanAddObservation = daysSinceLastAction >= 15,
                    DaysSinceLastAction = daysSinceLastAction,
                    ObservationCount = p.Observations.Count,
                    LastObservation = lastObservation != null ? new ObservationResponseDto
                    {
                        Id = lastObservation.Id,
                        Content = lastObservation.Content ?? "",
                        CreatedAt = lastObservation.CreatedAt,
                        IsResolved = lastObservation.IsResolved,
                        CreatedBy = lastObservation.CreatedBy ?? "",
                        ProjectId = lastObservation.ProjectId
                    } : null
                };
            }).ToList();

            return Ok(eligibleProjects);
        }

        // POST: Agregar observación a un proyecto (Offering_Resources)
        [HttpPost]
        [Route("api/projects/{projectId}/observations")]
        public async Task<IActionResult> AddObservation(int projectId, [FromBody] JsonElement body)
        {
            try {
                if (!body.TryGetProperty("content", out var contentElement))
                    return BadRequest(new { message = "El campo 'content' es obligatorio" });

                if (!body.TryGetProperty("createdBy", out var createdByElement))
                    return BadRequest(new { message = "El campo 'createdBy' es obligatorio" });

                var content = contentElement.GetString()?.Trim();
                var createdBy = createdByElement.GetString()?.Trim();

                if (string.IsNullOrEmpty(content))
                    return BadRequest(new { message = "La observación no puede estar vacía" });

                if (string.IsNullOrEmpty(createdBy))
                    return BadRequest(new { message = "El creador es obligatorio" });

                var project = await _context.Projects
                    .Include(p => p.Resources)
                    .FirstOrDefaultAsync(p => p.Id == projectId);
                    
                if (project == null)
                    return NotFound(new { message = "Proyecto no encontrado" });

                // Validar que han pasado al menos 15 días desde el inicio
                var daysSinceStart = (DateTime.UtcNow - project.StartDate).TotalDays;
                if (daysSinceStart < 15)
                    return BadRequest(new 
                    { 
                        message = $"Solo se pueden agregar observaciones después de 15 días. Días transcurridos: {Math.Floor(daysSinceStart)}" 
                    });

                // Validar que el usuario que crea la observación ofreció algún recurso en este proyecto
                var hasOfferedResource = project.Resources.Any(r => 
                    !string.IsNullOrEmpty(r.ContactEmail) && 
                    r.ContactEmail == createdBy &&
                    r.State == "accepted");

                if (!hasOfferedResource)
                    return BadRequest(new 
                    { 
                        message = "Solo las ONGs que ofrecieron y tienen recursos aceptados en este proyecto pueden agregar observaciones" 
                    });

                // Validar que han pasado 15 días desde la última acción
                var lastAction = await _context.ObservationActions
                    .Where(a => a.ProjectId == projectId && a.UserEmail == createdBy)
                    .OrderByDescending(a => a.ActionDate)
                    .FirstOrDefaultAsync();

                if (lastAction != null)
                {
                    var daysSinceLastAction = (DateTime.UtcNow - lastAction.ActionDate).TotalDays;
                    if (daysSinceLastAction < 15)
                    {
                        return BadRequest(new
                        {
                            message = $"Solo puedes hacer una observación cada 15 días. Días desde la última acción: {Math.Floor(daysSinceLastAction)}"
                        });
                    }
                }

                var observation = new Observation
                {
                    ProjectId = projectId,
                    Content = content,
                    CreatedBy = createdBy,
                    CreatedAt = DateTime.UtcNow
                };

                _context.Observations.Add(observation);

                // Registrar la acción
                var action = new ObservationAction
                {
                    ProjectId = projectId,
                    UserEmail = createdBy,
                    ActionType = "observation",
                    ActionDate = DateTime.UtcNow
                };

                _context.ObservationActions.Add(action);
                await _context.SaveChangesAsync();

                _logger.LogInformation("✅ Observación agregada al proyecto {ProjectId} por {CreatedBy}", projectId, createdBy);

                return Ok(new
                {
                    message = "Observación agregada correctamente",
                    observation = new
                    {
                        observation.Id,
                        observation.Content,
                        observation.CreatedAt,
                        observation.CreatedBy,
                        observation.IsResolved
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error al agregar observación al proyecto {ProjectId}", projectId);
                return StatusCode(500, new { message = "Error interno del servidor" });
            }
        }

        // GET: Obtener observaciones de un proyecto
        [HttpGet]
        [Route("api/projects/{projectId}/observations")]
        public async Task<IActionResult> GetObservations(int projectId)
        {
            var observations = await _context.Observations
                .Where(o => o.ProjectId == projectId)
                .OrderByDescending(o => o.CreatedAt)
                .Select(o => new
                {
                    o.Id,
                    o.Content,
                    o.CreatedAt,
                    o.CreatedBy,
                    o.IsResolved
                })
                .ToListAsync();

            return Ok(observations);
        }

        // GET: Proyectos del creador con observaciones - Para Offering_Projects
        [HttpGet]
        [Route("api/projects/with-observations")]
        public async Task<IActionResult> GetProjectsWithObservations([FromQuery] string? creatorEmail)
        {
            if (string.IsNullOrEmpty(creatorEmail))
                return BadRequest(new { message = "Email del creador es requerido" });

            var projectsWithObservations = await _context.Projects
                .Include(p => p.Observations)
                .Where(p => p.CreatorEmail == creatorEmail && p.Observations.Any())
                .Select(p => new
                {
                    p.Id,
                    p.Name,
                    p.StartDate,
                    p.EndDate,
                    TotalObservations = p.Observations.Count,
                    UnresolvedObservations = p.Observations.Count(o => !o.IsResolved),
                    ResolvedObservations = p.Observations.Count(o => o.IsResolved),
                    Observations = p.Observations.OrderByDescending(o => o.CreatedAt).Select(o => new
                    {
                        o.Id,
                        o.Content,
                        o.CreatedAt,
                        o.CreatedBy,
                        o.IsResolved
                    }).ToList()
                })
                .OrderByDescending(p => p.UnresolvedObservations)
                .ToListAsync();

            return Ok(projectsWithObservations);
        }

        // PATCH: Marcar observación como resuelta
        [HttpPatch]
        [Route("api/observations/{observationId}/resolve")]
        public async Task<IActionResult> ResolveObservation(int observationId)
        {
            try
            {
                var observation = await _context.Observations.FindAsync(observationId);
                if (observation == null)
                    return NotFound(new { message = "Observación no encontrada" });

                observation.IsResolved = true;
                await _context.SaveChangesAsync();

                _logger.LogInformation("✅ Observación {ObservationId} marcada como resuelta", observationId);

                return Ok(new { message = "Observación marcada como resuelta" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error al resolver observación {ObservationId}", observationId);
                return StatusCode(500, new { message = "Error interno del servidor" });
            }
        }

        // POST: Declinar hacer observación (esperar otros 15 días)
        [HttpPost]
        [Route("api/projects/{projectId}/decline-observation")]
        public async Task<IActionResult> DeclineObservation(int projectId, [FromBody] JsonElement body)
        {
            try
            {
                if (!body.TryGetProperty("userEmail", out var userEmailElement))
                    return BadRequest(new { message = "El campo 'userEmail' es obligatorio" });

                var userEmail = userEmailElement.GetString()?.Trim();

                if (string.IsNullOrEmpty(userEmail))
                    return BadRequest(new { message = "El email del usuario es obligatorio" });

                var project = await _context.Projects
                    .Include(p => p.Resources)
                    .FirstOrDefaultAsync(p => p.Id == projectId);
                    
                if (project == null)
                    return NotFound(new { message = "Proyecto no encontrado" });

                // Validar que el usuario ofreció recursos en este proyecto
                var hasOfferedResource = project.Resources.Any(r => 
                    !string.IsNullOrEmpty(r.ContactEmail) && 
                    r.ContactEmail == userEmail &&
                    r.State == "accepted");

                if (!hasOfferedResource)
                    return BadRequest(new { message = "Solo las ONGs que ofrecieron recursos pueden declinar observaciones" });

                // Validar que han pasado 15 días desde la última acción
                var lastAction = await _context.ObservationActions
                    .Where(a => a.ProjectId == projectId && a.UserEmail == userEmail)
                    .OrderByDescending(a => a.ActionDate)
                    .FirstOrDefaultAsync();

                if (lastAction != null)
                {
                    var daysSinceLastAction = (DateTime.UtcNow - lastAction.ActionDate).TotalDays;
                    if (daysSinceLastAction < 15)
                    {
                        return BadRequest(new
                        {
                            message = $"Solo puedes declinar una vez cada 15 días. Días desde la última acción: {Math.Floor(daysSinceLastAction)}"
                        });
                    }
                }

                // Registrar la acción de declinar
                var action = new ObservationAction
                {
                    ProjectId = projectId,
                    UserEmail = userEmail,
                    ActionType = "declined",
                    ActionDate = DateTime.UtcNow
                };

                _context.ObservationActions.Add(action);
                await _context.SaveChangesAsync();

                _logger.LogInformation("✅ Usuario {UserEmail} declinó hacer observación en proyecto {ProjectId}", userEmail, projectId);

                return Ok(new { message = "Acción registrada. Podrás hacer una observación en 15 días." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al declinar observación");
                return StatusCode(500, new { message = "Error al procesar la solicitud", error = ex.Message });
            }
        }
    }
}
