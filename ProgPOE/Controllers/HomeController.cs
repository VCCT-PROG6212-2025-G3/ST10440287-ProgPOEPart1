using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using ProgPOE.Models;

namespace ProgPOE.Controllers;

public class HomeController : Controller
{
    /// GET: Home/Index - Landing page
    public IActionResult Index()
    {
        // Simulate user role for prototype
        ViewBag.UserRole = "Lecturer"; // This would come from authentication
        ViewBag.UserName = "Dr. John Smith";

        return View();
    }

    // GET: Home/Dashboard - Main dashboard
    public IActionResult Dashboard()
    {
        var model = new DashboardViewModel
        {
            UserRole = "Lecturer", // Simulated for prototype
            UserName = "Dr. John Smith",
            TotalClaims = 8,
            PendingClaims = 2,
            ApprovedClaims = 5,
            RejectedClaims = 1,
            TotalEarnings = 284500.00m
        };

        return View(model);
    }

    // GET: Home/SubmitClaim - Claim submission form
    public IActionResult SubmitClaim()
    {
        var model = new SubmitClaimViewModel
        {
            MonthYear = DateTime.Now.AddMonths(-1).ToString("yyyy-MM"),
            HourlyRate = 450.00m // Default rate
        };

        return View(model);
    }

    // POST: Home/SubmitClaim - Handle form submission (non-functional)
    [HttpPost]
    public IActionResult SubmitClaim(SubmitClaimViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        // In a real system, this would save to database
        TempData["SuccessMessage"] = "Claim submitted successfully! This is a visual prototype.";

        return RedirectToAction("MyClaims");
    }

    // GET: Home/MyClaims - View personal claims
    public IActionResult MyClaims()
    {
        var model = GetSampleClaims(); // Simulated data
        return View(model);
    }

    // GET: Home/ApproveClaims - Claims approval interface
    public IActionResult ApproveClaims()
    {
        ViewBag.UserRole = "ProgrammeCoordinator"; // Simulated
        var model = GetPendingClaims(); // Simulated data
        return View(model);
    }

    // POST: Home/ProcessApproval - Handle approval action (non-functional)
    [HttpPost]
    public IActionResult ProcessApproval(int claimId, string action, string comments)
    {
        // Simulate approval process
        TempData["SuccessMessage"] = $"Claim {claimId} has been {action.ToLower()}ed. This is a visual prototype.";

        return RedirectToAction("ApproveClaims");
    }

    // Helper method to generate sample claims for prototype
    private List<Claim> GetSampleClaims()
    {
        return new List<Claim>
            {
                new Claim
                {
                    ClaimId = 1,
                    MonthYear = "2024-04",
                    HoursWorked = 125.5m,
                    HourlyRate = 450.00m,
                    Status = ClaimStatus.Pending,
                    SubmissionDate = DateTime.Now.AddDays(-3),
                    Lecturer = new User("Dr. John", "Smith"),
                    Documents = new List<SupportingDocument>
                    {
                        new SupportingDocument { FileName = "timesheet.pdf", FileType = "PDF", FileSize = 245000 }
                    }
                },
                new Claim
                {
                    ClaimId = 2,
                    MonthYear = "2024-03",
                    HoursWorked = 118.0m,
                    HourlyRate = 450.00m,
                    Status = ClaimStatus.Approved,
                    SubmissionDate = DateTime.Now.AddDays(-35),
                    CoordinatorApprovalDate = DateTime.Now.AddDays(-30),
                    Lecturer = new User("Dr. John", "Smith")
                }
            };
    }

    // Helper method to generate pending claims for coordinators/managers
    private List<Claim> GetPendingClaims()
    {
        return new List<Claim>
            {
                new Claim
                {
                    ClaimId = 3,
                    MonthYear = "2024-04",
                    HoursWorked = 142.0m,
                    HourlyRate = 420.00m,
                    Status = ClaimStatus.Pending,
                    SubmissionDate = DateTime.Now.AddDays(-2),
                    Lecturer = new User("Dr. Jane", "Wilson", "Mathematics")
                },
                new Claim
                {
                    ClaimId = 4,
                    MonthYear = "2024-04",
                    HoursWorked = 156.5m,
                    HourlyRate = 480.00m,
                    Status = ClaimStatus.Pending,
                    SubmissionDate = DateTime.Now.AddDays(-4),
                    Lecturer = new User("Prof. Mike", "Johnson", "Engineering")
                }
            };
    }
}


