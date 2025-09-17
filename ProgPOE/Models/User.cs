using System.ComponentModel.DataAnnotations;

namespace ProgPOE.Models
{
    public class User
    {
        public int UserId { get; set; }

        [Required]
        [StringLength(50)]
        public string Username { get; set; }

        [Required]
        [EmailAddress]
        public string Email { get; set; }

        [Required]
        public string FirstName { get; set; }

        [Required]
        public string LastName { get; set; }

        [Required]
        public UserRole Role { get; set; }

        public string Department { get; set; }

        public decimal? DefaultHourlyRate { get; set; }

        public DateTime CreatedDate { get; set; }

        public bool IsActive { get; set; } = true;

        // Computed property for display
        public string FullName => $"{FirstName} {LastName}";
    }

    public enum UserRole
    {
        Lecturer,
        ProgrammeCoordinator,
        AcademicManager
    }
}
