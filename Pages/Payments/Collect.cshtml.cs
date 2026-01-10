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

    // ===== FORM BINDINGS =====
    [BindProperty]
    public string PlateNumber { get; set; } = "";

    [BindProperty]
    public int MovementId { get; set; }

    [BindProperty]
    public int Quantity { get; set; } = 1;

    [BindProperty]
    public string ReferenceNumber { get; set; } = "";

    // ===== VIEW DATA =====
    public Vehicle? Vehicle { get; set; }
    public SelectList Movements { get; set; } = null!;
    public decimal UnitAmount { get; set; }
    public decimal Amount { get; set; }

    public bool ReferenceValid { get; set; }
    public bool ReferenceChecked { get; set; }
    public string? ReferenceMessage { get; set; }

    public string? ErrorMessage { get; set; }

    // ===== INITIAL LOAD =====
    public void OnGet()
    {
        Quantity = 1;
        LoadMovements();
    }

    // 🔍 SEARCH VEHICLE
    public void OnPostSearch()
    {
        NormalizeQuantity();
        LoadMovements();
        LoadVehicle();

        if (Vehicle == null)
            ErrorMessage = "Vehicle not found";
    }

    // 💰 CALCULATE TAX
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
                t.MovementId == MovementId
            );

        if (tax == null)
            return;

        UnitAmount = tax.Amount;
        Amount = UnitAmount * Quantity;
    }

    // ✅ COLLECT TAX
    public IActionResult OnPostCollect()
    {
        NormalizeQuantity();
        LoadMovements();
        LoadVehicle();

        if (Vehicle == null || MovementId == 0)
        {
            ErrorMessage = "Invalid data";
            return Page();
        }

        if (!ValidateReference())
        {
            ErrorMessage = ReferenceMessage;
            return Page();
        }

        var tax = _context.TaxAmounts
            .Include(t => t.Movement)
            .FirstOrDefault(t =>
                t.CarTypeId == Vehicle.CarTypeId &&
                t.MovementId == MovementId
            );

        if (tax == null)
        {
            ErrorMessage = "Tax amount not configured";
            return Page();
        }

        var totalAmount = tax.Amount * Quantity;

        // 🔎 GET RECEIPT
        var receipt = _context.ReceiptReferences
            .First(r => r.ReferenceNumber == ReferenceNumber);

        // 💾 SAVE PAYMENT WITH RECEIPT LINK
        var payment = new Payment
        {
            VehicleId = Vehicle.Id,
            MovementId = tax.MovementId,
            MovementType = tax.Movement?.Name ?? "",
            Amount = totalAmount,
            PaidAt = DateTime.Now,
            ReceiptReferenceId = receipt.Id   // ✅ LINK RECEIPT
        };

        _context.Payments.Add(payment);

        // 🔒 LOCK RECEIPT
        receipt.IsUsed = true;
        receipt.UsedAt = DateTime.Now;

        _context.SaveChanges();

        TempData["SuccessMessage"] = "Tax collected successfully";
        return RedirectToPage();
    }

    // ===== HELPERS =====
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
