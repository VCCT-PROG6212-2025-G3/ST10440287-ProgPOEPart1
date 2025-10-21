using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ProgPOE.Models
{
    public class SupportingDocument
    {
        public int DocumentId { get; set; }

        [Required]
        public int ClaimId { get; set; }

        [Required]
        [StringLength(255)]
        public string FileName { get; set; } = string.Empty;

        [Required]
        [StringLength(10)]
        public string FileType { get; set; } = string.Empty;

        [Required]
        public long FileSize { get; set; }

        [Required]
        [StringLength(500)]
        public string FilePath { get; set; } = string.Empty;

        public DateTime UploadDate { get; set; } = DateTime.Now;

        public bool IsActive { get; set; } = true;

        public virtual Claim? Claim { get; set; }

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