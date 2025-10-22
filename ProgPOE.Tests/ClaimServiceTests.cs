using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using ProgPOE.Data;
using ProgPOE.Models;
using ProgPOE.Services;
using Xunit;

namespace ProgPOE.Tests.Services
{
    public class ClaimServiceTests
    {
        private readonly Mock<ILogger<ClaimService>> _mockLogger;
        private readonly ApplicationDbContext _context;
        private readonly ClaimService _claimService;

        public ClaimServiceTests()
        {
            // Setup in-memory database
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            _context = new ApplicationDbContext(options);
            _mockLogger = new Mock<ILogger<ClaimService>>();
            _claimService = new ClaimService(_context, _mockLogger.Object);

            // Seed test data
            SeedTestData();
        }

        private void SeedTestData()
        {
            var users = new List<User>
            {
                new User
                {
                    UserId = 1,
                    Username = "test.lecturer",
                    Email = "test@test.com",
                    FirstName = "Test",
                    LastName = "Lecturer",
                    Role = UserRole.Lecturer,
                    DefaultHourlyRate = 450.00m,
                    IsActive = true
                },
                new User
                {
                    UserId = 2,
                    Username = "test.coordinator",
                    Email = "coordinator@test.com",
                    FirstName = "Test",
                    LastName = "Coordinator",
                    Role = UserRole.ProgrammeCoordinator,
                    IsActive = true
                },
                new User
                {
                    UserId = 3,
                    Username = "test.manager",
                    Email = "manager@test.com",
                    FirstName = "Test",
                    LastName = "Manager",
                    Role = UserRole.AcademicManager,
                    IsActive = true
                }
            };

            var claims = new List<Claim>
            {
                new Claim
                {
                    ClaimId = 1,
                    LecturerId = 1,
                    MonthYear = "2024-10",
                    HoursWorked = 100,
                    HourlyRate = 450,
                    Status = ClaimStatus.Pending,
                    SubmissionDate = DateTime.Now
                },
                new Claim
                {
                    ClaimId = 2,
                    LecturerId = 1,
                    MonthYear = "2024-09",
                    HoursWorked = 120,
                    HourlyRate = 450,
                    Status = ClaimStatus.Approved,
                    SubmissionDate = DateTime.Now.AddDays(-30),
                    CoordinatorApprovalDate = DateTime.Now.AddDays(-25),
                    ManagerApprovalDate = DateTime.Now.AddDays(-20)
                }
            };

            _context.Users.AddRange(users);
            _context.Claims.AddRange(claims);
            _context.SaveChanges();
        }

        [Fact]
        public async Task GetUserClaimsAsync_ShouldReturnUserClaims()
        {
            // Act
            var result = await _claimService.GetUserClaimsAsync(1);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(2, result.Count);
            Assert.All(result, claim => Assert.Equal(1, claim.LecturerId));
        }

        [Fact]
        public async Task GetUserClaimsAsync_WithInvalidUser_ShouldReturnEmptyList()
        {
            // Act
            var result = await _claimService.GetUserClaimsAsync(999);

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result);
        }

        [Fact]
        public async Task GetPendingClaimsAsync_ShouldReturnOnlyPendingClaims()
        {
            // Act
            var result = await _claimService.GetPendingClaimsAsync();

            // Assert
            Assert.NotNull(result);
            Assert.All(result, claim =>
                Assert.True(claim.Status == ClaimStatus.Pending ||
                           claim.Status == ClaimStatus.PendingManager));
        }

        [Fact]
        public async Task GetDashboardDataAsync_ForLecturer_ShouldReturnCorrectStats()
        {
            // Act
            var result = await _claimService.GetDashboardDataAsync(1);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("Lecturer", result.UserRole);
            Assert.Equal(2, result.TotalClaims);
            Assert.Equal(1, result.PendingClaims);
            Assert.Equal(1, result.ApprovedClaims);
            Assert.Equal(54000m, result.TotalEarnings); // 120 * 450
        }

        [Fact]
        public async Task GetClaimByIdAsync_WithValidId_ShouldReturnClaim()
        {
            // Act
            var result = await _claimService.GetClaimByIdAsync(1);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(1, result.ClaimId);
            Assert.Equal(ClaimStatus.Pending, result.Status);
        }

        [Fact]
        public async Task GetClaimByIdAsync_WithInvalidId_ShouldReturnNull()
        {
            // Act
            var result = await _claimService.GetClaimByIdAsync(999);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task ProcessApprovalAsync_CoordinatorApprove_ShouldChangeToPendingManager()
        {
            // Act
            var result = await _claimService.ProcessApprovalAsync(1, ApprovalAction.Approve, "Approved", 2);

            // Assert
            Assert.True(result);
            var claim = await _context.Claims.FindAsync(1);
            Assert.Equal(ClaimStatus.PendingManager, claim.Status);
            Assert.NotNull(claim.CoordinatorApprovalDate);
            Assert.NotNull(claim.CoordinatorNotes);
        }

        [Fact]
        public async Task ProcessApprovalAsync_ManagerApprove_ShouldChangeToApproved()
        {
            // Arrange - First approve by coordinator
            await _claimService.ProcessApprovalAsync(1, ApprovalAction.Approve, "Approved by coordinator", 2);

            // Act - Then approve by manager
            var result = await _claimService.ProcessApprovalAsync(1, ApprovalAction.Approve, "Final approval", 3);

            // Assert
            Assert.True(result);
            var claim = await _context.Claims.FindAsync(1);
            Assert.Equal(ClaimStatus.Approved, claim.Status);
            Assert.NotNull(claim.ManagerApprovalDate);
            Assert.NotNull(claim.ManagerNotes);
        }

        [Fact]
        public async Task ProcessApprovalAsync_Reject_ShouldChangeToRejected()
        {
            // Act
            var result = await _claimService.ProcessApprovalAsync(1, ApprovalAction.Reject, "Insufficient documentation", 2);

            // Assert
            Assert.True(result);
            var claim = await _context.Claims.FindAsync(1);
            Assert.Equal(ClaimStatus.Rejected, claim.Status);
            Assert.Contains("Insufficient documentation", claim.CoordinatorNotes);
        }

        [Fact]
        public async Task ProcessApprovalAsync_WithInvalidClaimId_ShouldReturnFalse()
        {
            // Act
            var result = await _claimService.ProcessApprovalAsync(999, ApprovalAction.Approve, "Test", 2);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task ProcessApprovalAsync_WithInvalidApproverId_ShouldReturnFalse()
        {
            // Act
            var result = await _claimService.ProcessApprovalAsync(1, ApprovalAction.Approve, "Test", 999);

            // Assert
            Assert.False(result);
        }
    }
}