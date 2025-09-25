namespace ProjectPlanning.DTOs
{
    public class BonitaConfig
    {
        public string BaseUrl { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;

        // esto queda asi si usamos el ID fijo, pero habria que cambiarlo para que busque dinamicamente el process definition ID
        public string? ProcessDefinitionId { get; set; }
    }
}

