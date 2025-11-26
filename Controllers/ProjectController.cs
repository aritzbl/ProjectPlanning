using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProjectPlanning.Web.Models;
using ProjectPlanning.Web.Services;
using ProjectPlanning.Web.Data;
using System.Text.Json;

namespace ProjectPlanning.Controllers
{
    public class ProjectController : Controller
    {
        private readonly IBonitaApiService _bonitaService;
        private readonly ILogger<ProjectController> _logger;
        private readonly ApplicationDbContext _context;

        public ProjectController(IBonitaApiService bonitaService, ILogger<ProjectController> logger, ApplicationDbContext context)
        {
            _bonitaService = bonitaService;
            _logger = logger;
            _context = context;
        }

        // VISTA CREATE
        public async Task<IActionResult> Create()
        {
            var isBonitaAvailable = await _bonitaService.IsBonitaAvailableAsync();
            ViewBag.IsBonitaAvailable = isBonitaAvailable;

            if (!isBonitaAvailable)
                TempData["ErrorMessage"] = "⚠️ Bonita BPM is not available.";

            return View(new Project());
        }

        // POST CREATE
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Project project)
        {
            if (!ModelState.IsValid)
                return View(project);

            try
            {
                project.StartDate = DateTime.SpecifyKind(project.StartDate, DateTimeKind.Utc);
                project.EndDate = DateTime.SpecifyKind(project.EndDate, DateTimeKind.Utc);

                if (project.Resources != null)
                {
                    foreach (var r in project.Resources)
                        r.Project = project;
                }

                _context.Projects.Add(project);
                await _context.SaveChangesAsync();

                if (!await _bonitaService.IsBonitaAvailableAsync())
                {
                    TempData["ErrorMessage"] = "Bonita BPM is not available.";
                    return View(project);
                }

                // 🚀 Esto inicia el proceso y, por lo que cambiamos recién,
                // TAMBIÉN ejecuta la tarea 'Publicar proyecto'
                var caseId = await _bonitaService.StartProcessInstanceAsync(project);

                project.BonitaCaseId = caseId;
                _context.Projects.Update(project);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Proyecto creado, publicado y proceso Bonita iniciado.";
                return RedirectToAction(nameof(Create));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating project {ProjectName}", project.Name);
                TempData["ErrorMessage"] = "Error creating project.";
                return View(project);
            }
        }


        // GET: todos los proyectos
        [HttpGet]
        [Route("api/projects")]
        public async Task<IActionResult> GetAllProjects()
        {
            var projects = await _context.Projects
                .OrderByDescending(p => p.StartDate)
                .Select(p => new
                {
                    p.Id,
                    p.Name,
                    p.StartDate,
                    p.EndDate
                })
                .ToListAsync();

            return Ok(projects);
        }

        // GET: detalle de un proyecto
        [HttpGet("{id}")]
        [Route("api/projects/{id}")]
        public async Task<IActionResult> GetProjectById(int id)
        {
            var project = await _context.Projects
                .Include(p => p.Resources)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (project == null)
                return NotFound(new { message = "Project not found." });

            return Ok(new
            {
                id = project.Id,
                name = project.Name,
                startDate = project.StartDate,
                endDate = project.EndDate,
                creatorEmail = project.CreatorEmail,
                resources = project.Resources.Select(r => new {
                    id = r.Id,
                    name = r.Name,
                    state = r.State
                })
            });
        }

        // Endpoint: proyectos activos para Bonita (StartDate <= now && EndDate >= now)
        [HttpGet]
        [Route("api/projects/active")]
        public async Task<IActionResult> GetActiveProjects()
        {
            var nowUtc = DateTime.UtcNow;

            var activeProjects = await _context.Projects
                .Where(p => p.StartDate <= nowUtc && p.EndDate >= nowUtc)
                .OrderBy(p => p.StartDate)
                .Select(p => new
                {
                    p.Id,
                    p.Name,
                    p.StartDate,
                    p.EndDate,
                    CreatorEmail = p.CreatorEmail,
                    Resources = p.Resources.Select(r => new
                    {
                        r.Id,
                        r.Name,
                        r.State
                    }).ToList()
                })
                .ToListAsync();

            _logger.LogInformation("Returning {Count} active projects for Bonita", activeProjects.Count);
            return Ok(activeProjects);
        }

        // PATCH: ofrecer recurso (aceptar con email)
        [HttpPatch("api/projects/{projectId}/resources/{resourceId}/offer")]
        public async Task<IActionResult> OfferResource(int projectId, int resourceId, [FromBody] JsonElement body)
        {
            try
            {
                if (!body.TryGetProperty("contactEmail", out var emailElement))
                    return BadRequest(new { message = "El campo 'contactEmail' es obligatorio." });

                var contactEmail = emailElement.GetString()?.Trim();
                if (string.IsNullOrEmpty(contactEmail) || !contactEmail.Contains("@"))
                    return BadRequest(new { message = "Debe ingresar un email válido." });

                var resource = await _context.Resources
                    .FirstOrDefaultAsync(r => r.Id == resourceId && r.ProjectId == projectId);

                if (resource == null)
                    return NotFound(new { message = "Recurso no encontrado." });

                resource.State = "accepted";
                resource.ContactEmail = contactEmail;

                await _context.SaveChangesAsync();

                _logger.LogInformation("✅ Recurso {ResourceId} del proyecto {ProjectId} ofrecido por {Email}",
                    resourceId, projectId, contactEmail);

                return Ok(new
                {
                    message = "Recurso ofrecido correctamente.",
                    resourceId,
                    newState = resource.State,
                    contactEmail
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error al ofrecer recurso {ResourceId} en proyecto {ProjectId}", resourceId, projectId);
                return StatusCode(500, new { message = "Error interno del servidor." });
            }
        }

        [HttpPost]
        public async Task<IActionResult> PublishProject(int id)
        {
            var project = await _context.Projects.FirstOrDefaultAsync(p => p.Id == id);

            if (project == null || string.IsNullOrEmpty(project.BonitaCaseId))
            {
                TempData["Error"] = "Proyecto no encontrado o sin CaseId.";
                return RedirectToAction("ProjectList", "AuthView");
            }

            await _bonitaService.PublishProjectTaskAsync(project.BonitaCaseId);

            TempData["Success"] = "Tarea 'Publicar proyecto' ejecutada en Bonita.";
            return RedirectToAction("ProjectList", "AuthView");
        }

    }
}
