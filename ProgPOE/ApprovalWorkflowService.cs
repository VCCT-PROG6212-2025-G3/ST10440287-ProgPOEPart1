using Microsoft.EntityFrameworkCore;
using ProgPOE.Data;
using ProgPOE.Models;

namespace ProgPOE.Services
{
    // Workflow decision result
    public class WorkflowDecision
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public ClaimStatus NewStatus { get; set; }
        public List<string> Notifications { get; set; } = new List<string>();
    }

    // Interface for approval workflow
    public interface IApprovalWorkflowService
    {
        Task<WorkflowDecision> ProcessWorkflowAsync(int claimId, ApprovalAction action, int approverId, string comments);
        Task<List<Claim>> GetClaimsForApproverAsync(int approverId, UserRole role);
        Task<bool> CanApproveClaimAsync(int claimId, int approverId, UserRole role);
    }

    // Automated approval workflow service
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

        // Process approval workflow with automated checks
        public async Task<WorkflowDecision> ProcessWorkflowAsync(
            int claimId,
            ApprovalAction action,
            int approverId,
            string comments)
        {
            var decision = new WorkflowDecision { Success = false };

            try
            {
                var claim = await _context.Claims
                    .Include(c => c.Lecturer)
                    .FirstOrDefaultAsync(c => c.ClaimId == claimId);

                if (claim == null)
                {
                    decision.Message = "Claim not found";
                    return decision;
                }

                var approver = await _context.Users.FindAsync(approverId);
                if (approver == null)
                {
                    decision.Message = "Approver not found";
                    return decision;
                }

                // Check if approver has permission
                if (!await CanApproveClaimAsync(claimId, approverId, approver.Role))
                {
                    decision.Message = "You don't have permission to approve this claim";
                    return decision;
                }

                // Run automated validation
                var validation = _validationService.ValidateClaim(claim);

                _logger.LogInformation($"Processing workflow for Claim {claimId} by {approver.FullName} ({approver.Role}): Action={action}");

                // Process based on action and role
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

        // Process approval action
        private async Task<WorkflowDecision> ProcessApprovalAsync(
            Claim claim,
            User approver,
            string comments,
            ValidationResult validation)
        {
            var decision = new WorkflowDecision { Success = true };

            // Add validation warnings to comments
            var fullComments = comments;
            if (validation.Warnings.Any())
            {
                fullComments += "\n\nAutomated Checks:\n" + string.Join("\n", validation.Warnings);
                fullComments += $"\nRisk Score: {validation.RiskScore}/100";
            }

            if (approver.Role == UserRole.ProgrammeCoordinator)
            {
                // Coordinator approval
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
                // Manager approval - final approval
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

        // Process rejection
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

        // Process return for revision
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

        // Get claims that an approver can review
        public async Task<List<Claim>> GetClaimsForApproverAsync(int approverId, UserRole role)
        {
            var query = _context.Claims
                .Include(c => c.Lecturer)
                .Include(c => c.Documents)
                .AsQueryable();

            if (role == UserRole.ProgrammeCoordinator)
            {
                // Coordinators see claims pending their review
                query = query.Where(c => c.Status == ClaimStatus.Pending);
            }
            else if (role == UserRole.AcademicManager)
            {
                // Managers see claims pending their approval
                query = query.Where(c => c.Status == ClaimStatus.PendingManager);
            }

            return await query
                .OrderBy(c => c.SubmissionDate)
                .ToListAsync();
        }

        // Check if user can approve a claim
        public async Task<bool> CanApproveClaimAsync(int claimId, int approverId, UserRole role)
        {
            var claim = await _context.Claims.FindAsync(claimId);
            if (claim == null) return false;

            // Coordinators can only approve pending claims
            if (role == UserRole.ProgrammeCoordinator && claim.Status != ClaimStatus.Pending)
                return false;

            // Managers can only approve claims pending manager review
            if (role == UserRole.AcademicManager && claim.Status != ClaimStatus.PendingManager)
                return false;

            // Lecturers cannot approve
            if (role == UserRole.Lecturer)
                return false;

            return true;
        }
    }
}