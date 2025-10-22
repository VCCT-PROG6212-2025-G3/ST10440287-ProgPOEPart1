using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ProgPOE.Models
{
    // Represents a lecturer's claim for hours worked
    public class Claim
    {
        // Primary key for the claim
        public int ClaimId { get; set; }

        // Foreign key linking claim to a lecturer
        [Required]
        public int LecturerId { get; set; }

        // Month and year of the claim in MM/YYYY format
        [Required]
        [StringLength(7)]
        public string MonthYear { get; set; } = string.Empty;

        // Number of hours worked (must be between 0.1 and 744)
        [Required]
        [Range(0.1, 744)]
        public decimal HoursWorked { get; set; }

        // Hourly rate (must be between 1 and 9999.99)
        [Required]
        [Range(1, 9999.99)]
        public decimal HourlyRate { get; set; }

        // Total amount calculated from HoursWorked * HourlyRate
        [NotMapped]
        public decimal TotalAmount => HoursWorked * HourlyRate;

        // Current status of the claim
        public ClaimStatus Status { get; set; } = ClaimStatus.Pending;

        // Date claim was submitted
        public DateTime SubmissionDate { get; set; } = DateTime.Now;

        // Date coordinator approved/reviewed the claim
        public DateTime? CoordinatorApprovalDate { get; set; }

        // Date manager approved/reviewed the claim
        public DateTime? ManagerApprovalDate { get; set; }

        // Optional notes by the lecturer
        [StringLength(1000)]
        public string? LecturerNotes { get; set; }

        // Optional notes by the coordinator
        [StringLength(1000)]
        public string? CoordinatorNotes { get; set; }

        // Optional notes by the manager
        [StringLength(1000)]
        public string? ManagerNotes { get; set; }

        // Navigation property to the lecturer (user)
        public virtual User? Lecturer { get; set; }

        // Navigation property for supporting documents attached to the claim
        public virtual ICollection<SupportingDocument> Documents { get; set; } = new List<SupportingDocument>();

        // Human-readable text representation of claim status
        [NotMapped]
        public string StatusDisplayText => Status switch
        {
            ClaimStatus.Pending => "Pending Coordinator Review",
            ClaimStatus.PendingManager => "Pending Manager Approval",
            ClaimStatus.Approved => "Approved",
            ClaimStatus.Rejected => "Rejected",
            ClaimStatus.Returned => "Returned for Revision",
            _ => "Unknown"
        };

        // Color code for status display in UI
        [NotMapped]
        public string StatusColor => Status switch
        {
            ClaimStatus.Pending => "#ffc107",
            ClaimStatus.PendingManager => "#17a2b8",
            ClaimStatus.Approved => "#28a745",
            ClaimStatus.Rejected => "#dc3545",
            ClaimStatus.Returned => "#6c757d",
            _ => "#6c757d"
        };

        // Emoji/icon representation of claim status
        [NotMapped]
        public string StatusIcon => Status switch
        {
            ClaimStatus.Pending => "⏳",
            ClaimStatus.PendingManager => "🔄",
            ClaimStatus.Approved => "✅",
            ClaimStatus.Rejected => "❌",
            ClaimStatus.Returned => "↩️",
            _ => "📋"
        };

        // Progress percentage used in UI progress bars
        [NotMapped]
        public int ProgressPercentage => Status switch
        {
            ClaimStatus.Pending => 33,
            ClaimStatus.PendingManager => 66,
            ClaimStatus.Approved => 100,
            ClaimStatus.Rejected => 100,
            ClaimStatus.Returned => 20,
            _ => 0
        };

        // Progress bar bootstrap color class
        [NotMapped]
        public string ProgressBarColor => Status switch
        {
            ClaimStatus.Pending => "warning",
            ClaimStatus.PendingManager => "info",
            ClaimStatus.Approved => "success",
            ClaimStatus.Rejected => "danger",
            ClaimStatus.Returned => "secondary",
            _ => "secondary"
        };

        // History of claim status changes for display in UI
        [NotMapped]
        public List<StatusHistoryItem> StatusHistory
        {
            get
            {
                var history = new List<StatusHistoryItem>
                {
                    new StatusHistoryItem
                    {
                        Step = 1,
                        Title = "Claim Submitted",
                        Description = "Claim submitted by lecturer",
                        Date = SubmissionDate,
                        IsCompleted = true,
                        Icon = "📝"
                    },
                    new StatusHistoryItem
                    {
                        Step = 2,
                        Title = "Coordinator Review",
                        Description = Status == ClaimStatus.Pending ? "Awaiting coordinator review" :
                                     Status == ClaimStatus.Rejected ? "Rejected by coordinator" :
                                     "Approved by coordinator",
                        Date = CoordinatorApprovalDate,
                        IsCompleted = Status != ClaimStatus.Pending,
                        Icon = Status == ClaimStatus.Rejected ? "❌" : "👤",
                        Notes = CoordinatorNotes
                    },
                    new StatusHistoryItem
                    {
                        Step = 3,
                        Title = "Manager Approval",
                        Description = Status == ClaimStatus.Approved ? "Approved by manager" :
                                     Status == ClaimStatus.PendingManager ? "Awaiting manager approval" :
                                     Status == ClaimStatus.Rejected && ManagerApprovalDate.HasValue ? "Rejected by manager" :
                                     "Pending",
                        Date = ManagerApprovalDate,
                        IsCompleted = Status == ClaimStatus.Approved || (Status == ClaimStatus.Rejected && ManagerApprovalDate.HasValue),
                        Icon = Status == ClaimStatus.Approved ? "✅" : Status == ClaimStatus.Rejected && ManagerApprovalDate.HasValue ? "❌" : "👔",
                        Notes = ManagerNotes
                    },
                    new StatusHistoryItem
                    {
                        Step = 4,
                        Title = "Payment Processing",
                        Description = Status == ClaimStatus.Approved ? "Ready for payment" : "Pending approval",
                        Date = Status == ClaimStatus.Approved ? ManagerApprovalDate : null,
                        IsCompleted = Status == ClaimStatus.Approved,
                        Icon = "💰"
                    }
                };

                return history;
            }
        }
    }

    // Enum representing claim status options
    public enum ClaimStatus
    {
        Pending,
        PendingManager,
        Approved,
        Rejected,
        Returned
    }

    // Represents a single step in claim status history
    public class StatusHistoryItem
    {
        public int Step { get; set; } // Step number in workflow
        public string Title { get; set; } = string.Empty; // Step title
        public string Description { get; set; } = string.Empty; // Description of status
        public DateTime? Date { get; set; } // Date when step occurred
        public bool IsCompleted { get; set; } // Whether the step is completed
        public string Icon { get; set; } = "📋"; // Icon representing the step
        public string? Notes { get; set; } // Optional notes/comments for the step
    }
}
