using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using LeakGuard.Data;
using LeakGuard.Models;

namespace LeakGuard.Controllers
{
    [Authorize(Roles = "Administrator")]
    public class RulesController : Controller
    {
        private readonly LeakGuardDbContext _context;
        private readonly LeakGuard.Services.IAuditService _auditService;

        public RulesController(LeakGuardDbContext context, LeakGuard.Services.IAuditService auditService)
        {
            _context = context;
            _auditService = auditService;
        }

        // GET: Rules
        public async Task<IActionResult> Index()
        {
            var rules = await _context.Rules
                .Include(r => r.Creator)
                .Include(r => r.RuleType)
                .Include(r => r.ActionType)
                .ToListAsync();
            ViewBag.RuleTypes = await _context.RuleTypes.ToListAsync();
            ViewBag.ActionTypes = await _context.ActionTypes.ToListAsync();
            return View(rules);
        }

        // GET: Rules/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var rule = await _context.Rules
                .Include(r => r.Creator)
                .Include(r => r.RuleType)
                .FirstOrDefaultAsync(m => m.RuleID == id);
            if (rule == null)
            {
                return NotFound();
            }

            return View(rule);
        }

        // GET: Rules/Create
        public IActionResult Create()
        {
            ViewData["CreatedBy"] = new SelectList(_context.Users, "UserID", "UserID");
            ViewData["RuleTypeID"] = new SelectList(_context.RuleTypes, "RuleTypeID", "RuleTypeID");
            return View();
        }

        // POST: Rules/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("RuleTypeID,RuleName,MatchValue,IsActive,ActionTypeID")] Rule rule)
        {
            _context.Add(rule);
            await _context.SaveChangesAsync();
            await _auditService.LogAsync($"Created detection rule: {rule.RuleName}");
            TempData["Success"] = $"Rule '{rule.RuleName}' was added successfully.";
            return RedirectToAction(nameof(Index));
        }

        // GET: Rules/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var rule = await _context.Rules.FindAsync(id);
            if (rule == null)
            {
                return NotFound();
            }
            ViewData["CreatedBy"] = new SelectList(_context.Users, "UserID", "UserID", rule.CreatedBy);
            ViewData["RuleTypeID"] = new SelectList(_context.RuleTypes, "RuleTypeID", "RuleTypeID", rule.RuleTypeID);
            return View(rule);
        }

        // POST: Rules/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("RuleID,RuleTypeID,RuleName,MatchValue,IsActive,ActionTypeID")] Rule rule)
        {
            if (id != rule.RuleID) return NotFound();

            var existing = await _context.Rules.AsNoTracking().FirstOrDefaultAsync(r => r.RuleID == id);
            if (existing == null) return NotFound();

            rule.CreatedBy = existing.CreatedBy;

            try
            {
                _context.Update(rule);
                await _context.SaveChangesAsync();
                await _auditService.LogAsync($"Updated detection rule: {rule.RuleName}");
                TempData["Success"] = $"Rule '{rule.RuleName}' was updated successfully.";
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!RuleExists(rule.RuleID)) return NotFound();
                else throw;
            }

            return RedirectToAction(nameof(Index));
        }

        // Rules are never deleted — they are archived (deactivated) instead.
        // This preserves the incident history that references each rule.

        // POST: Rules/ToggleActive/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleActive(int id)
        {
            var rule = await _context.Rules.FindAsync(id);
            if (rule == null) return NotFound();

            rule.IsActive = !rule.IsActive;
            await _context.SaveChangesAsync();

            var state = rule.IsActive ? "reactivated" : "archived";
            await _auditService.LogAsync($"{(rule.IsActive ? "Reactivated" : "Archived")} detection rule: {rule.RuleName}");
            TempData["Success"] = $"Rule '{rule.RuleName}' was {state}.";
            return RedirectToAction(nameof(Index));
        }

        private bool RuleExists(int id)
        {
            return _context.Rules.Any(e => e.RuleID == id);
        }
    }
}
