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
                var claims = await _context.Claims
                    .Include(c => c.Lecturer)
                    .Include(c => c.Documents)
                    .Where(c => c.LecturerId == userId)
                    .OrderByDescending(c => c.SubmissionDate)
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
                var user = await _context.Users.FindAsync(userId);

                // Get claims based on user role
                List<Claim> claims;
                if (user?.Role == UserRole.Lecturer)
                {
                    // Lecturers see only their own claims
                    claims = await _context.Claims
                        .Where(c => c.LecturerId == userId)
                        .ToListAsync();
                }
                else
                {
                    // Coordinators and Managers see all claims
                    claims = await _context.Claims.ToListAsync();
                }

                return new DashboardViewModel
                {
                    UserRole = user?.Role.ToString() ?? "Lecturer",
                    UserName = user?.FullName ?? "Unknown User",
                    TotalClaims = claims.Count,
                    PendingClaims = claims.Count(c => c.Status == ClaimStatus.Pending || c.Status == ClaimStatus.PendingManager),
                    ApprovedClaims = claims.Count(c => c.Status == ClaimStatus.Approved),
                    RejectedClaims = claims.Count(c => c.Status == ClaimStatus.Rejected),
                    TotalEarnings = claims.Where(c => c.Status == ClaimStatus.Approved).Sum(c => c.TotalAmount)
                };
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
                return await _context.Claims
                    .Include(c => c.Lecturer)
                    .Include(c => c.Documents)
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

                switch (action)
                {
                    case ApprovalAction.Approve:
                        if (approver.Role == UserRole.ProgrammeCoordinator)
                        {
                            claim.Status = ClaimStatus.PendingManager;
                            claim.CoordinatorApprovalDate = DateTime.Now;
                            claim.CoordinatorNotes = comments;
                            _logger.LogInformation($"Claim {claimId} approved by coordinator, moved to PendingManager");
                        }
                        else if (approver.Role == UserRole.AcademicManager)
                        {
                            claim.Status = ClaimStatus.Approved;
                            claim.ManagerApprovalDate = DateTime.Now;
                            claim.ManagerNotes = comments;
                            _logger.LogInformation($"Claim {claimId} approved by manager, status set to Approved");
                        }
                        break;

                    case ApprovalAction.Reject:
                        claim.Status = ClaimStatus.Rejected;
                        if (approver.Role == UserRole.ProgrammeCoordinator)
                        {
                            claim.CoordinatorNotes = comments;
                            claim.CoordinatorApprovalDate = DateTime.Now;
                        }
                        else
                        {
                            claim.ManagerNotes = comments;
                            claim.ManagerApprovalDate = DateTime.Now;
                        }
                        _logger.LogInformation($"Claim {claimId} rejected by {approver.Role}");
                        break;

                    case ApprovalAction.Return:
                        claim.Status = ClaimStatus.Returned;
                        if (approver.Role == UserRole.ProgrammeCoordinator)
                        {
                            claim.CoordinatorNotes = comments;
                            claim.CoordinatorApprovalDate = DateTime.Now;
                        }
                        else
                        {
                            claim.ManagerNotes = comments;
                            claim.ManagerApprovalDate = DateTime.Now;
                        }
                        _logger.LogInformation($"Claim {claimId} returned by {approver.Role}");
                        break;
                }

                await _context.SaveChangesAsync();
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