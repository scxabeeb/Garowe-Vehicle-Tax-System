using Microsoft.EntityFrameworkCore;
using VehicleTax.Web.Models;

namespace VehicleTax.Web.Services;

/// <summary>One payment line in a report's Details view.</summary>
public record PaymentDetailRow(
    int SystemId,
    DateTime PaidAt,
    string Plate,
    string Owner,
    string Traffida,
    string CarType,
    string Collector,
    string Checkpoint,
    string InvoiceNumber,
    string? GolisBillNo,
    decimal Amount,
    string Status);

/// <summary>One group line (e.g. one traffida) in a report's Summary view.</summary>
public record SummaryGroupRow(string Name, int Count, decimal Amount);

/// <summary>A titled list of counts for a record report's Summary view.</summary>
public record CountGroup(string Title, string Icon, List<SummaryGroupRow> Rows);

/// <summary>Paid totals of a report's payments, grouped several ways.</summary>
public class PaymentSummaryBreakdown
{
    public int PaidCount { get; init; }
    public decimal PaidAmount { get; init; }
    public List<SummaryGroupRow> ByTraffida { get; init; } = new();
    public List<SummaryGroupRow> ByCollector { get; init; } = new();
    public List<SummaryGroupRow> ByCheckpoint { get; init; } = new();
    public List<SummaryGroupRow> ByCarType { get; init; } = new();
}

/// <summary>
/// Loads the payment-level Details view for a report from the report's own
/// filtered query, so Summary and Details always cover the same payments.
/// </summary>
public static class ReportDetails
{
    public class NameAmount
    {
        public string? Name { get; set; }
        public decimal Amount { get; set; }
    }

    /// <summary>Upper bound on rows rendered in the page (exports are not limited).</summary>
    public const int MaxRows = 2000;

    public static async Task<List<PaymentDetailRow>> LoadAsync(IQueryable<Payment> query)
    {
        var payments = await query
            .Include(p => p.Vehicle).ThenInclude(v => v!.CarType)
            .Include(p => p.Movement)
            .Include(p => p.Collector)
            .Include(p => p.Checkpoint)
            .AsNoTracking()
            .OrderByDescending(p => p.PaidAt)
            .Take(MaxRows)
            .ToListAsync();

        return payments.Select(ToRow).ToList();
    }

    public static PaymentDetailRow ToRow(Payment p) => new(
        p.Id,
        AppTime.ToLocal(p.PaidAt),
        p.Vehicle?.PlateNumber ?? "-",
        p.Vehicle?.OwnerName ?? "-",
        p.Movement?.Name ?? p.MovementType,
        p.Vehicle?.CarType?.Name ?? "-",
        p.Collector?.Username ?? "-",
        p.Checkpoint?.Name ?? "-",
        p.InvoiceNumber,
        p.TransactionId,
        p.Amount,
        p.IsReverted ? "Cancelled" : (p.IsPaid ? "Paid" : "Pending"));

    /// <summary>
    /// Summary breakdown of the report's paid (not cancelled) payments,
    /// grouped in the database so it covers every matching payment.
    /// </summary>
    public static async Task<PaymentSummaryBreakdown> SummarizeAsync(IQueryable<Payment> query, int top = 10)
    {
        var paid = query.Where(p => p.IsPaid && !p.IsReverted);

        async Task<List<SummaryGroupRow>> Group(IQueryable<NameAmount> rows) =>
            (await rows.ToListAsync())
                .GroupBy(r => string.IsNullOrWhiteSpace(r.Name) ? "-" : r.Name!)
                .Select(g => new SummaryGroupRow(g.Key, g.Count(), g.Sum(r => r.Amount)))
                .OrderByDescending(g => g.Amount)
                .Take(top)
                .ToList();

        return new PaymentSummaryBreakdown
        {
            PaidCount = await paid.CountAsync(),
            PaidAmount = await paid.SumAsync(p => (decimal?)p.Amount) ?? 0m,
            ByTraffida = await Group(paid.Select(p => new NameAmount { Name = p.Movement != null ? p.Movement.Name : p.MovementType, Amount = p.Amount })),
            ByCollector = await Group(paid.Select(p => new NameAmount { Name = p.Collector != null ? p.Collector.Username : "-", Amount = p.Amount })),
            ByCheckpoint = await Group(paid.Select(p => new NameAmount { Name = p.Checkpoint != null ? p.Checkpoint.Name : "-", Amount = p.Amount })),
            ByCarType = await Group(paid.Select(p => new NameAmount { Name = p.Vehicle != null && p.Vehicle.CarType != null ? p.Vehicle.CarType.Name : "-", Amount = p.Amount })),
        };
    }

    /// <summary>Counts items per name, largest first (for record reports).</summary>
    public static List<SummaryGroupRow> CountBy<T>(IEnumerable<T> items, Func<T, string?> name, int top = 12) =>
        items.GroupBy(i => string.IsNullOrWhiteSpace(name(i)) ? "-" : name(i)!)
            .Select(g => new SummaryGroupRow(g.Key, g.Count(), 0m))
            .OrderByDescending(g => g.Count)
            .Take(top)
            .ToList();
}
