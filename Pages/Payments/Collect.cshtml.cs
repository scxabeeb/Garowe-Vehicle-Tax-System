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
    [BindProperty] public string ReferenceNumber { get; set; } = "";

    // =======================
    // VIEW DATA
    // =======================
    public Vehicle? Vehicle { get; set; }
    public SelectList Movements { get; set; } = null!;
    public decimal UnitAmount { get; set; }
    public decimal Amount { get; set; }

    public bool ReferenceChecked { get; set; }
    public bool ReferenceValid { get; set; }
    public string? ReferenceMessage { get; set; }

    public string? ErrorMessage { get; set; }

    public List<Payment> Payments { get; set; } = new();

    // =======================
    // GET
    // =======================
    public void OnGet()
    {
        LoadMovements();
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
        ReferenceNumber = "";
        ReferenceChecked = false;
        ReferenceValid = false;
        ReferenceMessage = null;
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

        ReferenceChecked = true;

        var receipt = _context.ReceiptReferences
            .FirstOrDefault(r => r.ReferenceNumber == ReferenceNumber);

        if (receipt == null)
        {
            ReferenceValid = false;
            ReferenceMessage = "Receipt not found";
            return;
        }

        if (receipt.IsUsed)
        {
            ReferenceValid = false;
            ReferenceMessage = "Receipt already used";
            return;
        }

        ReferenceValid = true;
        ReferenceMessage = "Receipt is available";

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
    // COLLECT PAYMENT  (FIXED)
    // =======================
    public IActionResult OnPostCollect()
    {
        using var tx = _context.Database.BeginTransaction();
        try
        {
            LoadVehicle();
            LoadMovements();

            var receipt = _context.ReceiptReferences
                .FirstOrDefault(r => r.ReferenceNumber == ReferenceNumber);

            if (receipt == null || receipt.IsUsed)
                throw new Exception("Invalid or already used receipt reference.");

            var tax = _context.TaxAmounts
                .Include(t => t.Movement)
                .FirstOrDefault(t =>
                    t.CarTypeId == Vehicle!.CarTypeId &&
                    t.MovementId == MovementId);

            if (tax == null)
                throw new Exception("Tax configuration not found.");

            var username = User.Identity?.Name;
            if (string.IsNullOrEmpty(username))
                throw new Exception("Collector not logged in.");

            var collector = _context.Users.FirstOrDefault(u => u.Username == username);
            if (collector == null)
                throw new Exception("Collector user not found.");

            var payment = new Payment
            {
                VehicleId = Vehicle!.Id,
                MovementId = MovementId,
                MovementType = tax.Movement!.Name,
                Amount = tax.Amount * Quantity,
                PaidAt = DateTime.UtcNow,
                ReceiptReferenceId = receipt.Id,
                CollectorId = collector.Id
            };

            _context.Payments.Add(payment);

            // 🔴 THIS IS THE KEY PART FOR YOUR REPORT
            receipt.IsUsed = true;
            receipt.UsedAt = DateTime.UtcNow;
            receipt.UsedBy = collector.Username;   // Collector name
            receipt.VehicleId = Vehicle.Id;        // Vehicle that used the receipt

            _context.SaveChanges();
            tx.Commit();

            TempData["SuccessMessage"] = "Payment collected successfully.";
            return RedirectToPage();
        }
        catch (Exception ex)
        {
            tx.Rollback();
            ErrorMessage = ex.Message;
            return Page();
        }
    }

    // =======================
    // REVERT (ADMIN ONLY)  (FIXED)
    // =======================
    [Authorize(Roles = "Admin")]
    public IActionResult OnPostRevert(int paymentId, string reason)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(reason))
                throw new Exception("Revert reason is required.");

            var payment = _context.Payments
                .Include(p => p.ReceiptReference)
                .FirstOrDefault(p => p.Id == paymentId);

            if (payment == null || payment.IsReverted)
                throw new Exception("Invalid payment or already reverted.");

            var username = User.Identity?.Name;
            var user = _context.Users.FirstOrDefault(u => u.Username == username);

            payment.IsReverted = true;
            payment.RevertedAt = DateTime.UtcNow;
            payment.RevertReason = reason;
            payment.RevertedByUserId = user!.Id;

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
}
