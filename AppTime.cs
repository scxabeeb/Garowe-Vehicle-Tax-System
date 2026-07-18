namespace VehicleTax.Web;

public static class AppTime
{
    private static TimeZoneInfo _timeZone = CreateTimeZone(3);

    public static void Configure(int utcOffsetHours)
    {
        _timeZone = CreateTimeZone(utcOffsetHours);
    }

    public static DateTime UtcNow => DateTime.UtcNow;

    public static DateTime Now => ToLocal(DateTime.UtcNow);

    public static DateTime Today => Now.Date;

    public static DateTime ToLocal(DateTime value)
    {
        var utcValue = value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };

        return TimeZoneInfo.ConvertTimeFromUtc(utcValue, _timeZone);
    }

    public static DateTime ToUtc(DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(value, DateTimeKind.Unspecified), _timeZone)
        };
    }

    public static (DateTime StartUtc, DateTime EndUtc) GetUtcDayRange(DateTime localDay)
    {
        var startLocal = DateTime.SpecifyKind(localDay.Date, DateTimeKind.Unspecified);
        var endLocal = DateTime.SpecifyKind(localDay.Date.AddDays(1), DateTimeKind.Unspecified);

        return (ToUtc(startLocal), ToUtc(endLocal));
    }

    private static TimeZoneInfo CreateTimeZone(int utcOffsetHours)
    {
        var offset = TimeSpan.FromHours(utcOffsetHours);
        var sign = utcOffsetHours >= 0 ? "+" : "-";
        var label = $"UTC{sign}{Math.Abs(utcOffsetHours):00}:00";

        return TimeZoneInfo.CreateCustomTimeZone(label, offset, label, label);
    }
}