using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using LeakGuard.Data;

namespace LeakGuard.Controllers
{
    [Authorize(Roles = "Administrator")]
    public class EndpointsController : Controller
    {
        private readonly LeakGuardDbContext _context;

        public EndpointsController(LeakGuardDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var endpoints = await _context.Endpoints.ToListAsync();
            return View(endpoints);
        }
    }
}
