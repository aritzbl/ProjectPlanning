using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ProjectPlanning.Web.Models
{
    [Table("ObservationActions")]
    public class ObservationAction
    {
        public int Id { get; set; }

        [Required]
        public int ProjectId { get; set; }

        [Required]
        [MaxLength(200)]
        public string? UserEmail { get; set; }

        public DateTime ActionDate { get; set; } = DateTime.UtcNow;

        // "observation" o "declined"
        [Required]
        [MaxLength(20)]
        public string? ActionType { get; set; }

        // Relación con Project
        [ForeignKey("ProjectId")]
        public Project? Project { get; set; }
    }
}
