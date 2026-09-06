using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Text;
using VehicleTax.Web;
using VehicleTax.Web.Data;
using VehicleTax.Web.Models;

namespace VehicleTax.Web.Pages.Reports
{
    public class TransactionDetail
    {
        public int? ReferenceNo { get; set; }
        public DateTime Date { get; set; }
        public string ReceiptNumber { get; set; } = "";
        public string PlateNumber { get; set; } = "";
        public string OwnerName { get; set; } = "";
        public string CarTypeName { get; set; } = "";
        public string MovementName { get; set; } = "";
        public decimal Amount { get; set; }
    }

    public class RevenueAccountDetailsModel : PageModel
    {
        private readonly AppDbContext _context;

        public RevenueAccountDetailsModel(AppDbContext context)
        {
            _context = context;
        }

        [BindProperty(SupportsGet = true)] public int AccountId { get; set; }
        [BindProperty(SupportsGet = true)] public DateTime? FromDate { get; set; }
        [BindProperty(SupportsGet = true)] public DateTime? ToDate { get; set; }
        [BindProperty(SupportsGet = true)] public int? MovementId { get; set; }
        [BindProperty(SupportsGet = true)] public int? CarTypeId { get; set; }

        public RevenueAccount? Account { get; set; }
        public List<TransactionDetail> Transactions { get; set; } = new();
        public decimal TotalAmount { get; set; }
        public int TotalTransactions { get; set; }

        public SelectList Movements { get; set; } = null!;
        public SelectList CarTypes { get; set; } = null!;

        public async Task<IActionResult> OnGetAsync(int accountId)
        {
            Account = await _context.RevenueAccounts
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.Id == accountId);

            if (Account == null)
                return NotFound();

            await LoadDropdownsAsync();
            await LoadTransactionsAsync(accountId);

            return Page();
        }

        public async Task<IActionResult> OnGetExportExcelAsync(int accountId)
        {
            Account = await _context.RevenueAccounts
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.Id == accountId);

            if (Account == null)
                return NotFound();

            await LoadDropdownsAsync();
            await LoadTransactionsAsync(accountId);

            var sb = new StringBuilder();
            sb.AppendLine($"Revenue Account Details: {Account.AccountCode} - {Account.AccountName}");
            sb.AppendLine($"From: {FromDate?.ToString("dd-MMM-yyyy") ?? "All"}  To: {ToDate?.ToString("dd-MMM-yyyy") ?? "All"}");
            sb.AppendLine("");
            sb.AppendLine("Ref No,Date,Receipt No,Plate,Owner,Car Type,Movement,Amount");

            foreach (var t in Transactions)
            {
                sb.AppendLine($"{(t.ReferenceNo?.ToString() ?? "-")},{t.Date:yyyy-MM-dd HH:mm},{t.ReceiptNumber},{t.PlateNumber},{t.OwnerName},{t.CarTypeName},{t.MovementName},{t.Amount:N0}");
            }

            sb.AppendLine($"TOTAL,{TotalTransactions},{TotalAmount:N0}");

            var bytes = Encoding.UTF8.GetBytes(sb.ToString());
            return File(bytes, "text/csv", $"RevenueAccountDetails_{accountId}_{AppTime.Now:yyyyMMddHHmmss}.csv");
        }

        private async Task LoadDropdownsAsync()
        {
            CarTypes = new SelectList(
                await _context.CarTypes.AsNoTracking().OrderBy(c => c.Name).ToListAsync(),
                "Id",
                "Name"
            );

            Movements = new SelectList(
                await _context.Movements.AsNoTracking()
                    .GroupBy(m => m.Name)
                    .Select(g => new { Id = g.Min(x => x.Id), Name = g.Key })
                    .OrderBy(x => x.Name)
                    .ToListAsync(),
                "Id",
                "Name"
            );
        }

        private async Task LoadTransactionsAsync(int accountId)
        {
            var query = _context.Payments
                .Where(p => p.IsPaid && !p.IsReverted)
                .Include(p => p.Vehicle).ThenInclude(v => v.CarType)
                .Include(p => p.Movement)
                    .ThenInclude(m => m.RevenueAccount)
                .Include(p => p.ReceiptReference)
                .AsQueryable();

            query = query.Where(p => p.Movement != null && p.Movement.RevenueAccountId == accountId);

            if (FromDate.HasValue)
                query = query.Where(p => p.PaidAt >= FromDate.Value.Date);

            if (ToDate.HasValue)
                query = query.Where(p => p.PaidAt < ToDate.Value.Date.AddDays(1));

            if (MovementId.HasValue)
                query = query.Where(p => p.MovementId == MovementId.Value);

            if (CarTypeId.HasValue)
                query = query.Where(p => p.Vehicle != null && p.Vehicle.CarTypeId == CarTypeId.Value);

            Transactions = await query
                .OrderByDescending(p => p.PaidAt)
                .Select(p => new TransactionDetail
                {
                    ReferenceNo = p.ReferenceNo,
                    Date = p.PaidAt,
                    ReceiptNumber = p.InvoiceNumber,
                    PlateNumber = p.Vehicle!.PlateNumber,
                    OwnerName = p.Vehicle!.OwnerName,
                    CarTypeName = p.Vehicle!.CarType!.Name,
                    MovementName = p.Movement!.Name,
                    Amount = p.Amount
                })
                .ToListAsync();

            TotalTransactions = Transactions.Count;
            TotalAmount = Transactions.Sum(t => t.Amount);
        }
    }
}
