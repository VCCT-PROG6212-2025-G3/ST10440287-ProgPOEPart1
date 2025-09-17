using ProgPOE.Controllers;
using ProgPOE.Models;

namespace ProgPOE
{
    public interface IClaimService
    {
        Task<List<Claim>> GetUserClaimsAsync(int userId);
        Task<List<Claim>> GetPendingClaimsAsync();
        Task<bool> SubmitClaimAsync(SubmitClaimViewModel model);
        Task<bool> ProcessApprovalAsync(int claimId, ApprovalAction action, string comments);
        Task<DashboardViewModel> GetDashboardDataAsync(int userId);
    }

    public class ClaimService : IClaimService
    {
        // Note: This is a non-functional prototype - no real database operations

        public async Task<List<Claim>> GetUserClaimsAsync(int userId)
        {
            // Simulate async operation
            await Task.Delay(100);

            // Return sample data for prototype
            return GetSampleUserClaims();
        }

        public async Task<List<Claim>> GetPendingClaimsAsync()
        {
            // Simulate async operation
            await Task.Delay(100);

            // Return sample pending claims
            return GetSamplePendingClaims();
        }

        public async Task<bool> SubmitClaimAsync(SubmitClaimViewModel model)
        {
            // Simulate async operation
            await Task.Delay(500);

            // In real implementation, this would:
            // 1. Validate the claim
            // 2. Save to database
            // 3. Upload documents
            // 4. Send notifications

            return true; // Always success in prototype
        }

        public async Task<bool> ProcessApprovalAsync(int claimId, ApprovalAction action, string comments)
        {
            // Simulate async operation
            await Task.Delay(300);

            // In real implementation, this would:
            // 1. Update claim status
            // 2. Record approval history
            // 3. Send notifications
            // 4. Update workflow

            return true; // Always success in prototype
        }

        public async Task<DashboardViewModel> GetDashboardDataAsync(int userId)
        {
            // Simulate async operation
            await Task.Delay(200);

            // Return sample dashboard data
            return new DashboardViewModel
            {
                UserRole = "Lecturer",
                UserName = "Dr. John Smith",
                TotalClaims = 8,
                PendingClaims = 2,
                ApprovedClaims = 5,
                RejectedClaims = 1,
                TotalEarnings = 284500.00m
            };
        }

        // Helper methods for sample data
        private List<Claim> GetSampleUserClaims()
        {
            return new List<Claim>
            {
                new Claim
                {
                    ClaimId = 1,
                    MonthYear = "2024-04",
                    HoursWorked = 125.5m,
                    HourlyRate = 450.00m,
                    Status = ClaimStatus.Pending,
                    SubmissionDate = DateTime.Now.AddDays(-3),
                    Lecturer = new User("Dr. John", "Smith")
                },
                new Claim
                {
                    ClaimId = 2,
                    MonthYear = "2024-03",
                    HoursWorked = 118.0m,
                    HourlyRate = 450.00m,
                    Status = ClaimStatus.Approved,
                    SubmissionDate = DateTime.Now.AddDays(-35),
                    Lecturer = new User("Dr. John", "Smith")
                }
            };
        }

        private List<Claim> GetSamplePendingClaims()
        {
            return new List<Claim>
            {
                new Claim
                {
                    ClaimId = 3,
                    MonthYear = "2024-04",
                    HoursWorked = 142.0m,
                    HourlyRate = 420.00m,
                    Status = ClaimStatus.Pending,
                    SubmissionDate = DateTime.Now.AddDays(-2),
                    Lecturer = new User("Dr. Jane", "Wilson")
                }
            };
        }
    }
    // Update the accessibility of the DashboardViewModel class to match the accessibility of the method using it.
    public class DashboardViewModel
    {
        public string UserRole { get; set; }
        public string UserName { get; set; }
        public int TotalClaims { get; set; }
        public int PendingClaims { get; set; }
        public int ApprovedClaims { get; set; }
        public int RejectedClaims { get; set; }
        public decimal TotalEarnings { get; set; }
    }
}