using System;

namespace LeakGuard.Models
{
    public class Incident
    {
        public int IncidentID { get; set; }
        public int EndpointID { get; set; }
        public LeakGuard.Models.Endpoint Endpoint { get; set; } = null!;
        public int RuleID { get; set; }
        public Rule Rule { get; set; } = null!;
        public int ActionTypeID { get; set; }
        public ActionType ActionType { get; set; } = null!;
        public string FilePath { get; set; } = null!;
        public string? MatchedText { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}
