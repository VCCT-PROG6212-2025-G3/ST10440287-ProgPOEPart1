using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ProgPOE.Models
{
    public class Claim
    {
        public int ClaimId { get; set; }

        [Required]
        public int LecturerId { get; set; }

        [Required]
        [StringLength(7)]
        public string MonthYear { get; set; } = string.Empty;

        [Required]
        [Range(0.1, 744)]
        public decimal HoursWorked { get; set; }

        [Required]
        [Range(1, 9999.99)]
        public decimal HourlyRate { get; set; }

        [NotMapped]
        public decimal TotalAmount => HoursWorked * HourlyRate;

        public ClaimStatus Status { get; set; } = ClaimStatus.Pending;

        public DateTime SubmissionDate { get; set; } = DateTime.Now;

        public DateTime? CoordinatorApprovalDate { get; set; }

        public DateTime? ManagerApprovalDate { get; set; }

        [StringLength(1000)]
        public string? LecturerNotes { get; set; }

        [StringLength(1000)]
        public string? CoordinatorNotes { get; set; }

        [StringLength(1000)]
        public string? ManagerNotes { get; set; }

        // Navigation properties
        public virtual User? Lecturer { get; set; }

        public virtual ICollection<SupportingDocument> Documents { get; set; } = new List<SupportingDocument>();
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