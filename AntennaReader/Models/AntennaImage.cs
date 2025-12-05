using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AntennaReader.Models
{
    /// <summary>
    /// Represents the metadata for an antenna diagram image
    /// </summary>
    public class AntennaImage
    {
        public int Id { get; set; } // primary key
        // properties
        // 1. image file name
        public string FileName { get; set; } = string.Empty;
        // 2. image file path
        public string FilePath { get; set; } = string.Empty;
        // 3. description
        public string? Description { get; set; }
        // 4. upload date
        public DateTime UploadDate { get; set; }
    }
}
