using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using LeakGuard.Data;

namespace LeakGuard.Controllers
{
    [Authorize(Roles = "Administrator")]
    public class AuditLogsController : Controller
    {
        private readonly LeakGuardDbContext _context;

        public AuditLogsController(LeakGuardDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var logs = await _context.AuditLogs.Include(a => a.User).OrderByDescending(a => a.LogTime).ToListAsync();
            return View(logs);
        }
    }
}
