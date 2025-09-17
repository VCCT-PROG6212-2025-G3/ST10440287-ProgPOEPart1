using System.ComponentModel.DataAnnotations;

namespace ProgPOE.Models
{
    public class SupportingDocument
    {
        public int DocumentId { get; set; }

        [Required]
        public int ClaimId { get; set; }

        [Required]
        [StringLength(255)]
        public string FileName { get; set; }

        [Required]
        [StringLength(10)]
        public string FileType { get; set; } // PDF, DOC, DOCX, JPG, PNG

        [Required]
        public long FileSize { get; set; } // In bytes

        [Required]
        public string FilePath { get; set; }

        public DateTime UploadDate { get; set; }

        public bool IsActive { get; set; } = true;

        // Helper methods for display
        public string GetFileSizeFormatted()
        {
            if (FileSize < 1024) return $"{FileSize} B";
            if (FileSize < 1024 * 1024) return $"{FileSize / 1024:F1} KB";
            return $"{FileSize / (1024 * 1024):F1} MB";
        }

        public string GetFileIcon()
        {
            return FileType.ToUpper() switch
            {
                "PDF" => "📕",
                "DOC" or "DOCX" => "📘",
                "JPG" or "JPEG" or "PNG" => "🖼️",
                _ => "📄"
            };
        }
    }
}