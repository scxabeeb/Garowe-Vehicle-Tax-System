using System.Globalization;

namespace VehicleTax.Web.Services;

/// <summary>Amounts in CSV / Excel exports: "$1234.00" (Excel reads it as a dollar number).</summary>
public static class Money
{
    public static string Csv(decimal amount) => "$" + amount.ToString("0.00", CultureInfo.InvariantCulture);
    public static string Csv(decimal? amount) => amount.HasValue ? Csv(amount.Value) : "";
}
