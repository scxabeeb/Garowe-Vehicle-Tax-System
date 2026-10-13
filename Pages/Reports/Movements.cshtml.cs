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
    public class MovementsModel : PageModel
    {
        private readonly AppDbContext _context;

        public MovementsModel(AppDbContext context)
        {
            _context = context;
        }

        [BindProperty(SupportsGet = true)]
        public string? MovementName { get; set; }

        [BindProperty(SupportsGet = true)]
        public int? CarTypeId { get; set; }

        [BindProperty(SupportsGet = true)]
        public int PageNumber { get; set; } = 1;

        [BindProperty(SupportsGet = true)]
        public int PageSize { get; set; } = 10;

        public int TotalRecords { get; set; }
        public List<VehicleTax.Web.Services.CountGroup> SummaryGroups { get; set; } = new();
        public int TotalPages =>
            PageSize == -1 ? 1 : (int)Math.Ceiling((double)TotalRecords / PageSize);

        public List<Movement> Movements { get; set; } = new();
        public SelectList CarTypes { get; set; } = null!;

        public async Task OnGetAsync()
        {
            CarTypes = new SelectList(
                await _context.CarTypes.OrderBy(c => c.Name).ToListAsync(),
                "Id",
                "Name"
            );

            var query = _context.Movements
                .Include(m => m.CarType)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(MovementName))
                query = query.Where(m => m.Name.Contains(MovementName));

            if (CarTypeId.HasValue)
                query = query.Where(m => m.CarTypeId == CarTypeId);

            TotalRecords = await query.CountAsync();
            var carTypeNames = await query.Select(m => m.CarType != null ? m.CarType.Name : null).ToListAsync();
            SummaryGroups = new()
            {
                new("Traffida by Car Type", "bi-truck",
                    VehicleTax.Web.Services.ReportDetails.CountBy(carTypeNames, n => n)),
            };

            if (PageSize != -1)
            {
                Movements = await query
                    .OrderBy(m => m.Name)
                    .Skip((PageNumber - 1) * PageSize)
                    .Take(PageSize)
                    .ToListAsync();
            }
            else
            {
                Movements = await query
                    .OrderBy(m => m.Name)
                    .ToListAsync();
            }
        }

        public async Task<IActionResult> OnGetExportCsvAsync(string? movementName, int? carTypeId)
        {
            var query = _context.Movements
                .Include(m => m.CarType)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(movementName))
                query = query.Where(m => m.Name.Contains(movementName));

            if (carTypeId.HasValue)
                query = query.Where(m => m.CarTypeId == carTypeId);

            var data = await query.OrderBy(m => m.Name).ToListAsync();

            var sb = new StringBuilder();
            sb.AppendLine("System ID,Traffida,CarType");

            foreach (var m in data)
            {
                sb.AppendLine($"{m.Id},{m.Name},{m.CarType?.Name}");
            }

            return File(
                Encoding.UTF8.GetBytes(sb.ToString()),
                "text/csv",
                $"MovementReport_{AppTime.Now:yyyyMMddHHmmss}.csv"
            );
        }
    }
}
