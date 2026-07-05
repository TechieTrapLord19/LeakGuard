using Microsoft.EntityFrameworkCore;
using LeakGuard.Models;

namespace LeakGuard.Data
{
    public class LeakGuardDbContext : DbContext
    {
        public LeakGuardDbContext(DbContextOptions<LeakGuardDbContext> options) : base(options)
        {
        }

        public DbSet<Role> Roles { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<RuleType> RuleTypes { get; set; }
        public DbSet<Rule> Rules { get; set; }
        public DbSet<LeakGuard.Models.Endpoint> Endpoints { get; set; }
        public DbSet<ActionType> ActionTypes { get; set; }
        public DbSet<Incident> Incidents { get; set; }
        public DbSet<AuditLog> AuditLogs { get; set; }
        public DbSet<MonitoredDirectory> MonitoredDirectories { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            
            // Adding a few relationships explicitly just to be safe.
            modelBuilder.Entity<Rule>()
                .HasOne(r => r.Creator)
                .WithMany()
                .HasForeignKey(r => r.CreatedBy)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<AuditLog>()
                .HasOne(al => al.User)
                .WithMany()
                .HasForeignKey(al => al.UserID)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
