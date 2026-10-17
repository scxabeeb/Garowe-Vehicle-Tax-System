namespace VehicleTax.Web.Services;

/// <summary>
/// Who collected a payment. A payment with a Golis transaction number was
/// paid by the customer through Golis; it is not counted as any collector's
/// collection, and is shown on its own line in per-collector figures.
/// </summary>
public static class PaymentSource
{
    public const string GolisLabel = "Golis (customer paid)";
}
