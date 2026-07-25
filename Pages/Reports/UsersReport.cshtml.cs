
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using VehicleTax.Web.Data;
using VehicleTax.Web.Models;

namespace VehicleTax.Web.Pages.Reports
{
    public class UsersReportModel : PageModel
    {
        private readonly AppDbContext _context;

        public UsersReportModel(AppDbContext context)
        {
            _context = context;
        }

        public List<User> Users { get; set; } = new();

        // Filters
        [BindProperty(SupportsGet = true)]
        public string? Search { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? Role { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? Status { get; set; } // Active / Locked

        // Pagination
        [BindProperty(SupportsGet = true)]
        public int PageNumber { get; set; } = 1;

        public int PageSize { get; set; } = 10;
        public int TotalPages { get; set; }

        public void OnGet()
        {
            var query = _context.Users.AsQueryable();

            // Search
            if (!string.IsNullOrWhiteSpace(Search))
            {
                query = query.Where(u =>
                    u.Username.Contains(Search!) ||
                    u.Role.Contains(Search!) ||
                    u.Permissions.Contains(Search!));
            }

            // Role filter
            if (!string.IsNullOrWhiteSpace(Role))
            {
                query = query.Where(u => u.Role == Role);
            }

            // Status filter
            if (!string.IsNullOrWhiteSpace(Status))
            {
                if (Status == "Active")
                    query = query.Where(u => !u.IsLocked);
                else if (Status == "Locked")
                    query = query.Where(u => u.IsLocked);
            }

            int totalCount = query.Count();
            TotalPages = (int)Math.Ceiling(totalCount / (double)PageSize);

            Users = query
                .OrderBy(u => u.Username)
                .Skip((PageNumber - 1) * PageSize)
                .Take(PageSize)
                .ToList();
        }

        // CSV Export (with filters)
        public IActionResult OnGetExportCsv(string? search, string? role, string? status)
        {
            var query = _context.Users.AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(u =>
                    u.Username.Contains(search!) ||
                    u.Role.Contains(search!) ||
                    u.Permissions.Contains(search!));
            }

            if (!string.IsNullOrWhiteSpace(role))
                query = query.Where(u => u.Role == role);

            if (!string.IsNullOrWhiteSpace(status))
            {
                if (status == "Active")
                    query = query.Where(u => !u.IsLocked);
                else if (status == "Locked")
                    query = query.Where(u => u.IsLocked);
            }

            var users = query.ToList();

            var csv = "Id,Username,Role,Permissions,Status\n";
            foreach (var u in users)
            {
                var state = u.IsLocked ? "Locked" : "Active";
                csv += $"{u.Id},{u.Username},{u.Role},\"{u.Permissions}\",{state}\n";
            }

            return File(
                System.Text.Encoding.UTF8.GetBytes(csv),
                "text/csv",
                "UsersReport.csv"
            );
        }
    }
}
