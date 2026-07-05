using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using LeakGuard.Data;
using LeakGuard.Models;

namespace LeakGuard.Controllers
{
    [Authorize(Roles = "Administrator")]
    public class RuleTypesController : Controller
    {
        private readonly LeakGuardDbContext _context;

        public RuleTypesController(LeakGuardDbContext context)
        {
            _context = context;
        }

        // GET: RuleTypes
        public async Task<IActionResult> Index()
        {
            return View(await _context.RuleTypes.Include(rt => rt.Rules).ToListAsync());
        }

        // POST: RuleTypes/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("TypeName")] RuleType ruleType)
        {
            if (string.IsNullOrWhiteSpace(ruleType.TypeName))
            {
                TempData["Error"] = "Type name cannot be empty.";
                return RedirectToAction(nameof(Index));
            }
            _context.Add(ruleType);
            await _context.SaveChangesAsync();
            TempData["Success"] = $"Rule Type '{ruleType.TypeName}' was created.";
            return RedirectToAction(nameof(Index));
        }

        // POST: RuleTypes/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("RuleTypeID,TypeName")] RuleType ruleType)
        {
            if (id != ruleType.RuleTypeID) return NotFound();

            try
            {
                _context.Update(ruleType);
                await _context.SaveChangesAsync();
                TempData["Success"] = $"Rule Type updated to '{ruleType.TypeName}'.";
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!_context.RuleTypes.Any(e => e.RuleTypeID == id)) return NotFound();
                else throw;
            }
            return RedirectToAction(nameof(Index));
        }

        // Rule types are never deleted: removing one would cascade to its rules
        // and their incident history. Rename via Edit instead.
    }
}
