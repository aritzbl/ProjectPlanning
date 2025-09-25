using Microsoft.AspNetCore.Mvc;
using ProjectPlanning.Web.Models;
using ProjectPlanning.Web.Services;

namespace ProjectPlanning.Controllers
{
    public class ProjectController : Controller
    {
        private readonly IBonitaApiService _bonitaService;
        private readonly ILogger<ProjectController> _logger;

        public ProjectController(IBonitaApiService bonitaService, ILogger<ProjectController> logger)
        {
            _bonitaService = bonitaService;
            _logger = logger;
        }

        public async Task<IActionResult> Create()
        {
            var isBonitaAvailable = await _bonitaService.IsBonitaAvailableAsync();
            ViewBag.IsBonitaAvailable = isBonitaAvailable;

            if (!isBonitaAvailable)
            {
                TempData["ErrorMessage"] = "⚠️ Bonita BPM is not available. Please check the connection.";
            }

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Project project)
        {
            if (ModelState.IsValid)
            {
                try
                {
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
