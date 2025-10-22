using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Hosting;
using Moq;
using ProgPOE.Data;
using ProgPOE.Models;
using ProgPOE.Services;
using Xunit;
using System.Text;

namespace ProgPOE.Tests.Services
{
    public class FileServiceTests
    {
        private readonly Mock<ILogger<FileService>> _mockLogger;
        private readonly Mock<IWebHostEnvironment> _mockEnvironment;
        private readonly ApplicationDbContext _context;
        private readonly FileService _fileService;
        private readonly string _testUploadPath;

        public FileServiceTests()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            _context = new ApplicationDbContext(options);
            _mockLogger = new Mock<ILogger<FileService>>();
            _mockEnvironment = new Mock<IWebHostEnvironment>();

            _testUploadPath = Path.Combine(Path.GetTempPath(), "test_uploads");
            Directory.CreateDirectory(_testUploadPath);

            _fileService = new FileService(_context, _mockLogger.Object, _mockEnvironment.Object);

            SeedTestData();
        }

        private void SeedTestData()
        {
            var claim = new Claim
            {
                ClaimId = 1,
                LecturerId = 1,
                MonthYear = "2024-10",
                HoursWorked = 100,
                HourlyRate = 450,
                Status = ClaimStatus.Pending,
                SubmissionDate = DateTime.Now
            };

            _context.Claims.Add(claim);
            _context.SaveChanges();
        }

        private Mock<IFormFile> CreateMockFile(string fileName, string content, long size)
        {
            var mockFile = new Mock<IFormFile>();
            var ms = new MemoryStream(Encoding.UTF8.GetBytes(content));

            mockFile.Setup(f => f.FileName).Returns(fileName);
            mockFile.Setup(f => f.Length).Returns(size);
            mockFile.Setup(f => f.OpenReadStream()).Returns(ms);
            mockFile.Setup(f => f.ContentType).Returns("application/pdf");
            mockFile.Setup(f => f.CopyToAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
                .Returns((Stream stream, CancellationToken token) => ms.CopyToAsync(stream, token));

            return mockFile;
        }

        [Fact]
        public void ValidateFile_WithValidPdfFile_ShouldReturnTrue()
        {
            // Arrange
            var mockFile = CreateMockFile("test.pdf", "Test content", 1024 * 1024); // 1MB

            // Act
            var result = _fileService.ValidateFile(mockFile.Object, out string errorMessage);

            // Assert
            Assert.True(result);
            Assert.Empty(errorMessage);
        }

        [Fact]
        public void ValidateFile_WithOversizedFile_ShouldReturnFalse()
        {
            // Arrange
            var mockFile = CreateMockFile("test.pdf", "Test content", 10 * 1024 * 1024); // 10MB

            // Act
            var result = _fileService.ValidateFile(mockFile.Object, out string errorMessage);

            // Assert
            Assert.False(result);
            Assert.Contains("exceeds the maximum limit", errorMessage);
        }

        [Fact]
        public void ValidateFile_WithInvalidFileType_ShouldReturnFalse()
        {
            // Arrange
            var mockFile = CreateMockFile("test.exe", "Test content", 1024);

            // Act
            var result = _fileService.ValidateFile(mockFile.Object, out string errorMessage);

            // Assert
            Assert.False(result);
            Assert.Contains("not allowed", errorMessage);
        }

        [Fact]
        public void ValidateFile_WithNullFile_ShouldReturnFalse()
        {
            // Act
            var result = _fileService.ValidateFile(null, out string errorMessage);

            // Assert
            Assert.False(result);
            Assert.Contains("empty or not provided", errorMessage);
        }

        [Fact]
        public void ValidateFile_WithDangerousFileName_ShouldReturnFalse()
        {
            // Arrange
            var mockFile = CreateMockFile("../../../test.pdf", "Test content", 1024);

            // Act
            var result = _fileService.ValidateFile(mockFile.Object, out string errorMessage);

            // Assert
            Assert.False(result);
            Assert.Contains("Invalid filename", errorMessage);
        }

        [Fact]
        public async Task UploadFilesAsync_WithValidFiles_ShouldSucceed()
        {
            // Arrange
            var mockFile1 = CreateMockFile("test1.pdf", "Test content 1", 1024);
            var mockFile2 = CreateMockFile("test2.docx", "Test content 2", 2048);
            var files = new List<IFormFile> { mockFile1.Object, mockFile2.Object };

            // Act
            var result = await _fileService.UploadFilesAsync(1, files, _testUploadPath);

            // Assert
            Assert.True(result.Success);
            Assert.Equal(2, result.Documents.Count);
            Assert.Contains("Successfully uploaded", result.Message);
        }

        [Fact]
        public async Task UploadFilesAsync_WithInvalidClaimId_ShouldFail()
        {
            // Arrange
            var mockFile = CreateMockFile("test.pdf", "Test content", 1024);
            var files = new List<IFormFile> { mockFile.Object };

            // Act
            var result = await _fileService.UploadFilesAsync(999, files, _testUploadPath);

            // Assert
            Assert.False(result.Success);
            Assert.Contains("Claim not found", result.Message);
        }

        [Fact]
        public async Task GetClaimDocumentsAsync_ShouldReturnDocuments()
        {
            // Arrange
            var doc = new SupportingDocument
            {
                ClaimId = 1,
                FileName = "test.pdf",
                FileType = "PDF",
                FileSize = 1024,
                FilePath = "/test/path.pdf",
                IsActive = true
            };
            _context.SupportingDocuments.Add(doc);
            await _context.SaveChangesAsync();

            // Act
            var result = await _fileService.GetClaimDocumentsAsync(1);

            // Assert
            Assert.NotNull(result);
            Assert.Single(result);
            Assert.Equal("test.pdf", result[0].FileName);
        }

        // Cleanup
        public void Dispose()
        {
            if (Directory.Exists(_testUploadPath))
            {
                Directory.Delete(_testUploadPath, true);
            }
        }
    }
}