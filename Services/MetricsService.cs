using Microsoft.EntityFrameworkCore;
using ProjectPlanning.Web.Data;
using ProjectPlanning.DTOs;

namespace ProjectPlanning.Web.Services
{
    public class MetricsService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<MetricsService> _logger;

        public MetricsService(ApplicationDbContext context, ILogger<MetricsService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<MetricsDto> GetMetricsForOngAsync(string ongEmail)
        {
            try
            {
                _logger.LogInformation("Calculando métricas para ONG: {Email}", ongEmail);

                var nowUtc = DateTime.UtcNow;

                // Obtener todos los proyectos de esta ONG
                var projects = await _context.Projects
                    .Include(p => p.Resources)
                    .Where(p => p.CreatorEmail == ongEmail)
                    .ToListAsync();

                if (!projects.Any())
                {
                    _logger.LogInformation("No se encontraron proyectos para {Email}", ongEmail);
                    return new MetricsDto();
                }

                // Calcular totales generales
                var totalProjects = projects.Count;

                // Calcular proyectos finalizados a tiempo vs tarde
                // Solo consideramos proyectos que realmente finalizaron (tienen ActualEndDate)
                var finishedProjects = projects.Where(p => p.ActualEndDate.HasValue).ToList();
                var projectsOnTime = finishedProjects.Count(p => p.ActualEndDate <= p.EndDate);
                var projectsLate = finishedProjects.Count(p => p.ActualEndDate > p.EndDate);
                var onTimeRate = finishedProjects.Count > 0
                    ? Math.Round((double)projectsOnTime / finishedProjects.Count * 100, 2)
                    : 0;

                var metrics = new MetricsDto
                {
                    TotalProjectsPublished = totalProjects,
                    ProjectsFinishedOnTime = projectsOnTime,
                    ProjectsFinishedLate = projectsLate,
                    OnTimeCompletionRate = onTimeRate
                };

                _logger.LogInformation("Métricas calculadas exitosamente para {Email}", ongEmail);
                return metrics;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al calcular métricas para {Email}", ongEmail);
                throw;
            }
        }

        public async Task<List<ProjectSummary>> GetProjectsByDateRangeAsync(string ongEmail, DateTime startDate, DateTime endDate)
        {
            try
            {
                // Convertir a UTC - crear nuevos DateTime con Kind = UTC
                var startDateUtc = new DateTime(startDate.Year, startDate.Month, startDate.Day, 0, 0, 0, DateTimeKind.Utc);
                var endDateUtc = new DateTime(endDate.Year, endDate.Month, endDate.Day, 23, 59, 59, DateTimeKind.Utc);
                
                _logger.LogInformation("Buscando proyectos finalizados entre {StartDate} y {EndDate} para {Email}", 
                    startDateUtc, endDateUtc, ongEmail);

                var projects = await _context.Projects
                    .Include(p => p.Resources)
                    .Where(p => p.CreatorEmail == ongEmail 
                             && p.EndDate >= startDateUtc 
                             && p.EndDate <= endDateUtc)
                    .OrderByDescending(p => p.EndDate)
                    .ToListAsync();

                var nowUtc = DateTime.UtcNow;

                var result = projects.Select(p => new ProjectSummary
                {
                    Id = p.Id,
                    Name = p.Name,
                    StartDate = p.StartDate,
                    EndDate = p.EndDate,
                    TotalResources = p.Resources.Count,
                    AcceptedResources = p.Resources.Count(r => r.State == "accepted"),
                    PendingResources = p.Resources.Count(r => r.State == "pending")
                }).ToList();

                _logger.LogInformation("Se encontraron {Count} proyectos en el rango de fechas", result.Count);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al buscar proyectos por rango de fechas para {Email}", ongEmail);
                throw;
            }
        }
    }
}
