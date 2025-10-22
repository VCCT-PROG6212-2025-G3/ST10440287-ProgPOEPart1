using Microsoft.EntityFrameworkCore;
using ProgPOE.Data;
using ProgPOE.Models;

namespace ProgPOE.Services
{
    public interface IFileService
    {
        Task<(bool Success, string Message, List<SupportingDocument> Documents)> UploadFilesAsync(
            int claimId,
            List<IFormFile> files,
            string uploadPath);

        Task<bool> DeleteFileAsync(int documentId);
        Task<(bool Success, byte[] FileData, string ContentType, string FileName)> DownloadFileAsync(int documentId);
        bool ValidateFile(IFormFile file, out string errorMessage);
        Task<List<SupportingDocument>> GetClaimDocumentsAsync(int claimId);
    }

    public class FileService : IFileService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<FileService> _logger;
        private readonly IWebHostEnvironment _environment;

        // File upload configuration
        private readonly long _maxFileSize = 5 * 1024 * 1024; // 5MB
        private readonly string[] _allowedExtensions = { ".pdf", ".doc", ".docx", ".xlsx", ".xls", ".jpg", ".jpeg", ".png" };
        private readonly Dictionary<string, string> _mimeTypes = new()
        {
            { ".pdf", "application/pdf" },
            { ".doc", "application/msword" },
            { ".docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document" },
            { ".xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" },
            { ".xls", "application/vnd.ms-excel" },
            { ".jpg", "image/jpeg" },
            { ".jpeg", "image/jpeg" },
            { ".png", "image/png" }
        };

        public FileService(
            ApplicationDbContext context,
            ILogger<FileService> logger,
            IWebHostEnvironment environment)
        {
            _context = context;
            _logger = logger;
            _environment = environment;
        }

        public bool ValidateFile(IFormFile file, out string errorMessage)
        {
            errorMessage = string.Empty;

            if (file == null || file.Length == 0)
            {
                errorMessage = "File is empty or not provided.";
                return false;
            }

            // Check file size
            if (file.Length > _maxFileSize)
            {
                errorMessage = $"File size exceeds the maximum limit of {_maxFileSize / (1024 * 1024)}MB.";
                return false;
            }

            // Check file extension
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!_allowedExtensions.Contains(extension))
            {
                errorMessage = $"File type '{extension}' is not allowed. Allowed types: {string.Join(", ", _allowedExtensions)}";
                return false;
            }

            // Check for potentially dangerous filenames
            if (file.FileName.Contains("..") || file.FileName.Contains("/") || file.FileName.Contains("\\"))
            {
                errorMessage = "Invalid filename detected.";
                return false;
            }

            return true;
        }

        public async Task<(bool Success, string Message, List<SupportingDocument> Documents)> UploadFilesAsync(
            int claimId,
            List<IFormFile> files,
            string uploadPath)
        {
            var uploadedDocuments = new List<SupportingDocument>();
            var errorMessages = new List<string>();

            try
            {
                // Verify claim exists
                var claim = await _context.Claims.FindAsync(claimId);
                if (claim == null)
                {
                    return (false, "Claim not found.", uploadedDocuments);
                }

                // Create upload directory if it doesn't exist
                var claimUploadPath = Path.Combine(uploadPath, $"Claim_{claimId}");
                if (!Directory.Exists(claimUploadPath))
                {
                    Directory.CreateDirectory(claimUploadPath);
                }

                foreach (var file in files)
                {
                    // Validate each file
                    if (!ValidateFile(file, out string validationError))
                    {
                        errorMessages.Add($"{file.FileName}: {validationError}");
                        _logger.LogWarning($"File validation failed for {file.FileName}: {validationError}");
                        continue;
                    }

                    try
                    {
                        // Generate unique filename to prevent overwriting
                        var fileExtension = Path.GetExtension(file.FileName).ToLowerInvariant();
                        var uniqueFileName = $"{Guid.NewGuid()}{fileExtension}";
                        var filePath = Path.Combine(claimUploadPath, uniqueFileName);

                        // Save file to disk
                        using (var stream = new FileStream(filePath, FileMode.Create))
                        {
                            await file.CopyToAsync(stream);
                        }

                        // Create database record
                        var document = new SupportingDocument
                        {
                            ClaimId = claimId,
                            FileName = file.FileName, // Store original filename
                            FileType = fileExtension.TrimStart('.').ToUpper(),
                            FileSize = file.Length,
                            FilePath = filePath, // Store actual file path
                            UploadDate = DateTime.Now,
                            IsActive = true
                        };

                        _context.SupportingDocuments.Add(document);
                        uploadedDocuments.Add(document);

                        _logger.LogInformation($"File uploaded successfully: {file.FileName} for Claim {claimId}");
                    }
                    catch (Exception ex)
                    {
                        errorMessages.Add($"{file.FileName}: Upload failed - {ex.Message}");
                        _logger.LogError(ex, $"Error uploading file {file.FileName}");
                    }
                }

                // Save all changes to database
                if (uploadedDocuments.Any())
                {
                    await _context.SaveChangesAsync();
                }

                // Prepare response message
                string message;
                if (uploadedDocuments.Any() && !errorMessages.Any())
                {
                    message = $"Successfully uploaded {uploadedDocuments.Count} file(s).";
                    return (true, message, uploadedDocuments);
                }
                else if (uploadedDocuments.Any() && errorMessages.Any())
                {
                    message = $"Uploaded {uploadedDocuments.Count} file(s). Errors: {string.Join("; ", errorMessages)}";
                    return (true, message, uploadedDocuments);
                }
                else
                {
                    message = $"No files uploaded. Errors: {string.Join("; ", errorMessages)}";
                    return (false, message, uploadedDocuments);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error in UploadFilesAsync for Claim {claimId}");
                return (false, $"Upload failed: {ex.Message}", uploadedDocuments);
            }
        }

        public async Task<bool> DeleteFileAsync(int documentId)
        {
            try
            {
                var document = await _context.SupportingDocuments.FindAsync(documentId);
                if (document == null)
                {
                    _logger.LogWarning($"Document {documentId} not found");
                    return false;
                }

                // Delete physical file
                if (File.Exists(document.FilePath))
                {
                    File.Delete(document.FilePath);
                }

                // Remove from database
                _context.SupportingDocuments.Remove(document);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Document {documentId} deleted successfully");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deleting document {documentId}");
                return false;
            }
        }

        public async Task<(bool Success, byte[] FileData, string ContentType, string FileName)> DownloadFileAsync(int documentId)
        {
            try
            {
                var document = await _context.SupportingDocuments.FindAsync(documentId);
                if (document == null)
                {
                    _logger.LogWarning($"Document {documentId} not found");
                    return (false, null, null, null);
                }

                if (!File.Exists(document.FilePath))
                {
                    _logger.LogWarning($"Physical file not found: {document.FilePath}");
                    return (false, null, null, null);
                }

                var fileData = await File.ReadAllBytesAsync(document.FilePath);
                var extension = Path.GetExtension(document.FileName).ToLowerInvariant();
                var contentType = _mimeTypes.ContainsKey(extension)
                    ? _mimeTypes[extension]
                    : "application/octet-stream";

                return (true, fileData, contentType, document.FileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error downloading document {documentId}");
                return (false, null, null, null);
            }
        }

        public async Task<List<SupportingDocument>> GetClaimDocumentsAsync(int claimId)
        {
            try
            {
                return await _context.SupportingDocuments
                    .Where(d => d.ClaimId == claimId && d.IsActive)
                    .OrderByDescending(d => d.UploadDate)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error retrieving documents for claim {claimId}");
                return new List<SupportingDocument>();
            }
        }
    }
}