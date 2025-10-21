using Microsoft.EntityFrameworkCore;
using ProgPOE.Data;
using ProgPOE.Models;

namespace ProgPOE.Services
{
    public interface IFileService
    {
        Task<bool> UploadDocumentsAsync(int claimId, List<IFormFile> files);
        Task<byte[]> GetDocumentAsync(int documentId);
        bool ValidateFile(IFormFile file);
        Task<List<SupportingDocument>> GetClaimDocumentsAsync(int claimId);
    }

    public class FileService : IFileService
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<FileService> _logger;
        private readonly string[] _allowedExtensions = { ".pdf", ".doc", ".docx", ".jpg", ".jpeg", ".png" };
        private const long _maxFileSize = 10 * 1024 * 1024; // 10MB

        public FileService(ApplicationDbContext context, IWebHostEnvironment environment, ILogger<FileService> logger)
        {
            _context = context;
            _environment = environment;
            _logger = logger;
        }

        public async Task<bool> UploadDocumentsAsync(int claimId, List<IFormFile> files)
        {
            try
            {
                var uploadPath = Path.Combine(_environment.WebRootPath, "uploads", claimId.ToString());
                if (!Directory.Exists(uploadPath))
                {
                    Directory.CreateDirectory(uploadPath);
                }

                foreach (var file in files)
                {
                    if (!ValidateFile(file))
                    {
                        _logger.LogWarning($"File {file.FileName} failed validation");
                        continue;
                    }

                    var fileName = $"{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";
                    var filePath = Path.Combine(uploadPath, fileName);

                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await file.CopyToAsync(stream);
                    }

                    var document = new SupportingDocument
                    {
                        ClaimId = claimId,
                        FileName = file.FileName,
                        FileType = Path.GetExtension(file.FileName).TrimStart('.').ToUpper(),
                        FileSize = file.Length,
                        FilePath = $"/uploads/{claimId}/{fileName}",
                        UploadDate = DateTime.Now,
                        IsActive = true
                    };

                    _context.SupportingDocuments.Add(document);
                }

                await _context.SaveChangesAsync();
                _logger.LogInformation($"Uploaded {files.Count} documents for claim {claimId}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error uploading documents for claim {claimId}");
                return false;
            }
        }

        public async Task<byte[]> GetDocumentAsync(int documentId)
        {
            try
            {
                var document = await _context.SupportingDocuments.FindAsync(documentId);
                if (document == null) return null;

                var fullPath = Path.Combine(_environment.WebRootPath, document.FilePath.TrimStart('/'));
                if (!File.Exists(fullPath)) return null;

                return await File.ReadAllBytesAsync(fullPath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error retrieving document {documentId}");
                return null;
            }
        }

        public bool ValidateFile(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return false;

            if (file.Length > _maxFileSize)
                return false;

            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            return _allowedExtensions.Contains(extension);
        }

        public async Task<List<SupportingDocument>> GetClaimDocumentsAsync(int claimId)
        {
            return await _context.SupportingDocuments
                .Where(d => d.ClaimId == claimId && d.IsActive)
                .OrderBy(d => d.UploadDate)
                .ToListAsync();
        }
    }
}