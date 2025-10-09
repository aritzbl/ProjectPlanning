using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ProjectPlanning.Web.Models
{
    [Table("Projects")]
    public class Project : IValidatableObject
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Project name is required")]
        public string? Name { get; set; }

        [Required(ErrorMessage = "Start date is required")]
        public DateTime StartDate { get; set; }

        [Required(ErrorMessage = "End date is required")]
        public DateTime EndDate { get; set; }

        [Required(ErrorMessage = "Resource is required")]
        public List<String> Resources { get; set; } = new List<String>();

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            var today = DateTime.Today;
            var maxYear = today.AddYears(15);
            if (StartDate < today || StartDate > maxYear)
            {
                yield return new ValidationResult(
                    $"Start date must be between {today:d} and {maxYear:d}.",
                    new[] { nameof(StartDate) }
                );
            }
            if (EndDate < today || EndDate > maxYear)
            {
                yield return new ValidationResult(
                    $"End date must be between {today:d} and {maxYear:d}.",
                    new[] { nameof(EndDate) }
                );
            }

            if (EndDate < StartDate)
            {
                yield return new ValidationResult(
                    "End date must be greater than or equal to start date",
                    new[] { nameof(EndDate) }
                );
            }
        }
    }
}
