using Microsoft.EntityFrameworkCore;
using ProgPOE.Data;
using ProgPOE.Models;

namespace ProgPOE.Services
{
    // Interface defining all HR-related service operations
    public interface IHRService
    {
        // Fetch HR dashboard statistics and recent activities
        Task<HRDashboardViewModel> GetDashboardDataAsync();

        // Get a list of all lecturers in the system
        Task<List<User>> GetAllLecturersAsync();

        // Get a specific lecturer by their ID
        Task<User> GetLecturerByIdAsync(int lecturerId);

        // Create a new lecturer based on ViewModel input
        Task<bool> CreateLecturerAsync(ManageLecturerViewModel model);

        // Update an existing lecturer's basic details
        Task<bool> UpdateLecturerAsync(ManageLecturerViewModel model);

        // Mark a lecturer as inactive (soft delete)
        Task<bool> DeactivateLecturerAsync(int lecturerId);

        // Reactivate a previously deactivated lecturer
        Task<bool> ActivateLecturerAsync(int lecturerId);

        // Generate high-level summaries of lecturer payments
        Task<List<LecturerPaymentSummary>> GetLecturerPaymentSummariesAsync(DateTime? startDate, DateTime? endDate);

        // Create a detailed payment invoice for a specific lecturer + month
        Task<PaymentInvoiceViewModel> GeneratePaymentInvoiceAsync(int lecturerId, string period);

        // Get a filtered list of approved claims for reporting purposes
        Task<List<Claim>> GetApprovedClaimsForReportAsync(DateTime? startDate, DateTime? endDate, int? lecturerId, string department);
    }

    // Implementation of the HR service operations
    public class HRService : IHRService
    {
        // Database context used for all queries
        private readonly ApplicationDbContext _context;

        // Logger service for recording system events and errors
        private readonly ILogger<HRService> _logger;

        public HRService(ApplicationDbContext context, ILogger<HRService> logger)
        {
            _context = context;
            _logger = logger;
        }

        // Fetch dashboard statistics and recent lecturer/claim activity
        public async Task<HRDashboardViewModel> GetDashboardDataAsync()
        {
            try
            {
                // Format used for Month-Year (example: "2025-11")
                var currentMonth = DateTime.Now.ToString("yyyy-MM");

                // Build dashboard data model with multiple queries
                var dashboard = new HRDashboardViewModel
                {
                    // Count all lecturers
                    TotalLecturers = await _context.Users.CountAsync(u => u.Role == UserRole.Lecturer),

                    // Count active lecturers
                    ActiveLecturers = await _context.Users.CountAsync(u => u.Role == UserRole.Lecturer && u.IsActive),

                    // Count inactive lecturers
                    InactiveLecturers = await _context.Users.CountAsync(u => u.Role == UserRole.Lecturer && !u.IsActive),

                    // Count this month's claims
                    TotalClaimsThisMonth = await _context.Claims.CountAsync(c => c.MonthYear == currentMonth),

                    // Count approved claims for this month
                    ApprovedClaimsThisMonth = await _context.Claims.CountAsync(c => c.MonthYear == currentMonth && c.Status == ClaimStatus.Approved),

                    // Sum total approved payments for this month
                    TotalPaymentsThisMonth = await _context.Claims
                        .Where(c => c.MonthYear == currentMonth && c.Status == ClaimStatus.Approved)
                        .SumAsync(c => c.TotalAmount),

                    // Show 5 most recently created lecturers
                    RecentLecturers = await _context.Users
                        .Where(u => u.Role == UserRole.Lecturer)
                        .OrderByDescending(u => u.CreatedDate)
                        .Take(5)
                        .ToListAsync(),

                    // Show 10 most recent approved claims
                    RecentApprovedClaims = await _context.Claims
                        .Include(c => c.Lecturer)
                        .Where(c => c.Status == ClaimStatus.Approved)
                        .OrderByDescending(c => c.ManagerApprovalDate)
                        .Take(10)
                        .ToListAsync()
                };

                return dashboard;
            }
            catch (Exception ex)
            {
                // Log error and return empty dashboard object
                _logger.LogError(ex, "Error getting HR dashboard data");
                return new HRDashboardViewModel();
            }
        }

        // Retrieve a sorted list of all lecturers
        public async Task<List<User>> GetAllLecturersAsync()
        {
            return await _context.Users
                .Where(u => u.Role == UserRole.Lecturer)
                .OrderBy(u => u.LastName)
                .ThenBy(u => u.FirstName)
                .ToListAsync();
        }

        // Find a lecturer based on their unique user ID
        public async Task<User> GetLecturerByIdAsync(int lecturerId)
        {
            return await _context.Users
                .FirstOrDefaultAsync(u => u.UserId == lecturerId && u.Role == UserRole.Lecturer);
        }

        // Create a new lecturer in the system
        public async Task<bool> CreateLecturerAsync(ManageLecturerViewModel model)
        {
            try
            {
                // Prevent duplicate username/email
                var existingUser = await _context.Users
                    .AnyAsync(u => u.Username == model.Username || u.Email == model.Email);

                if (existingUser)
                {
                    _logger.LogWarning($"Username or email already exists: {model.Username}, {model.Email}");
                    return false;
                }

                // Map ViewModel values to User entity
                var lecturer = new User
                {
                    Username = model.Username,
                    Email = model.Email,
                    FirstName = model.FirstName,
                    LastName = model.LastName,
                    Role = UserRole.Lecturer,
                    Department = model.Department,
                    DefaultHourlyRate = model.DefaultHourlyRate,
                    IsActive = model.IsActive,
                    CreatedDate = DateTime.Now
                };

                _context.Users.Add(lecturer);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Lecturer created: {lecturer.FullName} (ID: {lecturer.UserId})");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating lecturer");
                return false;
            }
        }

        // Update existing lecturer profile
        public async Task<bool> UpdateLecturerAsync(ManageLecturerViewModel model)
        {
            try
            {
                // Ensure the lecturer exists
                var lecturer = await _context.Users.FindAsync(model.UserId);
                if (lecturer == null || lecturer.Role != UserRole.Lecturer)
                {
                    _logger.LogWarning($"Lecturer not found: {model.UserId}");
                    return false;
                }

                // Prevent email/username conflicts with other users
                var conflict = await _context.Users
                    .AnyAsync(u => u.UserId != model.UserId &&
                             (u.Username == model.Username || u.Email == model.Email));

                if (conflict)
                {
                    _logger.LogWarning($"Username or email conflict: {model.Username}, {model.Email}");
                    return false;
                }

                // Apply updates
                lecturer.Username = model.Username;
                lecturer.Email = model.Email;
                lecturer.FirstName = model.FirstName;
                lecturer.LastName = model.LastName;
                lecturer.Department = model.Department;
                lecturer.DefaultHourlyRate = model.DefaultHourlyRate;
                lecturer.IsActive = model.IsActive;

                await _context.SaveChangesAsync();

                _logger.LogInformation($"Lecturer updated: {lecturer.FullName} (ID: {lecturer.UserId})");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating lecturer {model.UserId}");
                return false;
            }
        }

        // Mark a lecturer as inactive (soft delete)
        public async Task<bool> DeactivateLecturerAsync(int lecturerId)
        {
            try
            {
                var lecturer = await _context.Users.FindAsync(lecturerId);
                if (lecturer == null || lecturer.Role != UserRole.Lecturer)
                    return false;

                lecturer.IsActive = false;
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Lecturer deactivated: {lecturer.FullName} (ID: {lecturerId})");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deactivating lecturer {lecturerId}");
                return false;
            }
        }

        // Reactivate a previously inactive lecturer
        public async Task<bool> ActivateLecturerAsync(int lecturerId)
        {
            try
            {
                var lecturer = await _context.Users.FindAsync(lecturerId);
                if (lecturer == null || lecturer.Role != UserRole.Lecturer)
                    return false;

                lecturer.IsActive = true;
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Lecturer activated: {lecturer.FullName} (ID: {lecturerId})");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error activating lecturer {lecturerId}");
                return false;
            }
        }

        // Retrieve earnings + claim summary for all lecturers
        public async Task<List<LecturerPaymentSummary>> GetLecturerPaymentSummariesAsync(DateTime? startDate, DateTime? endDate)
        {
            try
            {
                // Start by selecting all lecturers including their claims
                var query = _context.Users
                    .Where(u => u.Role == UserRole.Lecturer)
                    .Include(u => u.Claims)
                    .AsQueryable();

                // Build summary objects from the data
                var summaries = await query.Select(u => new LecturerPaymentSummary
                {
                    Lecturer = u,
                    TotalClaims = u.Claims.Count,
                    ApprovedClaims = u.Claims.Count(c => c.Status == ClaimStatus.Approved),
                    TotalEarnings = u.Claims.Where(c => c.Status == ClaimStatus.Approved).Sum(c => c.TotalAmount),
                    LastClaimDate = u.Claims.OrderByDescending(c => c.SubmissionDate).Select(c => c.SubmissionDate).FirstOrDefault()
                }).ToListAsync();

                // Sort by highest earnings
                return summaries.OrderByDescending(s => s.TotalEarnings).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting payment summaries");
                return new List<LecturerPaymentSummary>();
            }
        }

        // Creates a payment invoice containing all approved claims for a lecturer for a given month
        public async Task<PaymentInvoiceViewModel> GeneratePaymentInvoiceAsync(int lecturerId, string period)
        {
            try
            {
                // Ensure lecturer exists
                var lecturer = await _context.Users.FindAsync(lecturerId);
                if (lecturer == null)
                    return null;

                // Get all approved claims from the specified period
                var claims = await _context.Claims
                    .Include(c => c.Documents)
                    .Where(c => c.LecturerId == lecturerId &&
                               c.MonthYear == period &&
                               c.Status == ClaimStatus.Approved)
                    .ToListAsync();

                // Build invoice object
                var invoice = new PaymentInvoiceViewModel
                {
                    InvoiceNumber = $"INV-{DateTime.Now:yyyyMMdd}-{lecturerId:D4}",
                    InvoiceDate = DateTime.Now,
                    Lecturer = lecturer,
                    Claims = claims,
                    TotalAmount = claims.Sum(c => c.TotalAmount),
                    Period = period
                };

                return invoice;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error generating invoice for lecturer {lecturerId}, period {period}");
                return null;
            }
        }

        // Retrieve approved claims filtered by optional parameters
        public async Task<List<Claim>> GetApprovedClaimsForReportAsync(
            DateTime? startDate,
            DateTime? endDate,
            int? lecturerId,
            string department)
        {
            try
            {
                // Base query: only approved claims
                var query = _context.Claims
                    .Include(c => c.Lecturer)
                    .Include(c => c.Documents)
                    .Where(c => c.Status == ClaimStatus.Approved)
                    .AsQueryable();

                // Filter by lecturer
                if (lecturerId.HasValue)
                    query = query.Where(c => c.LecturerId == lecturerId.Value);

                // Filter by department
                if (!string.IsNullOrEmpty(department))
                    query = query.Where(c => c.Lecturer.Department == department);

                // Filter by start date
                if (startDate.HasValue)
                    query = query.Where(c => c.SubmissionDate >= startDate.Value);

                // Filter by end date
                if (endDate.HasValue)
                    query = query.Where(c => c.SubmissionDate <= endDate.Value);

                // Sort newest first
                return await query
                    .OrderByDescending(c => c.ManagerApprovalDate)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting approved claims for report");
                return new List<Claim>();
            }
        }
    }
}
