namespace VehicleTax.Web.Services;

/// <summary>
/// Every report, grouped. One list drives the Reports menu, the Reports
/// Center page and the tabs that link the reports of a group together.
/// Texts are English keys translated with Lang (LangSo).
/// </summary>
public static class ReportCatalog
{
    public record Report(string Page, string Title, string Icon, string Description, string Permission);

    public record Group(string Key, string Title, string Icon, string Color, string Description, Report[] Reports);

    public const string CenterPage = "/Reports/Center";

    public static readonly Group[] Groups =
    {
        new("overview", "Overview", "bi-speedometer2", "#0b57d0",
            "Today's money at a glance.",
            new[]
            {
                new Report("/Reports/Index", "Collections Dashboard", "bi-speedometer2",
                    "Totals for any period by collector, checkpoint and revenue account, with every payment.", "Reports.View"),
                new Report("/Finance/Index", "Finance Dashboard", "bi-cash-stack",
                    "Money per revenue account for a period; put payments into RVs for FMIS.", "Finance.View"),
            }),
        new("collections", "Collections", "bi-cash-coin", "#15803d",
            "What was collected, where and by whom.",
            new[]
            {
                new Report("/Reports/Summary", "Summary Report", "bi-file-earmark-bar-graph",
                    "Paid, pending and cancelled totals by day, collector and tariff.", "Reports.View"),
                new Report("/Reports/CheckpointReport", "Checkpoint Collection Report", "bi-geo-alt",
                    "What each checkpoint collected, with every receipt.", "Reports.View"),
                new Report("/Reports/PaymentSummary", "Payment Summary Report", "bi-bar-chart-line",
                    "Payments grouped by Golis transaction and status.", "Reports.View"),
            }),
        new("finance", "Revenue & Finance", "bi-wallet2", "#7c3aed",
            "Money per revenue account and what went to FMIS.",
            new[]
            {
                new Report("/Reports/RevenueAccountReport", "Revenue Account Report", "bi-wallet2",
                    "Collected per revenue account, with each account's payments.", "Reports.View"),
                new Report("/Finance/Rv/Index", "RV History", "bi-file-earmark-text",
                    "Every RV sent to the finance system (FMIS) and its status.", "Finance.View"),
            }),
        new("tariffs", "Tariffs & Tax", "bi-signpost-split", "#b45309",
            "Tariffs, tax amounts and what each tariff brought in.",
            new[]
            {
                new Report("/Reports/Movements", "Tariff Types Report", "bi-signpost-split",
                    "Every tariff with its car type, account, amount and status; collected per checkpoint.", "Reports.View"),
                new Report("/Reports/AccountTariffs", "Revenue Account Tariffs", "bi-diagram-2",
                    "Each revenue account with its tariffs and the tax amount per car type.", "Reports.View"),
                new Report("/Reports/TaxMovement", "Tax Amount by Tariff Report", "bi-bar-chart",
                    "Tax collected per tariff.", "Reports.View"),
                new Report("/Reports/TaxMovementCarType", "Tax Amount by Tariff & Car Type", "bi-diagram-3",
                    "Tax collected per tariff and car type.", "Reports.View"),
                new Report("/Reports/TaxAmountReport", "Tax Amount Schedule", "bi-currency-dollar",
                    "The tax amount set for each tariff and car type.", "Reports.View"),
            }),
        new("registry", "Registry & Users", "bi-truck", "#0e7490",
            "Registered vehicles and system users.",
            new[]
            {
                new Report("/Reports/Vehicles", "Vehicle Report", "bi-truck",
                    "Registered vehicles by car type and owner.", "Reports.View"),
                new Report("/Reports/UsersReport", "User Activity Report", "bi-people",
                    "Users by role and status.", "Reports.View"),
            }),
        new("audit", "Audit & Control", "bi-shield-check", "#b91c1c",
            "Check every payment and every change.",
            new[]
            {
                new Report("/Transactions/Index", "Transactions Audit Log", "bi-arrow-left-right",
                    "Every payment, paid, pending or cancelled; sort, filter and export.", "Reports.View"),
                new Report("/Reports/Logs", "Logs & Audit Report", "bi-journal-text",
                    "Payment log with methods, Golis IDs and cancel reasons.", "Reports.View"),
                new Report("/Reports/GolisAudit", "Golis Audit", "bi-shield-check",
                    "Compare Golis payments with the system.", "Reports.View"),
                new Report("/Reports/GolisAuditHistory", "Golis Audit History", "bi-clock-history",
                    "Past Golis reconciliations and their differences.", "Reports.View"),
                new Report("/Reports/ActivityLog", "Activity Log", "bi-activity",
                    "Who logged in, and every record created, updated or deleted.", "Users.View"),
            }),
    };

    /// <summary>The group and report for a request path (e.g. "/reports/summary"), if it is a report.</summary>
    public static (Group Group, Report Report)? Find(string? path)
    {
        if (string.IsNullOrEmpty(path)) return null;
        var p = path.TrimEnd('/');
        foreach (var g in Groups)
            foreach (var r in g.Reports)
            {
                var page = r.Page.EndsWith("/Index") ? r.Page[..^"/Index".Length] : r.Page;
                if (p.Equals(r.Page, StringComparison.OrdinalIgnoreCase) || p.Equals(page, StringComparison.OrdinalIgnoreCase))
                    return (g, r);
            }
        return null;
    }
}
