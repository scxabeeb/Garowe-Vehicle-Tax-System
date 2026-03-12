using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Text;
using VehicleTax.Web.Data;
using VehicleTax.Web.Models;

namespace VehicleTax.Web.Pages.Reports;

public class ReceiptPaymentsModel : PageModel
{
    private readonly AppDbContext _context;

    public ReceiptPaymentsModel(AppDbContext context)
    {
        _context = context;
    }

    [BindProperty(SupportsGet = true)] public DateTime? FromDate { get; set; }
    [BindProperty(SupportsGet = true)] public DateTime? ToDate { get; set; }
    [BindProperty(SupportsGet = true)] public string? ReceiptFrom { get; set; }
    [BindProperty(SupportsGet = true)] public string? ReceiptTo { get; set; }
    [BindProperty(SupportsGet = true)] public int? MovementId { get; set; }
    [BindProperty(SupportsGet = true)] public int? CollectorId { get; set; }
    [BindProperty(SupportsGet = true)] public string? Search { get; set; }
    [BindProperty(SupportsGet = true)] public int PageSize { get; set; } = 10;
    [BindProperty(SupportsGet = true)] public int CurrentPage { get; set; } = 1;

    public int TotalPages { get; set; }
    public int TotalPayments { get; set; }
    public decimal TotalAmount { get; set; }
    public int TotalVehicles { get; set; }
    public decimal PageTotalAmount { get; set; }

    public int UsedReceipts { get; set; }
    public int AvailableReceipts { get; set; }
    public int CancelledReceipts { get; set; }

    public SelectList Movements { get; set; } = null!;
    public SelectList Collectors { get; set; } = null!;
    public List<Payment> Payments { get; set; } = new();

    private IQueryable<Payment> BuildQuery()
    {
        var q = _context.Payments
            .Where(p => !p.IsReverted && p.ReceiptReferenceId != null)
            .Include(p => p.Vehicle)
            .Include(p => p.Movement)
            .Include(p => p.Collector)
            .Include(p => p.ReceiptReference)
            .AsQueryable();

        if (FromDate.HasValue) q = q.Where(p => p.PaidAt >= FromDate.Value.Date);
        if (ToDate.HasValue) q = q.Where(p => p.PaidAt < ToDate.Value.Date.AddDays(1));
        if (!string.IsNullOrWhiteSpace(ReceiptFrom))
            q = q.Where(p => string.Compare(p.ReceiptReference!.ReferenceNumber, ReceiptFrom) >= 0);
        if (!string.IsNullOrWhiteSpace(ReceiptTo))
            q = q.Where(p => string.Compare(p.ReceiptReference!.ReferenceNumber, ReceiptTo) <= 0);
        if (MovementId.HasValue) q = q.Where(p => p.MovementId == MovementId);
        if (CollectorId.HasValue) q = q.Where(p => p.CollectorId == CollectorId);

        return q.OrderByDescending(x => x.PaidAt);
    }

    public async Task OnGetAsync()
    {
        Movements = new SelectList(await _context.Movements.ToListAsync(), "Id", "Name");
        Collectors = new SelectList(await _context.Users.ToListAsync(), "Id", "Username");

        var q = BuildQuery();

        var count = await q.CountAsync();

        TotalPayments = count;
        TotalAmount = Math.Round(await q.SumAsync(x => (decimal?)x.Amount) ?? 0, 2);
        TotalVehicles = await q.Select(x => x.VehicleId).Distinct().CountAsync();

        TotalPages = PageSize == -1 ? 1 : (int)Math.Ceiling(count / (double)PageSize);

        Payments = PageSize == -1
            ? await q.ToListAsync()
            : await q.Skip((CurrentPage - 1) * PageSize).Take(PageSize).ToListAsync();

        PageTotalAmount = Payments.Sum(x => x.Amount);

        // Cards now depend on filtered data
        UsedReceipts = Payments.Count(x => x.ReceiptReference!.IsUsed);
        CancelledReceipts = Payments.Count(x => x.ReceiptReference!.IsCancelled);
        AvailableReceipts = Payments.Count(x =>
            !x.ReceiptReference!.IsUsed && !x.ReceiptReference!.IsCancelled);
    }

    public async Task<IActionResult> OnGetExportPdfAsync()
    {
        var payments = await BuildQuery().ToListAsync();
        QuestPDF.Settings.License = LicenseType.Community;

        var totalAmount = payments.Sum(x => x.Amount);
        var totalVehicles = payments.Select(x => x.VehicleId).Distinct().Count();
        var totalPayments = payments.Count;

        var doc = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(20);

                page.Header().AlignCenter().Text("Receipt Payments Report")
                    .FontSize(18)
                    .SemiBold();

                page.Content().Table(table =>
                {
                    table.ColumnsDefinition(c =>
                    {
                        for (int i = 0; i < 8; i++)
                            c.RelativeColumn();
                    });

                    string[] headers = { "Date", "Receipt", "Plate", "Owner", "Mobile", "Movement", "Collector", "Amount" };

                    foreach (var h in headers)
                        table.Cell().Background(Colors.Grey.Lighten2).Padding(5).Text(h).SemiBold();

                    foreach (var p in payments)
                    {
                        table.Cell().Padding(5).Text(p.PaidAt.ToString("yyyy-MM-dd"));
                        table.Cell().Padding(5).Text(p.ReceiptReference!.ReferenceNumber);
                        table.Cell().Padding(5).Text(p.Vehicle?.PlateNumber ?? "");
                        table.Cell().Padding(5).Text(p.Vehicle?.OwnerName ?? "");
                        table.Cell().Padding(5).Text(p.Vehicle?.Mobile ?? "");
                        table.Cell().Padding(5).Text(p.Movement?.Name ?? "");
                        table.Cell().Padding(5).Text(p.Collector?.Username ?? "");
                        table.Cell().Padding(5).AlignRight().Text(p.Amount.ToString("0.##"));
                    }

                    table.Cell().ColumnSpan(6).Padding(6)
                        .AlignRight()
                        .Text($"TOTAL PAYMENTS: {totalPayments}   |   UNIQUE VEHICLES: {totalVehicles}")
                        .SemiBold();

                    table.Cell().Padding(6)
                        .Text("TOTAL AMOUNT")
                        .SemiBold();

                    table.Cell().Padding(6)
                        .AlignRight()
                        .Text(totalAmount.ToString("0.##"))
                        .SemiBold();
                });
            });
        });

        using var ms = new MemoryStream();
        doc.GeneratePdf(ms);

        return File(ms.ToArray(), "application/pdf", "ReceiptPaymentsReport.pdf");
    }
}