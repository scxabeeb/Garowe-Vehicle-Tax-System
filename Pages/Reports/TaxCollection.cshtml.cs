using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using VehicleTax.Web.Data;
using VehicleTax.Web.Models;

namespace VehicleTax.Web.Pages.Reports;

public class TaxCollectionModel : PageModel
{
    private readonly AppDbContext _context;

    public TaxCollectionModel(AppDbContext context)
    {
        _context = context;
    }

    // FILTERS
    [BindProperty(SupportsGet = true)]
    public DateTime? FromDate { get; set; }

    [BindProperty(SupportsGet = true)]
    public DateTime? ToDate { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? PlateNumber { get; set; }

    [BindProperty(SupportsGet = true)]
    public int? CarTypeId { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? MovementType { get; set; }

    // DATA
    public List<Payment> Collections { get; set; } = new();
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
            query = query.Where(p => p.PaidAt.Date >= FromDate.Value.Date);

        if (ToDate.HasValue)
            query = query.Where(p => p.PaidAt.Date <= ToDate.Value.Date);

        if (!string.IsNullOrWhiteSpace(PlateNumber))
            query = query.Where(p =>
                p.Vehicle != null &&
                p.Vehicle.PlateNumber.Contains(PlateNumber));

        if (CarTypeId.HasValue)
            query = query.Where(p =>
                p.Vehicle != null &&
                p.Vehicle.CarTypeId == CarTypeId.Value);

        if (!string.IsNullOrWhiteSpace(MovementType))
            query = query.Where(p =>
                p.MovementType == MovementType);

        Collections = query
            .OrderByDescending(p => p.PaidAt)
            .ToList();

        TotalAmount = Collections.Sum(p => p.Amount);
    }
}
