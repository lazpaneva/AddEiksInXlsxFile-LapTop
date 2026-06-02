using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using AddEiksInXlsxFile.Models;

namespace AddEiksInXlsxFile.Data
{
    public class ApplicationDbContext : IdentityDbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {
        }

        public DbSet<ProcessingStatistics> ProcessingStatistics { get; set; } = null!;
        
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<ProcessingStatistics>(eb =>
            {
                // Ensure SQL column can hold sufficient precision to avoid silent truncation.
                // Choosing decimal(18,6) to preserve fractional rates without loss.
                eb.Property(p => p.SuccessRate).HasPrecision(18, 6);
            });
        }
    }
}
