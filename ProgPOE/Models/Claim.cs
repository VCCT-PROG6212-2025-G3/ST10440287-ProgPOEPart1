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

        // Status tracking methods
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

    public enum ClaimStatus
    {
        Pending,
        PendingManager,
        Approved,
        Rejected,
        Returned
    }

    public class StatusHistoryItem
    {
        public int Step { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DateTime? Date { get; set; }
        public bool IsCompleted { get; set; }
        public string Icon { get; set; } = "📋";
        public string? Notes { get; set; }
    }
}