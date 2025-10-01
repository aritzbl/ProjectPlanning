using Microsoft.AspNetCore.Mvc;
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

        public async Task<IActionResult> Create()
        {
            var isBonitaAvailable = await _bonitaService.IsBonitaAvailableAsync();
            ViewBag.IsBonitaAvailable = isBonitaAvailable;

            if (!isBonitaAvailable)
            {
                TempData["ErrorMessage"] = "⚠️ Bonita BPM is not available. Please check the connection.";
            }

            return View(new Project());
        }

        public async Task<IActionResult> ListProcesses()
        {
            try
            {
                var processes = await _bonitaService.GetAvailableProcessesAsync();
                return Json(processes);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error listing processes");
                return Json(new { error = ex.Message });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Project project)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    
                    // convierte las fechas a UTC si no lo están porque postgres no lo puede guardar
                    if (project.StartDate.Kind == DateTimeKind.Unspecified)
                        project.StartDate = DateTime.SpecifyKind(project.StartDate, DateTimeKind.Utc);
                    else
                        project.StartDate = project.StartDate.ToUniversalTime();

                    if (project.EndDate.Kind == DateTimeKind.Unspecified)
                        project.EndDate = DateTime.SpecifyKind(project.EndDate, DateTimeKind.Utc);
                    else
                        project.EndDate = project.EndDate.ToUniversalTime();

                    _context.Projects.Add(project);
                    await _context.SaveChangesAsync();

                    var isBonitaAvailable = await _bonitaService.IsBonitaAvailableAsync();

                    if (!isBonitaAvailable)
                    {
                        TempData["ErrorMessage"] = "⚠️ Bonita BPM is not available. The form will not be able to create process instances.";
                        return View(project);
                    }

                    // inicia el proceso
                    var processInstanceId = await _bonitaService.StartProcessInstanceAsync(project);

                    TempData["SuccessMessage"] = $"✅ Project created successfully! Process instance ID: {processInstanceId}";
                    _logger.LogInformation("Project {ProjectName} created with process instance {ProcessId}", 
                        project.Name, processInstanceId);

                    return RedirectToAction(nameof(Create));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error creating project {ProjectName}", project.Name);
                    TempData["ErrorMessage"] = "❌ An error occurred while creating the project. Check logs for details.";
                }
            }

            return View(project);
        }
    }
}
