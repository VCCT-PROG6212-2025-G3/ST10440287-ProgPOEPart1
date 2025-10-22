using System.ComponentModel.DataAnnotations;

namespace ProgPOE.Models
{
    // Dashboard data view model for displaying summary info to the user
    public class DashboardViewModel
    {
        public string UserRole { get; set; }            // Current user role
        public string UserName { get; set; }            // Current user name
        public int TotalClaims { get; set; }            // Total number of claims
        public int PendingClaims { get; set; }          // Number of pending claims
        public int ApprovedClaims { get; set; }         // Number of approved claims
        public int RejectedClaims { get; set; }         // Number of rejected claims
        public decimal TotalEarnings { get; set; }      // Total earnings from approved claims

        // Formatted string for displaying earnings in R currency format
        public string FormattedEarnings => $"R {TotalEarnings:N2}";
    }

    // View model used when submitting a new claim
    public class SubmitClaimViewModel
    {
        [Required]
        [Display(Name = "Claim Period (YYYY-MM)")]
        public string MonthYear { get; set; }           // Claim period in YYYY-MM format

        [Required]
        [Range(0.1, 744, ErrorMessage = "Hours must be between 0.1 and 744")]
        [Display(Name = "Hours Worked")]
        public decimal HoursWorked { get; set; }        // Hours worked for this claim

        [Required]
        [Range(1, 9999.99, ErrorMessage = "Rate must be between 1 and 9999.99")]
        [Display(Name = "Hourly Rate (R)")]
        public decimal HourlyRate { get; set; }         // Hourly rate for this claim

        [Display(Name = "Additional Notes")]
        [StringLength(500)]
        public string Notes { get; set; }               // Optional lecturer notes

        [Display(Name = "Supporting Documents")]
        public List<IFormFile> Documents { get; set; } = new List<IFormFile>();  // Uploaded files

        // Calculated total amount for the claim
        public decimal TotalAmount => HoursWorked * HourlyRate;

        // Formatted string for displaying total in R currency format
        public string FormattedTotal => $"R {TotalAmount:N2}";
    }

    // View model for displaying a list of claims with optional filters
    public class ClaimsListViewModel
    {
        public List<Claim> Claims { get; set; } = new List<Claim>();  // List of claims to display
        public string FilterStatus { get; set; }                      // Optional filter by claim status
        public string SearchTerm { get; set; }                        // Optional search term

        // Total number of claims in the list
        public int TotalClaims => Claims.Count;

        // Total value of all claims in the list
        public decimal TotalValue => Claims.Sum(c => c.TotalAmount);
    }

    // View model used for claim approval actions by coordinator/manager
    public class ClaimApprovalViewModel
    {
        public Claim Claim { get; set; }               // Claim being reviewed

        [Required]
        [Display(Name = "Review Comments")]
        [StringLength(1000)]
        public string Comments { get; set; }           // Review comments from approver

        [Required]
        [Display(Name = "Action")]
        public ApprovalAction Action { get; set; }     // Action taken: Approve, Reject, Return

        public string ReviewerRole { get; set; }       // Role of the reviewer (Coordinator or Manager)
    }

    // View model for uploading documents for a claim
    public class DocumentUploadViewModel
    {
        [Required]
        public int ClaimId { get; set; }               // Claim ID for which files are being uploaded

        [Required]
        [Display(Name = "Select Files")]
        public List<IFormFile> Files { get; set; } = new List<IFormFile>(); // Files to upload

        [Display(Name = "Description")]
        [StringLength(255)]
        public string Description { get; set; }        // Optional description for the uploaded files
    }

    // Enum representing possible actions for claim approval
    public enum ApprovalAction
    {
        Approve,    // Approve the claim
        Reject,     // Reject the claim
        Return      // Return the claim for revision
    }
}
