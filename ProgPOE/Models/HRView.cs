using System.ComponentModel.DataAnnotations;

namespace ProgPOE.Models
{
    // ViewModel for displaying summary data on the HR Dashboard
    public class HRDashboardViewModel
    {
        // Total number of lecturers in the system
        public int TotalLecturers { get; set; }

        // Number of active lecturers
        public int ActiveLecturers { get; set; }

        // Number of inactive lecturers
        public int InactiveLecturers { get; set; }

        // Total claims submitted for the current month
        public int TotalClaimsThisMonth { get; set; }

        // Number of approved claims for the current month
        public int ApprovedClaimsThisMonth { get; set; }

        // Total monetary value of payments approved for the current month
        public decimal TotalPaymentsThisMonth { get; set; }

        // List of recently added lecturers
        public List<User> RecentLecturers { get; set; } = new List<User>();

        // List of recently approved claims
        public List<Claim> RecentApprovedClaims { get; set; } = new List<Claim>();
    }

    // ViewModel used when adding or editing a lecturer’s details
    public class ManageLecturerViewModel
    {
        // Optional (null for creating, set for editing an existing lecturer)
        public int? UserId { get; set; }

        // Username field with validation rules
        [Required]
        [StringLength(50)]
        [Display(Name = "Username")]
        public string Username { get; set; }

        // Email address with validation and format enforcement
        [Required]
        [EmailAddress]
        [StringLength(100)]
        [Display(Name = "Email Address")]
        public string Email { get; set; }

        // Lecturer's first name
        [Required]
        [StringLength(50)]
        [Display(Name = "First Name")]
        public string FirstName { get; set; }

        // Lecturer's last name
        [Required]
        [StringLength(50)]
        [Display(Name = "Last Name")]
        public string LastName { get; set; }

        // Lecturer's academic or administrative department
        [StringLength(100)]
        [Display(Name = "Department")]
        public string Department { get; set; }

        // Default hourly rate used for calculating claim amounts
        [Required]
        [Range(1, 9999.99)]
        [Display(Name = "Default Hourly Rate (R)")]
        public decimal DefaultHourlyRate { get; set; }

        // Indicates whether the lecturer is currently active
        [Display(Name = "Active")]
        public bool IsActive { get; set; } = true;
    }

    // ViewModel used for generating custom HR reports
    public class GenerateReportViewModel
    {
        // The type of report the user wants to generate
        [Display(Name = "Report Type")]
        public ReportType ReportType { get; set; }

        // Optional starting date for filtering report results
        [Display(Name = "Start Date")]
        [DataType(DataType.Date)]
        public DateTime? StartDate { get; set; }

        // Optional ending date for filtering report results
        [Display(Name = "End Date")]
        [DataType(DataType.Date)]
        public DateTime? EndDate { get; set; }

        // Filter by lecturer ID (optional)
        [Display(Name = "Lecturer")]
        public int? LecturerId { get; set; }

        // Filter by department (optional)
        [Display(Name = "Department")]
        public string Department { get; set; }

        // Filter by claim status (optional)
        [Display(Name = "Status")]
        public ClaimStatus? Status { get; set; }
    }

    // Enum listing all available report types
    public enum ReportType
    {
        ApprovedClaimsSummary,
        LecturerPaymentReport,
        DepartmentalSummary,
        MonthlyClaimReport,
        LecturerDirectory,
        PaymentInvoice
    }

    // ViewModel representing a detailed payment invoice for a lecturer
    public class PaymentInvoiceViewModel
    {
        // Unique invoice number
        public string InvoiceNumber { get; set; }

        // Date invoice was generated
        public DateTime InvoiceDate { get; set; }

        // Lecturer the invoice is for
        public User Lecturer { get; set; }

        // List of claims included in the invoice
        public List<Claim> Claims { get; set; }

        // Total payable amount for all claims
        public decimal TotalAmount { get; set; }

        // Period covered by the invoice (e.g., "January 2025")
        public string Period { get; set; }
    }

    // Summary object used for displaying lecturer payment statistics
    public class LecturerPaymentSummary
    {
        // Lecturer being summarized
        public User Lecturer { get; set; }

        // Total number of claims submitted by the lecturer
        public int TotalClaims { get; set; }

        // Number of approved claims
        public int ApprovedClaims { get; set; }

        // Total amount the lecturer earned
        public decimal TotalEarnings { get; set; }

        // Date of most recent claim (null if none)
        public DateTime? LastClaimDate { get; set; }
    }
}
