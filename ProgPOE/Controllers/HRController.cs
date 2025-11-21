using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProgPOE.Data;
using ProgPOE.Models;
using ProgPOE.Services;
using System.Text;

namespace ProgPOE.Controllers
{
    public class HRController : Controller
    {
        // Inject required services: HR Service, DB Context, and Logger
        private readonly IHRService _hrService;
        private readonly ApplicationDbContext _context;
        private readonly ILogger<HRController> _logger;

        public HRController(
            IHRService hrService,
            ApplicationDbContext context,
            ILogger<HRController> logger)
        {
            _hrService = hrService;
            _context = context;
            _logger = logger;
        }

        // HR Dashboard page
        public async Task<IActionResult> Dashboard()
        {
            try
            {
                // Only allow HR users
                if (!IsHRUser())
                {
                    TempData["Error"] = "Access denied. HR access only.";
                    return RedirectToAction("Index", "Home");
                }

                // Load dashboard data
                var dashboard = await _hrService.GetDashboardDataAsync();
                ViewBag.CurrentUser = GetCurrentUserInfo();

                return View(dashboard);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading HR dashboard");
                TempData["Error"] = "Error loading dashboard.";
                return RedirectToAction("Index", "Home");
            }
        }

        // List all lecturers
        public async Task<IActionResult> ManageLecturers()
        {
            try
            {
                if (!IsHRUser())
                {
                    TempData["Error"] = "Access denied. HR access only.";
                    return RedirectToAction("Index", "Home");
                }

                // Load lecturers from service
                var lecturers = await _hrService.GetAllLecturersAsync();
                ViewBag.CurrentUser = GetCurrentUserInfo();

                return View(lecturers);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading lecturers");
                TempData["Error"] = "Error loading lecturers.";
                return RedirectToAction("Dashboard");
            }
        }

        // Page to create a new lecturer
        public IActionResult CreateLecturer()
        {
            if (!IsHRUser())
            {
                TempData["Error"] = "Access denied. HR access only.";
                return RedirectToAction("Index", "Home");
            }

            // Default model values
            var model = new ManageLecturerViewModel
            {
                IsActive = true,
                DefaultHourlyRate = 450.00m
            };

            ViewBag.CurrentUser = GetCurrentUserInfo();
            return View(model);
        }

        // Save new lecturer
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateLecturer(ManageLecturerViewModel model)
        {
            try
            {
                if (!IsHRUser())
                {
                    TempData["Error"] = "Access denied. HR access only.";
                    return RedirectToAction("Index", "Home");
                }

                // Validate form
                if (!ModelState.IsValid)
                {
                    ViewBag.CurrentUser = GetCurrentUserInfo();
                    return View(model);
                }

                // Attempt to create lecturer
                var result = await _hrService.CreateLecturerAsync(model);

                if (result)
                {
                    TempData["Success"] = $"Lecturer {model.FirstName} {model.LastName} created successfully!";
                    return RedirectToAction("ManageLecturers");
                }
                else
                {
                    // Username/email conflict
                    TempData["Error"] = "Username or email already exists. Please use different credentials.";
                    ViewBag.CurrentUser = GetCurrentUserInfo();
                    return View(model);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating lecturer");
                TempData["Error"] = "Error creating lecturer.";
                ViewBag.CurrentUser = GetCurrentUserInfo();
                return View(model);
            }
        }

        // Load lecturer details for editing
        public async Task<IActionResult> EditLecturer(int id)
        {
            try
            {
                if (!IsHRUser())
                {
                    TempData["Error"] = "Access denied. HR access only.";
                    return RedirectToAction("Index", "Home");
                }

                // Get lecturer by ID
                var lecturer = await _hrService.GetLecturerByIdAsync(id);
                if (lecturer == null)
                {
                    TempData["Error"] = "Lecturer not found.";
                    return RedirectToAction("ManageLecturers");
                }

                // Fill ViewModel
                var model = new ManageLecturerViewModel
                {
                    UserId = lecturer.UserId,
                    Username = lecturer.Username,
                    Email = lecturer.Email,
                    FirstName = lecturer.FirstName,
                    LastName = lecturer.LastName,
                    Department = lecturer.Department,
                    DefaultHourlyRate = lecturer.DefaultHourlyRate ?? 450.00m,
                    IsActive = lecturer.IsActive
                };

                ViewBag.CurrentUser = GetCurrentUserInfo();
                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error loading lecturer {id}");
                TempData["Error"] = "Error loading lecturer.";
                return RedirectToAction("ManageLecturers");
            }
        }

        // Save lecturer updates
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditLecturer(ManageLecturerViewModel model)
        {
            try
            {
                if (!IsHRUser())
                {
                    TempData["Error"] = "Access denied. HR access only.";
                    return RedirectToAction("Index", "Home");
                }

                // Validate input
                if (!ModelState.IsValid)
                {
                    ViewBag.CurrentUser = GetCurrentUserInfo();
                    return View(model);
                }

                // Update lecturer via service
                var result = await _hrService.UpdateLecturerAsync(model);

                if (result)
                {
                    TempData["Success"] = $"Lecturer {model.FirstName} {model.LastName} updated successfully!";
                    return RedirectToAction("ManageLecturers");
                }
                else
                {
                    TempData["Error"] = "Error updating lecturer. Username or email may conflict with another user.";
                    ViewBag.CurrentUser = GetCurrentUserInfo();
                    return View(model);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating lecturer");
                TempData["Error"] = "Error updating lecturer.";
                ViewBag.CurrentUser = GetCurrentUserInfo();
                return View(model);
            }
        }

        // Deactivate lecturer
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeactivateLecturer(int id)
        {
            try
            {
                if (!IsHRUser())
                {
                    TempData["Error"] = "Access denied. HR access only.";
                    return RedirectToAction("Index", "Home");
                }

                // Deactivate lecturer
                var result = await _hrService.DeactivateLecturerAsync(id);

                TempData[result ? "Success" : "Error"] =
                    result ? "Lecturer deactivated successfully." : "Error deactivating lecturer.";

                return RedirectToAction("ManageLecturers");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deactivating lecturer {id}");
                TempData["Error"] = "Error deactivating lecturer.";
                return RedirectToAction("ManageLecturers");
            }
        }

        // Activate lecturer
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ActivateLecturer(int id)
        {
            try
            {
                if (!IsHRUser())
                {
                    TempData["Error"] = "Access denied. HR access only.";
                    return RedirectToAction("Index", "Home");
                }

                var result = await _hrService.ActivateLecturerAsync(id);

                TempData[result ? "Success" : "Error"] =
                    result ? "Lecturer activated successfully." : "Error activating lecturer.";

                return RedirectToAction("ManageLecturers");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error activating lecturer {id}");
                TempData["Error"] = "Error activating lecturer.";
                return RedirectToAction("ManageLecturers");
            }
        }

        // Load report filters page
        public IActionResult Reports()
        {
            if (!IsHRUser())
            {
                TempData["Error"] = "Access denied. HR access only.";
                return RedirectToAction("Index", "Home");
            }

            // Preload default dates
            var model = new GenerateReportViewModel
            {
                StartDate = DateTime.Now.AddMonths(-1),
                EndDate = DateTime.Now
            };

            ViewBag.CurrentUser = GetCurrentUserInfo();

            // Lecturer dropdown list
            ViewBag.Lecturers = _context.Users
                .Where(u => u.Role == UserRole.Lecturer)
                .OrderBy(u => u.LastName)
                .Select(u => new { u.UserId, FullName = u.FirstName + " " + u.LastName })
                .ToList();

            // Department dropdown
            ViewBag.Departments = _context.Users
                .Where(u => u.Role == UserRole.Lecturer && !string.IsNullOrEmpty(u.Department))
                .Select(u => u.Department)
                .Distinct()
                .OrderBy(d => d)
                .ToList();

            return View(model);
        }

        // Handle report generation request
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GenerateReport(GenerateReportViewModel model)
        {
            try
            {
                if (!IsHRUser())
                {
                    TempData["Error"] = "Access denied. HR access only.";
                    return RedirectToAction("Index", "Home");
                }

                // Log report request
                _logger.LogInformation($"Generating report: Type={model.ReportType}");

                // Redirect to the correct report generator
                switch (model.ReportType)
                {
                    case ReportType.ApprovedClaimsSummary:
                        return await GenerateApprovedClaimsSummaryReport(model);

                    case ReportType.LecturerPaymentReport:
                        return await GenerateLecturerPaymentReportFile(model);

                    case ReportType.MonthlyClaimReport:
                        return await GenerateMonthlyClaimReportFile(model);

                    case ReportType.LecturerDirectory:
                        return await GenerateLecturerDirectoryFile();

                    case ReportType.PaymentInvoice:
                        return await GeneratePaymentInvoicePage(model);

                    default:
                        TempData["Error"] = "Invalid report type.";
                        return RedirectToAction("Reports");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating report");
                TempData["Error"] = "Error generating report.";
                return RedirectToAction("Reports");
            }
        }

        // Generate Approved Claims Summary CSV file
        private async Task<IActionResult> GenerateApprovedClaimsSummaryReport(GenerateReportViewModel model)
        {
            // Get claims based on filters
            var claims = await _hrService.GetApprovedClaimsForReportAsync(
                model.StartDate,
                model.EndDate,
                model.LecturerId,
                model.Department);

            _logger.LogInformation($"Found {claims.Count} approved claims for report");

            if (!claims.Any())
            {
                TempData["Warning"] = "No approved claims found for the selected criteria.";
                return RedirectToAction("Reports");
            }

            // Build CSV
            var csv = new StringBuilder();
            csv.AppendLine("Claim ID,Lecturer,Department,Period,Hours,Rate,Total Amount,Submission Date,Approval Date");

            foreach (var claim in claims)
            {
                csv.AppendLine($"{claim.ClaimId}," +
                              $"\"{claim.Lecturer?.FullName ?? "Unknown"}\"," +
                              $"\"{claim.Lecturer?.Department ?? "N/A"}\"," +
                              $"{claim.MonthYear}," +
                              $"{claim.HoursWorked}," +
                              $"{claim.HourlyRate:F2}," +
                              $"{claim.TotalAmount:F2}," +
                              $"{claim.SubmissionDate:yyyy-MM-dd}," +
                              $"{(claim.ManagerApprovalDate.HasValue ? claim.ManagerApprovalDate.Value.ToString("yyyy-MM-dd") : "N/A")}");
            }

            var bytes = Encoding.UTF8.GetBytes(csv.ToString());
            var fileName = $"ApprovedClaims_{DateTime.Now:yyyyMMdd_HHmmss}.csv";

            return File(bytes, "text/csv", fileName);
        }

        // Generate Lecturer Payment Summary CSV
        private async Task<IActionResult> GenerateLecturerPaymentReportFile(GenerateReportViewModel model)
        {
            var summaries = await _hrService.GetLecturerPaymentSummariesAsync(model.StartDate, model.EndDate);

            _logger.LogInformation($"Found {summaries.Count} lecturer payment summaries");

            if (!summaries.Any())
            {
                TempData["Warning"] = "No lecturer payment data found.";
                return RedirectToAction("Reports");
            }

            // Create CSV structure
            var csv = new StringBuilder();
            csv.AppendLine("Lecturer,Email,Department,Total Claims,Approved Claims,Total Earnings,Last Claim Date,Status");

            foreach (var summary in summaries)
            {
                csv.AppendLine($"\"{summary.Lecturer.FullName}\"," +
                              $"{summary.Lecturer.Email}," +
                              $"\"{summary.Lecturer.Department ?? "N/A"}\"," +
                              $"{summary.TotalClaims}," +
                              $"{summary.ApprovedClaims}," +
                              $"{summary.TotalEarnings:F2}," +
                              $"{(summary.LastClaimDate.HasValue ? summary.LastClaimDate.Value.ToString("yyyy-MM-dd") : "Never")}," +
                              $"{(summary.Lecturer.IsActive ? "Active" : "Inactive")}");
            }

            var bytes = Encoding.UTF8.GetBytes(csv.ToString());
            var fileName = $"LecturerPayments_{DateTime.Now:yyyyMMdd_HHmmss}.csv";

            return File(bytes, "text/csv", fileName);
        }

        // Generate monthly grouped claims report CSV
        private async Task<IActionResult> GenerateMonthlyClaimReportFile(GenerateReportViewModel model)
        {
            // Load approved claims
            var claims = await _context.Claims
                .Include(c => c.Lecturer)
                .Where(c => c.Status == ClaimStatus.Approved)
                .ToListAsync();

            _logger.LogInformation($"Found {claims.Count} total approved claims");

            // Apply optional date filters
            if (model.StartDate.HasValue)
                claims = claims.Where(c => c.SubmissionDate >= model.StartDate.Value).ToList();

            if (model.EndDate.HasValue)
                claims = claims.Where(c => c.SubmissionDate <= model.EndDate.Value).ToList();

            _logger.LogInformation($"After date filtering: {claims.Count} claims");

            if (!claims.Any())
            {
                TempData["Warning"] = "No approved claims found for the selected date range.";
                return RedirectToAction("Reports");
            }

            // Group by month
            var groupedByMonth = claims
                .GroupBy(c => c.MonthYear)
                .OrderBy(g => g.Key);

            // Create CSV
            var csv = new StringBuilder();
            csv.AppendLine("Period,Total Claims,Total Hours,Total Amount Paid");

            foreach (var group in groupedByMonth)
            {
                csv.AppendLine($"{group.Key}," +
                              $"{group.Count()}," +
                              $"{group.Sum(c => c.HoursWorked):F1}," +
                              $"{group.Sum(c => c.TotalAmount):F2}");
            }

            var bytes = Encoding.UTF8.GetBytes(csv.ToString());
            var fileName = $"MonthlyReport_{DateTime.Now:yyyyMMdd_HHmmss}.csv";

            return File(bytes, "text/csv", fileName);
        }

        // Generate lecturer directory CSV
        private async Task<IActionResult> GenerateLecturerDirectoryFile()
        {
            var lecturers = await _hrService.GetAllLecturersAsync();

            _logger.LogInformation($"Found {lecturers.Count} lecturers for directory");

            if (!lecturers.Any())
            {
                TempData["Warning"] = "No lecturers found in the system.";
                return RedirectToAction("Reports");
            }

            var csv = new StringBuilder();
            csv.AppendLine("Lecturer ID,Full Name,Email,Department,Hourly Rate,Status,Created Date");

            foreach (var lecturer in lecturers)
            {
                csv.AppendLine($"{lecturer.UserId}," +
                              $"\"{lecturer.FullName}\"," +
                              $"{lecturer.Email}," +
                              $"\"{lecturer.Department ?? "N/A"}\"," +
                              $"{(lecturer.DefaultHourlyRate.HasValue ? lecturer.DefaultHourlyRate.Value.ToString("F2") : "N/A")}," +
                              $"{(lecturer.IsActive ? "Active" : "Inactive")}," +
                              $"{lecturer.CreatedDate:yyyy-MM-dd}");
            }

            var bytes = Encoding.UTF8.GetBytes(csv.ToString());
            var fileName = $"LecturerDirectory_{DateTime.Now:yyyyMMdd_HHmmss}.csv";

            return File(bytes, "text/csv", fileName);
        }

        // Generate and display a payment invoice HTML View
        private async Task<IActionResult> GeneratePaymentInvoicePage(GenerateReportViewModel model)
        {
            if (!model.LecturerId.HasValue)
            {
                TempData["Error"] = "Please select a lecturer for payment invoice.";
                return RedirectToAction("Reports");
            }

            // Use StartDate's month OR use current month
            var period = model.StartDate?.ToString("yyyy-MM") ?? DateTime.Now.ToString("yyyy-MM");

            _logger.LogInformation($"Generating invoice for Lecturer {model.LecturerId}, Period {period}");

            var invoice = await _hrService.GeneratePaymentInvoiceAsync(model.LecturerId.Value, period);

            if (invoice == null || !invoice.Claims.Any())
            {
                TempData["Warning"] = $"No approved claims found for the selected lecturer in period {period}.";
                return RedirectToAction("Reports");
            }

            _logger.LogInformation($"Invoice generated with {invoice.Claims.Count} claims, Total: R{invoice.TotalAmount}");

            return View("Invoice", invoice);
        }

        // Check if logged-in user is HR
        private bool IsHRUser()
        {
            var roleString = HttpContext.Session.GetString("UserRole");
            return roleString == "HR";
        }

        // Retrieve current logged-in user's information from session
        private object GetCurrentUserInfo()
        {
            var userId = HttpContext.Session.GetInt32("UserId") ?? 4;
            var userName = HttpContext.Session.GetString("UserName") ?? "Sarah Adams";
            var userRole = HttpContext.Session.GetString("UserRole") ?? "HR";

            return new
            {
                UserId = userId,
                UserName = userName,
                UserRole = userRole
            };
        }
    }
}
