namespace VehicleTax.Web.Security;

public static class Permissions
{
    // VEHICLES
    public const string VehicleView   = "vehicle.view";
    public const string VehicleCreate = "vehicle.create";
    public const string VehicleEdit   = "vehicle.edit";
    public const string VehicleDelete = "vehicle.delete";

    // PAYMENTS
    public const string PaymentView   = "payment.view";
    public const string PaymentCreate = "payment.create";
    public const string PaymentEdit   = "payment.edit";
    public const string PaymentDelete = "payment.delete";

    // USERS
    public const string UserView   = "user.view";
    public const string UserCreate = "user.create";
    public const string UserEdit   = "user.edit";
    public const string UserDelete = "user.delete";

    // REPORTS
    public const string ReportsView = "reports.view";
}
