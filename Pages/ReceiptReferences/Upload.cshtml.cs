using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OfficeOpenXml;
using System.Text;
using VehicleTax.Web.Data;
using VehicleTax.Web.Models;

namespace VehicleTax.Web.Pages.ReceiptReferences;

public class UploadModel : PageModel
{
    private readonly AppDbContext _context;

    public UploadModel(AppDbContext context)
    {
        _context = context;
    }

    // ===== PREVIEW STATE =====
    public bool HasPreview => PreviewRows.Any();

    [BindProperty]
    public List<PreviewRow> PreviewRows { get; set; } = new();

    // ===== UPLOAD & PREVIEW =====
    public async Task<IActionResult> OnPostAsync(IFormFile file)
    {
        if (file == null)
        {
            TempData["Error"] = "Please select a file.";
            return Page();
        }

        try
        {
            var ext = Path.GetExtension(file.FileName).ToLower();
            var refs = ext == ".xlsx"
                ? await ReadExcel(file)
                : ext == ".csv"
                    ? await ReadCsv(file)
                    : throw new Exception("Unsupported file type.");

            foreach (var r in refs)
            {
                PreviewRows.Add(new PreviewRow
                {
                    ReferenceNumber = r,
                    IsDuplicate = _context.ReceiptReferences
                        .Any(x => x.ReferenceNumber == r)
                });
            }

            HttpContext.Session.Set(
                "PreviewData",
                Encoding.UTF8.GetBytes(
                    System.Text.Json.JsonSerializer.Serialize(PreviewRows)
                )
            );

            return Page();
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
            return Page();
        }
    }

    // ===== CONFIRM SAVE =====
    public async Task<IActionResult> OnPostConfirmAsync()
    {
        var json = HttpContext.Session.Get("PreviewData");
        if (json == null) return RedirectToPage("Upload");

        var rows = System.Text.Json.JsonSerializer.Deserialize<List<PreviewRow>>(
            Encoding.UTF8.GetString(json)
        )!;

        foreach (var row in rows.Where(r => !r.IsDuplicate))
        {
            _context.ReceiptReferences.Add(new ReceiptReference
            {
                ReferenceNumber = row.ReferenceNumber
            });
        }

        await _context.SaveChangesAsync();
        HttpContext.Session.Remove("PreviewData");

        TempData["Error"] = null;
        return RedirectToPage("Index");
    }

    // ===== DOWNLOAD ERRORS =====
    public IActionResult OnPostDownloadErrors()
    {
        var json = HttpContext.Session.Get("PreviewData");
        if (json == null) return RedirectToPage("Upload");

        var rows = System.Text.Json.JsonSerializer.Deserialize<List<PreviewRow>>(
            Encoding.UTF8.GetString(json)
        )!;

        var sb = new StringBuilder();
        sb.AppendLine("ReferenceNumber,Reason");

        foreach (var r in rows.Where(r => r.IsDuplicate))
        {
            sb.AppendLine($"{r.ReferenceNumber},Duplicate");
        }

        return File(
            Encoding.UTF8.GetBytes(sb.ToString()),
            "text/csv",
            "ReceiptUploadErrors.csv"
        );
    }

    // ===== DOWNLOAD TEMPLATE =====
    public IActionResult OnPostDownloadTemplate()
    {
        var csv = "ReferenceNumber\n";

        return File(
            Encoding.UTF8.GetBytes(csv),
            "text/csv",
            "ReceiptTemplate.csv"
        );
    }

    // ===== DOWNLOAD SAMPLE DATA =====
    public IActionResult OnPostDownloadSample()
    {
        var sb = new StringBuilder();
        sb.AppendLine("ReferenceNumber");

        for (int i = 1; i <= 30; i++)
            sb.AppendLine($"RCPT-{10000 + i}");

        return File(
            Encoding.UTF8.GetBytes(sb.ToString()),
            "text/csv",
            "SampleReceiptNumbers.csv"
        );
    }

    // ===== READERS =====
    private async Task<List<string>> ReadExcel(IFormFile file)
    {
        using var ms = new MemoryStream();
        await file.CopyToAsync(ms);

        // EPPlus License (your version)
        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

        using var pkg = new ExcelPackage(ms);
        var ws = pkg.Workbook.Worksheets[0];

        if (ws.Cells[1, 1].Text.Trim() != "ReferenceNumber")
            throw new Exception("Invalid Excel header.");

        var list = new List<string>();

        for (int r = 2; r <= ws.Dimension.Rows; r++)
        {
            var v = ws.Cells[r, 1].Text.Trim();
            if (!string.IsNullOrEmpty(v))
                list.Add(v);
        }

        return list;
    }

    private async Task<List<string>> ReadCsv(IFormFile file)
    {
        using var reader = new StreamReader(file.OpenReadStream());
        var header = await reader.ReadLineAsync();

        if (header != "ReferenceNumber")
            throw new Exception("Invalid CSV header.");

        var list = new List<string>();

        while (!reader.EndOfStream)
        {
            var line = await reader.ReadLineAsync();
            if (!string.IsNullOrWhiteSpace(line))
                list.Add(line.Trim());
        }

        return list;
    }
}

// ===== PREVIEW MODEL =====
public class PreviewRow
{
    public string ReferenceNumber { get; set; } = "";
    public bool IsDuplicate { get; set; }
}
