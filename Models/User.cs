using System.ComponentModel.DataAnnotations;

namespace ProjectPlanning.Web.Models
{
    public class User
    {
        [Key]
        public int Id { get; set; }

        [Required, EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string Name { get; set; } = string.Empty;

        public string Organization { get; set; } = string.Empty;

        public bool IsOfferingOng { get; set; }

        [Required]
        public string Password { get; set; } = string.Empty;
    }
}
