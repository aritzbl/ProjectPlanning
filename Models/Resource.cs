using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace ProjectPlanning.Web.Models
{
    [Table("Resources")]
    public class Resource
    {
        public int Id { get; set; }

        [ForeignKey("Project")]
        public int ProjectId { get; set; }

        [JsonIgnore] // Evita bucle
        public virtual Project? Project { get; set; }

        [Required]
        [StringLength(100)]
        public string? Name { get; set; }

        // Pending, offer, accepted
        public string? State { get; set; } = "pending";
    }
}
