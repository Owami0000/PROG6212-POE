namespace RaceDay.Mvc.Helpers;

/// <summary>Display helpers shared by the views.</summary>
public static class Format
{
    public static string Ordinal(int n)
    {
        var suffix = (n % 100) is 11 or 12 or 13
            ? "th"
            : (n % 10) switch { 1 => "st", 2 => "nd", 3 => "rd", _ => "th" };
        return $"{n}{suffix}";
    }

    public static string Time(TimeSpan t) => $"{(int)t.TotalHours:D2}:{t.Minutes:D2}:{t.Seconds:D2}";

    public static string Date(DateTime d) => d.ToString("dd MMM yyyy");

    public static string TypeIcon(string eventType) => eventType switch
    {
        "Run" => "🏃",
        "Walk" => "🚶",
        "Cycle" => "🚴",
        _ => "🏅"
    };

    /// <summary>Parses hh:mm:ss (e.g. 01:45:30). "45:30" is rejected to avoid it being read as 45 hours.</summary>
    public static bool TryParseFinishTime(string? input, out TimeSpan time)
    {
        time = default;
        if (string.IsNullOrWhiteSpace(input)) return false;
        var parts = input.Trim().Split(':');
        if (parts.Length != 3) return false;
        if (!int.TryParse(parts[0], out var h) || !int.TryParse(parts[1], out var m) || !int.TryParse(parts[2], out var s)) return false;
        if (h < 0 || h > 99 || m is < 0 or > 59 || s is < 0 or > 59) return false;
        time = new TimeSpan(h, m, s);
        return time > TimeSpan.Zero;
    }
}
