using Microsoft.EntityFrameworkCore;
using VehicleTax.Web.Models;

namespace VehicleTax.Web.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options)
        {
        }

        public DbSet<User> Users => Set<User>();
        public DbSet<Vehicle> Vehicles => Set<Vehicle>();
        public DbSet<CarType> CarTypes => Set<CarType>();
        public DbSet<TaxAmount> TaxAmounts => Set<TaxAmount>();
        public DbSet<Checkpoint> Checkpoints => Set<Checkpoint>();
        public DbSet<Payment> Payments => Set<Payment>();
        public DbSet<Movement> Movements => Set<Movement>();
        public DbSet<ReceiptReference> ReceiptReferences => Set<ReceiptReference>();
        public DbSet<RevenueAccount> RevenueAccounts => Set<RevenueAccount>();
        public DbSet<GolisAudit> GolisAudits => Set<GolisAudit>();
        public DbSet<GolisTransaction> GolisTransactions => Set<GolisTransaction>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<User>()
                .HasIndex(u => u.Username)
                .HasDatabaseName("IX_Users_Username");

            modelBuilder.Entity<Checkpoint>()
                .HasIndex(c => c.Name)
                .IsUnique();

            // Collector → Checkpoint relation (reverse navigation via Users collection)
            modelBuilder.Entity<User>()
                .HasOne(u => u.Checkpoint)
                .WithMany(c => c.Users)
                .HasForeignKey(u => u.CheckpointId)
                .OnDelete(DeleteBehavior.SetNull);

            // Payment → Collector (User) relation
            modelBuilder.Entity<Payment>()
                .HasOne(p => p.Collector)
                .WithMany()
                .HasForeignKey(p => p.CollectorId)
                .OnDelete(DeleteBehavior.Restrict);

            // Payment → Checkpoint relation (snapshot of checkpoint at payment time)
            // Ensures historical payments stay attributed to the original checkpoint
            // even when a collector is later reassigned to a different checkpoint.
            modelBuilder.Entity<Payment>()
                .HasOne(p => p.Checkpoint)
                .WithMany()
                .HasForeignKey(p => p.CheckpointId)
                .OnDelete(DeleteBehavior.SetNull);

            // Payment → Vehicle relation
            modelBuilder.Entity<Payment>()
                .HasOne(p => p.Vehicle)
                .WithMany()
                .HasForeignKey(p => p.VehicleId)
                .OnDelete(DeleteBehavior.Restrict);

            // Payment → Movement relation
            modelBuilder.Entity<Payment>()
                .HasOne(p => p.Movement)
                .WithMany()
                .HasForeignKey(p => p.MovementId)
                .OnDelete(DeleteBehavior.Restrict);

            // Payment → ReceiptReference relation
            modelBuilder.Entity<Payment>()
                .HasOne(p => p.ReceiptReference)
                .WithMany()
                .HasForeignKey(p => p.ReceiptReferenceId)
                .OnDelete(DeleteBehavior.Restrict);

            // ✅ RevenueAccount → Movements (one-to-many)
            modelBuilder.Entity<RevenueAccount>()
                .HasKey(r => r.Id);

            modelBuilder.Entity<RevenueAccount>()
                .HasIndex(r => r.AccountCode)
                .IsUnique();

            modelBuilder.Entity<Movement>()
                .HasOne(m => m.RevenueAccount)
                .WithMany(r => r.Movements)
                .HasForeignKey(m => m.RevenueAccountId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Movement>()
                .HasIndex(m => m.RevenueAccountId);

            // GolisAudit → CreatedByUser relation
            modelBuilder.Entity<GolisAudit>()
                .HasOne(a => a.CreatedByUser)
                .WithMany()
                .HasForeignKey(a => a.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            // GolisAudit → FinalizedByUser relation
            modelBuilder.Entity<GolisAudit>()
                .HasOne(a => a.FinalizedByUser)
                .WithMany()
                .HasForeignKey(a => a.FinalizedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            // GolisTransaction → GolisAudit relation
            modelBuilder.Entity<GolisTransaction>()
                .HasOne(t => t.GolisAudit)
                .WithMany(a => a.GolisTransactions)
                .HasForeignKey(t => t.GolisAuditId)
                .OnDelete(DeleteBehavior.Cascade);

            // GolisTransaction → EnteredByUser relation
            modelBuilder.Entity<GolisTransaction>()
                .HasOne(t => t.EnteredByUser)
                .WithMany()
                .HasForeignKey(t => t.EnteredByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            // GolisTransaction → ReviewedByUser relation
            modelBuilder.Entity<GolisTransaction>()
                .HasOne(t => t.ReviewedByUser)
                .WithMany()
                .HasForeignKey(t => t.ReviewedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            // GolisTransaction → MatchedPayment relation
            modelBuilder.Entity<GolisTransaction>()
                .HasOne(t => t.MatchedPayment)
                .WithMany()
                .HasForeignKey(t => t.MatchedPaymentId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<GolisTransaction>()
                .HasIndex(t => t.GolisTransactionReference);

            modelBuilder.Entity<GolisTransaction>()
                .HasIndex(t => new { t.GolisAuditId, t.ReconciliationStatus });
        }
    }
}
