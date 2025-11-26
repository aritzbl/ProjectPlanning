using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ProjectPlanning.Web.Models
{
    [Table("Observations")]
    public class Observation
    {
        public int Id { get; set; }

        [Required]
        public int ProjectId { get; set; }

        [Required]
        [MaxLength(1000)]
        public string? Content { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public bool IsResolved { get; set; } = false;

        [Required]
        [MaxLength(200)]
        public string? CreatedBy { get; set; } // Email/username de la ONG que creó la observación

        // Relación con Project
        [ForeignKey("ProjectId")]
        public Project? Project { get; set; }
    }
}
