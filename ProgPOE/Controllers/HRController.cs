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

        // GET: HR/Dashboard
        public async Task<IActionResult> Dashboard()
        {
            try
            {
                // Check if user is HR
                if (!IsHRUser())
                {
                    TempData["Error"] = "Access denied. HR access only.";
                    return RedirectToAction("Index", "Home");
                }

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

        // GET: HR/ManageLecturers
        public async Task<IActionResult> ManageLecturers()
        {
            try
            {
                if (!IsHRUser())
                {
                    TempData["Error"] = "Access denied. HR access only.";
                    return RedirectToAction("Index", "Home");
                }

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

        // GET: HR/CreateLecturer
        public IActionResult CreateLecturer()
        {
            if (!IsHRUser())
            {
                TempData["Error"] = "Access denied. HR access only.";
                return RedirectToAction("Index", "Home");
            }

            var model = new ManageLecturerViewModel
            {
                IsActive = true,
                DefaultHourlyRate = 450.00m
            };

            ViewBag.CurrentUser = GetCurrentUserInfo();
            return View(model);
        }

        // POST: HR/CreateLecturer
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

                if (!ModelState.IsValid)
                {
                    ViewBag.CurrentUser = GetCurrentUserInfo();
                    return View(model);
                }

                var result = await _hrService.CreateLecturerAsync(model);

                if (result)
                {
                    TempData["Success"] = $"Lecturer {model.FirstName} {model.LastName} created successfully!";
                    return RedirectToAction("ManageLecturers");
                }
                else
                {
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

        // GET: HR/EditLecturer/5
        public async Task<IActionResult> EditLecturer(int id)
        {
            try
            {
                if (!IsHRUser())
                {
                    TempData["Error"] = "Access denied. HR access only.";
                    return RedirectToAction("Index", "Home");
                }

                var lecturer = await _hrService.GetLecturerByIdAsync(id);
                if (lecturer == null)
                {
                    TempData["Error"] = "Lecturer not found.";
                    return RedirectToAction("ManageLecturers");
                }

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

        // POST: HR/EditLecturer
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

                if (!ModelState.IsValid)
                {
                    ViewBag.CurrentUser = GetCurrentUserInfo();
                    return View(model);
                }

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

        // POST: HR/DeactivateLecturer
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

                var result = await _hrService.DeactivateLecturerAsync(id);

                if (result)
                {
                    TempData["Success"] = "Lecturer deactivated successfully.";
                }
                else
                {
                    TempData["Error"] = "Error deactivating lecturer.";
                }

                return RedirectToAction("ManageLecturers");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deactivating lecturer {id}");
                TempData["Error"] = "Error deactivating lecturer.";
                return RedirectToAction("ManageLecturers");
            }
        }

        // POST: HR/ActivateLecturer
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

                if (result)
                {
                    TempData["Success"] = "Lecturer activated successfully.";
                }
                else
                {
                    TempData["Error"] = "Error activating lecturer.";
                }

                return RedirectToAction("ManageLecturers");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error activating lecturer {id}");
                TempData["Error"] = "Error activating lecturer.";
                return RedirectToAction("ManageLecturers");
            }
        }

        // GET: HR/Reports
        public IActionResult Reports()
        {
            if (!IsHRUser())
            {
                TempData["Error"] = "Access denied. HR access only.";
                return RedirectToAction("Index", "Home");
            }

            var model = new GenerateReportViewModel
            {
                StartDate = DateTime.Now.AddMonths(-1),
                EndDate = DateTime.Now
            };

            ViewBag.CurrentUser = GetCurrentUserInfo();
            ViewBag.Lecturers = _context.Users
                .Where(u => u.Role == UserRole.Lecturer)
                .OrderBy(u => u.LastName)
                .Select(u => new { u.UserId, FullName = u.FirstName + " " + u.LastName })
                .ToList();

            ViewBag.Departments = _context.Users
                .Where(u => u.Role == UserRole.Lecturer && !string.IsNullOrEmpty(u.Department))
                .Select(u => u.Department)
                .Distinct()
                .OrderBy(d => d)
                .ToList();

            return View(model);
        }

        // POST: HR/GenerateReport
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

                switch (model.ReportType)
                {
                    case ReportType.ApprovedClaimsSummary:
                        return await GenerateApprovedClaimsSummary(model);

                    case ReportType.LecturerPaymentReport:
                        return await GenerateLecturerPaymentReport(model);

                    case ReportType.MonthlyClaimReport:
                        return await GenerateMonthlyClaimReport(model);

                    case ReportType.LecturerDirectory:
                        return await GenerateLecturerDirectory();

                    case ReportType.PaymentInvoice:
                        return await GeneratePaymentInvoice(model);

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

        // Generate Approved Claims Summary CSV
        private async Task<IActionResult> GenerateApprovedClaimsSummary(GenerateReportViewModel model)
        {
            var claims = await _hrService.GetApprovedClaimsForReportAsync(
                model.StartDate,
                model.EndDate,
                model.LecturerId,
                model.Department);

            var csv = new StringBuilder();
            csv.AppendLine("Claim ID,Lecturer,Department,Period,Hours,Rate,Total Amount,Submission Date,Approval Date");

            foreach (var claim in claims)
            {
                csv.AppendLine($"{claim.ClaimId}," +
                              $"\"{claim.Lecturer?.FullName}\"," +
                              $"\"{claim.Lecturer?.Department}\"," +
                              $"{claim.MonthYear}," +
                              $"{claim.HoursWorked}," +
                              $"{claim.HourlyRate:F2}," +
                              $"{claim.TotalAmount:F2}," +
                              $"{claim.SubmissionDate:yyyy-MM-dd}," +
                              $"{claim.ManagerApprovalDate:yyyy-MM-dd}");
            }

            var bytes = Encoding.UTF8.GetBytes(csv.ToString());
            var fileName = $"ApprovedClaims_{DateTime.Now:yyyyMMdd}.csv";

            return File(bytes, "text/csv", fileName);
        }

        // Generate Lecturer Payment Report CSV
        private async Task<IActionResult> GenerateLecturerPaymentReport(GenerateReportViewModel model)
        {
            var summaries = await _hrService.GetLecturerPaymentSummariesAsync(model.StartDate, model.EndDate);

            var csv = new StringBuilder();
            csv.AppendLine("Lecturer,Email,Department,Total Claims,Approved Claims,Total Earnings,Last Claim Date,Status");

            foreach (var summary in summaries)
            {
                csv.AppendLine($"\"{summary.Lecturer.FullName}\"," +
                              $"{summary.Lecturer.Email}," +
                              $"\"{summary.Lecturer.Department}\"," +
                              $"{summary.TotalClaims}," +
                              $"{summary.ApprovedClaims}," +
                              $"{summary.TotalEarnings:F2}," +
                              $"{summary.LastClaimDate:yyyy-MM-dd}," +
                              $"{(summary.Lecturer.IsActive ? "Active" : "Inactive")}");
            }

            var bytes = Encoding.UTF8.GetBytes(csv.ToString());
            var fileName = $"LecturerPayments_{DateTime.Now:yyyyMMdd}.csv";

            return File(bytes, "text/csv", fileName);
        }

        // Generate Monthly Claim Report
        private async Task<IActionResult> GenerateMonthlyClaimReport(GenerateReportViewModel model)
        {
            var claims = await _context.Claims
                .Include(c => c.Lecturer)
                .Where(c => c.Status == ClaimStatus.Approved)
                .ToListAsync();

            if (model.StartDate.HasValue)
                claims = claims.Where(c => c.SubmissionDate >= model.StartDate.Value).ToList();

            if (model.EndDate.HasValue)
                claims = claims.Where(c => c.SubmissionDate <= model.EndDate.Value).ToList();

            var groupedByMonth = claims
                .GroupBy(c => c.MonthYear)
                .OrderBy(g => g.Key);

            var csv = new StringBuilder();
            csv.AppendLine("Period,Total Claims,Total Hours,Total Amount Paid");

            foreach (var group in groupedByMonth)
            {
                csv.AppendLine($"{group.Key}," +
                              $"{group.Count()}," +
                              $"{group.Sum(c => c.HoursWorked)}," +
                              $"{group.Sum(c => c.TotalAmount):F2}");
            }

            var bytes = Encoding.UTF8.GetBytes(csv.ToString());
            var fileName = $"MonthlyReport_{DateTime.Now:yyyyMMdd}.csv";

            return File(bytes, "text/csv", fileName);
        }

        // Generate Lecturer Directory
        private async Task<IActionResult> GenerateLecturerDirectory()
        {
            var lecturers = await _hrService.GetAllLecturersAsync();

            var csv = new StringBuilder();
            csv.AppendLine("Lecturer ID,Full Name,Email,Department,Hourly Rate,Status,Created Date");

            foreach (var lecturer in lecturers)
            {
                csv.AppendLine($"{lecturer.UserId}," +
                              $"\"{lecturer.FullName}\"," +
                              $"{lecturer.Email}," +
                              $"\"{lecturer.Department}\"," +
                              $"{lecturer.DefaultHourlyRate:F2}," +
                              $"{(lecturer.IsActive ? "Active" : "Inactive")}," +
                              $"{lecturer.CreatedDate:yyyy-MM-dd}");
            }

            var bytes = Encoding.UTF8.GetBytes(csv.ToString());
            var fileName = $"LecturerDirectory_{DateTime.Now:yyyyMMdd}.csv";

            return File(bytes, "text/csv", fileName);
        }

        // Generate Payment Invoice
        private async Task<IActionResult> GeneratePaymentInvoice(GenerateReportViewModel model)
        {
            if (!model.LecturerId.HasValue)
            {
                TempData["Error"] = "Please select a lecturer for payment invoice.";
                return RedirectToAction("Reports");
            }

            var period = model.StartDate?.ToString("yyyy-MM") ?? DateTime.Now.ToString("yyyy-MM");
            var invoice = await _hrService.GeneratePaymentInvoiceAsync(model.LecturerId.Value, period);

            if (invoice == null)
            {
                TempData["Error"] = "No approved claims found for the selected lecturer and period.";
                return RedirectToAction("Reports");
            }

            return View("Invoice", invoice);
        }

        // Helper: Check if current user is HR
        private bool IsHRUser()
        {
            var roleString = HttpContext.Session.GetString("UserRole");
            return roleString == "HR";
        }

        // Helper: Get current user info
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