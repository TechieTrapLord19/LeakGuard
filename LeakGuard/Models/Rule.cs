namespace LeakGuard.Models
{
    public class Rule
    {
        public int RuleID { get; set; }
        public int RuleTypeID { get; set; }
        public RuleType RuleType { get; set; } = null!;
        public string RuleName { get; set; } = null!;
        public string MatchValue { get; set; } = null!;
        public int? CreatedBy { get; set; }
        public User? Creator { get; set; }
        public bool IsActive { get; set; } = true;

        // Enforcement action for this rule (Alert / Quarantine / Block)
        public int? ActionTypeID { get; set; }
        public ActionType? ActionType { get; set; }
    }
}
