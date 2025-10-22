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
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _environment;

        public HomeController(
            ILogger<HomeController> logger,
            IClaimService claimService,
            IFileService fileService,
            ApplicationDbContext context,
            IWebHostEnvironment environment)
        {
            _logger = logger;
            _claimService = claimService;
            _fileService = fileService;
            _context = context;
            _environment = environment;
        }

        // GET: Home/Index
        public IActionResult Index()
        {
            return View();
        }

        // GET: Home/Dashboard
        public async Task<IActionResult> Dashboard()
        {
            try
            {
                // TODO: Get actual user ID from session/authentication
                int userId = GetCurrentUserId();

                var dashboardData = await _claimService.GetDashboardDataAsync(userId);
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
            var model = new SubmitClaimViewModel
            {
                // Pre-fill with current month
                MonthYear = DateTime.Now.ToString("yyyy-MM"),
                // Get user's default rate from session or database
                HourlyRate = GetUserDefaultRate()
            };

            return View(model);
        }

        // POST: Home/SubmitClaim
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubmitClaim(SubmitClaimViewModel model)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    TempData["Error"] = "Please correct the errors in the form.";
                    return View(model);
                }

                // Get current user ID
                int userId = GetCurrentUserId();

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

                // Add claim to database
                _context.Claims.Add(claim);
                await _context.SaveChangesAsync();

                // Handle file uploads if any
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
                        TempData["Success"] = $"Claim submitted successfully with {uploadResult.Documents.Count} document(s)!";
                    }
                }
                else
                {
                    TempData["Success"] = "Claim submitted successfully!";
                }

                _logger.LogInformation($"Claim {claim.ClaimId} submitted by user {userId}");
                return RedirectToAction("MyClaims");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error submitting claim");
                TempData["Error"] = "An error occurred while submitting your claim. Please try again.";
                return View(model);
            }
        }

        // GET: Home/MyClaims
        public async Task<IActionResult> MyClaims()
        {
            try
            {
                int userId = GetCurrentUserId();
                var claims = await _claimService.GetUserClaimsAsync(userId);
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

                // TODO: Add authorization check - ensure user can view this claim
                int userId = GetCurrentUserId();
                if (claim.LecturerId != userId)
                {
                    TempData["Error"] = "You are not authorized to view this claim.";
                    return RedirectToAction("MyClaims");
                }

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
        public async Task<IActionResult> ApproveClaims()
        {
            try
            {
                var pendingClaims = await _claimService.GetPendingClaimsAsync();
                return View(pendingClaims);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading pending claims");
                TempData["Error"] = "Error loading pending claims.";
                return RedirectToAction("Dashboard");
            }
        }

        // POST: Home/ProcessApproval
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ProcessApproval(int claimId, ApprovalAction action, string comments)
        {
            try
            {
                int approverId = GetCurrentUserId();

                var result = await _claimService.ProcessApprovalAsync(claimId, action, comments, approverId);

                if (result)
                {
                    TempData["Success"] = $"Claim {action.ToString().ToLower()}d successfully!";
                }
                else
                {
                    TempData["Error"] = "Error processing approval.";
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
                var result = await _fileService.DownloadFileAsync(id);

                if (!result.Success)
                {
                    TempData["Error"] = "Document not found.";
                    return RedirectToAction("MyClaims");
                }

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
                // TODO: Add authorization - only allow deletion by claim owner and if claim is pending
                var claim = await _claimService.GetClaimByIdAsync(claimId);

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

                // Only allow upload for pending claims
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
                if (model.Files == null || !model.Files.Any())
                {
                    TempData["Error"] = "Please select at least one file to upload.";
                    return RedirectToAction("UploadDocuments", new { id = model.ClaimId });
                }

                var uploadPath = Path.Combine(_environment.ContentRootPath, "uploads");

                var result = await _fileService.UploadFilesAsync(
                    model.ClaimId,
                    model.Files,
                    uploadPath);

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

        // Helper method to get current user ID
        // TODO: Replace with actual authentication
        private int GetCurrentUserId()
        {
            // Temporarily return user 1 (John Smith - Lecturer)
            // In production, get from HttpContext.Session or User.Identity
            return HttpContext.Session.GetInt32("UserId") ?? 1;
        }

        // Helper method to get user's default hourly rate
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
                return 450.00m; // Default fallback
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