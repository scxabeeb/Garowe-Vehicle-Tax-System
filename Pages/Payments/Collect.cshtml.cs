using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using VehicleTax.Web.Data;
using VehicleTax.Web.Models;

namespace VehicleTax.Web.Pages.Payments;

[Authorize] 
public class CollectModel : PageModel
{
    private readonly AppDbContext _context;

    public CollectModel(AppDbContext context)
    {
        _context = context;
    }

    // =======================
    // BINDINGS
    // =======================
    [BindProperty] public string PlateNumber { get; set; } = "";
    [BindProperty] public int MovementId { get; set; }
    [BindProperty] public int Quantity { get; set; } = 1;
    [BindProperty] public int? CheckpointId { get; set; }

    // =======================
    // VIEW DATA
    // =======================
    public Vehicle? Vehicle { get; set; }
    public SelectList Movements { get; set; } = null!;
    public SelectList Checkpoints { get; set; } = null!;
    public decimal UnitAmount { get; set; }
    public decimal Amount { get; set; }

    public string? ErrorMessage { get; set; }

    public List<Payment> Payments { get; set; } = new();

    // =======================
    // GET
    // =======================
    public void OnGet()
    {
        LoadMovements();
        LoadCheckpoints();
    }

    public IActionResult OnPostReset()
    {
        PlateNumber = string.Empty;
        MovementId = 0;
        Quantity = 1;
        UnitAmount = 0;
        Amount = 0;
        ErrorMessage = null;
        Vehicle = null;
        Payments = new List<Payment>();
        LoadMovements();
        LoadCheckpoints();
        return Page();
    }

    // =======================
    // SEARCH
    // =======================
    public void OnPostSearch()
    {
        MovementId = 0;
        Quantity = 1;
        UnitAmount = 0;
        Amount = 0;
        ErrorMessage = null;

        LoadVehicle();
        LoadMovements();
    }

    // =======================
    // CALCULATE
    // =======================
    public void OnPostCalculate()
    {
        LoadVehicle();
        LoadMovements();

        if (Vehicle == null || MovementId == 0)
            return;

        var tax = _context.TaxAmounts
            .Include(t => t.Movement)
            .FirstOrDefault(t =>
                t.CarTypeId == Vehicle.CarTypeId &&
                t.MovementId == MovementId);

        if (tax == null)
        {
            ErrorMessage = "Tax not configured for this movement and car type";
            return;
        }

        UnitAmount = tax.Amount;
        Amount = UnitAmount * Quantity;
    }

    // =======================
    // GENERATE INVOICE PREVIEW (NO DB RECORD)
    // =======================
    public IActionResult OnPostGenerateInvoice()
    {
        try
        {
            LoadVehicle();
            LoadMovements();

            if (Vehicle == null)
            {
                ErrorMessage = "Vehicle not found.";
                return Page();
            }

            var tax = _context.TaxAmounts
                .Include(t => t.Movement)
                .FirstOrDefault(t =>
                    t.CarTypeId == Vehicle.CarTypeId &&
                    t.MovementId == MovementId);

            if (tax == null)
            {
                ErrorMessage = "Tax configuration not found.";
                return Page();
            }

            if (Quantity < 1)
            {
                ErrorMessage = "Quantity must be at least 1.";
                return Page();
            }

            return RedirectToPage("/Payments/Receipt", new
            {
                vehicleId = Vehicle.Id,
                movementId = MovementId,
                quantity = Quantity
            });
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            LoadVehicle();
            LoadMovements();
            return Page();
        }
    }

    // =======================
    // COLLECT INVOICE (ADMIN ONLY)
    // =======================
    // The checkpoint dropdown on the form lets the admin explicitly choose
    // which checkpoint this payment is attributed to.  If no checkpoint is
    // selected, the collector's currently-assigned checkpoint is used as a
    // fallback so payments are never left without a checkpoint when one is
    // available.
    // =======================
    public IActionResult OnPostCollectInvoice(int paymentId, int? checkpointId = null)
    {
        try
        {
            if (!User.IsInRole("Admin"))
            {
                ErrorMessage = "Only admins can collect invoice payments.";
                LoadVehicle();
                LoadMovements();
                LoadCheckpoints();
                return Page();
            }

            var username = User.Identity?.Name;
            if (string.IsNullOrWhiteSpace(username))
            {
                return RedirectToPage("/Account/Login", new { ReturnUrl = "/Payments/Collect" });
            }

            var collector = _context.Users.FirstOrDefault(u => u.Username == username);
            if (collector == null)
            {
                ErrorMessage = "Admin user not found.";
                LoadVehicle();
                LoadMovements();
                LoadCheckpoints();
                return Page();
            }

            var payment = _context.Payments.FirstOrDefault(p => p.Id == paymentId);
            if (payment == null || payment.IsReverted)
            {
                ErrorMessage = "Invalid invoice.";
                LoadVehicle();
                LoadMovements();
                LoadCheckpoints();
                return Page();
            }

            if (payment.IsPaid)
            {
                TempData["SuccessMessage"] = "Invoice was already collected.";
                return RedirectToPage("/Payments/Receipt", new { paymentId = payment.Id });
            }

            payment.IsPaid = true;
            payment.PaidAt = DateTime.UtcNow;
            payment.CollectorId = collector.Id;
            // Snapshot the checkpoint at payment time so historical
            // payments stay attributed to the original checkpoint even
            // if the collector is later reassigned.
            // Prefer the checkpoint explicitly selected from the dropdown;
            // otherwise fall back to the collector's checkpoint.
            payment.CheckpointId = checkpointId.HasValue
                ? checkpointId.Value
                : collector.CheckpointId;

            _context.SaveChanges();

            TempData["SuccessMessage"] = "Invoice collected successfully.";
            return RedirectToPage("/Payments/Receipt", new { paymentId = payment.Id });
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            LoadVehicle();
            LoadMovements();
            LoadCheckpoints();
            return Page();
        }
    }

    // =======================
    // REVERT (ADMIN ONLY)
    // =======================
    public IActionResult OnPostRevert(int paymentId, string reason)
    {
        try
        {
            if (!User.IsInRole("Admin"))
            {
                ErrorMessage = "Only admins can revert payments.";
                return Page();
            }

            if (string.IsNullOrWhiteSpace(reason))
            {
                ErrorMessage = "Revert reason is required.";
                return Page();
            }

            var payment = _context.Payments
                .Include(p => p.ReceiptReference)
                .FirstOrDefault(p => p.Id == paymentId);

            if (payment == null || payment.IsReverted)
            {
                ErrorMessage = "Invalid payment or already reverted.";
                return Page();
            }

            var username = User.Identity?.Name;
            var user = _context.Users.FirstOrDefault(u => u.Username == username);
            if (user == null)
            {
                ErrorMessage = "Admin user not found.";
                return Page();
            }

            payment.IsReverted = true;
            payment.IsPaid = false;
            payment.RevertedAt = DateTime.UtcNow;
            payment.RevertReason = reason;
            payment.RevertedByUserId = user.Id;

            // Reset receipt so it becomes available again
            if (payment.ReceiptReference != null)
            {
                payment.ReceiptReference.IsUsed = false;
                payment.ReceiptReference.UsedAt = null;
                payment.ReceiptReference.UsedBy = null;
                payment.ReceiptReference.VehicleId = null;
            }

            _context.SaveChanges();

            TempData["SuccessMessage"] = "Payment reverted successfully.";
            return RedirectToPage();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            return Page();
        }
    }

    // =======================
    // HELPERS
    // =======================
    private void LoadVehicle()
    {
        var plate = PlateNumber.Trim().ToUpper();

        Vehicle = _context.Vehicles
            .Include(v => v.CarType)
            .FirstOrDefault(v => v.PlateNumber.ToUpper() == plate);

        if (Vehicle != null)
        {
            Payments = _context.Payments
                .Include(p => p.ReceiptReference)
                .Where(p => p.VehicleId == Vehicle.Id)
                .OrderByDescending(p => p.PaidAt)
                .ToList();
        }
    }

    private void LoadMovements()
    {
        if (Vehicle == null)
        {
            Movements = new SelectList(Enumerable.Empty<SelectListItem>());
            return;
        }

        var movementIds = _context.TaxAmounts
            .Where(t => t.CarTypeId == Vehicle.CarTypeId)
            .Select(t => t.MovementId)
            .Distinct()
            .ToList();

        Movements = new SelectList(
            _context.Movements
                .Where(m => movementIds.Contains(m.Id))
                .OrderBy(m => m.Name),
            "Id",
            "Name"
        );
    }

    private void LoadCheckpoints()
    {
        Checkpoints = new SelectList(
            _context.Checkpoints.AsNoTracking().OrderBy(c => c.Name),
            "Id",
            "Name"
        );
    }
}
