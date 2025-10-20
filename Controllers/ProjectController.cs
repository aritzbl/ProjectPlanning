using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProjectPlanning.Web.Models;
using ProjectPlanning.Web.Services;
using ProjectPlanning.Web.Data;

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
                TempData["ErrorMessage"] = "⚠️ Bonita BPM is not available. Please check the connection.";

            return View(new Project());
        }

        // POST CREATE
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Project project)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    // Convertir fechas a UTC para postgres
                    project.StartDate = DateTime.SpecifyKind(project.StartDate, DateTimeKind.Utc);
                    project.EndDate = DateTime.SpecifyKind(project.EndDate, DateTimeKind.Utc);

                    if (project.Resources != null)
                    {
                        foreach (var resource in project.Resources)
                        {
                            resource.Project = project;
                        }
                    }

                    _context.Projects.Add(project);
                    await _context.SaveChangesAsync();

                    _logger.LogInformation("Project saved to DB with Id: {ProjectId}", project.Id);

                    // Iniciar proceso en Bonita
                    var isBonitaAvailable = await _bonitaService.IsBonitaAvailableAsync();
                    if (!isBonitaAvailable)
                    {
                        TempData["ErrorMessage"] = "⚠️ Bonita BPM is not available.";
                        return View(project);
                    }

                    var processInstanceId = await _bonitaService.StartProcessInstanceAsync(project);
                    TempData["SuccessMessage"] = $"✅ Project created successfully! Process instance ID: {processInstanceId}";

                    _logger.LogInformation("Project {ProjectName} created with process instance {ProcessId}", project.Name, processInstanceId);

                    return RedirectToAction(nameof(Create));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error creating project {ProjectName}", project.Name);
                    TempData["ErrorMessage"] = "❌ Error creating project.";
                }
            }

            return View(project);
        }

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

        [HttpGet]
        [Route("api/projects/{id}")]
        public async Task<IActionResult> GetProjectById(int id)
        {
            var project = await _context.Projects
                .Include(p => p.Resources)
                .FirstOrDefaultAsync(p => p.Id == id);

                    if (project == null)
                        return NotFound(new { message = "Project not found." });

                    return Ok(project);
                }

        [HttpPatch("api/projects/{projectId}/resources/{resourceId}/offer")]
        public async Task<IActionResult> OfferResource(int projectId, int resourceId)
        {
            try
            {
                // Buscamos el recurso dentro del proyecto
                var resource = await _context.Resources
                    .FirstOrDefaultAsync(r => r.Id == resourceId && r.ProjectId == projectId);

                if (resource == null)
                    return NotFound(new { message = "Resource not found" });

                // Cambiamos el estado a "offer"
                resource.State = "offer";

                // Guardamos cambios
                await _context.SaveChangesAsync();

                return Ok(new { message = "Resource offered successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error offering resource {ResourceId} in project {ProjectId}", resourceId, projectId);
                return StatusCode(500, new { message = "Internal server error" });
            }
        }
    }
}
