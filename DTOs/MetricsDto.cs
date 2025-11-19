namespace ProjectPlanning.DTOs
{
    public class MetricsDto
    {
        // Totales generales
        public int TotalProjectsPublished { get; set; }
        
        // Métricas de cumplimiento de plazos
        public int ProjectsFinishedOnTime { get; set; } // Finalizaron antes o en la fecha estimada
        public int ProjectsFinishedLate { get; set; } // Finalizaron después de la fecha estimada
        public double OnTimeCompletionRate { get; set; } // % de proyectos a tiempo
    }
    
    public class ProjectSummary
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public int TotalResources { get; set; }
        public int AcceptedResources { get; set; }
        public int PendingResources { get; set; }
    }
    
}
