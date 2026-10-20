namespace Ascend.Api.Services;

public static class Clock
{
    public static DateTime UtcNowMs()
    {
        var now = DateTime.UtcNow;
        return new DateTime(now.Ticks - (now.Ticks % TimeSpan.TicksPerMillisecond), DateTimeKind.Utc);
    }

    public static string Iso(DateTime utc) => utc.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'");
}
