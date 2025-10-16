using Microsoft.EntityFrameworkCore;
using ProjectPlanning.Web.Models;

namespace ProjectPlanning.Web.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Project> Projects { get; set; }
    }
}
