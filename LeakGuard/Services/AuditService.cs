using System.Security.Claims;
using LeakGuard.Data;
using LeakGuard.Models;

namespace LeakGuard.Services
{
    /// <summary>
    /// Service for writing audit log entries when admin actions are performed.
    /// </summary>
    public interface IAuditService
    {
        Task LogAsync(string activity, int? userId = null);
    }

    public class AuditService : IAuditService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger<AuditService> _logger;

        public AuditService(IServiceScopeFactory scopeFactory, IHttpContextAccessor httpContextAccessor, ILogger<AuditService> logger)
        {
            _scopeFactory = scopeFactory;
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;
        }

        public async Task LogAsync(string activity, int? userId = null)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<LeakGuardDbContext>();

                // Try to get the currently logged-in user's ID from their claims
                int resolvedUserId = userId ?? 0;
                if (resolvedUserId == 0)
                {
                    var userIdClaim = _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                    if (int.TryParse(userIdClaim, out int parsedId))
                        resolvedUserId = parsedId;
                }

                // Only log if we have a valid user
                if (resolvedUserId > 0 && db.Users.Any(u => u.UserID == resolvedUserId))
                {
                    var ip = _httpContextAccessor.HttpContext?.Connection?.RemoteIpAddress?.ToString() ?? "unknown";
                    db.AuditLogs.Add(new AuditLog
                    {
                        UserID = resolvedUserId,
                        Activity = activity,
                        IPAddress = ip,
                        LogTime = DateTime.UtcNow
                    });
                    await db.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Audit log failed: {ex.Message}");
            }
        }
    }
}
