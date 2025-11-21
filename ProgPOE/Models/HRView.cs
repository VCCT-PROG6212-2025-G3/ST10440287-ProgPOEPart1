using System.ComponentModel.DataAnnotations;

namespace ProgPOE.Models
{
    // HR Dashboard View Model
    public class HRDashboardViewModel
    {
        public int TotalLecturers { get; set; }
        public int ActiveLecturers { get; set; }
        public int InactiveLecturers { get; set; }
        public int TotalClaimsThisMonth { get; set; }
        public int ApprovedClaimsThisMonth { get; set; }
        public decimal TotalPaymentsThisMonth { get; set; }
        public List<User> RecentLecturers { get; set; } = new List<User>();
        public List<Claim> RecentApprovedClaims { get; set; } = new List<Claim>();
    }

    // Add/Edit Lecturer View Model
    public class ManageLecturerViewModel
    {
        public int? UserId { get; set; }

        [Required]
        [StringLength(50)]
        [Display(Name = "Username")]
        public string Username { get; set; }

        [Required]
        [EmailAddress]
        [StringLength(100)]
        [Display(Name = "Email Address")]
        public string Email { get; set; }

        [Required]
        [StringLength(50)]
        [Display(Name = "First Name")]
        public string FirstName { get; set; }

        [Required]
        [StringLength(50)]
        [Display(Name = "Last Name")]
        public string LastName { get; set; }

        [StringLength(100)]
        [Display(Name = "Department")]
        public string Department { get; set; }

        [Required]
        [Range(1, 9999.99)]
        [Display(Name = "Default Hourly Rate (R)")]
        public decimal DefaultHourlyRate { get; set; }

        [Display(Name = "Active")]
        public bool IsActive { get; set; } = true;
    }

    // Report Generation View Model
    public class GenerateReportViewModel
    {
        [Display(Name = "Report Type")]
        public ReportType ReportType { get; set; }

        [Display(Name = "Start Date")]
        [DataType(DataType.Date)]
        public DateTime? StartDate { get; set; }

        [Display(Name = "End Date")]
        [DataType(DataType.Date)]
        public DateTime? EndDate { get; set; }

        [Display(Name = "Lecturer")]
        public int? LecturerId { get; set; }

        [Display(Name = "Department")]
        public string Department { get; set; }

        [Display(Name = "Status")]
        public ClaimStatus? Status { get; set; }
    }

    // Report Types
    public enum ReportType
    {
        ApprovedClaimsSummary,
        LecturerPaymentReport,
        DepartmentalSummary,
        MonthlyClaimReport,
        LecturerDirectory,
        PaymentInvoice
    }

    // Payment Invoice Model
    public class PaymentInvoiceViewModel
    {
        public string InvoiceNumber { get; set; }
        public DateTime InvoiceDate { get; set; }
        public User Lecturer { get; set; }
        public List<Claim> Claims { get; set; }
        public decimal TotalAmount { get; set; }
        public string Period { get; set; }
    }

    // Lecturer Payment Summary
    public class LecturerPaymentSummary
    {
        public User Lecturer { get; set; }
        public int TotalClaims { get; set; }
        public int ApprovedClaims { get; set; }
        public decimal TotalEarnings { get; set; }
        public DateTime? LastClaimDate { get; set; }
    }
}