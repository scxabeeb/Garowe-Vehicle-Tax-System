using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Text;
using VehicleTax.Web.Data;
using VehicleTax.Web.Models;

namespace VehicleTax.Web.Pages.Reports
{
    public class VehiclesModel : PageModel
    {
        private readonly AppDbContext _context;

        public VehiclesModel(AppDbContext context)
        {
            _context = context;
        }

        // Filters
        [BindProperty(SupportsGet = true)]
        public string? PlateNumber { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? OwnerName { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? Mobile { get; set; }

        [BindProperty(SupportsGet = true)]
        public int? CarTypeId { get; set; }

        // Pagination
        [BindProperty(SupportsGet = true)]
        public int PageNumber { get; set; } = 1;

        [BindProperty(SupportsGet = true)]
        public int PageSize { get; set; } = 10;

        public int TotalRecords { get; set; }
        public int TotalPages => PageSize == -1 ? 1 : (int)Math.Ceiling((double)TotalRecords / PageSize);

        // Data
        public List<Vehicle> Vehicles { get; set; } = new();
        public SelectList CarTypes { get; set; } = null!;

        public async Task OnGetAsync()
        {
            CarTypes = new SelectList(
                await _context.CarTypes.OrderBy(c => c.Name).ToListAsync(),
                "Id",
                "Name"
            );

            var query = _context.Vehicles
                .Include(v => v.CarType)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(PlateNumber))
                query = query.Where(v => v.PlateNumber.Contains(PlateNumber));

            if (!string.IsNullOrWhiteSpace(OwnerName))
                query = query.Where(v => v.OwnerName.Contains(OwnerName));

            if (!string.IsNullOrWhiteSpace(Mobile))
                query = query.Where(v => v.Mobile.Contains(Mobile));

            if (CarTypeId.HasValue)
                query = query.Where(v => v.CarTypeId == CarTypeId.Value);

            TotalRecords = await query.CountAsync();

            if (PageSize != -1)
            {
                Vehicles = await query
                    .OrderBy(v => v.PlateNumber)
                    .Skip((PageNumber - 1) * PageSize)
                    .Take(PageSize)
                    .ToListAsync();
            }
            else
            {
                Vehicles = await query
                    .OrderBy(v => v.PlateNumber)
                    .ToListAsync();
            }
        }

        // CSV Export
        public async Task<IActionResult> OnGetExportCsvAsync(
            string? plateNumber,
            string? ownerName,
            string? mobile,
            int? carTypeId)
        {
            var query = _context.Vehicles
                .Include(v => v.CarType)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(plateNumber))
                query = query.Where(v => v.PlateNumber.Contains(plateNumber));

            if (!string.IsNullOrWhiteSpace(ownerName))
                query = query.Where(v => v.OwnerName.Contains(ownerName));

            if (!string.IsNullOrWhiteSpace(mobile))
                query = query.Where(v => v.Mobile.Contains(mobile));

            if (carTypeId.HasValue)
                query = query.Where(v => v.CarTypeId == carTypeId.Value);

            var data = await query.OrderBy(v => v.PlateNumber).ToListAsync();

            var sb = new StringBuilder();
            sb.AppendLine("PlateNumber,OwnerName,Mobile,CarType");

            foreach (var v in data)
            {
                sb.AppendLine($"{v.PlateNumber},{v.OwnerName},{v.Mobile},{v.CarType?.Name}");
            }

            return File(
                Encoding.UTF8.GetBytes(sb.ToString()),
                "text/csv",
                $"VehicleReport_{DateTime.Now:yyyyMMddHHmmss}.csv"
            );
        }
    }
}
