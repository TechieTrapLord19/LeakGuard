using Microsoft.AspNetCore.Identity;
using LeakGuard.Data;
using LeakGuard.Models;

namespace LeakGuard.Services
{
    /// <summary>
    /// Seeds a default Administrator role and admin user on first startup if none exist.
    /// Passwords come from configuration (SeedUsers:AdminPassword / SeedUsers:AnalystPassword)
    /// so deployments can override them without a code change.
    /// </summary>
    public class AuthSeederService : IHostedService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<AuthSeederService> _logger;
        private readonly IConfiguration _configuration;

        public AuthSeederService(IServiceScopeFactory scopeFactory, ILogger<AuthSeederService> logger, IConfiguration configuration)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
            _configuration = configuration;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<LeakGuardDbContext>();

                var adminPassword = _configuration["SeedUsers:AdminPassword"] ?? "LG-Admin#2026!secure";
                var analystPassword = _configuration["SeedUsers:AnalystPassword"] ?? "LG-Analyst#2026!secure";

                // Seed the Administrator role if it doesn't exist
                if (!db.Roles.Any())
                {
                    db.Roles.Add(new Role { RoleName = "Administrator", Description = "Full system access" });
                    db.Roles.Add(new Role { RoleName = "Analyst", Description = "View-only access to incidents and rules" });
                    await db.SaveChangesAsync();
                    _logger.LogInformation("Seeded default roles.");
                }

                // Seed the default admin user if no users exist
                if (!db.Users.Any())
                {
                    var adminRole = db.Roles.First(r => r.RoleName == "Administrator");
                    var hasher = new PasswordHasher<User>();
                    var adminUser = new User
                    {
                        RoleID = adminRole.RoleID,
                        FullName = "System Administrator",
                        Email = "admin@admin.com",
                        IsActive = true,
                        PasswordHash = "" // will be set below
                    };
                    adminUser.PasswordHash = hasher.HashPassword(adminUser, adminPassword);
                    db.Users.Add(adminUser);
                    await db.SaveChangesAsync();

                    _logger.LogInformation("Seeded default admin user: admin@admin.com");
                }

                // Seed default analyst user
                if (!db.Users.Any(u => u.Email == "analyst@analyst.com"))
                {
                    var analystRole = db.Roles.First(r => r.RoleName == "Analyst");
                    var hasher = new PasswordHasher<User>();
                    var analystUser = new User
                    {
                        RoleID = analystRole.RoleID,
                        FullName = "Security Analyst",
                        Email = "analyst@analyst.com",
                        IsActive = true,
                        PasswordHash = ""
                    };
                    analystUser.PasswordHash = hasher.HashPassword(analystUser, analystPassword);
                    db.Users.Add(analystUser);
                    await db.SaveChangesAsync();

                    _logger.LogInformation("Seeded default analyst user: analyst@analyst.com");
                }

                // One-time upgrade: accounts still using the old well-known default
                // passwords get re-hashed with the configured ones.
                await UpgradeDefaultPasswordAsync(db, "admin@admin.com", "Admin@123", adminPassword);
                await UpgradeDefaultPasswordAsync(db, "analyst@analyst.com", "Analyst@123", analystPassword);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to seed default authentication data.");
            }
        }

        private async Task UpgradeDefaultPasswordAsync(LeakGuardDbContext db, string email, string oldDefaultPassword, string newPassword)
        {
            if (oldDefaultPassword == newPassword) return;

            var user = db.Users.FirstOrDefault(u => u.Email == email);
            if (user == null) return;

            var hasher = new PasswordHasher<User>();
            if (hasher.VerifyHashedPassword(user, user.PasswordHash, oldDefaultPassword) != PasswordVerificationResult.Failed)
            {
                user.PasswordHash = hasher.HashPassword(user, newPassword);
                await db.SaveChangesAsync();
                _logger.LogWarning("Account {Email} was using a default password; it has been replaced with the configured seed password.", email);
            }
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
