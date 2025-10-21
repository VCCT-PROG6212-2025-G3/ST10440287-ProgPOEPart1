using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProgPOE.Models;
using ProgPOE.Services;
using ProgPOE.Data;
using System.Diagnostics;

namespace ProgPOE.Controllers
{
    public class HomeController : Controller
    {
        private readonly IClaimService _claimService;
        private readonly IFileService _fileService;
        private readonly ILogger<HomeController> _logger;
        private readonly ApplicationDbContext _context;

        public HomeController(
            IClaimService claimService,
            IFileService fileService,
            ILogger<HomeController> logger,
            ApplicationDbContext context)
        {
            _claimService = claimService;
            _fileService = fileService;
            _logger = logger;
            _context = context;
        }

        // GET: Home/Index - Landing page
        public IActionResult Index()
        {
            // Set default session for lecturer
            HttpContext.Session.SetInt32("UserId", 1);
            HttpContext.Session.SetString("UserRole", "Lecturer");
            HttpContext.Session.SetString("UserName", "Dr. John Smith");

            ViewBag.UserRole = HttpContext.Session.GetString("UserRole");
            ViewBag.UserName = HttpContext.Session.GetString("UserName");

            return View();
        }

        // GET: Home/Dashboard
        public async Task<IActionResult> Dashboard()
        {
            var userId = HttpContext.Session.GetInt32("UserId") ?? 1;
            ViewBag.UserRole = HttpContext.Session.GetString("UserRole") ?? "Lecturer";
            ViewBag.UserName = HttpContext.Session.GetString("UserName") ?? "Dr. John Smith";

            try
            {
                var model = await _claimService.GetDashboardDataAsync(userId);
                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading dashboard");
                TempData["ErrorMessage"] = "❌ Error loading dashboard data.";
                return View(new DashboardViewModel());
            }
        }

        // GET: Home/SubmitClaim
        public IActionResult SubmitClaim()
        {
            ViewBag.UserRole = HttpContext.Session.GetString("UserRole") ?? "Lecturer";
            ViewBag.UserName = HttpContext.Session.GetString("UserName") ?? "Dr. John Smith";

            var model = new SubmitClaimViewModel
            {
                MonthYear = DateTime.Now.AddMonths(-1).ToString("yyyy-MM"),
                HourlyRate = 450.00m
            };

            return View(model);
        }

        // POST: Home/SubmitClaim
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubmitClaim(SubmitClaimViewModel model)
        {
            ViewBag.UserRole = HttpContext.Session.GetString("UserRole") ?? "Lecturer";
            ViewBag.UserName = HttpContext.Session.GetString("UserName") ?? "Dr. John Smith";

            _logger.LogInformation($"SubmitClaim: MonthYear={model.MonthYear}, Hours={model.HoursWorked}, Rate={model.HourlyRate}");

            if (!ModelState.IsValid)
            {
                _logger.LogWarning("ModelState is invalid");
                TempData["ErrorMessage"] = "❌ Please correct the errors in the form.";
                return View(model);
            }

            var userId = HttpContext.Session.GetInt32("UserId") ?? 1;

            try
            {
                // Verify user exists
                var user = await _context.Users.FindAsync(userId);
                if (user == null)
                {
                    _logger.LogError($"User {userId} not found in database");
                    TempData["ErrorMessage"] = "❌ User not found. Please log in again.";
                    return View(model);
                }

                _logger.LogInformation($"User found: {user.FullName}");

                // Check if claim already exists
                var existingClaim = await _context.Claims
                    .FirstOrDefaultAsync(c => c.LecturerId == userId && c.MonthYear == model.MonthYear);

                if (existingClaim != null)
                {
                    _logger.LogWarning($"Duplicate claim attempt for period {model.MonthYear}");
                    TempData["ErrorMessage"] = $"❌ A claim for {model.MonthYear} already exists! Please choose a different period.";
                    return View(model);
                }

                // Create new claim
                var claim = new Claim
                {
                    LecturerId = userId,
                    MonthYear = model.MonthYear,
                    HoursWorked = model.HoursWorked,
                    HourlyRate = model.HourlyRate,
                    Status = ClaimStatus.Pending,
                    SubmissionDate = DateTime.Now,
                    LecturerNotes = model.Notes
                };

                _logger.LogInformation("Adding claim to context");
                _context.Claims.Add(claim);

                _logger.LogInformation("Saving changes to database");
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Claim created successfully with ID: {claim.ClaimId}");

                // Handle file uploads
                var files = Request.Form.Files;
                if (files != null && files.Count > 0)
                {
                    _logger.LogInformation($"Uploading {files.Count} files");
                    var uploadResult = await _fileService.UploadDocumentsAsync(claim.ClaimId, files.ToList());

                    if (!uploadResult)
                    {
                        _logger.LogWarning("File upload failed but claim was saved");
                    }
                }

                TempData["SuccessMessage"] = $"✅ Claim submitted successfully! Your claim for {model.MonthYear} totaling R {claim.TotalAmount:N2} is now pending coordinator review.";
                return RedirectToAction("MyClaims");
            }
            catch (DbUpdateException dbEx)
            {
                _logger.LogError(dbEx, "Database update error");
                _logger.LogError($"Inner Exception: {dbEx.InnerException?.Message}");
                TempData["ErrorMessage"] = $"❌ Database error: {dbEx.InnerException?.Message ?? dbEx.Message}";
                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error submitting claim");
                _logger.LogError($"Inner Exception: {ex.InnerException?.Message}");
                TempData["ErrorMessage"] = $"❌ Error: {ex.InnerException?.Message ?? ex.Message}";
                return View(model);
            }
        }

        // GET: Home/MyClaims
        public async Task<IActionResult> MyClaims()
        {
            ViewBag.UserRole = HttpContext.Session.GetString("UserRole") ?? "Lecturer";
            ViewBag.UserName = HttpContext.Session.GetString("UserName") ?? "Dr. John Smith";

            var userId = HttpContext.Session.GetInt32("UserId") ?? 1;

            _logger.LogInformation($"MyClaims - Getting claims for UserId: {userId}");

            try
            {
                var claims = await _context.Claims
                    .Include(c => c.Lecturer)
                    .Include(c => c.Documents)
                    .Where(c => c.LecturerId == userId)
                    .OrderByDescending(c => c.SubmissionDate)
                    .ToListAsync();

                _logger.LogInformation($"MyClaims - Found {claims.Count} claims");

                // Log each claim for debugging
                foreach (var claim in claims)
                {
                    _logger.LogInformation($"Claim: ID={claim.ClaimId}, Period={claim.MonthYear}, Amount=R{claim.TotalAmount}, Status={claim.Status}");
                }

                return View(claims);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving claims");
                TempData["ErrorMessage"] = "❌ Error loading your claims.";
                return View(new List<Claim>());
            }
        }

        // GET: Home/ApproveClaims - Claims approval interface
        public async Task<IActionResult> ApproveClaims()
        {
            // Get current user role
            var userRole = HttpContext.Session.GetString("UserRole");
            var userId = HttpContext.Session.GetInt32("UserId");

            // Determine if user is coordinator or manager
            bool isCoordinator = userRole == "ProgrammeCoordinator";
            bool isManager = userRole == "AcademicManager";

            // If neither, set to coordinator by default
            if (!isCoordinator && !isManager)
            {
                HttpContext.Session.SetInt32("UserId", 2);
                HttpContext.Session.SetString("UserRole", "ProgrammeCoordinator");
                ViewBag.UserRole = "Programme Coordinator";
                ViewBag.UserName = "Dr. Jane Wilson";
                isCoordinator = true;
            }
            else
            {
                ViewBag.UserRole = isCoordinator ? "Programme Coordinator" : "Academic Manager";
                ViewBag.UserName = HttpContext.Session.GetString("UserName") ??
                    (isCoordinator ? "Dr. Jane Wilson" : "Prof. Mike Johnson");
            }

            _logger.LogInformation($"ApproveClaims - Loading claims for role: {ViewBag.UserRole}");

            try
            {
                List<Claim> claims;

                if (isCoordinator)
                {
                    // Coordinators see only claims with Pending status
                    claims = await _context.Claims
                        .Include(c => c.Lecturer)
                        .Include(c => c.Documents)
                        .Where(c => c.Status == ClaimStatus.Pending)
                        .OrderBy(c => c.SubmissionDate)
                        .ToListAsync();

                    _logger.LogInformation($"ApproveClaims - Found {claims.Count} pending claims for coordinator");
                }
                else
                {
                    // Managers see only claims with PendingManager status
                    claims = await _context.Claims
                        .Include(c => c.Lecturer)
                        .Include(c => c.Documents)
                        .Where(c => c.Status == ClaimStatus.PendingManager)
                        .OrderBy(c => c.SubmissionDate)
                        .ToListAsync();

                    _logger.LogInformation($"ApproveClaims - Found {claims.Count} pending claims for manager");
                }

                // Pass the role to the view
                ViewBag.IsCoordinator = isCoordinator;
                ViewBag.IsManager = isManager;

                return View(claims);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading pending claims");
                TempData["ErrorMessage"] = "❌ Error loading pending claims.";
                return View(new List<Claim>());
            }
        }

        // POST: Home/ProcessApproval - Handle approval action
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ProcessApproval(int claimId, string action, string comments)
        {
            var approverId = HttpContext.Session.GetInt32("UserId") ?? 2;

            _logger.LogInformation($"ProcessApproval - ClaimId: {claimId}, Action: {action}, ApproverId: {approverId}");

            // Validate action
            if (!Enum.TryParse<ApprovalAction>(action, true, out var approvalAction))
            {
                _logger.LogWarning($"Invalid approval action: {action}");
                TempData["ErrorMessage"] = "❌ Invalid action specified.";
                return RedirectToAction("ApproveClaims");
            }

            // Validate rejection/return comments
            if ((approvalAction == ApprovalAction.Reject || approvalAction == ApprovalAction.Return) &&
                string.IsNullOrWhiteSpace(comments))
            {
                TempData["ErrorMessage"] = "❌ Comments are required when rejecting or returning a claim.";
                return RedirectToAction("ApproveClaims");
            }

            try
            {
                var claim = await _context.Claims
                    .Include(c => c.Lecturer)
                    .FirstOrDefaultAsync(c => c.ClaimId == claimId);

                if (claim == null)
                {
                    _logger.LogWarning($"Claim {claimId} not found");
                    TempData["ErrorMessage"] = "❌ Claim not found.";
                    return RedirectToAction("ApproveClaims");
                }

                var approver = await _context.Users.FindAsync(approverId);
                if (approver == null)
                {
                    _logger.LogWarning($"Approver {approverId} not found");
                    TempData["ErrorMessage"] = "❌ Approver not found.";
                    return RedirectToAction("ApproveClaims");
                }

                var lecturerName = claim.Lecturer?.FullName ?? "Unknown Lecturer";

                // Process the approval
                switch (approvalAction)
                {
                    case ApprovalAction.Approve:
                        if (approver.Role == UserRole.ProgrammeCoordinator)
                        {
                            claim.Status = ClaimStatus.PendingManager;
                            claim.CoordinatorApprovalDate = DateTime.Now;
                            claim.CoordinatorNotes = comments ?? "Approved by coordinator - forwarded to manager";
                            _logger.LogInformation($"Claim {claimId} approved by coordinator, now pending manager");

                            TempData["SuccessMessage"] = $"✅ Claim CL-{claimId:D6} for {lecturerName} approved and forwarded to Academic Manager for final review!";
                        }
                        else if (approver.Role == UserRole.AcademicManager)
                        {
                            claim.Status = ClaimStatus.Approved;
                            claim.ManagerApprovalDate = DateTime.Now;
                            claim.ManagerNotes = comments ?? "Final approval granted";
                            _logger.LogInformation($"Claim {claimId} fully approved by manager");

                            TempData["SuccessMessage"] = $"✅ Claim CL-{claimId:D6} for {lecturerName} has been fully approved! Payment will be processed.";
                        }
                        break;

                    case ApprovalAction.Reject:
                        claim.Status = ClaimStatus.Rejected;
                        if (approver.Role == UserRole.ProgrammeCoordinator)
                        {
                            claim.CoordinatorNotes = comments;
                        }
                        else
                        {
                            claim.ManagerNotes = comments;
                        }
                        _logger.LogInformation($"Claim {claimId} rejected by {approver.Role}");

                        TempData["SuccessMessage"] = $"❌ Claim CL-{claimId:D6} for {lecturerName} has been rejected. The lecturer will be notified.";
                        break;

                    case ApprovalAction.Return:
                        claim.Status = ClaimStatus.Returned;
                        if (approver.Role == UserRole.ProgrammeCoordinator)
                        {
                            claim.CoordinatorNotes = comments;
                        }
                        else
                        {
                            claim.ManagerNotes = comments;
                        }
                        _logger.LogInformation($"Claim {claimId} returned for revision by {approver.Role}");

                        TempData["SuccessMessage"] = $"↩️ Claim CL-{claimId:D6} for {lecturerName} has been returned for revision. The lecturer can resubmit.";
                        break;
                }

                await _context.SaveChangesAsync();

                _logger.LogInformation($"Claim {claimId} successfully processed");

                return RedirectToAction("ApproveClaims");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error processing approval for claim {claimId}");
                TempData["ErrorMessage"] = $"❌ An error occurred while processing the approval: {ex.Message}";
                return RedirectToAction("ApproveClaims");
            }
        }

        // GET: Home/ViewClaim - View specific claim details
        public async Task<IActionResult> ViewClaim(int id)
        {
            ViewBag.UserRole = HttpContext.Session.GetString("UserRole") ?? "Lecturer";
            ViewBag.UserName = HttpContext.Session.GetString("UserName") ?? "Dr. John Smith";

            try
            {
                var claim = await _context.Claims
                    .Include(c => c.Lecturer)
                    .Include(c => c.Documents)
                    .FirstOrDefaultAsync(c => c.ClaimId == id);

                if (claim == null)
                {
                    _logger.LogWarning($"Claim {id} not found");
                    TempData["ErrorMessage"] = "❌ Claim not found.";
                    return RedirectToAction("MyClaims");
                }

                return View(claim);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error loading claim {id}");
                TempData["ErrorMessage"] = "❌ Error loading claim details.";
                return RedirectToAction("MyClaims");
            }
        }

        // GET: Home/SwitchRole - Switch between user roles (for testing)
        public IActionResult SwitchRole(string role)
        {
            switch (role?.ToLower())
            {
                case "lecturer":
                    HttpContext.Session.SetInt32("UserId", 1);
                    HttpContext.Session.SetString("UserRole", "Lecturer");
                    HttpContext.Session.SetString("UserName", "Dr. John Smith");
                    TempData["SuccessMessage"] = "✅ Switched to Lecturer role";
                    return RedirectToAction("Dashboard");

                case "coordinator":
                    HttpContext.Session.SetInt32("UserId", 2);
                    HttpContext.Session.SetString("UserRole", "ProgrammeCoordinator");
                    HttpContext.Session.SetString("UserName", "Dr. Jane Wilson");
                    TempData["SuccessMessage"] = "✅ Switched to Programme Coordinator role";
                    return RedirectToAction("ApproveClaims");

                case "manager":
                    HttpContext.Session.SetInt32("UserId", 3);
                    HttpContext.Session.SetString("UserRole", "AcademicManager");
                    HttpContext.Session.SetString("UserName", "Prof. Mike Johnson");
                    TempData["SuccessMessage"] = "✅ Switched to Academic Manager role";
                    return RedirectToAction("ApproveClaims");

                default:
                    TempData["ErrorMessage"] = "❌ Invalid role specified";
                    return RedirectToAction("Index");
            }
        }

        // Debug endpoints - Remove in production
        #region Debug Endpoints

        [HttpGet]
        public async Task<IActionResult> ResetDatabase()
        {
            try
            {
                _logger.LogInformation("Resetting database - removing all claims and documents");

                var claims = await _context.Claims.ToListAsync();
                _context.Claims.RemoveRange(claims);

                var documents = await _context.SupportingDocuments.ToListAsync();
                _context.SupportingDocuments.RemoveRange(documents);

                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "✅ Database reset successfully! All claims and documents removed.";
                _logger.LogInformation("Database reset completed");

                return RedirectToAction("Dashboard");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resetting database");
                TempData["ErrorMessage"] = $"❌ Error resetting database: {ex.Message}";
                return RedirectToAction("Dashboard");
            }
        }

        [HttpGet]
        public async Task<IActionResult> ViewAllClaims()
        {
            try
            {
                var allClaims = await _context.Claims
                    .Include(c => c.Lecturer)
                    .OrderByDescending(c => c.SubmissionDate)
                    .ToListAsync();

                var details = string.Join("\n", allClaims.Select(c =>
                    $"ID: {c.ClaimId} | User: {c.LecturerId} ({c.Lecturer?.FullName}) | Period: {c.MonthYear} | Amount: R{c.TotalAmount:N2} | Status: {c.Status}"));

                return Content($"=== ALL CLAIMS IN DATABASE ===\n\nTotal Claims: {allClaims.Count}\n\n{details}", "text/plain");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error viewing all claims");
                return Content($"Error: {ex.Message}", "text/plain");
            }
        }

        [HttpGet]
        public async Task<IActionResult> CheckDatabase()
        {
            try
            {
                var userId = HttpContext.Session.GetInt32("UserId") ?? 1;

                var allClaims = await _context.Claims.ToListAsync();
                var userClaims = await _context.Claims.Where(c => c.LecturerId == userId).ToListAsync();
                var users = await _context.Users.ToListAsync();

                var result = $"=== DATABASE CHECK ===\n\n";
                result += $"Session UserId: {userId}\n";
                result += $"Session UserRole: {HttpContext.Session.GetString("UserRole")}\n";
                result += $"Session UserName: {HttpContext.Session.GetString("UserName")}\n\n";

                result += $"Total Users in DB: {users.Count}\n";
                foreach (var u in users)
                {
                    result += $"  - User {u.UserId}: {u.FirstName} {u.LastName} ({u.Role})\n";
                }

                result += $"\nTotal Claims in DB: {allClaims.Count}\n";
                foreach (var c in allClaims)
                {
                    result += $"  - Claim {c.ClaimId}: User={c.LecturerId}, Period={c.MonthYear}, Amount=R{c.TotalAmount:N2}, Status={c.Status}\n";
                }

                result += $"\nClaims for Current User (UserId {userId}): {userClaims.Count}\n";
                foreach (var c in userClaims)
                {
                    result += $"  - Claim {c.ClaimId}: Period={c.MonthYear}, Amount=R{c.TotalAmount:N2}, Status={c.Status}\n";
                }

                var pendingClaims = allClaims.Where(c => c.Status == ClaimStatus.Pending).ToList();
                result += $"\nPending Claims (Coordinator): {pendingClaims.Count}\n";
                foreach (var c in pendingClaims)
                {
                    result += $"  - Claim {c.ClaimId}: Period={c.MonthYear}, Amount=R{c.TotalAmount:N2}\n";
                }

                var pendingManagerClaims = allClaims.Where(c => c.Status == ClaimStatus.PendingManager).ToList();
                result += $"\nPending Manager Claims: {pendingManagerClaims.Count}\n";
                foreach (var c in pendingManagerClaims)
                {
                    result += $"  - Claim {c.ClaimId}: Period={c.MonthYear}, Amount=R{c.TotalAmount:N2}\n";
                }

                var approvedClaims = allClaims.Where(c => c.Status == ClaimStatus.Approved).ToList();
                result += $"\nApproved Claims: {approvedClaims.Count}\n";
                foreach (var c in approvedClaims)
                {
                    result += $"  - Claim {c.ClaimId}: Period={c.MonthYear}, Amount=R{c.TotalAmount:N2}\n";
                }

                return Content(result, "text/plain");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking database");
                return Content($"Error: {ex.Message}\n\nStack Trace: {ex.StackTrace}", "text/plain");
            }
        }

        #endregion

        // Error handler
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}