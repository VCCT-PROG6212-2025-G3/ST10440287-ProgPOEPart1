using Microsoft.EntityFrameworkCore;
using ProgPOE.Data;
using ProgPOE.Models;

namespace ProgPOE.Services
{
    // Interface for HR operations
    public interface IHRService
    {
        Task<HRDashboardViewModel> GetDashboardDataAsync();
        Task<List<User>> GetAllLecturersAsync();
        Task<User> GetLecturerByIdAsync(int lecturerId);
        Task<bool> CreateLecturerAsync(ManageLecturerViewModel model);
        Task<bool> UpdateLecturerAsync(ManageLecturerViewModel model);
        Task<bool> DeactivateLecturerAsync(int lecturerId);
        Task<bool> ActivateLecturerAsync(int lecturerId);
        Task<List<LecturerPaymentSummary>> GetLecturerPaymentSummariesAsync(DateTime? startDate, DateTime? endDate);
        Task<PaymentInvoiceViewModel> GeneratePaymentInvoiceAsync(int lecturerId, string period);
        Task<List<Claim>> GetApprovedClaimsForReportAsync(DateTime? startDate, DateTime? endDate, int? lecturerId, string department);
    }

    // HR Service Implementation
    public class HRService : IHRService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<HRService> _logger;

        public HRService(ApplicationDbContext context, ILogger<HRService> logger)
        {
            _context = context;
            _logger = logger;
        }

        // Get HR Dashboard Data
        public async Task<HRDashboardViewModel> GetDashboardDataAsync()
        {
            try
            {
                var currentMonth = DateTime.Now.ToString("yyyy-MM");

                var dashboard = new HRDashboardViewModel
                {
                    TotalLecturers = await _context.Users.CountAsync(u => u.Role == UserRole.Lecturer),
                    ActiveLecturers = await _context.Users.CountAsync(u => u.Role == UserRole.Lecturer && u.IsActive),
                    InactiveLecturers = await _context.Users.CountAsync(u => u.Role == UserRole.Lecturer && !u.IsActive),
                    TotalClaimsThisMonth = await _context.Claims.CountAsync(c => c.MonthYear == currentMonth),
                    ApprovedClaimsThisMonth = await _context.Claims.CountAsync(c => c.MonthYear == currentMonth && c.Status == ClaimStatus.Approved),
                    TotalPaymentsThisMonth = await _context.Claims
                        .Where(c => c.MonthYear == currentMonth && c.Status == ClaimStatus.Approved)
                        .SumAsync(c => c.TotalAmount),
                    RecentLecturers = await _context.Users
                        .Where(u => u.Role == UserRole.Lecturer)
                        .OrderByDescending(u => u.CreatedDate)
                        .Take(5)
                        .ToListAsync(),
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
                _logger.LogError(ex, "Error getting HR dashboard data");
                return new HRDashboardViewModel();
            }
        }

        // Get all lecturers
        public async Task<List<User>> GetAllLecturersAsync()
        {
            return await _context.Users
                .Where(u => u.Role == UserRole.Lecturer)
                .OrderBy(u => u.LastName)
                .ThenBy(u => u.FirstName)
                .ToListAsync();
        }

        // Get lecturer by ID
        public async Task<User> GetLecturerByIdAsync(int lecturerId)
        {
            return await _context.Users
                .FirstOrDefaultAsync(u => u.UserId == lecturerId && u.Role == UserRole.Lecturer);
        }

        // Create new lecturer
        public async Task<bool> CreateLecturerAsync(ManageLecturerViewModel model)
        {
            try
            {
                // Check if username or email already exists
                var existingUser = await _context.Users
                    .AnyAsync(u => u.Username == model.Username || u.Email == model.Email);

                if (existingUser)
                {
                    _logger.LogWarning($"Username or email already exists: {model.Username}, {model.Email}");
                    return false;
                }

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

        // Update existing lecturer
        public async Task<bool> UpdateLecturerAsync(ManageLecturerViewModel model)
        {
            try
            {
                var lecturer = await _context.Users.FindAsync(model.UserId);
                if (lecturer == null || lecturer.Role != UserRole.Lecturer)
                {
                    _logger.LogWarning($"Lecturer not found: {model.UserId}");
                    return false;
                }

                // Check if username/email conflict with other users
                var conflict = await _context.Users
                    .AnyAsync(u => u.UserId != model.UserId &&
                             (u.Username == model.Username || u.Email == model.Email));

                if (conflict)
                {
                    _logger.LogWarning($"Username or email conflict: {model.Username}, {model.Email}");
                    return false;
                }

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

        // Deactivate lecturer
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

        // Activate lecturer
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

        // Get lecturer payment summaries
        public async Task<List<LecturerPaymentSummary>> GetLecturerPaymentSummariesAsync(DateTime? startDate, DateTime? endDate)
        {
            try
            {
                var query = _context.Users
                    .Where(u => u.Role == UserRole.Lecturer)
                    .Include(u => u.Claims)
                    .AsQueryable();

                var summaries = await query.Select(u => new LecturerPaymentSummary
                {
                    Lecturer = u,
                    TotalClaims = u.Claims.Count,
                    ApprovedClaims = u.Claims.Count(c => c.Status == ClaimStatus.Approved),
                    TotalEarnings = u.Claims.Where(c => c.Status == ClaimStatus.Approved).Sum(c => c.TotalAmount),
                    LastClaimDate = u.Claims.OrderByDescending(c => c.SubmissionDate).Select(c => c.SubmissionDate).FirstOrDefault()
                }).ToListAsync();

                return summaries.OrderByDescending(s => s.TotalEarnings).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting payment summaries");
                return new List<LecturerPaymentSummary>();
            }
        }

        // Generate payment invoice
        public async Task<PaymentInvoiceViewModel> GeneratePaymentInvoiceAsync(int lecturerId, string period)
        {
            try
            {
                var lecturer = await _context.Users.FindAsync(lecturerId);
                if (lecturer == null)
                    return null;

                var claims = await _context.Claims
                    .Include(c => c.Documents)
                    .Where(c => c.LecturerId == lecturerId &&
                               c.MonthYear == period &&
                               c.Status == ClaimStatus.Approved)
                    .ToListAsync();

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

        // Get approved claims for reports
        public async Task<List<Claim>> GetApprovedClaimsForReportAsync(
            DateTime? startDate,
            DateTime? endDate,
            int? lecturerId,
            string department)
        {
            try
            {
                var query = _context.Claims
                    .Include(c => c.Lecturer)
                    .Include(c => c.Documents)
                    .Where(c => c.Status == ClaimStatus.Approved)
                    .AsQueryable();

                if (lecturerId.HasValue)
                    query = query.Where(c => c.LecturerId == lecturerId.Value);

                if (!string.IsNullOrEmpty(department))
                    query = query.Where(c => c.Lecturer.Department == department);

                if (startDate.HasValue)
                    query = query.Where(c => c.SubmissionDate >= startDate.Value);

                if (endDate.HasValue)
                    query = query.Where(c => c.SubmissionDate <= endDate.Value);

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