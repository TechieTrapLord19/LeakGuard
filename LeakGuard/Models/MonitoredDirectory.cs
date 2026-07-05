namespace LeakGuard.Models
{
    public class MonitoredDirectory
    {
        public int MonitoredDirectoryID { get; set; }
        public string DirectoryPath { get; set; } = null!;
        public string Status { get; set; } = "Enabled";
    }
}
