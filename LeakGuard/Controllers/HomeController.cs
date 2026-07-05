using System.Diagnostics;
using LeakGuard.Data;
using LeakGuard.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;

namespace LeakGuard.Controllers
{
    [Authorize]
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly LeakGuardDbContext _context;

        public HomeController(ILogger<HomeController> logger, LeakGuardDbContext context)
        {
            _logger = logger;
            _context = context;
        }

        public IActionResult Index()
        {
            var viewModel = new DashboardViewModel
            {
                TotalIncidents = _context.Incidents.Count(),
                ActiveRulesCount = _context.Rules.Count(r => r.IsActive),
                MonitoredEndpointsCount = _context.Endpoints.Count(),
                RecentIncidents = _context.Incidents
                    .Include(i => i.Endpoint)
                    .Include(i => i.Rule)
                    .Include(i => i.ActionType)
                    .OrderByDescending(i => i.Timestamp)
                    .Take(5)
                    .ToList()
            };

            return View(viewModel);
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
