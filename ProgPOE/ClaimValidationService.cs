using ProgPOE.Data;
using ProgPOE.Models;

namespace ProgPOE.Services
{
    // Validation result class used to hold validation output
    public class ValidationResult
    {
        public bool IsValid { get; set; } // Indicates if the claim passed validation
        public List<string> Errors { get; set; } = new List<string>(); // Critical validation errors
        public List<string> Warnings { get; set; } = new List<string>(); // Non-critical warnings
        public string RecommendedAction { get; set; } = "Approve"; // Suggested next step based on risk
        public int RiskScore { get; set; } = 0; // Risk score (0–100 scale)
    }

    // Interface defining validation methods
    public interface IClaimValidationService
    {
        ValidationResult ValidateClaim(Claim claim); // Performs full validation
        ValidationResult AutoVerifyClaim(Claim claim); // Validation done during submission
        bool MeetsApprovalCriteria(Claim claim); // Determines if claim can auto-approve
    }

    // Implementation of the validation logic
    public class ClaimValidationService : IClaimValidationService
    {
        private readonly ILogger<ClaimValidationService> _logger; // For logging validation results
        private readonly ApplicationDbContext _context; // Database context

        // Policy limits and thresholds
        private const decimal MAX_HOURS_PER_MONTH = 744m; // Max hours in a month
        private const decimal MIN_HOURS_PER_MONTH = 0.1m; // Minimum allowed hours
        private const decimal MAX_HOURLY_RATE = 9999.99m; // Max allowed hourly rate
        private const decimal MIN_HOURLY_RATE = 1m; // Minimum allowed hourly rate
        private const decimal STANDARD_RATE = 450m; // Typical lecturer hourly rate
        private const decimal HIGH_HOURS_THRESHOLD = 200m; // Warning threshold
        private const decimal HIGH_AMOUNT_THRESHOLD = 100000m; // High-value claim threshold
        private const int REQUIRED_DOCUMENTS = 1; // Minimum documents for approval

        // Constructor injecting database context and logger
        public ClaimValidationService(ApplicationDbContext context, ILogger<ClaimValidationService> logger)
        {
            _context = context;
            _logger = logger;
        }

        // Main claim validation logic
        public ValidationResult ValidateClaim(Claim claim)
        {
            var result = new ValidationResult { IsValid = true };

            try
            {
                // 1. Validate claimed hours
                if (claim.HoursWorked < MIN_HOURS_PER_MONTH)
                {
                    result.Errors.Add($"Hours worked ({claim.HoursWorked}) is below minimum ({MIN_HOURS_PER_MONTH})");
                    result.IsValid = false;
                }

                if (claim.HoursWorked > MAX_HOURS_PER_MONTH)
                {
                    result.Errors.Add($"Hours worked ({claim.HoursWorked}) exceeds maximum ({MAX_HOURS_PER_MONTH})");
                    result.IsValid = false;
                }

                // 2. Validate hourly rate
                if (claim.HourlyRate < MIN_HOURLY_RATE)
                {
                    result.Errors.Add($"Hourly rate (R{claim.HourlyRate}) is below minimum (R{MIN_HOURLY_RATE})");
                    result.IsValid = false;
                }

                if (claim.HourlyRate > MAX_HOURLY_RATE)
                {
                    result.Errors.Add($"Hourly rate (R{claim.HourlyRate}) exceeds maximum (R{MAX_HOURLY_RATE})");
                    result.IsValid = false;
                }

                // 3. Compare with lecturer's default rate
                var lecturer = _context.Users.Find(claim.LecturerId);
                if (lecturer != null && lecturer.DefaultHourlyRate.HasValue)
                {
                    if (claim.HourlyRate != lecturer.DefaultHourlyRate.Value)
                    {
                        result.Warnings.Add($"Rate (R{claim.HourlyRate}) differs from lecturer's default rate (R{lecturer.DefaultHourlyRate})");
                        result.RiskScore += 20;
                    }
                }

                // 4. Warn for unusually high hours
                if (claim.HoursWorked > HIGH_HOURS_THRESHOLD)
                {
                    result.Warnings.Add($"High hours claimed ({claim.HoursWorked} hours). Please verify timesheet.");
                    result.RiskScore += 15;
                }

                // 5. Warn for high-value claims
                if (claim.TotalAmount > HIGH_AMOUNT_THRESHOLD)
                {
                    result.Warnings.Add($"High total amount (R{claim.TotalAmount:N2}). Requires additional verification.");
                    result.RiskScore += 25;
                }

                // 6. Validate supporting documents
                var documentCount = _context.SupportingDocuments
                    .Count(d => d.ClaimId == claim.ClaimId && d.IsActive);

                if (documentCount < REQUIRED_DOCUMENTS)
                {
                    result.Warnings.Add($"Only {documentCount} document(s) uploaded. Minimum recommended: {REQUIRED_DOCUMENTS}");
                    result.RiskScore += 10;
                }

                // 7. Detect duplicate claim for same lecturer and month
                var duplicateClaim = _context.Claims
                    .Any(c => c.LecturerId == claim.LecturerId
                           && c.MonthYear == claim.MonthYear
                           && c.ClaimId != claim.ClaimId
                           && c.Status != ClaimStatus.Rejected);

                if (duplicateClaim)
                {
                    result.Errors.Add("Duplicate claim detected for this month/year");
                    result.IsValid = false;
                    result.RiskScore += 50;
                }

                // 8. Determine recommended action using risk score
                if (result.RiskScore >= 50)
                {
                    result.RecommendedAction = "Manual Review Required";
                }
                else if (result.RiskScore >= 30)
                {
                    result.RecommendedAction = "Review with Caution";
                }
                else if (result.RiskScore > 0)
                {
                    result.RecommendedAction = "Approve with Notes";
                }
                else
                {
                    result.RecommendedAction = "Auto-Approve";
                }

                // Log final decision
                _logger.LogInformation($"Claim {claim.ClaimId} validation: IsValid={result.IsValid}, RiskScore={result.RiskScore}, Action={result.RecommendedAction}");
            }
            catch (Exception ex)
            {
                // Log and return system-level error
                _logger.LogError(ex, $"Error validating claim {claim.ClaimId}");
                result.Errors.Add("System error during validation");
                result.IsValid = false;
            }

            return result;
        }

        // Additional verification during submission
        public ValidationResult AutoVerifyClaim(Claim claim)
        {
            var result = ValidateClaim(claim);

            // Date-based checks
            if (!string.IsNullOrEmpty(claim.MonthYear))
            {
                var claimDate = DateTime.Parse(claim.MonthYear + "-01");
                var currentDate = DateTime.Now;

                // Disallow future claims
                if (claimDate > currentDate)
                {
                    result.Errors.Add("Cannot submit claims for future months");
                    result.IsValid = false;
                }

                // Warn for claims older than 3 months
                if (claimDate < currentDate.AddMonths(-3))
                {
                    result.Warnings.Add("Claim is for more than 3 months ago. May require additional justification.");
                    result.RiskScore += 10;
                }
            }

            return result;
        }

        // Evaluates whether claim can be automatically approved
        public bool MeetsApprovalCriteria(Claim claim)
        {
            var result = ValidateClaim(claim);

            // Conditions for auto-approval:
            // Must have no errors
            if (!result.IsValid)
                return false;

            // Risk score must be under threshold
            if (result.RiskScore >= 30)
                return false;

            // Must have supporting docs
            var documentCount = _context.SupportingDocuments
                .Count(d => d.ClaimId == claim.ClaimId && d.IsActive);

            if (documentCount < REQUIRED_DOCUMENTS)
                return false;

            // High amount claims require manual review
            if (claim.TotalAmount > HIGH_AMOUNT_THRESHOLD)
                return false;

            return true; // Passed all auto-approval criteria
        }
    }
}
