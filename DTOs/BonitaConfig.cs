namespace ProjectPlanning.DTOs
{
    public class BonitaConfig
    {
        public string BaseUrl { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;

        public string? ProcessDefinitionId { get; set; }
    }
}

