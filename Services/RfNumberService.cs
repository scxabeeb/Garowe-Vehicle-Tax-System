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
    public interface IRfNumberService
    {
        string GetNextRfNumber();
    }

    public class RfNumberService : IRfNumberService
    {
        private readonly AppDbContext _context;

        public RfNumberService(AppDbContext context)
        {
            _context = context;
        }

        public string GetNextRfNumber()
        {
            using var tx = _context.Database.BeginTransaction(IsolationLevel.ReadCommitted);

            // Ensure the single seed row exists (idempotent)
            _context.Database.ExecuteSqlRaw(
                @"INSERT IGNORE INTO RfNumberSequences (Id, LastRfNumber) VALUES (1, 0)");

            // Atomically increment under a row lock — concurrent callers serialize on this single row.

            _context.Database.ExecuteSqlRaw(
                @"UPDATE RfNumberSequences
                    SET LastRfNumber = LastRfNumber + 1,
                        LastAssignedAt = NOW()
                  WHERE Id = 1");

            var seq = _context.RfNumberSequences
                .FromSqlRaw(@"SELECT Id, LastRfNumber, LastAssignedAt
                              FROM RfNumberSequences WHERE Id = 1")
                .AsNoTracking()
                .FirstOrDefault();

            tx.Commit();

            return $"RF-{seq!.LastRfNumber:D6}";
        }
    }
}