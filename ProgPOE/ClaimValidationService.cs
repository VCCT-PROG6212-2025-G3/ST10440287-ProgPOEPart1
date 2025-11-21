using ProgPOE.Data;
using ProgPOE.Models;

namespace ProgPOE.Services
{
    // Validation result class
    public class ValidationResult
    {
        public bool IsValid { get; set; }
        public List<string> Errors { get; set; } = new List<string>();
        public List<string> Warnings { get; set; } = new List<string>();
        public string RecommendedAction { get; set; } = "Approve";
        public int RiskScore { get; set; } = 0; // 0-100
    }

    // Interface for claim validation
    public interface IClaimValidationService
    {
        ValidationResult ValidateClaim(Claim claim);
        ValidationResult AutoVerifyClaim(Claim claim);
        bool MeetsApprovalCriteria(Claim claim);
    }

    // Automated claim validation service
    public class ClaimValidationService : IClaimValidationService
    {
        private readonly ILogger<ClaimValidationService> _logger;
        private readonly ApplicationDbContext _context;

        // Policy rules (can be moved to database/config)
        private const decimal MAX_HOURS_PER_MONTH = 744m;
        private const decimal MIN_HOURS_PER_MONTH = 0.1m;
        private const decimal MAX_HOURLY_RATE = 9999.99m;
        private const decimal MIN_HOURLY_RATE = 1m;
        private const decimal STANDARD_RATE = 450m;
        private const decimal HIGH_HOURS_THRESHOLD = 200m; // Flag if over 200 hours
        private const decimal HIGH_AMOUNT_THRESHOLD = 100000m; // Flag if over R100k
        private const int REQUIRED_DOCUMENTS = 1; // Minimum documents required

        public ClaimValidationService(ApplicationDbContext context, ILogger<ClaimValidationService> logger)
        {
            _context = context;
            _logger = logger;
        }

        // Comprehensive claim validation
        public ValidationResult ValidateClaim(Claim claim)
        {
            var result = new ValidationResult { IsValid = true };

            try
            {
                // 1. Validate Hours Worked
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

                // 2. Validate Hourly Rate
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

                // 3. Check against lecturer's default rate
                var lecturer = _context.Users.Find(claim.LecturerId);
                if (lecturer != null && lecturer.DefaultHourlyRate.HasValue)
                {
                    if (claim.HourlyRate != lecturer.DefaultHourlyRate.Value)
                    {
                        result.Warnings.Add($"Rate (R{claim.HourlyRate}) differs from lecturer's default rate (R{lecturer.DefaultHourlyRate})");
                        result.RiskScore += 20;
                    }
                }

                // 4. High hours warning
                if (claim.HoursWorked > HIGH_HOURS_THRESHOLD)
                {
                    result.Warnings.Add($"High hours claimed ({claim.HoursWorked} hours). Please verify timesheet.");
                    result.RiskScore += 15;
                }

                // 5. High amount warning
                if (claim.TotalAmount > HIGH_AMOUNT_THRESHOLD)
                {
                    result.Warnings.Add($"High total amount (R{claim.TotalAmount:N2}). Requires additional verification.");
                    result.RiskScore += 25;
                }

                // 6. Document validation
                var documentCount = _context.SupportingDocuments
                    .Count(d => d.ClaimId == claim.ClaimId && d.IsActive);

                if (documentCount < REQUIRED_DOCUMENTS)
                {
                    result.Warnings.Add($"Only {documentCount} document(s) uploaded. Minimum recommended: {REQUIRED_DOCUMENTS}");
                    result.RiskScore += 10;
                }

                // 7. Check for duplicate claims (same month/year)
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

                // 8. Determine recommended action based on risk score
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

                _logger.LogInformation($"Claim {claim.ClaimId} validation: IsValid={result.IsValid}, RiskScore={result.RiskScore}, Action={result.RecommendedAction}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error validating claim {claim.ClaimId}");
                result.Errors.Add("System error during validation");
                result.IsValid = false;
            }

            return result;
        }

        // Auto-verify claim (called on submission)
        public ValidationResult AutoVerifyClaim(Claim claim)
        {
            var result = ValidateClaim(claim);

            // Add submission-specific checks
            if (!string.IsNullOrEmpty(claim.MonthYear))
            {
                var claimDate = DateTime.Parse(claim.MonthYear + "-01");
                var currentDate = DateTime.Now;

                // Can't claim for future months
                if (claimDate > currentDate)
                {
                    result.Errors.Add("Cannot submit claims for future months");
                    result.IsValid = false;
                }

                // Warn if claiming for more than 3 months ago
                if (claimDate < currentDate.AddMonths(-3))
                {
                    result.Warnings.Add("Claim is for more than 3 months ago. May require additional justification.");
                    result.RiskScore += 10;
                }
            }

            return result;
        }

        // Check if claim meets auto-approval criteria
        public bool MeetsApprovalCriteria(Claim claim)
        {
            var result = ValidateClaim(claim);

            // Auto-approve criteria:
            // 1. No errors
            // 2. Risk score below 30
            // 3. Has supporting documents
            // 4. Amount within reasonable limits

            if (!result.IsValid)
                return false;

            if (result.RiskScore >= 30)
                return false;

            var documentCount = _context.SupportingDocuments
                .Count(d => d.ClaimId == claim.ClaimId && d.IsActive);

            if (documentCount < REQUIRED_DOCUMENTS)
                return false;

            if (claim.TotalAmount > HIGH_AMOUNT_THRESHOLD)
                return false;

            return true;
        }
    }
}