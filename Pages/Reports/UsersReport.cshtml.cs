
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
        public List<VehicleTax.Web.Services.CountGroup> SummaryGroups { get; set; } = new();
        public int TotalCount { get; set; }

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

        [BindProperty(SupportsGet = true)]
        public int PageSize { get; set; } = 10;
        public VehicleTax.Web.Services.Pager Pager => new(TotalCount, PageNumber, PageSize, "PageNumber");
        public int TotalPages { get; set; }

        public void OnGet()
        {
            if (PageSize <= 0) PageSize = VehicleTax.Web.Services.Pager.AllRows; // "All"
            var query = _context.Users.AsQueryable();

            // Search
            if (!string.IsNullOrWhiteSpace(Search))
            {
                query = query.Where(u =>
                    u.Username.Contains(Search!) ||
                    u.FullName.Contains(Search!) ||
                    u.Phone.Contains(Search!) ||
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
            TotalCount = totalCount;
            TotalPages = (int)Math.Ceiling(totalCount / (double)PageSize);
            var summary = query.Select(u => new { u.Role, u.IsLocked }).ToList();
            SummaryGroups = new()
            {
                new("Users by Role", "bi-person-badge",
                    VehicleTax.Web.Services.ReportDetails.CountBy(summary, u => u.Role)),
                new("Users by Status", "bi-shield-lock",
                    VehicleTax.Web.Services.ReportDetails.CountBy(summary, u => u.IsLocked ? "Locked" : "Active")),
            };

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
                    u.FullName.Contains(search!) ||
                    u.Phone.Contains(search!) ||
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

            var csv = "System ID,Username,Full Name,Phone,Role,Permissions,Status\n";
            foreach (var u in users)
            {
                var state = u.IsLocked ? "Locked" : "Active";
                csv += $"{u.Id},{u.Username},\"{u.FullName}\",{u.Phone},{u.Role},\"{u.Permissions}\",{state}\n";
            }

            return File(
                System.Text.Encoding.UTF8.GetBytes(csv),
                "text/csv",
                "UsersReport.csv"
            );
        }
    }
}
