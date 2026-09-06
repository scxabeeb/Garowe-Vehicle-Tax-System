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
        public DbSet<PaymentReferenceSequence> PaymentReferenceSequences => Set<PaymentReferenceSequence>();
        public DbSet<RfDocument> RfDocuments => Set<RfDocument>();
        public DbSet<RfPayment> RfPayments => Set<RfPayment>();
        public DbSet<RfAuditLog> RfAuditLogs => Set<RfAuditLog>();
        public DbSet<RfNumberSequence> RfNumberSequences => Set<RfNumberSequence>();

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

            // ✅ Audit Reference Number — unique and never reused.
            // NULL is allowed for pending / failed / cancelled-before-payment payments;
            // MySQL's UNIQUE index permits multiple NULLs while enforcing uniqueness
            // for assigned (non-null) reference numbers.
            modelBuilder.Entity<Payment>()
                .HasIndex(p => p.ReferenceNo)
                .IsUnique()
                .HasDatabaseName("IX_Payments_ReferenceNo");

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

            // PaymentReferenceSequence — single seed row (Id = 1) holds the running
            // counter. The row is seeded by the AddPaymentReferenceNo migration
            // (INSERT IGNORE) and also defensively via PaymentReferenceService before
            // every increment, so no HasData seed is needed here (avoids a model/snapshot
            // mismatch for future migrations).

            // ── RF / FMIS module ──────────────────────────────────────
            // RF number is unique — never duplicated.
            modelBuilder.Entity<RfDocument>()
                .HasIndex(r => r.RfNumber)
                .IsUnique();

            modelBuilder.Entity<RfPayment>()
                .HasIndex(rp => rp.PaymentId)
                .IsUnique();

            modelBuilder.Entity<RfPayment>()
                .HasOne(rp => rp.Payment)
                .WithMany()
                .HasForeignKey(rp => rp.PaymentId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<RfPayment>()
                .HasOne(rp => rp.RfDocument)
                .WithMany(r => r.Payments)
                .HasForeignKey(rp => rp.RfDocumentId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<RfAuditLog>()
                .HasOne(l => l.RfDocument)
                .WithMany(r => r.AuditLogs)
                .HasForeignKey(l => l.RfDocumentId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<RfAuditLog>()
                .HasOne(l => l.ByUser)
                .WithMany()
                .HasForeignKey(l => l.ByUserId)
                .OnDelete(DeleteBehavior.SetNull);

            // RfNumberSequence — single seed row (Id = 1) holds the running RF counter.
            // Seeded by migration + defensively by RfNumberService before each increment.
        }
    }
}
