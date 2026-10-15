namespace VehicleTax.Web.Services;

/// <summary>Paging state for the shared _Pager partial.</summary>
public sealed class Pager
{
    public static readonly int[] Sizes = { 10, 20, 50, 100, 200 };

    /// <summary>
    /// Page size that older report pages use for "All" (their paging code
    /// then takes every row on one page); the pager shows it as All.
    /// </summary>
    public const int AllRows = 1_000_000;

    /// <summary>Total matching rows.</summary>
    public int Total { get; }
    /// <summary>Rows per page; 0 shows everything.</summary>
    public int PageSize { get; }
    /// <summary>1-based page, clamped to the available pages.</summary>
    public int Page { get; }
    /// <summary>Query-string name of the page number (e.g. "CurrentPage").</summary>
    public string PageParam { get; }

    public Pager(int total, int page, int pageSize, string pageParam = "CurrentPage")
    {
        Total = Math.Max(0, total);
        PageSize = pageSize >= AllRows ? 0 : Math.Max(0, pageSize);
        PageParam = pageParam;
        Page = Math.Clamp(page, 1, TotalPages);
    }

    public int TotalPages => PageSize == 0 ? 1 : Math.Max(1, (int)Math.Ceiling(Total / (double)PageSize));
    public int Skip => PageSize == 0 ? 0 : (Page - 1) * PageSize;
    public int Take => PageSize == 0 ? int.MaxValue : PageSize;
    public int From => Total == 0 ? 0 : Skip + 1;
    public int To => Math.Min(Total, Skip + (PageSize == 0 ? Total : PageSize));

    /// <summary>Page numbers to show, with 0 marking a gap (1 … 4 5 6 … 20).</summary>
    public IEnumerable<int> Window(int around = 2)
    {
        var last = TotalPages;
        var shown = new SortedSet<int> { 1, last };
        for (var i = Page - around; i <= Page + around; i++)
            if (i >= 1 && i <= last) shown.Add(i);
        var prev = 0;
        foreach (var p in shown)
        {
            if (prev != 0 && p - prev > 1) yield return 0;
            yield return p;
            prev = p;
        }
    }

    public IQueryable<T> Apply<T>(IQueryable<T> query) =>
        PageSize == 0 ? query : query.Skip(Skip).Take(PageSize);
}
