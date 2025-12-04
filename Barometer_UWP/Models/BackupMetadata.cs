using System;

namespace Barometer_UWP.Models
{
    public class BackupMetadata
    {
        public string Filename { get; set; }
        public DateTime CreatedAt { get; set; }
        public string Storage { get; set; } // Local / OneDrive
    }
}