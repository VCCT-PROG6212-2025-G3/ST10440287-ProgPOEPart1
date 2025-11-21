using Microsoft.AspNetCore.Mvc;
using ProgPOE.Models;
using ProgPOE.Services;
using ProgPOE.Data;
using Microsoft.EntityFrameworkCore;

namespace ProgPOE.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly IClaimService _claimService;
        private readonly IFileService _fileService;
        private readonly IClaimValidationService _validationService;
        private readonly IApprovalWorkflowService _workflowService;
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _environment;

        // Constructor: inject dependencies
        public HomeController(
            ILogger<HomeController> logger,
            IClaimService claimService,
            IFileService fileService,
            IClaimValidationService validationService,
            IApprovalWorkflowService workflowService,
            ApplicationDbContext context,
            IWebHostEnvironment environment)
        {
            _logger = logger;
            _claimService = claimService;
            _fileService = fileService;
            _validationService = validationService;
            _workflowService = workflowService;
            _context = context;
            _environment = environment;
        }

        // GET: Home/Index
        public IActionResult Index()
        {
            // Initialize session if not set
            if (!HttpContext.Session.Keys.Contains("UserId"))
            {
                // Default to lecturer (John Smith)
                HttpContext.Session.SetInt32("UserId", 1);
                HttpContext.Session.SetString("UserRole", "Lecturer");
                HttpContext.Session.SetString("UserName", "John Smith");
            }

            // Pass current user info to the view
            ViewBag.CurrentUser = GetCurrentUserInfo();
            return View();
        }

        // GET: Home/SwitchRole
        public async Task<IActionResult> SwitchRole(int userId)
        {
            try
            {
                // Find the user in the database
                var user = await _context.Users.FindAsync(userId);
                if (user == null)
                {
                    TempData["Error"] = "User not found.";
                    return RedirectToAction("Index");
                }

                // Update session variables
                HttpContext.Session.SetInt32("UserId", user.UserId);
                HttpContext.Session.SetString("UserRole", user.Role.ToString());
                HttpContext.Session.SetString("UserName", user.FullName);

                TempData["Success"] = $"Switched to {user.FullName} ({user.Role})";
                _logger.LogInformation($"Role switched to {user.FullName} ({user.Role})");

                // Redirect to Dashboard
                return RedirectToAction("Dashboard");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error switching role");
                TempData["Error"] = "Error switching role.";
                return RedirectToAction("Index");
            }
        }

        // GET: Home/Dashboard
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public async Task<IActionResult> Dashboard()
        {
            try
            {
                int userId = GetCurrentUserId();
                var userRole = GetCurrentUserRole();

                // ADD THIS: Redirect HR users to HR Dashboard
                if (userRole == UserRole.HR)
                {
                    return RedirectToAction("Dashboard", "HR");
                }

                // Get dashboard data from claim service
                var dashboardData = await _claimService.GetDashboardDataAsync(userId);
                ViewBag.CurrentUser = GetCurrentUserInfo();

                _logger.LogInformation($"Dashboard loaded for user {userId}: {dashboardData.TotalClaims} total claims");

                return View(dashboardData);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading dashboard");
                TempData["Error"] = "Error loading dashboard data.";
                return RedirectToAction("Index");
            }
        }

        // GET: Home/SubmitClaim
        public IActionResult SubmitClaim()
        {
            // Prepare model with default values
            var model = new SubmitClaimViewModel
            {
                MonthYear = DateTime.Now.ToString("yyyy-MM"),
                HourlyRate = GetUserDefaultRate()
            };

            ViewBag.CurrentUser = GetCurrentUserInfo();
            return View(model);
        }

        // POST: Home/SubmitClaim
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubmitClaim(SubmitClaimViewModel model)
        {
            try
            {
                // Validate model
                if (!ModelState.IsValid)
                {
                    TempData["Error"] = "Please correct the errors in the form.";
                    ViewBag.CurrentUser = GetCurrentUserInfo();
                    return View(model);
                }

                int userId = GetCurrentUserId();

                // Create new claim entity
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

                // Run automated validation BEFORE saving
                var validation = _validationService.AutoVerifyClaim(claim);

                if (!validation.IsValid)
                {
                    TempData["Error"] = "Claim validation failed: " + string.Join(", ", validation.Errors);
                    ViewBag.CurrentUser = GetCurrentUserInfo();
                    return View(model);
                }

                // Save claim to database
                _context.Claims.Add(claim);
                await _context.SaveChangesAsync();

                // Show validation warnings if any
                if (validation.Warnings.Any())
                {
                    TempData["Warning"] = "Claim submitted with warnings: " + string.Join(", ", validation.Warnings);
                }

                // Handle optional file uploads
                if (model.Documents != null && model.Documents.Any())
                {
                    var uploadPath = Path.Combine(_environment.ContentRootPath, "uploads");

                    var uploadResult = await _fileService.UploadFilesAsync(
                        claim.ClaimId,
                        model.Documents,
                        uploadPath);

                    if (!uploadResult.Success)
                    {
                        _logger.LogWarning($"File upload issues: {uploadResult.Message}");
                        TempData["Warning"] = $"Claim submitted but some files failed to upload: {uploadResult.Message}";
                    }
                    else
                    {
                        TempData["Success"] = $"Claim submitted successfully with {uploadResult.Documents.Count} document(s)! Risk Score: {validation.RiskScore}/100";
                    }
                }
                else
                {
                    TempData["Success"] = $"Claim submitted successfully! Risk Score: {validation.RiskScore}/100";
                }

                _logger.LogInformation($"Claim {claim.ClaimId} submitted by user {userId} with risk score {validation.RiskScore}");
                return RedirectToAction("MyClaims");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error submitting claim");
                TempData["Error"] = "An error occurred while submitting your claim. Please try again.";
                ViewBag.CurrentUser = GetCurrentUserInfo();
                return View(model);
            }
        }

        // GET: Home/MyClaims
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public async Task<IActionResult> MyClaims()
        {
            try
            {
                int userId = GetCurrentUserId();
                var claims = await _claimService.GetUserClaimsAsync(userId);
                ViewBag.CurrentUser = GetCurrentUserInfo();

                _logger.LogInformation($"My Claims loaded for user {userId}: {claims.Count} claims found");

                return View(claims);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading claims");
                TempData["Error"] = "Error loading your claims.";
                return RedirectToAction("Dashboard");
            }
        }

        // GET: Home/ViewClaim/5
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public async Task<IActionResult> ViewClaim(int id)
        {
            try
            {
                var claim = await _claimService.GetClaimByIdAsync(id);

                if (claim == null)
                {
                    TempData["Error"] = "Claim not found.";
                    return RedirectToAction("MyClaims");
                }

                // Authorization check: lecturers can view only their own claims
                int userId = GetCurrentUserId();
                var userRole = GetCurrentUserRole();

                if (claim.LecturerId != userId && userRole == UserRole.Lecturer)
                {
                    TempData["Error"] = "You are not authorized to view this claim.";
                    return RedirectToAction("MyClaims");
                }

                // Run validation for display
                var validation = _validationService.ValidateClaim(claim);
                ViewBag.Validation = validation;
                ViewBag.CurrentUser = GetCurrentUserInfo();

                _logger.LogInformation($"Viewing Claim {id}: Status = {claim.Status}, RiskScore = {validation.RiskScore}");

                return View(claim);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error viewing claim {id}");
                TempData["Error"] = "Error loading claim details.";
                return RedirectToAction("MyClaims");
            }
        }

        // GET: Home/ApproveClaims
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public async Task<IActionResult> ApproveClaims()
        {
            try
            {
                var userRole = GetCurrentUserRole();
                var userId = GetCurrentUserId();

                // Lecturers cannot approve claims
                if (userRole == UserRole.Lecturer)
                {
                    TempData["Error"] = "You don't have permission to approve claims.";
                    return RedirectToAction("Dashboard");
                }

                // Get claims for this approver using workflow service
                var pendingClaims = await _workflowService.GetClaimsForApproverAsync(userId, userRole);

                // Run validation on each claim for display
                var claimsWithValidation = new List<(Claim Claim, ValidationResult Validation)>();
                foreach (var claim in pendingClaims)
                {
                    var validation = _validationService.ValidateClaim(claim);
                    claimsWithValidation.Add((claim, validation));
                }

                ViewBag.ClaimsWithValidation = claimsWithValidation;
                ViewBag.CurrentUser = GetCurrentUserInfo();

                _logger.LogInformation($"Approve Claims loaded for {userRole}: {pendingClaims.Count} claims to review");

                return View(pendingClaims);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading pending claims");
                TempData["Error"] = "Error loading pending claims.";
                return RedirectToAction("Dashboard");
            }
        }

        // POST: Home/ProcessApproval - NOW USES WORKFLOW SERVICE
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ProcessApproval(int claimId, ApprovalAction action, string comments)
        {
            try
            {
                int approverId = GetCurrentUserId();
                var userRole = GetCurrentUserRole();

                // Lecturers cannot approve claims
                if (userRole == UserRole.Lecturer)
                {
                    TempData["Error"] = "You don't have permission to approve claims.";
                    return RedirectToAction("Dashboard");
                }

                _logger.LogInformation($"Processing approval: ClaimId={claimId}, Action={action}, Approver={approverId}");

                // Process through automated workflow
                var decision = await _workflowService.ProcessWorkflowAsync(claimId, action, approverId, comments);

                if (decision.Success)
                {
                    TempData["Success"] = decision.Message;

                    // Show notifications
                    if (decision.Notifications.Any())
                    {
                        TempData["Info"] = string.Join("<br>", decision.Notifications);
                    }

                    _logger.LogInformation($"Workflow success: Claim {claimId} -> {decision.NewStatus}");
                }
                else
                {
                    TempData["Error"] = decision.Message;
                }

                return RedirectToAction("ApproveClaims");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error processing approval for claim {claimId}");
                TempData["Error"] = "An error occurred while processing the approval.";
                return RedirectToAction("ApproveClaims");
            }
        }

        // GET: Home/DownloadDocument/5
        public async Task<IActionResult> DownloadDocument(int id)
        {
            try
            {
                // Download file via service
                var result = await _fileService.DownloadFileAsync(id);

                if (!result.Success)
                {
                    TempData["Error"] = "Document not found.";
                    return RedirectToAction("MyClaims");
                }

                // Return file to user
                return File(result.FileData, result.ContentType, result.FileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error downloading document {id}");
                TempData["Error"] = "Error downloading document.";
                return RedirectToAction("MyClaims");
            }
        }

        // POST: Home/DeleteDocument
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteDocument(int documentId, int claimId)
        {
            try
            {
                var claim = await _claimService.GetClaimByIdAsync(claimId);

                // Only allow deletion from pending claims
                if (claim == null || claim.Status != ClaimStatus.Pending)
                {
                    TempData["Error"] = "Cannot delete documents from this claim.";
                    return RedirectToAction("ViewClaim", new { id = claimId });
                }

                var result = await _fileService.DeleteFileAsync(documentId);

                if (result)
                {
                    TempData["Success"] = "Document deleted successfully.";
                }
                else
                {
                    TempData["Error"] = "Error deleting document.";
                }

                return RedirectToAction("ViewClaim", new { id = claimId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deleting document {documentId}");
                TempData["Error"] = "An error occurred while deleting the document.";
                return RedirectToAction("MyClaims");
            }
        }

        // GET: Home/UploadDocuments/5
        public async Task<IActionResult> UploadDocuments(int id)
        {
            try
            {
                var claim = await _claimService.GetClaimByIdAsync(id);

                if (claim == null)
                {
                    TempData["Error"] = "Claim not found.";
                    return RedirectToAction("MyClaims");
                }

                // Only allow upload for pending or returned claims
                if (claim.Status != ClaimStatus.Pending && claim.Status != ClaimStatus.Returned)
                {
                    TempData["Error"] = "Cannot upload documents to this claim.";
                    return RedirectToAction("ViewClaim", new { id });
                }

                var model = new DocumentUploadViewModel
                {
                    ClaimId = id
                };

                ViewBag.Claim = claim;
                ViewBag.CurrentUser = GetCurrentUserInfo();
                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error loading upload page for claim {id}");
                TempData["Error"] = "Error loading upload page.";
                return RedirectToAction("MyClaims");
            }
        }

        // POST: Home/UploadDocuments
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UploadDocuments(DocumentUploadViewModel model)
        {
            try
            {
                // Validate that at least one file is selected
                if (model.Files == null || !model.Files.Any())
                {
                    TempData["Error"] = "Please select at least one file to upload.";
                    return RedirectToAction("UploadDocuments", new { id = model.ClaimId });
                }

                var uploadPath = Path.Combine(_environment.ContentRootPath, "uploads");

                // Upload files via service
                var result = await _fileService.UploadFilesAsync(
                    model.ClaimId,
                    model.Files,
                    uploadPath);

                // Display result messages
                if (result.Success)
                {
                    TempData["Success"] = result.Message;
                }
                else
                {
                    TempData["Error"] = result.Message;
                }

                return RedirectToAction("ViewClaim", new { id = model.ClaimId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error uploading documents for claim {model.ClaimId}");
                TempData["Error"] = "An error occurred while uploading documents.";
                return RedirectToAction("UploadDocuments", new { id = model.ClaimId });
            }
        }

        // Helper: get current user ID from session
        private int GetCurrentUserId()
        {
            return HttpContext.Session.GetInt32("UserId") ?? 1;
        }

        // Helper: get current user role from session
        private UserRole GetCurrentUserRole()
        {
            var roleString = HttpContext.Session.GetString("UserRole");
            if (string.IsNullOrEmpty(roleString))
            {
                return UserRole.Lecturer;
            }
            return Enum.Parse<UserRole>(roleString);
        }

        // Helper: get current user info for view
        private object GetCurrentUserInfo()
        {
            var userId = HttpContext.Session.GetInt32("UserId") ?? 1;
            var userName = HttpContext.Session.GetString("UserName") ?? "John Smith";
            var userRole = HttpContext.Session.GetString("UserRole") ?? "Lecturer";

            return new
            {
                UserId = userId,
                UserName = userName,
                UserRole = userRole
            };
        }

        // Helper: get default hourly rate for current user
        private decimal GetUserDefaultRate()
        {
            try
            {
                int userId = GetCurrentUserId();
                var user = _context.Users.Find(userId);
                return user?.DefaultHourlyRate ?? 450.00m;
            }
            catch
            {
                return 450.00m;
            }
        }

        // Error page
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View();
        }
    }
}