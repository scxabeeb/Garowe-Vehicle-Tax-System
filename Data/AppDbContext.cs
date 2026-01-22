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
        public DbSet<Payment> Payments => Set<Payment>();
        public DbSet<Movement> Movements => Set<Movement>();
        public DbSet<ReceiptReference> ReceiptReferences => Set<ReceiptReference>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Payment → Collector (User) relation
            modelBuilder.Entity<Payment>()
                .HasOne(p => p.Collector)
                .WithMany()
                .HasForeignKey(p => p.CollectorId)
                .OnDelete(DeleteBehavior.Restrict);

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
        }
    }
}
