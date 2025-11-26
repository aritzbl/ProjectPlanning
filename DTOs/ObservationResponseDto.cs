namespace ProjectPlanning.Web.DTOs
{
    public class ObservationResponseDto
    {
        public int Id { get; set; }
        public string Content { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public bool IsResolved { get; set; }
        public string CreatedBy { get; set; } = string.Empty;
        public int ProjectId { get; set; }
    }

    public class ProjectEligibleForObservationsDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public int DaysSinceStart { get; set; }
        public bool CanAddObservation { get; set; }
        public int DaysSinceLastAction { get; set; }
        public int ObservationCount { get; set; }
        public ObservationResponseDto? LastObservation { get; set; }
    }
}