using System.Collections.Generic;
using LeakGuard.Models;

namespace LeakGuard.Models
{
    public class DashboardViewModel
    {
        public int TotalIncidents { get; set; }
        public int ActiveRulesCount { get; set; }
        public int MonitoredEndpointsCount { get; set; }
        public List<Incident> RecentIncidents { get; set; } = new List<Incident>();
    }
}
