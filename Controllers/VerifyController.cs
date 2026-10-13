using System.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VehicleTax.Web.Data;
using VehicleTax.Web.Models;

namespace VehicleTax.Web.Controllers;

/// <summary>
/// Public receipt verification, used by the QR code printed on receipts.
/// GET /verify/{code}          -> small HTML page (any phone camera)
/// GET /api/verify/{code}      -> JSON (mobile app QR checker)
/// {code} is the invoice number (yyyyMM + 6+ digit payment ID) or the
/// payment ID itself.
/// </summary>
[ApiController]
[AllowAnonymous]
public class VerifyController : ControllerBase
{
    private readonly AppDbContext _context;

    public VerifyController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet("api/verify/{code}")]
    public IActionResult VerifyJson(string code)
    {
        var payment = Find(code);
        if (payment == null)
            return NotFound(new { status = "error", valid = false, message = "Receipt not found" });

        return Ok(new
        {
            status = "success",
            valid = payment.IsPaid && !payment.IsReverted,
            paymentId = payment.Id,
            invoiceNumber = payment.InvoiceNumber,
            receiptNumber = payment.IsPaid && !payment.IsReverted ? payment.InvoiceNumber : null,
            isPaid = payment.IsPaid,
            isReverted = payment.IsReverted,
            plateNumber = payment.Vehicle?.PlateNumber,
            ownerName = payment.Vehicle?.OwnerName,
            movement = payment.Movement?.Name ?? payment.MovementType,
            amount = payment.Amount,
            paidAt = DateTime.SpecifyKind(payment.PaidAt, DateTimeKind.Utc),
            collector = payment.Collector?.Username,
            golisBillNo = payment.TransactionId,
            referenceNo = payment.ReferenceNo
        });
    }

    [HttpGet("verify/{code}")]
    public ContentResult VerifyPage(string code)
    {
        var payment = Find(code);
        string body;
        if (payment == null)
        {
            body = Card("#b91c1c", "#fef2f2", "NOT FOUND",
                "No receipt matches this code.", "");
        }
        else
        {
            var valid = payment.IsPaid && !payment.IsReverted;
            var status = payment.IsReverted ? "CANCELLED" : (payment.IsPaid ? "PAID" : "NOT PAID");
            var color = valid ? "#15803d" : "#b91c1c";
            var bg = valid ? "#f0fdf4" : "#fef2f2";
            var rows =
                Row("Invoice No", payment.InvoiceNumber) +
                Row("Plate", payment.Vehicle?.PlateNumber) +
                Row("Owner", payment.Vehicle?.OwnerName) +
                Row("Movement", payment.Movement?.Name ?? payment.MovementType) +
                Row("Amount", "$" + payment.Amount.ToString("0.00")) +
                Row("Paid At", AppTime.ToLocal(payment.PaidAt).ToString("dd MMM yyyy, HH:mm")) +
                Row("Collector", payment.Collector?.Username) +
                (string.IsNullOrWhiteSpace(payment.TransactionId) ? "" : Row("Golis Bill", payment.TransactionId));
            body = Card(color, bg, status,
                valid ? "This receipt is genuine and paid." : "This receipt is not valid as a paid receipt.",
                rows);
        }

        var html = $@"<!doctype html><html lang=""en""><head><meta charset=""utf-8"">
<meta name=""viewport"" content=""width=device-width,initial-scale=1"">
<title>Receipt Verification - Garowe Vehicle Tax</title>
<style>
body{{margin:0;font-family:system-ui,-apple-system,Segoe UI,Roboto,sans-serif;background:#f8fafc;color:#0f172a}}
.wrap{{max-width:420px;margin:0 auto;padding:24px 16px}}
h1{{font-size:16px;text-align:center;margin:0 0 16px;color:#0b57d0}}
.card{{background:#fff;border:1px solid #e2e8f0;border-radius:18px;overflow:hidden}}
.status{{padding:20px;text-align:center}}
.badge{{display:inline-block;font-weight:800;font-size:20px;letter-spacing:1px}}
.msg{{margin-top:6px;font-size:14px}}
table{{width:100%;border-collapse:collapse}}
td{{padding:10px 16px;border-top:1px solid #f1f5f9;font-size:14px}}
td:first-child{{color:#64748b}} td:last-child{{text-align:right;font-weight:700}}
.foot{{text-align:center;color:#94a3b8;font-size:12px;margin-top:14px}}
</style></head><body><div class=""wrap"">
<h1>Garowe Vehicle Tax &middot; Receipt Verification</h1>
{body}
<div class=""foot"">Garowe Local Government</div>
</div></body></html>";

        return Content(html, "text/html; charset=utf-8");
    }

    private Payment? Find(string code)
    {
        code = (code ?? "").Trim();
        if (string.IsNullOrEmpty(code) || !code.All(char.IsDigit)) return null;

        // Invoice number = yyyyMM + payment ID (zero-padded to 6+ digits).
        int? id = null;
        if (code.Length >= 12 && int.TryParse(code.Substring(6), out var fromInvoice))
            id = fromInvoice;
        else if (int.TryParse(code, out var plainId))
            id = plainId;
        if (id == null) return null;

        var payment = _context.Payments
            .Include(p => p.Vehicle)
            .Include(p => p.Movement)
            .Include(p => p.Collector)
            .FirstOrDefault(p => p.Id == id.Value);

        // A full invoice number must match exactly (guards against guessing).
        if (payment != null && code.Length >= 12 && payment.InvoiceNumber != code)
            return null;
        return payment;
    }

    private static string Row(string label, string? value) =>
        $"<tr><td>{WebUtility.HtmlEncode(label)}</td><td>{WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(value) ? "-" : value)}</td></tr>";

    private static string Card(string color, string bg, string status, string message, string rows) =>
        $@"<div class=""card""><div class=""status"" style=""background:{bg}"">
<div class=""badge"" style=""color:{color}"">{status}</div>
<div class=""msg"" style=""color:{color}"">{WebUtility.HtmlEncode(message)}</div></div>
{(rows.Length > 0 ? "<table>" + rows + "</table>" : "")}</div>";
}
