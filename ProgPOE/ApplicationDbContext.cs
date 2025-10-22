using Microsoft.EntityFrameworkCore;
using ProgPOE.Models;

namespace ProgPOE.Data
{
    // Main EF Core database context for the application
    public class ApplicationDbContext : DbContext
    {
        // Constructor accepting DbContextOptions
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        // DbSets represent tables in the database
        public DbSet<User> Users { get; set; }                       // Users table
        public DbSet<Claim> Claims { get; set; }                     // Claims table
        public DbSet<SupportingDocument> SupportingDocuments { get; set; } // SupportingDocuments table

        // Configure entity relationships and keys
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure User entity
            modelBuilder.Entity<User>(entity =>
            {
                entity.HasKey(e => e.UserId);  // Primary key for Users table
            });

            // Configure Claim entity
            modelBuilder.Entity<Claim>(entity =>
            {
                entity.HasKey(e => e.ClaimId); // Primary key for Claims table

                // Configure relationship: Claim has one Lecturer (User)
                // Restrict delete so that deleting a User does not delete related claims
                entity.HasOne(e => e.Lecturer)
                      .WithMany()
                      .HasForeignKey(e => e.LecturerId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            // Configure SupportingDocument entity
            modelBuilder.Entity<SupportingDocument>(entity =>
            {
                entity.HasKey(e => e.DocumentId); // Primary key for SupportingDocuments table

                // Configure relationship: Document belongs to one Claim
                // Cascade delete: deleting a claim deletes all associated documents
                entity.HasOne(e => e.Claim)
                      .WithMany(c => c.Documents)
                      .HasForeignKey(e => e.ClaimId)
                      .OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
}
