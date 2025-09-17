namespace ProgPOE
{
    public interface IFileService
    {
        Task<bool> UploadDocumentsAsync(int claimId, List<IFormFile> files);
        Task<byte[]> GetDocumentAsync(int documentId);
        bool ValidateFile(IFormFile file);
    }

    public class FileService : IFileService
    {
        private readonly string[] _allowedExtensions = { ".pdf", ".doc", ".docx", ".jpg", ".jpeg", ".png" };
        private const long _maxFileSize = 10 * 1024 * 1024; // 10MB

        public async Task<bool> UploadDocumentsAsync(int claimId, List<IFormFile> files)
        {
            // Simulate async operation
            await Task.Delay(1000);

            // In real implementation, this would:
            // 1. Validate each file
            // 2. Save files to storage
            // 3. Create database records
            // 4. Generate unique file names

            return true; // Always success in prototype
        }

        public async Task<byte[]> GetDocumentAsync(int documentId)
        {
            // Simulate async operation
            await Task.Delay(200);

            // In real implementation, this would return actual file bytes
            return new byte[0]; // Empty array for prototype
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
    }
}