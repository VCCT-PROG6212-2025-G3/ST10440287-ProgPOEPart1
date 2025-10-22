using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ProgPOE.Models
{
    // Represents a system user such as a Lecturer, Coordinator, or Manager
    public class User
    {
        // Primary key for the User table
        public int UserId { get; set; }

        // Unique username required for login and identification
        [Required]
        [StringLength(50)]
        public string Username { get; set; } = string.Empty;

        // User's email address, used for contact or authentication
        [Required]
        [EmailAddress]
        [StringLength(100)]
        public string Email { get; set; } = string.Empty;

        // User's first name (required field)
        [Required]
        [StringLength(50)]
        public string FirstName { get; set; } = string.Empty;

        // User's last name (required field)
        [Required]
        [StringLength(50)]
        public string LastName { get; set; } = string.Empty;

        // Defines the role of the user in the system (Lecturer, Coordinator, or Manager)
        [Required]
        public UserRole Role { get; set; }

        // Department the user belongs to (optional field)
        [StringLength(100)]
        public string? Department { get; set; }

        // Optional default hourly rate for lecturers' claim calculations
        public decimal? DefaultHourlyRate { get; set; }

        // Date when the user account was created (auto-filled with current date)
        public DateTime CreatedDate { get; set; } = DateTime.Now;

        // Indicates if the user account is active or deactivated
        public bool IsActive { get; set; } = true;

        // Computed property combining first and last names — not stored in the database
        [NotMapped]
        public string FullName => $"{FirstName} {LastName}";
    }

    // Enum representing different roles available in the system
    public enum UserRole
    {
        Lecturer,              // Basic user who submits claims
        ProgrammeCoordinator,  // Approves claims before manager review
        AcademicManager         // Final approver of claims
    }
}
