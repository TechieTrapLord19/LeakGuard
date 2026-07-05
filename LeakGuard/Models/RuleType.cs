using System.Collections.Generic;

namespace LeakGuard.Models
{
    public class RuleType
    {
        public int RuleTypeID { get; set; }
        public string TypeName { get; set; } = null!;
        public ICollection<Rule> Rules { get; set; } = new List<Rule>();
    }
}
