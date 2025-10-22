using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ProgPOE.Models
{
    // Represents a supporting document attached to a claim
    public class SupportingDocument
    {
        // Primary key for the document
        public int DocumentId { get; set; }

        // Foreign key linking document to a specific claim
        [Required]
        public int ClaimId { get; set; }

        // Original file name as uploaded by the user
        [Required]
        [StringLength(255)]
        public string FileName { get; set; } = string.Empty;

        // File type (extension) in uppercase (e.g., PDF, DOCX)
        [Required]
        [StringLength(10)]
        public string FileType { get; set; } = string.Empty;

        // File size in bytes
        [Required]
        public long FileSize { get; set; }

        // Physical path where the file is stored
        [Required]
        [StringLength(500)]
        public string FilePath { get; set; } = string.Empty;

        // Date and time the document was uploaded
        public DateTime UploadDate { get; set; } = DateTime.Now;

        // Indicates if the document is active or deleted
        public bool IsActive { get; set; } = true;

        // Navigation property to access related claim
        public virtual Claim? Claim { get; set; }

        // Returns a human-readable file size (B, KB, MB)
        public string GetFileSizeFormatted()
        {
            if (FileSize < 1024) return $"{FileSize} B";
            if (FileSize < 1024 * 1024) return $"{FileSize / 1024:F1} KB";
            return $"{FileSize / (1024 * 1024):F1} MB";
        }

        // Returns a simple icon representing the file type
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
