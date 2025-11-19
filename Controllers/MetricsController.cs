using Microsoft.AspNetCore.Mvc;
using ProjectPlanning.Web.Services;

namespace ProjectPlanning.Controllers
{
    public class MetricsController : Controller
    {
        private readonly MetricsService _metricsService;
        private readonly ILogger<MetricsController> _logger;

        public MetricsController(MetricsService metricsService, ILogger<MetricsController> logger)
        {
            _metricsService = metricsService;
            _logger = logger;
        }

        // Vista de métricas
        public IActionResult Index()
        {
            return View();
        }

        // API: Obtener métricas por email
        [HttpGet]
        [Route("api/metrics")]
        public async Task<IActionResult> GetMetrics([FromQuery] string email)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(email))
                {
                    return BadRequest(new { message = "Email is required" });
                }

                _logger.LogInformation("Solicitando métricas para: {Email}", email);
                
                var metrics = await _metricsService.GetMetricsForOngAsync(email);
                
                return Ok(metrics);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener métricas para {Email}", email);
                return StatusCode(500, new { message = "Error al calcular métricas", error = ex.Message });
            }
        }

        // API: Buscar proyectos por rango de fechas
        [HttpGet]
        [Route("api/metrics/projects-by-date")]
        public async Task<IActionResult> GetProjectsByDateRange(
            [FromQuery] string email, 
            [FromQuery] DateTime startDate, 
            [FromQuery] DateTime endDate)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(email))
                {
                    return BadRequest(new { message = "Email is required" });
                }

                if (startDate > endDate)
                {
                    return BadRequest(new { message = "Start date must be before or equal to end date" });
                }

                _logger.LogInformation("Buscando proyectos por rango de fechas para: {Email}", email);
                
                var projects = await _metricsService.GetProjectsByDateRangeAsync(email, startDate, endDate);
                
                return Ok(new { 
                    count = projects.Count, 
                    projects 
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al buscar proyectos por fecha para {Email}", email);
                return StatusCode(500, new { message = "Error al buscar proyectos", error = ex.Message });
            }
        }
    }
}
