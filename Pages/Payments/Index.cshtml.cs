using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using VehicleTax.Web;
using VehicleTax.Web.Data;
using VehicleTax.Web.Models;

namespace VehicleTax.Web.Pages.Payments;

[Authorize]
public class IndexModel : PageModel
{
    private readonly AppDbContext _context;

    public List<PaymentRecord> Payments { get; set; } = new();

    // Pagination
    public int PageIndex { get; set; } = 1;
    public int TotalPages { get; set; }
    public int PageSize { get; set; } = 10;

    // Filters
    public string? SearchPlate { get; set; }
    public string? PaymentStatus { get; set; } = "all"; // all, paid, pending, reverted
    public DateTime? DateFrom { get; set; }
    public DateTime? DateTo { get; set; }

    public int TotalRecords { get; set; }

    public IndexModel(AppDbContext context)
    {
        _context = context;
    }

    public async Task OnGetAsync(int pageIndex = 1, int pageSize = 10, string? searchPlate = null, string? paymentStatus = "all", DateTime? dateFrom = null, DateTime? dateTo = null)
    {
        PageIndex = pageIndex;
        PageSize = pageSize;
        SearchPlate = searchPlate;
        PaymentStatus = paymentStatus ?? "all";
        DateFrom = dateFrom;
        DateTo = dateTo;

        // Build query
        var query = _context.Payments
            .Include(p => p.Vehicle)
            .Include(p => p.Collector)
            .Include(p => p.Movement)
            .AsQueryable();

        // Apply filters
        if (!string.IsNullOrWhiteSpace(SearchPlate))
        {
            query = query.Where(p => p.Vehicle != null && p.Vehicle.PlateNumber.Contains(SearchPlate));
        }

        if (PaymentStatus == "paid")
        {
            query = query.Where(p => p.IsPaid && !p.IsReverted);
        }
        else if (PaymentStatus == "pending")
        {
            query = query.Where(p => !p.IsPaid && !p.IsReverted);
        }
        else if (PaymentStatus == "reverted")
        {
            query = query.Where(p => p.IsReverted);
        }

        if (DateFrom.HasValue)
        {
            query = query.Where(p => p.PaidAt >= DateFrom.Value);
        }

        if (DateTo.HasValue)
        {
            var endOfDay = DateTo.Value.AddDays(1);
            query = query.Where(p => p.PaidAt < endOfDay);
        }

        TotalRecords = await query.CountAsync();

        List<Payment> pagePayments;

        if (PageSize == -1) // -1 means ALL
        {
            TotalPages = 1;
            pagePayments = await query
                .OrderByDescending(p => p.PaidAt)
                .ToListAsync();
        }
        else
        {
            TotalPages = (int)Math.Ceiling(TotalRecords / (double)PageSize);

            pagePayments = await query
                .OrderByDescending(p => p.PaidAt)
                .Skip((PageIndex - 1) * PageSize)
                .Take(PageSize)
                .ToListAsync();
        }

        Payments = new List<PaymentRecord>(pagePayments.Count);
        foreach (var p in pagePayments)
        {
            var receiptNumber = await BuildReceiptNumberAsync(p.Id, p.PaidAt, p.IsPaid, p.IsReverted, p.ReceiptReferenceId);
            Payments.Add(new PaymentRecord
            {
                Id = p.Id,
                InvoiceId = p.InvoiceNumber,
                GolisBillNo = p.TransactionId,
                PlateNumber = p.Vehicle?.PlateNumber ?? "N/A",
                OwnerName = p.Vehicle?.OwnerName ?? "N/A",
                Amount = p.Amount,
                PaidAt = p.PaidAt,
                MovementType = p.MovementType,
                CollectorName = p.Collector != null ? p.Collector.Username : "N/A",
                PaymentStatus = p.IsReverted ? "Reverted" : (p.IsPaid ? "Paid" : "Pending"),
                ReceiptNumber = receiptNumber,
                IsReverted = p.IsReverted,
                IsPaid = p.IsPaid
            });
        }
    }

    private async Task<string> BuildReceiptNumberAsync(int paymentId, DateTime paidAtUtc, bool isPaid, bool isReverted, int? receiptReferenceId)
    {
        if ((!isPaid || isReverted) && receiptReferenceId == null)
        {
            return "-";
        }

        var localPaidAt = AppTime.ToLocal(paidAtUtc);
        var dayStartLocal = localPaidAt.Date;
        var dayEndLocal = dayStartLocal.AddDays(1);

        var dayStartUtc = AppTime.ToUtc(dayStartLocal);
        var dayEndUtc = AppTime.ToUtc(dayEndLocal);

        var serial = await _context.Payments
            .CountAsync(x =>
                x.PaidAt >= dayStartUtc &&
                x.PaidAt < dayEndUtc &&
                x.Id <= paymentId &&
                ((x.IsPaid && !x.IsReverted) || x.ReceiptReferenceId != null));

        return $"{localPaidAt:yyMMdd}{serial:D2}";
    }

    public class PaymentRecord
    {
        public int Id { get; set; }
        public string InvoiceId { get; set; } = string.Empty;
        public string? GolisBillNo { get; set; }
        public string PlateNumber { get; set; } = string.Empty;
        public string OwnerName { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public DateTime PaidAt { get; set; }
        public string MovementType { get; set; } = string.Empty;
        public string CollectorName { get; set; } = string.Empty;
        public string PaymentStatus { get; set; } = string.Empty;
        public string ReceiptNumber { get; set; } = string.Empty;
        public bool IsReverted { get; set; }
        public bool IsPaid { get; set; }
    }
}
