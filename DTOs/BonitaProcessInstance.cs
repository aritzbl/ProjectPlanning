namespace ProjectPlanning.DTOs
{
    public class BonitaProcessInstance
    {
        public string ProcessDefinitionId { get; set; } = string.Empty;
        public List<BonitaVariable> Variables { get; set; } = new();
    }

    public class BonitaVariable
    {
        public string Name { get; set; } = string.Empty;
        public object Value { get; set; } = string.Empty;
    }

    public class BonitaProcessInstanceResponse
    {
        public long CaseId { get; set; } 
    }

    public class BonitaProcessDefinition
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
    }
}
