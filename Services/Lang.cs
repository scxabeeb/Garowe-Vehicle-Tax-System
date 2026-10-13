namespace VehicleTax.Web.Services;

/// <summary>
/// Current UI language (English / Somali) for the web pages.
/// Chosen with /set-language/{code}, remembered in the "lang" cookie; a
/// "?lang=so" query parameter overrides it for one request (used by the
/// public receipt verification page). Pages write English and wrap it:
/// <c>@L["Payments"]</c>.
/// </summary>
public class Lang
{
    public const string CookieName = "lang";
    public const string English = "en";
    public const string Somali = "so";

    private readonly IHttpContextAccessor _http;

    public Lang(IHttpContextAccessor http)
    {
        _http = http;
    }

    public string Code => Resolve(_http.HttpContext);

    public bool IsSomali => Code == Somali;

    public string this[string english] => Translate(english, IsSomali);

    public static string Resolve(HttpContext? context)
    {
        var query = context?.Request.Query["lang"].ToString();
        var value = !string.IsNullOrEmpty(query)
            ? query
            : context?.Request.Cookies[CookieName];
        return value == Somali ? Somali : English;
    }

    public static string Translate(string english, bool somali) =>
        somali && LangSo.Strings.TryGetValue(english, out var so) ? so : english;
}
