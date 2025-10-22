using Microsoft.EntityFrameworkCore;
using ProgPOE.Data;
using ProgPOE.Models;

namespace ProgPOE.Services
{
    public interface IClaimService
    {
        Task<List<Claim>> GetUserClaimsAsync(int userId);
        Task<List<Claim>> GetPendingClaimsAsync();
        Task<DashboardViewModel> GetDashboardDataAsync(int userId);
        Task<Claim> GetClaimByIdAsync(int claimId);
        Task<bool> ProcessApprovalAsync(int claimId, ApprovalAction action, string comments, int approverId);
    }

    public class ClaimService : IClaimService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<ClaimService> _logger;

        public ClaimService(ApplicationDbContext context, ILogger<ClaimService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<List<Claim>> GetUserClaimsAsync(int userId)
        {
            try
            {
                // Get ALL claims for the user, regardless of status
                var claims = await _context.Claims
                    .Include(c => c.Lecturer)
                    .Include(c => c.Documents)
                    .Where(c => c.LecturerId == userId)
                    .OrderByDescending(c => c.SubmissionDate)
                    .AsNoTracking() // Ensure we get fresh data
                    .ToListAsync();

                _logger.LogInformation($"Retrieved {claims.Count} claims for user {userId}");
                return claims;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error retrieving claims for user {userId}");
                return new List<Claim>();
            }
        }

        public async Task<List<Claim>> GetPendingClaimsAsync()
        {
            try
            {
                return await _context.Claims
                    .Include(c => c.Lecturer)
                    .Include(c => c.Documents)
                    .Where(c => c.Status == ClaimStatus.Pending || c.Status == ClaimStatus.PendingManager)
                    .OrderBy(c => c.SubmissionDate)
                    .AsNoTracking()
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving pending claims");
                return new List<Claim>();
            }
        }

        public async Task<DashboardViewModel> GetDashboardDataAsync(int userId)
        {
            try
            {
                var user = await _context.Users
                    .AsNoTracking()
                    .FirstOrDefaultAsync(u => u.UserId == userId);

                // Get claims based on user role with fresh data
                List<Claim> claims;
                if (user?.Role == UserRole.Lecturer)
                {
                    // Lecturers see only their own claims
                    claims = await _context.Claims
                        .AsNoTracking()
                        .Where(c => c.LecturerId == userId)
                        .ToListAsync();
                }
                else
                {
                    // Coordinators and Managers see all claims
                    claims = await _context.Claims
                        .AsNoTracking()
                        .ToListAsync();
                }

                var dashboardData = new DashboardViewModel
                {
                    UserRole = user?.Role.ToString() ?? "Lecturer",
                    UserName = user?.FullName ?? "Unknown User",
                    TotalClaims = claims.Count,
                    PendingClaims = claims.Count(c => c.Status == ClaimStatus.Pending || c.Status == ClaimStatus.PendingManager),
                    ApprovedClaims = claims.Count(c => c.Status == ClaimStatus.Approved),
                    RejectedClaims = claims.Count(c => c.Status == ClaimStatus.Rejected),
                    TotalEarnings = claims.Where(c => c.Status == ClaimStatus.Approved).Sum(c => c.TotalAmount)
                };

                _logger.LogInformation($"Dashboard data for user {userId}: Total={dashboardData.TotalClaims}, Pending={dashboardData.PendingClaims}, Approved={dashboardData.ApprovedClaims}, Rejected={dashboardData.RejectedClaims}");

                return dashboardData;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting dashboard data for user {userId}");
                return new DashboardViewModel
                {
                    UserRole = "Lecturer",
                    UserName = "Unknown User",
                    TotalClaims = 0,
                    PendingClaims = 0,
                    ApprovedClaims = 0,
                    RejectedClaims = 0,
                    TotalEarnings = 0
                };
            }
        }

        public async Task<Claim> GetClaimByIdAsync(int claimId)
        {
            try
            {
                // Always get fresh data from database
                return await _context.Claims
                    .Include(c => c.Lecturer)
                    .Include(c => c.Documents)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(c => c.ClaimId == claimId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting claim {claimId}");
                return null;
            }
        }

        public async Task<bool> ProcessApprovalAsync(int claimId, ApprovalAction action, string comments, int approverId)
        {
            try
            {
                // Get claim with tracking enabled for updates
                var claim = await _context.Claims.FindAsync(claimId);
                if (claim == null)
                {
                    _logger.LogWarning($"Claim {claimId} not found");
                    return false;
                }

                var approver = await _context.Users.FindAsync(approverId);
                if (approver == null)
                {
                    _logger.LogWarning($"Approver {approverId} not found");
                    return false;
                }

                _logger.LogInformation($"Processing {action} for Claim {claimId} by {approver.FullName} ({approver.Role})");
                _logger.LogInformation($"Current Status: {claim.Status}");

                switch (action)
                {
                    case ApprovalAction.Approve:
                        if (approver.Role == UserRole.ProgrammeCoordinator)
                        {
                            claim.Status = ClaimStatus.PendingManager;
                            claim.CoordinatorApprovalDate = DateTime.Now;
                            claim.CoordinatorNotes = string.IsNullOrEmpty(comments) ? "Approved by coordinator" : comments;
                            _logger.LogInformation($"Claim {claimId} approved by coordinator, status changed from {ClaimStatus.Pending} to {ClaimStatus.PendingManager}");
                        }
                        else if (approver.Role == UserRole.AcademicManager)
                        {
                            claim.Status = ClaimStatus.Approved;
                            claim.ManagerApprovalDate = DateTime.Now;
                            claim.ManagerNotes = string.IsNullOrEmpty(comments) ? "Approved by manager" : comments;
                            _logger.LogInformation($"Claim {claimId} approved by manager, status changed from {ClaimStatus.PendingManager} to {ClaimStatus.Approved}");
                        }
                        break;

                    case ApprovalAction.Reject:
                        claim.Status = ClaimStatus.Rejected;
                        if (approver.Role == UserRole.ProgrammeCoordinator)
                        {
                            claim.CoordinatorNotes = string.IsNullOrEmpty(comments) ? "Rejected by coordinator" : comments;
                            claim.CoordinatorApprovalDate = DateTime.Now;
                            _logger.LogInformation($"Claim {claimId} rejected by coordinator");
                        }
                        else
                        {
                            claim.ManagerNotes = string.IsNullOrEmpty(comments) ? "Rejected by manager" : comments;
                            claim.ManagerApprovalDate = DateTime.Now;
                            _logger.LogInformation($"Claim {claimId} rejected by manager");
                        }
                        break;

                    case ApprovalAction.Return:
                        claim.Status = ClaimStatus.Returned;
                        if (approver.Role == UserRole.ProgrammeCoordinator)
                        {
                            claim.CoordinatorNotes = string.IsNullOrEmpty(comments) ? "Returned for revision" : comments;
                            claim.CoordinatorApprovalDate = DateTime.Now;
                        }
                        else
                        {
                            claim.ManagerNotes = string.IsNullOrEmpty(comments) ? "Returned for revision" : comments;
                            claim.ManagerApprovalDate = DateTime.Now;
                        }
                        _logger.LogInformation($"Claim {claimId} returned by {approver.Role}");
                        break;
                }

                // Save changes and verify
                await _context.SaveChangesAsync();

                // Verify the update
                var updatedClaim = await _context.Claims.AsNoTracking().FirstOrDefaultAsync(c => c.ClaimId == claimId);
                _logger.LogInformation($"Claim {claimId} status after save: {updatedClaim?.Status}");
                _logger.LogInformation($"Claim {claimId} processed successfully by {approver.FullName}");

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error processing approval for claim {claimId}");
                return false;
            }
        }
    }
}