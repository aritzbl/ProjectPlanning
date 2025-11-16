using Microsoft.AspNetCore.Mvc;
using ProjectPlanning.Web.Services;
using ProjectPlanning.Web.Models;

namespace ProjectPlanning.Controllers
{
    [ApiController]
    [Route("api/bonita")]
    public class BonitaController : ControllerBase
    {
        private readonly ProjectService _projectService;
        private readonly ILogger<BonitaController> _logger;

        public BonitaController(ProjectService projectService, ILogger<BonitaController> logger)
        {
            _projectService = projectService;
            _logger = logger;
        }

        // GET api/bonita/project/{id}
        [HttpGet("project/{id}")]
        public async Task<IActionResult> GetProjectForBonita(int id)
        {
            var project = await _projectService.GetProjectByIdAsync(id);
            if (project == null) return NotFound(new { message = "Project not found." });

            return Ok(new
            {
                project.Id,
                project.Name,
                project.StartDate,
                project.EndDate,
                creatorEmail = project.CreatorEmail,
                resources = project.Resources.Select(r => new {
                    id = r.Id,
                    name = r.Name,
                    state = r.State,
                    contactEmail = r.ContactEmail
                }).ToList()
            });
        }

        [HttpGet("projects/active")]
        public async Task<IActionResult> GetActiveProjectsForBonita()
        {
            // Obtener los proyectos activos (no terminados)
            var activeProjects = await _projectService.GetActiveProjectsAsync();

            // Mapear a DTO o anónimo
            var outList = activeProjects
                .Where(p => p.EndDate > DateTime.UtcNow) // solo los que no terminaron
                .Select(p => new
                {
                    id = p.Id,
                    name = p.Name,
                    startDate = p.StartDate,
                    endDate = p.EndDate,
                    creatorEmail = p.CreatorEmail,
                    resources = p.Resources.Select(r => new
                    {
                        id = r.Id,
                        name = r.Name,
                        state = r.State,
                        contactEmail = r.ContactEmail
                    }).ToList()
                })
                .ToList();

            return Ok(outList);
        }

        // GET api/bonita/startprocess/{processId}
        [HttpPost("startprocess/{processId}")]
        public async Task<IActionResult> StartBonitaProcess(int processId)
        {
            try
            {
                // Llama a ProjectService para disparar el proceso en Bonita
                var result = await _projectService.StartBonitaProcessAsync(processId);

                _logger.LogInformation("Proceso Bonita instanciado correctamente: {result}", result);

                return Ok(new { message = "Proceso instanciado en Bonita.", result });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al instanciar el proceso en Bonita.");
                return StatusCode(500, new { message = "Error al instanciar proceso.", error = ex.Message });
            }
        }
    }
}
