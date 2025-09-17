using System.ComponentModel.DataAnnotations;
using ProgPOE.Models;
namespace ProgPOE
{
    public class ViewModels
    {
        public string UserRole { get; set; }
        public string UserName { get; set; }
        public int TotalClaims { get; set; }
        public int PendingClaims { get; set; }
        public int ApprovedClaims { get; set; }
        public int RejectedClaims { get; set; }
        public decimal TotalEarnings { get; set; }

        public string FormattedEarnings => $"R {TotalEarnings:N2}";
    }

    // Claim submission view model
    public class SubmitClaimViewModel
    {
        [Required]
        [Display(Name = "Claim Period (YYYY-MM)")]
        public string MonthYear { get; set; }

        [Required]
        [Range(0.1, 744, ErrorMessage = "Hours must be between 0.1 and 744")]
        [Display(Name = "Hours Worked")]
        public decimal HoursWorked { get; set; }

        [Required]
        [Range(1, 9999.99, ErrorMessage = "Rate must be between 1 and 9999.99")]
        [Display(Name = "Hourly Rate (R)")]
        public decimal HourlyRate { get; set; }

        [Display(Name = "Additional Notes")]
        [StringLength(500)]
        public string Notes { get; set; }

        [Display(Name = "Supporting Documents")]
        public List<IFormFile> Documents { get; set; } = new List<IFormFile>();

        // Calculated field for display
        public decimal TotalAmount => HoursWorked * HourlyRate;
        public string FormattedTotal => $"R {TotalAmount:N2}";
    }

    // Claims list view model
    public class ClaimsListViewModel
    {
        public List<Claim> Claims { get; set; } = new List<Claim>();
        public string FilterStatus { get; set; }
        public string SearchTerm { get; set; }
        public int TotalClaims => Claims.Count;
        public decimal TotalValue => Claims.Sum(c => c.TotalAmount);
    }

    // Claim approval view model
    public class ClaimApprovalViewModel
    {
        public Claim Claim { get; set; }

        [Required]
        [Display(Name = "Review Comments")]
        [StringLength(1000)]
        public string Comments { get; set; }

        [Required]
        [Display(Name = "Action")]
        public ApprovalAction Action { get; set; }

        public string ReviewerRole { get; set; }
    }

    // Document upload view model
    public class DocumentUploadViewModel
    {
        [Required]
        public int ClaimId { get; set; }

        [Required]
        [Display(Name = "Select Files")]
        public List<IFormFile> Files { get; set; } = new List<IFormFile>();

        [Display(Name = "Description")]
        [StringLength(255)]
        public string Description { get; set; }
    }

    // Approval action enumeration
    public enum ApprovalAction
    {
        Approve,
        Reject,
        Return
    }
}
