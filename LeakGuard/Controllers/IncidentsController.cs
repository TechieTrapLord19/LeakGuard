using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using LeakGuard.Data;
using LeakGuard.Models;
using LeakGuard.Reports;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace LeakGuard.Controllers
{
    [Authorize]
    public class IncidentsController : Controller
    {
        private readonly LeakGuardDbContext _context;

        public IncidentsController(LeakGuardDbContext context)
        {
            _context = context;
        }

        // GET: Incidents
        public async Task<IActionResult> Index(int page = 1, string sort = "desc",
            string? search = null, int? ruleId = null, int? actionTypeId = null,
            DateTime? from = null, DateTime? to = null)
        {
            int pageSize = 10;

            var query = BuildIncidentQuery(sort, search, ruleId, actionTypeId, from, to);

            var totalIncidents = await query.CountAsync();
            var totalPages = (int)Math.Ceiling(totalIncidents / (double)pageSize);

            if (page < 1) page = 1;
            if (page > totalPages && totalPages > 0) page = totalPages;

            var incidents = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.TotalIncidents = totalIncidents;
            ViewBag.Sort = sort == "asc" ? "asc" : "desc";
            ViewBag.Search = search;
            ViewBag.RuleId = ruleId;
            ViewBag.ActionTypeId = actionTypeId;
            ViewBag.From = from?.ToString("yyyy-MM-dd");
            ViewBag.To = to?.ToString("yyyy-MM-dd");

            ViewBag.RuleOptions = await _context.Rules.OrderBy(r => r.RuleName).ToListAsync();
            ViewBag.ActionTypeOptions = await _context.ActionTypes.OrderBy(a => a.ActionName).ToListAsync();

            return View(incidents);
        }

        // GET: Incidents/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var incident = await _context.Incidents
                .Include(i => i.ActionType)
                .Include(i => i.Endpoint)
                .Include(i => i.Rule)
                .FirstOrDefaultAsync(m => m.IncidentID == id);
            if (incident == null)
            {
                return NotFound();
            }

            return View(incident);
        }

        // Incidents are an append-only forensic record: only the endpoint scanner
        // creates them, and no one (including administrators) can edit or delete
        // them through the UI. They can only be viewed and exported.

        // Shared by Index and ExportPdf so the report always matches the on-screen view.
        private IQueryable<Incident> BuildIncidentQuery(string sort, string? search,
            int? ruleId, int? actionTypeId, DateTime? from, DateTime? to)
        {
            var query = _context.Incidents
                .Include(i => i.Endpoint)
                .Include(i => i.Rule).ThenInclude(r => r.RuleType)
                .Include(i => i.ActionType)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(i => i.FilePath.Contains(search)
                    || (i.MatchedText != null && i.MatchedText.Contains(search)));

            if (ruleId.HasValue)
                query = query.Where(i => i.RuleID == ruleId.Value);

            if (actionTypeId.HasValue)
                query = query.Where(i => i.ActionTypeID == actionTypeId.Value);

            // Dates come from the browser as local calendar days; timestamps are stored
            // in UTC, so convert the day boundaries before comparing.
            if (from.HasValue)
                query = query.Where(i => i.Timestamp >= from.Value.ToUniversalTime());

            if (to.HasValue)
                query = query.Where(i => i.Timestamp < to.Value.AddDays(1).ToUniversalTime());

            return sort == "asc"
                ? query.OrderBy(i => i.Timestamp).ThenBy(i => i.IncidentID)
                : query.OrderByDescending(i => i.Timestamp).ThenByDescending(i => i.IncidentID);
        }

        // GET: Incidents/ExportPdf
        [HttpGet]
        public async Task<IActionResult> ExportPdf(string sort = "desc",
            string? search = null, int? ruleId = null, int? actionTypeId = null,
            DateTime? from = null, DateTime? to = null)
        {
            // Set QuestPDF to Community license (free for open-source / personal use)
            QuestPDF.Settings.License = LicenseType.Community;

            var incidents = await BuildIncidentQuery(sort, search, ruleId, actionTypeId, from, to)
                .ToListAsync();

            // Describe the active filters so the report states its own scope
            var scopeParts = new List<string>();
            if (!string.IsNullOrWhiteSpace(search))
                scopeParts.Add($"search \"{search}\"");
            if (ruleId.HasValue)
            {
                var ruleName = await _context.Rules
                    .Where(r => r.RuleID == ruleId.Value)
                    .Select(r => r.RuleName)
                    .FirstOrDefaultAsync();
                scopeParts.Add($"rule: {ruleName ?? $"#{ruleId.Value}"}");
            }
            if (actionTypeId.HasValue)
            {
                var actionName = await _context.ActionTypes
                    .Where(a => a.ActionTypeID == actionTypeId.Value)
                    .Select(a => a.ActionName)
                    .FirstOrDefaultAsync();
                scopeParts.Add($"action: {actionName ?? $"#{actionTypeId.Value}"}");
            }
            if (from.HasValue)
                scopeParts.Add($"from {from.Value:MMM dd, yyyy}");
            if (to.HasValue)
                scopeParts.Add($"to {to.Value:MMM dd, yyyy}");

            string filterScope = scopeParts.Count > 0
                ? string.Join("  •  ", scopeParts)
                : "All incidents (unfiltered)";

            // Resolve the currently logged-in user's full name
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            string generatedBy = "LeakGuard System";
            if (int.TryParse(userIdClaim, out int userId))
            {
                var user = await _context.Users.FindAsync(userId);
                if (user != null)
                    generatedBy = $"{user.FullName} ({user.Email})";
            }

            var document = new IncidentReportDocument(incidents, generatedBy, DateTime.UtcNow, filterScope);
            var pdfBytes = document.GeneratePdf();

            var fileName = $"LeakGuard_Incidents_{DateTime.UtcNow:yyyyMMdd_HHmmss}.pdf";
            return File(pdfBytes, "application/pdf", fileName);
        }
    }
}
