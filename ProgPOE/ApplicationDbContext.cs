using Microsoft.EntityFrameworkCore;
using ProgPOE.Models;

namespace ProgPOE.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<User> Users { get; set; }
        public DbSet<Claim> Claims { get; set; }
        public DbSet<SupportingDocument> SupportingDocuments { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // User configuration
            modelBuilder.Entity<User>(entity =>
            {
                entity.HasKey(e => e.UserId);
            });

            // Claim configuration
            modelBuilder.Entity<Claim>(entity =>
            {
                entity.HasKey(e => e.ClaimId);

                entity.HasOne(e => e.Lecturer)
                      .WithMany()
                      .HasForeignKey(e => e.LecturerId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            // SupportingDocument configuration
            modelBuilder.Entity<SupportingDocument>(entity =>
            {
                entity.HasKey(e => e.DocumentId);

                entity.HasOne(e => e.Claim)
                      .WithMany(c => c.Documents)
                      .HasForeignKey(e => e.ClaimId)
                      .OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
}