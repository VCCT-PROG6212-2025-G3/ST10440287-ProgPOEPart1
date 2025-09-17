using System.ComponentModel.DataAnnotations;

namespace ProgPOE.Models
{
    public class Claim
    {
        public int ClaimId { get; set; }

        [Required]
        public int LecturerId { get; set; }

        [Required]
        [StringLength(7)]
        public string MonthYear { get; set; } // Format: "2024-04"

        [Required]
        [Range(0.1, 744)]
        public decimal HoursWorked { get; set; }

        [Required]
        [Range(1, 9999.99)]
        public decimal HourlyRate { get; set; }

        public decimal TotalAmount => HoursWorked * HourlyRate;

        public ClaimStatus Status { get; set; } = ClaimStatus.Pending;

        public DateTime SubmissionDate { get; set; }

        public DateTime? CoordinatorApprovalDate { get; set; }

        public DateTime? ManagerApprovalDate { get; set; }

        public string LecturerNotes { get; set; }

        public string CoordinatorNotes { get; set; }

        public string ManagerNotes { get; set; }

        // Navigation properties (for display purposes in prototype)
        public User Lecturer { get; set; }
        public List<SupportingDocument> Documents { get; set; } = new List<SupportingDocument>();
    }

    public enum ClaimStatus
    {
        Pending,
        PendingManager,
        Approved,
        Rejected,
        Returned
    }
}
