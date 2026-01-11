using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using VehicleTax.Web.Data;
using VehicleTax.Web.Models;

namespace VehicleTax.Web.Pages.Payments;

public class CollectModel : PageModel
{
    private readonly AppDbContext _context;

    public CollectModel(AppDbContext context)
    {
        _context = context;
    }

    // ===== Bindings =====
    [BindProperty] public string PlateNumber { get; set; } = "";
    [BindProperty] public int MovementId { get; set; }
    [BindProperty] public int Quantity { get; set; } = 1;
    [BindProperty] public string ReferenceNumber { get; set; } = "";

    // ===== View Data =====
    public Vehicle? Vehicle { get; set; }
    public SelectList Movements { get; set; } = null!;
    public decimal UnitAmount { get; set; }
    public decimal Amount { get; set; }

    public bool ReferenceValid { get; set; }
    public bool ReferenceChecked { get; set; }
    public string? ReferenceMessage { get; set; }
    public string? ErrorMessage { get; set; }

    // 🔍 Payment History
    public List<Payment> Payments { get; set; } = new();

    // ===== GET =====
    public void OnGet()
    {
        Quantity = 1;
        LoadMovements();
    }

    // 🔍 SEARCH
    public void OnPostSearch()
    {
        NormalizeQuantity();
        LoadMovements();
        LoadVehicle();

        if (Vehicle == null)
            ErrorMessage = "Vehicle not found";
    }

    // 💰 CALCULATE
    public void OnPostCalculate()
    {
        NormalizeQuantity();
        LoadMovements();
        LoadVehicle();

        if (Vehicle == null || MovementId == 0)
            return;

        if (!ValidateReference())
            return;

        var tax = _context.TaxAmounts
            .Include(t => t.Movement)
            .FirstOrDefault(t =>
                t.CarTypeId == Vehicle.CarTypeId &&
                t.MovementId == MovementId);

        if (tax == null)
            return;

        UnitAmount = tax.Amount;
        Amount = UnitAmount * Quantity;
    }

    // ✅ COLLECT PAYMENT (SAFE VERSION)
    public IActionResult OnPostCollect()
    {
        using var transaction = _context.Database.BeginTransaction();

        try
        {
            NormalizeQuantity();
            LoadMovements();
            LoadVehicle();

            if (Vehicle == null)
                throw new Exception("Vehicle not found");

            if (MovementId == 0)
                throw new Exception("Movement type is required");

            if (!ValidateReference())
                throw new Exception(ReferenceMessage);

            // Load tax + movement safely
            var tax = _context.TaxAmounts
                .Include(t => t.Movement)
                .FirstOrDefault(t =>
                    t.CarTypeId == Vehicle.CarTypeId &&
                    t.MovementId == MovementId);

            if (tax == null)
                throw new Exception("Tax amount not configured");

            // Load receipt safely
            var receipt = _context.ReceiptReferences
                .FirstOrDefault(r => r.ReferenceNumber == ReferenceNumber);

            if (receipt == null)
                throw new Exception("Receipt number not found");

            if (receipt.IsUsed)
                throw new Exception("Receipt number already used");

            var payment = new Payment
            {
                VehicleId = Vehicle.Id,
                MovementId = tax.MovementId,
                MovementType = tax.Movement?.Name ?? "Unknown",
                Amount = tax.Amount * Quantity,
                PaidAt = DateTime.UtcNow,
                ReceiptReferenceId = receipt.Id,

                // TODO: replace with logged-in user id
                CollectorId = 1
            };

            _context.Payments.Add(payment);

            // Lock receipt
            receipt.IsUsed = true;
            receipt.UsedAt = DateTime.UtcNow;

            _context.SaveChanges();
            transaction.Commit();

            TempData["SuccessMessage"] = "Payment collected successfully";
            return RedirectToPage();
        }
        catch (Exception ex)
        {
            transaction.Rollback();
            ErrorMessage = ex.Message;
            return Page();
        }
    }

    // 🔁 REVERT PAYMENT
    public IActionResult OnPostRevert(int paymentId, string reason)
    {
        using var transaction = _context.Database.BeginTransaction();

        try
        {
            var payment = _context.Payments
                .Include(p => p.ReceiptReference)
                .FirstOrDefault(p => p.Id == paymentId);

            if (payment == null)
                throw new Exception("Payment not found");

            if (payment.IsReverted)
                throw new Exception("Payment already reverted");

            payment.IsReverted = true;
            payment.RevertReason = reason;
            payment.RevertedAt = DateTime.UtcNow;

            if (payment.ReceiptReference != null)
            {
                payment.ReceiptReference.IsUsed = false;
                payment.ReceiptReference.UsedAt = null;
            }

            _context.SaveChanges();
            transaction.Commit();

            TempData["SuccessMessage"] = "Payment reverted successfully";
            return RedirectToPage();
        }
        catch (Exception ex)
        {
            transaction.Rollback();
            ErrorMessage = ex.Message;
            return Page();
        }
    }

    // ===== Helpers =====
    private void NormalizeQuantity()
    {
        Quantity = Quantity <= 0 ? 1 : Quantity;
    }

    private bool ValidateReference()
    {
        ReferenceChecked = true;

        if (string.IsNullOrWhiteSpace(ReferenceNumber))
        {
            ReferenceMessage = "Receipt number is required";
            ReferenceValid = false;
            return false;
        }

        var reference = _context.ReceiptReferences
            .FirstOrDefault(r => r.ReferenceNumber == ReferenceNumber);

        if (reference == null)
        {
            ReferenceMessage = "Receipt number not found";
            ReferenceValid = false;
            return false;
        }

        if (reference.IsUsed)
        {
            ReferenceMessage = "Receipt number already used";
            ReferenceValid = false;
            return false;
        }

        ReferenceValid = true;
        return true;
    }

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
        Movements = new SelectList(
            _context.Movements.AsNoTracking(),
            "Id",
            "Name"
        );
    }
}
