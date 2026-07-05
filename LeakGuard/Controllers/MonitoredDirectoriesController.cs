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
    public class MonitoredDirectoriesController : Controller
    {
        private readonly LeakGuardDbContext _context;
        private readonly LeakGuard.Services.IAuditService _auditService;

        public MonitoredDirectoriesController(LeakGuardDbContext context, LeakGuard.Services.IAuditService auditService)
        {
            _context = context;
            _auditService = auditService;
        }

        // GET: MonitoredDirectories
        public async Task<IActionResult> Index()
        {
            return View(await _context.MonitoredDirectories.ToListAsync());
        }

        // GET: MonitoredDirectories/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var monitoredDirectory = await _context.MonitoredDirectories
                .FirstOrDefaultAsync(m => m.MonitoredDirectoryID == id);
            if (monitoredDirectory == null)
            {
                return NotFound();
            }

            return View(monitoredDirectory);
        }

        // GET: MonitoredDirectories/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: MonitoredDirectories/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("MonitoredDirectoryID,DirectoryPath,Status")] MonitoredDirectory monitoredDirectory)
        {
            _context.Add(monitoredDirectory);
            await _context.SaveChangesAsync();
            await _auditService.LogAsync($"Added monitored directory: {monitoredDirectory.DirectoryPath}");
            TempData["Success"] = $"Directory '{monitoredDirectory.DirectoryPath}' is now being monitored.";
            return RedirectToAction(nameof(Index));
        }

        // GET: MonitoredDirectories/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var monitoredDirectory = await _context.MonitoredDirectories.FindAsync(id);
            if (monitoredDirectory == null)
            {
                return NotFound();
            }
            return View(monitoredDirectory);
        }

        // POST: MonitoredDirectories/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("MonitoredDirectoryID,DirectoryPath,Status")] MonitoredDirectory monitoredDirectory)
        {
            if (id != monitoredDirectory.MonitoredDirectoryID) return NotFound();

            try
            {
                _context.Update(monitoredDirectory);
                await _context.SaveChangesAsync();
                await _auditService.LogAsync($"Updated monitored directory: {monitoredDirectory.DirectoryPath} to {monitoredDirectory.Status}");
                TempData["Success"] = $"Directory updated successfully.";
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!MonitoredDirectoryExists(monitoredDirectory.MonitoredDirectoryID)) return NotFound();
                else throw;
            }
            return RedirectToAction(nameof(Index));
        }

        // Directories are never deleted — they are disabled instead, which stops
        // monitoring while keeping the configuration history.

        // POST: MonitoredDirectories/ToggleStatus/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleStatus(int id)
        {
            var monitoredDirectory = await _context.MonitoredDirectories.FindAsync(id);
            if (monitoredDirectory == null) return NotFound();

            monitoredDirectory.Status = monitoredDirectory.Status == "Enabled" ? "Disabled" : "Enabled";
            await _context.SaveChangesAsync();

            await _auditService.LogAsync($"{(monitoredDirectory.Status == "Enabled" ? "Enabled" : "Disabled")} monitored directory: {monitoredDirectory.DirectoryPath}");
            TempData["Success"] = $"Directory '{monitoredDirectory.DirectoryPath}' is now {monitoredDirectory.Status.ToLower()}.";
            return RedirectToAction(nameof(Index));
        }

        private bool MonitoredDirectoryExists(int id)
        {
            return _context.MonitoredDirectories.Any(e => e.MonitoredDirectoryID == id);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult SimulateLeak(string leakContent)
        {
            try
            {
                var testDirPath = System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), "App_Data", "Monitored_Test");
                if (!System.IO.Directory.Exists(testDirPath))
                {
                    System.IO.Directory.CreateDirectory(testDirPath);
                }

                string fileName = $"simulated_leak_{Guid.NewGuid().ToString().Substring(0, 8)}.txt";
                string fullPath = System.IO.Path.Combine(testDirPath, fileName);

                System.IO.File.WriteAllText(fullPath, leakContent);

                // Push directly to the background service's queue to bypass broken FileSystemWatcher events on MonsterASP
                LeakGuard.Services.EndpointScannerService.SimulatorQueue.Enqueue(fullPath);

                TempData["Success"] = $"Simulation File created! Check your incidents dashboard for the alert.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Failed to simulate file: " + ex.Message;
            }

            return RedirectToAction(nameof(Index));
        }
    }
}
