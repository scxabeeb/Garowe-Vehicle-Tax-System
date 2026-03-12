using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
<<<<<<< HEAD
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
=======
>>>>>>> 7b24e7a6af078c71b19b1652f4befd2904f706dd
using System.Text;
using VehicleTax.Web.Data;
using VehicleTax.Web.Models;

namespace VehicleTax.Web.Pages.Reports
{
    public class CollectorSummary
    {
        public string CollectorName { get; set; } = "";
        public decimal TotalAmount { get; set; }
        public int TotalPayments { get; set; }
    }

    public class IndexModel : PageModel
    {
        private readonly AppDbContext _context;
<<<<<<< HEAD
        public IndexModel(AppDbContext context) => _context = context;
=======

        public IndexModel(AppDbContext context)
        {
            _context = context;
        }
>>>>>>> 7b24e7a6af078c71b19b1652f4befd2904f706dd

        // Filters
        [BindProperty(SupportsGet = true)] public DateTime? FromDate { get; set; }
        [BindProperty(SupportsGet = true)] public DateTime? ToDate { get; set; }
        [BindProperty(SupportsGet = true)] public string? PlateNumber { get; set; }
<<<<<<< HEAD
        [BindProperty(SupportsGet = true)] public string? ReceiptNumber { get; set; }
        [BindProperty(SupportsGet = true)] public int? CarTypeId { get; set; }
        [BindProperty(SupportsGet = true)] public int? MovementId { get; set; }
        [BindProperty(SupportsGet = true)] public int? CollectorId { get; set; }
        [BindProperty(SupportsGet = true)] public string? Quick { get; set; }

        // Pagination
        [BindProperty(SupportsGet = true)] public int PageSize { get; set; } = 20;
=======
        [BindProperty(SupportsGet = true)] public int? CarTypeId { get; set; }
        [BindProperty(SupportsGet = true)] public int? MovementId { get; set; }
        [BindProperty(SupportsGet = true)] public int? CollectorId { get; set; }

        // Pagination
        [BindProperty(SupportsGet = true)] public int PageSize { get; set; } = 10;
>>>>>>> 7b24e7a6af078c71b19b1652f4befd2904f706dd
        [BindProperty(SupportsGet = true)] public int CurrentPage { get; set; } = 1;
        public int TotalPages { get; set; }

        // Data
        public List<Payment> Payments { get; set; } = new();
<<<<<<< HEAD
        public List<CollectorSummary> CollectorSummaries { get; set; } = new();

        public SelectList CarTypes { get; set; } = null!;
        public SelectList Movements { get; set; } = null!;
        public SelectList Collectors { get; set; } = null!;

        // Totals for cards
=======
        public SelectList CarTypes { get; set; } = null!;
        public SelectList Movements { get; set; } = null!;
        public SelectList Collectors { get; set; } = null!;
        public List<CollectorSummary> CollectorSummaries { get; set; } = new();

        // Totals
>>>>>>> 7b24e7a6af078c71b19b1652f4befd2904f706dd
        public int TotalVehicles { get; set; }
        public int TotalPayments { get; set; }
        public decimal TotalAmount { get; set; }

        public async Task OnGetAsync()
        {
<<<<<<< HEAD
            if (Quick == "today")
            {
                FromDate = DateTime.Today;
                ToDate = DateTime.Today;
            }
            else if (Quick == "month")
            {
                FromDate = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
                ToDate = DateTime.Today;
            }

            CarTypes = new SelectList(await _context.CarTypes.OrderBy(x => x.Name).ToListAsync(), "Id", "Name");

            Movements = new SelectList(await _context.Movements
                .GroupBy(m => m.Name)
                .Select(g => new { Id = g.Min(x => x.Id), Name = g.Key })
                .OrderBy(x => x.Name)
                .ToListAsync(), "Id", "Name");

            Collectors = new SelectList(await _context.Users.OrderBy(u => u.Username).ToListAsync(), "Id", "Username");

            var query = BuildQuery();

            TotalPayments = await query.CountAsync();
            TotalAmount = await query.SumAsync(x => (decimal?)x.Amount) ?? 0;
            TotalVehicles = await query.Select(x => x.VehicleId).Distinct().CountAsync();

            CollectorSummaries = await query
                .Where(x => x.Collector != null)
                .GroupBy(x => x.Collector!.Username)
=======
            // Car Types
            CarTypes = new SelectList(
                await _context.CarTypes
                    .AsNoTracking()
                    .OrderBy(c => c.Name)
                    .ToListAsync(),
                "Id",
                "Name"
            );

            // Movements (UNIQUE BY NAME, even if many vehicles/payments use it)
            Movements = new SelectList(
                await _context.Movements
                    .AsNoTracking()
                    .GroupBy(m => m.Name)
                    .Select(g => new
                    {
                        Id = g.Min(x => x.Id),   // take one Id per movement name
                        Name = g.Key
                    })
                    .OrderBy(x => x.Name)
                    .ToListAsync(),
                "Id",
                "Name"
            );

            // Collectors
            Collectors = new SelectList(
                await _context.Users
                    .AsNoTracking()
                    .OrderBy(u => u.Username)
                    .ToListAsync(),
                "Id",
                "Username"
            );

            var query = _context.Payments
                .Where(p => !p.IsReverted)
                .Include(p => p.Vehicle).ThenInclude(v => v.CarType)
                .Include(p => p.Movement)
                .Include(p => p.ReceiptReference)
                .Include(p => p.Collector)
                .AsQueryable();

            if (FromDate.HasValue)
                query = query.Where(p => p.PaidAt >= FromDate.Value.Date);

            if (ToDate.HasValue)
                query = query.Where(p => p.PaidAt < ToDate.Value.Date.AddDays(1));

            if (!string.IsNullOrWhiteSpace(PlateNumber))
                query = query.Where(p => p.Vehicle != null && p.Vehicle.PlateNumber.Contains(PlateNumber));

            if (CarTypeId.HasValue)
                query = query.Where(p => p.Vehicle != null && p.Vehicle.CarTypeId == CarTypeId.Value);

            if (MovementId.HasValue)
                query = query.Where(p => p.MovementId == MovementId.Value);

            if (CollectorId.HasValue)
                query = query.Where(p => p.CollectorId == CollectorId.Value);

            // Totals
            TotalPayments = await query.CountAsync();
            TotalAmount = await query.SumAsync(p => (decimal?)p.Amount) ?? 0;
            TotalVehicles = await query.Select(p => p.VehicleId).Distinct().CountAsync();

            // Collector summary
            CollectorSummaries = await query
                .Where(p => p.Collector != null)
                .GroupBy(p => p.Collector!.Username)
>>>>>>> 7b24e7a6af078c71b19b1652f4befd2904f706dd
                .Select(g => new CollectorSummary
                {
                    CollectorName = g.Key,
                    TotalPayments = g.Count(),
                    TotalAmount = g.Sum(x => x.Amount)
                })
                .OrderByDescending(x => x.TotalAmount)
                .ToListAsync();

<<<<<<< HEAD
            var count = await query.CountAsync();
            TotalPages = (int)Math.Ceiling(count / (double)PageSize);

            Payments = await query
                .OrderByDescending(x => x.PaidAt)
                .Skip((CurrentPage - 1) * PageSize)
                .Take(PageSize)
                .ToListAsync();
        }

        private IQueryable<Payment> BuildQuery()
        {
            var q = _context.Payments
=======
            var totalCount = await query.CountAsync();

            // Pagination
            if (PageSize == -1)
            {
                TotalPages = 1;
                Payments = await query
                    .OrderByDescending(p => p.PaidAt)
                    .ToListAsync();
            }
            else
            {
                TotalPages = (int)Math.Ceiling(totalCount / (double)PageSize);

                Payments = await query
                    .OrderByDescending(p => p.PaidAt)
                    .Skip((CurrentPage - 1) * PageSize)
                    .Take(PageSize)
                    .ToListAsync();
            }
        }

        public async Task<IActionResult> OnGetExportExcelAsync()
        {
            var query = _context.Payments
>>>>>>> 7b24e7a6af078c71b19b1652f4befd2904f706dd
                .Where(p => !p.IsReverted)
                .Include(p => p.Vehicle).ThenInclude(v => v.CarType)
                .Include(p => p.Movement)
                .Include(p => p.ReceiptReference)
                .Include(p => p.Collector)
                .AsQueryable();

            if (FromDate.HasValue)
<<<<<<< HEAD
                q = q.Where(p => p.PaidAt >= FromDate.Value.Date);

            if (ToDate.HasValue)
                q = q.Where(p => p.PaidAt < ToDate.Value.Date.AddDays(1));

            if (!string.IsNullOrWhiteSpace(PlateNumber))
                q = q.Where(p => p.Vehicle!.PlateNumber.Contains(PlateNumber));

            if (!string.IsNullOrWhiteSpace(ReceiptNumber))
                q = q.Where(p => p.ReceiptReference!.ReferenceNumber.Contains(ReceiptNumber));

            if (CarTypeId.HasValue)
                q = q.Where(p => p.Vehicle!.CarTypeId == CarTypeId);

            if (MovementId.HasValue)
                q = q.Where(p => p.MovementId == MovementId);

            if (CollectorId.HasValue)
                q = q.Where(p => p.CollectorId == CollectorId);

            return q;
        }

        // CSV EXPORT WITH TOTAL
        public async Task<IActionResult> OnGetExportExcelAsync()
        {
            var payments = await BuildQuery().OrderByDescending(p => p.PaidAt).ToListAsync();
            var total = payments.Sum(p => p.Amount);
=======
                query = query.Where(p => p.PaidAt >= FromDate.Value.Date);

            if (ToDate.HasValue)
                query = query.Where(p => p.PaidAt < ToDate.Value.Date.AddDays(1));

            if (!string.IsNullOrWhiteSpace(PlateNumber))
                query = query.Where(p => p.Vehicle != null && p.Vehicle.PlateNumber.Contains(PlateNumber));

            if (CarTypeId.HasValue)
                query = query.Where(p => p.Vehicle != null && p.Vehicle.CarTypeId == CarTypeId.Value);

            if (MovementId.HasValue)
                query = query.Where(p => p.MovementId == MovementId.Value);

            if (CollectorId.HasValue)
                query = query.Where(p => p.CollectorId == CollectorId.Value);

            var payments = await query
                .OrderByDescending(p => p.PaidAt)
                .ToListAsync();
>>>>>>> 7b24e7a6af078c71b19b1652f4befd2904f706dd

            var sb = new StringBuilder();
            sb.AppendLine("Date,Plate,Owner,Mobile,Car Type,Movement,Collector,Receipt Ref,Amount");

            foreach (var p in payments)
            {
<<<<<<< HEAD
                sb.AppendLine($"{p.PaidAt:yyyy-MM-dd},{p.Vehicle?.PlateNumber},{p.Vehicle?.OwnerName},{p.Vehicle?.Mobile},{p.Vehicle?.CarType?.Name},{p.Movement?.Name},{p.Collector?.Username},{p.ReceiptReference?.ReferenceNumber},{p.Amount}");
            }

            sb.AppendLine($",,,,,,,TOTAL,{total}");

            return File(Encoding.UTF8.GetBytes(sb.ToString()), "text/csv",
                $"Payments_Report_{DateTime.Now:yyyyMMddHHmmss}.csv");
        }

        // PDF EXPORT WITH TOTAL + DATE NAME
        public async Task<IActionResult> OnGetPrintPdfAsync()
        {
            var payments = await BuildQuery().OrderByDescending(p => p.PaidAt).ToListAsync();
            var total = payments.Sum(p => p.Amount);

            var stream = new MemoryStream();

            Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Margin(20);
                    page.Content().Table(table =>
                    {
                        table.ColumnsDefinition(c =>
                        {
                            for (int i = 0; i < 9; i++) c.RelativeColumn();
                        });

                        string[] headers = { "Date","Plate","Owner","Mobile","Car Type","Movement","Collector","Receipt Ref","Amount" };

                        foreach (var h in headers)
                            table.Cell().Text(h).Bold();

                        foreach (var p in payments)
                        {
                            table.Cell().Text(p.PaidAt.ToString("yyyy-MM-dd"));
                            table.Cell().Text(p.Vehicle?.PlateNumber);
                            table.Cell().Text(p.Vehicle?.OwnerName);
                            table.Cell().Text(p.Vehicle?.Mobile);
                            table.Cell().Text(p.Vehicle?.CarType?.Name);
                            table.Cell().Text(p.Movement?.Name);
                            table.Cell().Text(p.Collector?.Username);
                            table.Cell().Text(p.ReceiptReference?.ReferenceNumber);
                            table.Cell().Text(p.Amount.ToString("0.##"));
                        }

                        table.Cell().ColumnSpan(8).AlignRight().Text("TOTAL").Bold();
                        table.Cell().Text(total.ToString("0.##")).Bold();
                    });
                });
            }).GeneratePdf(stream);

            stream.Position = 0;

            var from = FromDate?.ToString("yyyyMMdd") ?? "All";
            var to = ToDate?.ToString("yyyyMMdd") ?? "All";

            return File(stream, "application/pdf",
                $"Payments_Report_{from}_to_{to}.pdf");
=======
                sb.AppendLine(string.Join(",",
                    p.PaidAt.ToString("yyyy-MM-dd"),
                    p.Vehicle?.PlateNumber,
                    p.Vehicle?.OwnerName,
                    p.Vehicle?.Mobile,
                    p.Vehicle?.CarType?.Name,
                    p.Movement?.Name ?? p.MovementType,
                    p.Collector?.Username ?? "System",
                    p.ReceiptReference?.ReferenceNumber ?? "-",
                    p.Amount
                ));
            }

            var bytes = Encoding.UTF8.GetBytes(sb.ToString());
            return File(bytes, "text/csv", $"Payments_{DateTime.Now:yyyyMMddHHmmss}.csv");
>>>>>>> 7b24e7a6af078c71b19b1652f4befd2904f706dd
        }
    }
}
