using Microsoft.EntityFrameworkCore;
using ProgPOE.Data;
using ProgPOE.Models;

namespace ProgPOE.Services
{
    // Interface defining claim-related operations
    public interface IClaimService
    {
        Task<List<Claim>> GetUserClaimsAsync(int userId);                // Retrieve claims for a specific user
        Task<List<Claim>> GetPendingClaimsAsync();                       // Retrieve all claims currently pending
        Task<DashboardViewModel> GetDashboardDataAsync(int userId);      // Retrieve dashboard statistics for a user
        Task<Claim> GetClaimByIdAsync(int claimId);                      // Retrieve a specific claim by ID
        Task<bool> ProcessApprovalAsync(int claimId, ApprovalAction action, string comments, int approverId); // Process claim approval/rejection
    }

    // Concrete implementation of IClaimService
    public class ClaimService : IClaimService
    {
        private readonly ApplicationDbContext _context;  // Database context for EF Core operations
        private readonly ILogger<ClaimService> _logger;  // Logger for error and information tracking

        // Constructor that injects the database context and logger
        public ClaimService(ApplicationDbContext context, ILogger<ClaimService> logger)
        {
            _context = context;
            _logger = logger;
        }

        // Retrieves all claims for a specific user
        public async Task<List<Claim>> GetUserClaimsAsync(int userId)
        {
            try
            {
                // Query all claims for the given lecturer/user, including related data
                var claims = await _context.Claims
                    .Include(c => c.Lecturer)
                    .Include(c => c.Documents)
                    .Where(c => c.LecturerId == userId)
                    .OrderByDescending(c => c.SubmissionDate)
                    .AsNoTracking() // Ensures the query doesn’t use cached entities
                    .ToListAsync();

                // Log successful retrieval
                _logger.LogInformation($"Retrieved {claims.Count} claims for user {userId}");
                return claims;
            }
            catch (Exception ex)
            {
                // Log any exception and return an empty list
                _logger.LogError(ex, $"Error retrieving claims for user {userId}");
                return new List<Claim>();
            }
        }

        // Retrieves all claims with a pending status
        public async Task<List<Claim>> GetPendingClaimsAsync()
        {
            try
            {
                // Return claims that are pending coordinator or manager approval
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
                // Log error and return empty list
                _logger.LogError(ex, "Error retrieving pending claims");
                return new List<Claim>();
            }
        }

        // Retrieves dashboard statistics for a given user
        public async Task<DashboardViewModel> GetDashboardDataAsync(int userId)
        {
            try
            {
                // Get user info for role and name display
                var user = await _context.Users
                    .AsNoTracking()
                    .FirstOrDefaultAsync(u => u.UserId == userId);

                List<Claim> claims;

                // Role-based claim visibility
                if (user?.Role == UserRole.Lecturer)
                {
                    // Lecturers see only their claims
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

                // Create dashboard summary model
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

                // Log generated statistics
                _logger.LogInformation($"Dashboard data for user {userId}: Total={dashboardData.TotalClaims}, Pending={dashboardData.PendingClaims}, Approved={dashboardData.ApprovedClaims}, Rejected={dashboardData.RejectedClaims}");

                return dashboardData;
            }
            catch (Exception ex)
            {
                // Log and return default empty dashboard in case of error
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

        // Retrieves a specific claim by ID
        public async Task<Claim> GetClaimByIdAsync(int claimId)
        {
            try
            {
                // Fetch claim details including lecturer and document info
                return await _context.Claims
                    .Include(c => c.Lecturer)
                    .Include(c => c.Documents)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(c => c.ClaimId == claimId);
            }
            catch (Exception ex)
            {
                // Log any errors during retrieval
                _logger.LogError(ex, $"Error getting claim {claimId}");
                return null;
            }
        }

        // Handles claim approval, rejection, or return actions
        public async Task<bool> ProcessApprovalAsync(int claimId, ApprovalAction action, string comments, int approverId)
        {
            try
            {
                // Find the claim record for updating
                var claim = await _context.Claims.FindAsync(claimId);
                if (claim == null)
                {
                    _logger.LogWarning($"Claim {claimId} not found");
                    return false;
                }

                // Find the approver (Coordinator or Manager)
                var approver = await _context.Users.FindAsync(approverId);
                if (approver == null)
                {
                    _logger.LogWarning($"Approver {approverId} not found");
                    return false;
                }

                // Log action details
                _logger.LogInformation($"Processing {action} for Claim {claimId} by {approver.FullName} ({approver.Role})");
                _logger.LogInformation($"Current Status: {claim.Status}");

                // Determine which action to process
                switch (action)
                {
                    case ApprovalAction.Approve:
                        if (approver.Role == UserRole.ProgrammeCoordinator)
                        {
                            // Coordinator approves and passes to manager
                            claim.Status = ClaimStatus.PendingManager;
                            claim.CoordinatorApprovalDate = DateTime.Now;
                            claim.CoordinatorNotes = string.IsNullOrEmpty(comments) ? "Approved by coordinator" : comments;
                            _logger.LogInformation($"Claim {claimId} approved by coordinator, status changed from {ClaimStatus.Pending} to {ClaimStatus.PendingManager}");
                        }
                        else if (approver.Role == UserRole.AcademicManager)
                        {
                            // Manager gives final approval
                            claim.Status = ClaimStatus.Approved;
                            claim.ManagerApprovalDate = DateTime.Now;
                            claim.ManagerNotes = string.IsNullOrEmpty(comments) ? "Approved by manager" : comments;
                            _logger.LogInformation($"Claim {claimId} approved by manager, status changed from {ClaimStatus.PendingManager} to {ClaimStatus.Approved}");
                        }
                        break;

                    case ApprovalAction.Reject:
                        // Reject claim at either level
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
                        // Return claim for revision
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

                // Save all updates to database
                await _context.SaveChangesAsync();

                // Verify and log final status after save
                var updatedClaim = await _context.Claims.AsNoTracking().FirstOrDefaultAsync(c => c.ClaimId == claimId);
                _logger.LogInformation($"Claim {claimId} status after save: {updatedClaim?.Status}");
                _logger.LogInformation($"Claim {claimId} processed successfully by {approver.FullName}");

                return true;
            }
            catch (Exception ex)
            {
                // Log and return false on error
                _logger.LogError(ex, $"Error processing approval for claim {claimId}");
                return false;
            }
        }
    }
}
