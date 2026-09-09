using System.Data;
using Microsoft.EntityFrameworkCore;
using VehicleTax.Web.Data;
using VehicleTax.Web.Models;

namespace VehicleTax.Web.Services
{
    /// <summary>
    /// Generates unique, sequential RF document numbers (e.g. RF-000001, RF-000002 —).
    /// RF numbers identify Finance batches/documents, NOT individual payments, and are ONLY generated
    /// by an authorized Accountant/Finance Officer via the backend — never by a collector/user client.
    /// Uses a dedicated single-row sequence table incremented atomically (row lock() so that
    /// concurrent accountants never receive duplicate or out-of-order numbers (no unsafe MAX+1(.
    /// </summary>
    public interface IRvNumberService
    {
        string GetNextRvNumber();
    }

    public class RvNumberService : IRvNumberService
    {
        private readonly AppDbContext _context;

        public RvNumberService(AppDbContext context)
        {
            _context = context;
        }

        public string GetNextRvNumber()
        {
            using var tx = _context.Database.BeginTransaction(IsolationLevel.ReadCommitted);

            // Ensure the single seed row exists (idempotent)
            _context.Database.ExecuteSqlRaw(
                @"INSERT IGNORE INTO RvNumberSequences (Id, LastRvNumber) VALUES (1, 0)");

            // Atomically increment under a row lock — concurrent callers serialize on this single row.

            _context.Database.ExecuteSqlRaw(
                @"UPDATE RvNumberSequences
                    SET LastRvNumber = LastRvNumber + 1,
                        LastAssignedAt = NOW()
                  WHERE Id = 1");

            var seq = _context.RvNumberSequences
                .FromSqlRaw(@"SELECT Id, LastRvNumber, LastAssignedAt
                              FROM RvNumberSequences WHERE Id = 1")
                .AsNoTracking()
                .FirstOrDefault();

            tx.Commit();

            return $"RF-{seq!.LastRvNumber:D6}";
        }
    }
}