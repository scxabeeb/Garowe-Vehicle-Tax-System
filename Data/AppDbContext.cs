
using Microsoft.EntityFrameworkCore;
using VehicleTax.Web.Models;
namespace VehicleTax.Web.Data;
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }
    public DbSet<User> Users => Set<User>();
    public DbSet<Vehicle> Vehicles => Set<Vehicle>();
    public DbSet<CarType> CarTypes => Set<CarType>();
    public DbSet<TaxAmount> TaxAmounts => Set<TaxAmount>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Movement> Movements => Set<Movement>();
    public DbSet<ReceiptReference> ReceiptReferences => Set<ReceiptReference>();


}
