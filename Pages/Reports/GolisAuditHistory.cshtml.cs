using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using VehicleTax.Web.Data;
using VehicleTax.Web.Models;

namespace VehicleTax.Web.Pages.Reports;

[Authorize(Roles = "Admin,Finance Officer,Auditor,Manager")]
public class GolisAuditHistoryModel : PageModel
{
    private readonly AppDbContext _context;

    public GolisAuditHistoryModel(AppDbContext context)
    {
        _context = context;
    }

    public List<GolisAudit> Audits { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public string? Search { get; set; }

    [BindProperty(SupportsGet = true)]
    public AuditStatus? StatusFilter { get; set; }

    public void OnGet()
    {
        var query = _context.GolisAudits
            .Include(a => a.CreatedByUser)
            .Include(a => a.FinalizedByUser)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(Search))
        {
            query = query.Where(a =>
                a.StatementNumber.Contains(Search) ||
                (a.Notes != null && a.Notes.Contains(Search)));
        }

        if (StatusFilter.HasValue)
        {
            query = query.Where(a => a.Status == StatusFilter.Value);
        }

        Audits = query
            .OrderByDescending(a => a.CreatedAt)
            .ToList();
    }
}
