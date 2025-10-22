using Microsoft.EntityFrameworkCore;
using ProgPOE.Data;
using ProgPOE.Models;

namespace ProgPOE.Services
{
    // Interface defining the contract for file management operations
    public interface IFileService
    {
        // Upload multiple files linked to a specific claim
        Task<(bool Success, string Message, List<SupportingDocument> Documents)> UploadFilesAsync(
            int claimId,
            List<IFormFile> files,
            string uploadPath);

        // Delete a file and its database record by document ID
        Task<bool> DeleteFileAsync(int documentId);

        // Download a file as a byte array along with its metadata
        Task<(bool Success, byte[] FileData, string ContentType, string FileName)> DownloadFileAsync(int documentId);

        // Validate file size, type, and filename safety
        bool ValidateFile(IFormFile file, out string errorMessage);

        // Retrieve all documents for a specific claim
        Task<List<SupportingDocument>> GetClaimDocumentsAsync(int claimId);
    }

    // Service implementation for handling file uploads, downloads, and deletions
    public class FileService : IFileService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<FileService> _logger;
        private readonly IWebHostEnvironment _environment;

        // Maximum allowed file size (5 MB)
        private readonly long _maxFileSize = 5 * 1024 * 1024;

        // Allowed file extensions
        private readonly string[] _allowedExtensions = { ".pdf", ".doc", ".docx", ".xlsx", ".xls", ".jpg", ".jpeg", ".png" };

        // MIME types for allowed file extensions
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

        // Constructor injecting dependencies for logging, database, and hosting environment
        public FileService(
            ApplicationDbContext context,
            ILogger<FileService> logger,
            IWebHostEnvironment environment)
        {
            _context = context;
            _logger = logger;
            _environment = environment;
        }

        // Validate an uploaded file before saving it
        public bool ValidateFile(IFormFile file, out string errorMessage)
        {
            errorMessage = string.Empty;

            // Check if the file is empty or null
            if (file == null || file.Length == 0)
            {
                errorMessage = "File is empty or not provided.";
                return false;
            }

            // Ensure file does not exceed the size limit
            if (file.Length > _maxFileSize)
            {
                errorMessage = $"File size exceeds the maximum limit of {_maxFileSize / (1024 * 1024)}MB.";
                return false;
            }

            // Verify the file extension is supported
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!_allowedExtensions.Contains(extension))
            {
                errorMessage = $"File type '{extension}' is not allowed. Allowed types: {string.Join(", ", _allowedExtensions)}";
                return false;
            }

            // Check for invalid or malicious filenames
            if (file.FileName.Contains("..") || file.FileName.Contains("/") || file.FileName.Contains("\\"))
            {
                errorMessage = "Invalid filename detected.";
                return false;
            }

            return true;
        }

        // Upload multiple files for a claim and save their metadata to the database
        public async Task<(bool Success, string Message, List<SupportingDocument> Documents)> UploadFilesAsync(
            int claimId,
            List<IFormFile> files,
            string uploadPath)
        {
            var uploadedDocuments = new List<SupportingDocument>();
            var errorMessages = new List<string>();

            try
            {
                // Ensure the claim exists before uploading files
                var claim = await _context.Claims.FindAsync(claimId);
                if (claim == null)
                {
                    return (false, "Claim not found.", uploadedDocuments);
                }

                // Create claim-specific directory if it doesn't exist
                var claimUploadPath = Path.Combine(uploadPath, $"Claim_{claimId}");
                if (!Directory.Exists(claimUploadPath))
                {
                    Directory.CreateDirectory(claimUploadPath);
                }

                // Loop through and process each uploaded file
                foreach (var file in files)
                {
                    // Validate the file before saving
                    if (!ValidateFile(file, out string validationError))
                    {
                        errorMessages.Add($"{file.FileName}: {validationError}");
                        _logger.LogWarning($"File validation failed for {file.FileName}: {validationError}");
                        continue;
                    }

                    try
                    {
                        // Generate a unique file name to prevent overwriting
                        var fileExtension = Path.GetExtension(file.FileName).ToLowerInvariant();
                        var uniqueFileName = $"{Guid.NewGuid()}{fileExtension}";
                        var filePath = Path.Combine(claimUploadPath, uniqueFileName);

                        // Save file to physical storage
                        using (var stream = new FileStream(filePath, FileMode.Create))
                        {
                            await file.CopyToAsync(stream);
                        }

                        // Add a new document record to the database
                        var document = new SupportingDocument
                        {
                            ClaimId = claimId,
                            FileName = file.FileName, // Keep original name
                            FileType = fileExtension.TrimStart('.').ToUpper(),
                            FileSize = file.Length,
                            FilePath = filePath,
                            UploadDate = DateTime.Now,
                            IsActive = true
                        };

                        _context.SupportingDocuments.Add(document);
                        uploadedDocuments.Add(document);

                        _logger.LogInformation($"File uploaded successfully: {file.FileName} for Claim {claimId}");
                    }
                    catch (Exception ex)
                    {
                        // Log and record errors for failed uploads
                        errorMessages.Add($"{file.FileName}: Upload failed - {ex.Message}");
                        _logger.LogError(ex, $"Error uploading file {file.FileName}");
                    }
                }

                // Save changes to the database if uploads succeeded
                if (uploadedDocuments.Any())
                {
                    await _context.SaveChangesAsync();
                }

                // Build response message based on upload results
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

        // Delete a document record and its physical file
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

                // Delete the file from the file system
                if (File.Exists(document.FilePath))
                {
                    File.Delete(document.FilePath);
                }

                // Remove document record from the database
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

        // Download a stored file and return it as a byte array
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

                // Check that the file still exists on disk
                if (!File.Exists(document.FilePath))
                {
                    _logger.LogWarning($"Physical file not found: {document.FilePath}");
                    return (false, null, null, null);
                }

                // Read file data into memory
                var fileData = await File.ReadAllBytesAsync(document.FilePath);

                // Determine correct MIME type or use generic fallback
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

        // Retrieve all active documents associated with a given claim
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
