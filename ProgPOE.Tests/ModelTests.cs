using ProgPOE.Models;
using Xunit;

namespace ProgPOE.Tests.Models
{
    public class ModelTests
    {
        [Fact]
        public void Claim_TotalAmount_ShouldCalculateCorrectly()
        {
            // Arrange
            var claim = new Claim
            {
                HoursWorked = 100,
                HourlyRate = 450
            };

            // Act
            var total = claim.TotalAmount;

            // Assert
            Assert.Equal(45000, total);
        }

        [Fact]
        public void Claim_StatusDisplayText_ShouldReturnCorrectText()
        {
            // Arrange & Act & Assert
            var pendingClaim = new Claim { Status = ClaimStatus.Pending };
            Assert.Equal("Pending Coordinator Review", pendingClaim.StatusDisplayText);

            var approvedClaim = new Claim { Status = ClaimStatus.Approved };
            Assert.Equal("Approved", approvedClaim.StatusDisplayText);

            var rejectedClaim = new Claim { Status = ClaimStatus.Rejected };
            Assert.Equal("Rejected", rejectedClaim.StatusDisplayText);
        }

        [Fact]
        public void Claim_ProgressPercentage_ShouldReturnCorrectValue()
        {
            // Arrange & Act & Assert
            var pendingClaim = new Claim { Status = ClaimStatus.Pending };
            Assert.Equal(33, pendingClaim.ProgressPercentage);

            var pendingManagerClaim = new Claim { Status = ClaimStatus.PendingManager };
            Assert.Equal(66, pendingManagerClaim.ProgressPercentage);

            var approvedClaim = new Claim { Status = ClaimStatus.Approved };
            Assert.Equal(100, approvedClaim.ProgressPercentage);
        }

        [Fact]
        public void User_FullName_ShouldConcatenateNames()
        {
            // Arrange
            var user = new User
            {
                FirstName = "John",
                LastName = "Smith"
            };

            // Act
            var fullName = user.FullName;

            // Assert
            Assert.Equal("John Smith", fullName);
        }

        [Fact]
        public void SupportingDocument_GetFileSizeFormatted_ShouldFormatCorrectly()
        {
            // Arrange & Act & Assert
            var smallDoc = new SupportingDocument { FileSize = 500 };
            Assert.Equal("500 B", smallDoc.GetFileSizeFormatted());

            var mediumDoc = new SupportingDocument { FileSize = 1024 * 500 }; // 500 KB
            Assert.Equal("500.0 KB", mediumDoc.GetFileSizeFormatted());

            var largeDoc = new SupportingDocument { FileSize = 1024 * 1024 * 2 }; // 2 MB
            Assert.Equal("2.0 MB", largeDoc.GetFileSizeFormatted());
        }

        [Fact]
        public void SupportingDocument_GetFileIcon_ShouldReturnCorrectIcon()
        {
            // Arrange & Act & Assert
            var pdfDoc = new SupportingDocument { FileType = "PDF" };
            Assert.Equal("📕", pdfDoc.GetFileIcon());

            var docxDoc = new SupportingDocument { FileType = "DOCX" };
            Assert.Equal("📘", docxDoc.GetFileIcon());

            var jpgDoc = new SupportingDocument { FileType = "JPG" };
            Assert.Equal("🖼️", jpgDoc.GetFileIcon());
        }

        [Theory]
        [InlineData(0.1, 450, 45)]
        [InlineData(100, 450, 45000)]
        [InlineData(744, 500, 372000)]
        public void Claim_TotalAmount_WithVariousInputs_ShouldCalculateCorrectly(
            decimal hours, decimal rate, decimal expected)
        {
            // Arrange
            var claim = new Claim
            {
                HoursWorked = hours,
                HourlyRate = rate
            };

            // Act
            var total = claim.TotalAmount;

            // Assert
            Assert.Equal(expected, total);
        }

        [Fact]
        public void DashboardViewModel_FormattedEarnings_ShouldFormatCorrectly()
        {
            // Arrange
            var viewModel = new DashboardViewModel
            {
                TotalEarnings = 45000.50m
            };

            // Act
            var formatted = viewModel.FormattedEarnings;

            // Assert
            Assert.Equal("R 45,000.50", formatted);
        }
    }
}