using Microsoft.EntityFrameworkCore;
using ProgPOE.Data;
using ProgPOE.Models;

namespace ProgPOE.Services
{
    // Represents the result of a workflow action (approve, reject, return)
    public class WorkflowDecision
    {
        public bool Success { get; set; }                  // Whether the workflow action completed successfully
        public string Message { get; set; }               // Message to show to the user
        public ClaimStatus NewStatus { get; set; }        // Updated claim status
        public List<string> Notifications { get; set; } = new List<string>(); // Notifications to send
    }

    // Interface defining the approval workflow operations
    public interface IApprovalWorkflowService
    {
        Task<WorkflowDecision> ProcessWorkflowAsync(int claimId, ApprovalAction action, int approverId, string comments);
        Task<List<Claim>> GetClaimsForApproverAsync(int approverId, UserRole role);
        Task<bool> CanApproveClaimAsync(int claimId, int approverId, UserRole role);
    }

    // Handles the automated approval workflow for claims
    public class ApprovalWorkflowService : IApprovalWorkflowService
    {
        private readonly ApplicationDbContext _context;
        private readonly IClaimValidationService _validationService;
        private readonly ILogger<ApprovalWorkflowService> _logger;

        public ApprovalWorkflowService(
            ApplicationDbContext context,
            IClaimValidationService validationService,
            ILogger<ApprovalWorkflowService> logger)
        {
            _context = context;
            _validationService = validationService;
            _logger = logger;
        }

        // Main workflow processor (Approve, Reject, Return)
        public async Task<WorkflowDecision> ProcessWorkflowAsync(
            int claimId,
            ApprovalAction action,
            int approverId,
            string comments)
        {
            var decision = new WorkflowDecision { Success = false };

            try
            {
                // Load claim with lecturer details
                var claim = await _context.Claims
                    .Include(c => c.Lecturer)
                    .FirstOrDefaultAsync(c => c.ClaimId == claimId);

                if (claim == null)
                {
                    decision.Message = "Claim not found";
                    return decision;
                }

                // Load approver
                var approver = await _context.Users.FindAsync(approverId);
                if (approver == null)
                {
                    decision.Message = "Approver not found";
                    return decision;
                }

                // Check permission for this approver
                if (!await CanApproveClaimAsync(claimId, approverId, approver.Role))
                {
                    decision.Message = "You don't have permission to approve this claim";
                    return decision;
                }

                // Run validation rules on the claim
                var validation = _validationService.ValidateClaim(claim);

                _logger.LogInformation($"Processing workflow for Claim {claimId} by {approver.FullName} ({approver.Role}): Action={action}");

                // Route action to appropriate internal method
                switch (action)
                {
                    case ApprovalAction.Approve:
                        decision = await ProcessApprovalAsync(claim, approver, comments, validation);
                        break;

                    case ApprovalAction.Reject:
                        decision = await ProcessRejectionAsync(claim, approver, comments);
                        break;

                    case ApprovalAction.Return:
                        decision = await ProcessReturnAsync(claim, approver, comments);
                        break;
                }

                // Save changes if action succeeded
                if (decision.Success)
                {
                    await _context.SaveChangesAsync();
                    _logger.LogInformation($"Workflow completed: Claim {claimId} -> {decision.NewStatus}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error processing workflow for claim {claimId}");
                decision.Message = "System error processing workflow";
            }

            return decision;
        }

        // Handles approval logic for both Coordinator and Academic Manager
        private async Task<WorkflowDecision> ProcessApprovalAsync(
            Claim claim,
            User approver,
            string comments,
            ValidationResult validation)
        {
            var decision = new WorkflowDecision { Success = true };

            // Append validation warnings to comments when approving
            var fullComments = comments;
            if (validation.Warnings.Any())
            {
                fullComments += "\n\nAutomated Checks:\n" + string.Join("\n", validation.Warnings);
                fullComments += $"\nRisk Score: {validation.RiskScore}/100";
            }

            if (approver.Role == UserRole.ProgrammeCoordinator)
            {
                // First level approval → forwards claim to manager
                claim.Status = ClaimStatus.PendingManager;
                claim.CoordinatorApprovalDate = DateTime.Now;
                claim.CoordinatorNotes = string.IsNullOrEmpty(fullComments)
                    ? "Approved by coordinator"
                    : fullComments;

                decision.NewStatus = ClaimStatus.PendingManager;
                decision.Message = "Claim approved and forwarded to Academic Manager";
                decision.Notifications.Add($"Notify manager: New claim awaiting approval");

                _logger.LogInformation($"Coordinator approved Claim {claim.ClaimId}: {ClaimStatus.Pending} -> {ClaimStatus.PendingManager}");
            }
            else if (approver.Role == UserRole.AcademicManager)
            {
                // Final approval
                claim.Status = ClaimStatus.Approved;
                claim.ManagerApprovalDate = DateTime.Now;
                claim.ManagerNotes = string.IsNullOrEmpty(fullComments)
                    ? "Approved by manager"
                    : fullComments;

                decision.NewStatus = ClaimStatus.Approved;
                decision.Message = "Claim approved for payment processing";
                decision.Notifications.Add($"Notify lecturer {claim.Lecturer?.FullName}: Claim approved - R{claim.TotalAmount:N2}");

                _logger.LogInformation($"Manager approved Claim {claim.ClaimId}: {ClaimStatus.PendingManager} -> {ClaimStatus.Approved}");
            }

            return decision;
        }

        // Handles claim rejection for both roles
        private async Task<WorkflowDecision> ProcessRejectionAsync(
            Claim claim,
            User approver,
            string comments)
        {
            var decision = new WorkflowDecision
            {
                Success = true,
                NewStatus = ClaimStatus.Rejected,
                Message = "Claim rejected"
            };

            claim.Status = ClaimStatus.Rejected;

            // Save rejection comments depending on role
            if (approver.Role == UserRole.ProgrammeCoordinator)
            {
                claim.CoordinatorNotes = string.IsNullOrEmpty(comments)
                    ? "Rejected by coordinator"
                    : comments;
                claim.CoordinatorApprovalDate = DateTime.Now;
            }
            else if (approver.Role == UserRole.AcademicManager)
            {
                claim.ManagerNotes = string.IsNullOrEmpty(comments)
                    ? "Rejected by manager"
                    : comments;
                claim.ManagerApprovalDate = DateTime.Now;
            }

            decision.Notifications.Add($"Notify lecturer {claim.Lecturer?.FullName}: Claim rejected");

            _logger.LogInformation($"Claim {claim.ClaimId} rejected by {approver.Role}");

            return decision;
        }

        // Handles returning a claim for revision
        private async Task<WorkflowDecision> ProcessReturnAsync(
            Claim claim,
            User approver,
            string comments)
        {
            var decision = new WorkflowDecision
            {
                Success = true,
                NewStatus = ClaimStatus.Returned,
                Message = "Claim returned for revision"
            };

            claim.Status = ClaimStatus.Returned;

            // Save return notes depending on role
            if (approver.Role == UserRole.ProgrammeCoordinator)
            {
                claim.CoordinatorNotes = string.IsNullOrEmpty(comments)
                    ? "Returned for revision"
                    : comments;
                claim.CoordinatorApprovalDate = DateTime.Now;
            }
            else if (approver.Role == UserRole.AcademicManager)
            {
                claim.ManagerNotes = string.IsNullOrEmpty(comments)
                    ? "Returned for revision"
                    : comments;
                claim.ManagerApprovalDate = DateTime.Now;
            }

            decision.Notifications.Add($"Notify lecturer {claim.Lecturer?.FullName}: Claim needs revision");

            _logger.LogInformation($"Claim {claim.ClaimId} returned by {approver.Role}");

            return decision;
        }

        // Returns all claims that an approver can see depending on their role
        public async Task<List<Claim>> GetClaimsForApproverAsync(int approverId, UserRole role)
        {
            var query = _context.Claims
                .Include(c => c.Lecturer)
                .Include(c => c.Documents)
                .AsQueryable();

            if (role == UserRole.ProgrammeCoordinator)
            {
                // Coordinators view claims in "Pending" status
                query = query.Where(c => c.Status == ClaimStatus.Pending);
            }
            else if (role == UserRole.AcademicManager)
            {
                // Managers view claims forwarded from Coordinator
                query = query.Where(c => c.Status == ClaimStatus.PendingManager);
            }

            return await query
                .OrderBy(c => c.SubmissionDate)
                .ToListAsync();
        }

        // Checks whether the given user has permission to approve the given claim
        public async Task<bool> CanApproveClaimAsync(int claimId, int approverId, UserRole role)
        {
            var claim = await _context.Claims.FindAsync(claimId);
            if (claim == null) return false;

            // Coordinator permission check
            if (role == UserRole.ProgrammeCoordinator && claim.Status != ClaimStatus.Pending)
                return false;

            // Manager permission check
            if (role == UserRole.AcademicManager && claim.Status != ClaimStatus.PendingManager)
                return false;

            // Lecturers cannot approve any claim
            if (role == UserRole.Lecturer)
                return false;

            return true;
        }
    }
}
