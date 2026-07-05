using System;

namespace LeakGuard.Models
{
    public class AuditLog
    {
        public int AuditLogID { get; set; }
        public int UserID { get; set; }
        public User User { get; set; } = null!;
        public string Activity { get; set; } = null!;
        public string? IPAddress { get; set; }
        public DateTime LogTime { get; set; } = DateTime.UtcNow;
    }
}
