using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using VehicleTax.Web.Data;
using VehicleTax.Web.Models;

namespace VehicleTax.Web.Pages.Reports;

public class PaymentsModel : PageModel
{
    private readonly AppDbContext _context;

    public PaymentsModel(AppDbContext context)
    {
        _context = context;
    }

    [BindProperty(SupportsGet = true)]
    public DateTime? FromDate { get; set; }

    [BindProperty(SupportsGet = true)]
    public DateTime? ToDate { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? PlateNumber { get; set; }

    [BindProperty(SupportsGet = true)]
    public int? CarTypeId { get; set; }

    public List<Payment> Payments { get; set; } = [];
    public decimal TotalAmount { get; set; }
    public SelectList CarTypes { get; set; } = null!;

    public void OnGet()
    {
        CarTypes = new SelectList(_context.CarTypes, "Id", "Name");

        var query = _context.Payments
            .Include(p => p.Vehicle)
                .ThenInclude(v => v.CarType)
            .AsQueryable();

        if (FromDate.HasValue)
            query = query.Where(p => p.PaymentDate.Date >= FromDate.Value.Date);

        if (ToDate.HasValue)
            query = query.Where(p => p.PaymentDate.Date <= ToDate.Value.Date);

        if (!string.IsNullOrWhiteSpace(PlateNumber))
            query = query.Where(p =>
                p.Vehicle!.PlateNumber.Contains(PlateNumber));

        if (CarTypeId.HasValue)
            query = query.Where(p =>
                p.Vehicle!.CarTypeId == CarTypeId);

        Payments = query
            .OrderByDescending(p => p.PaymentDate)
            .ToList();

        TotalAmount = Payments.Sum(p => p.AmountPaid);
    }
}
