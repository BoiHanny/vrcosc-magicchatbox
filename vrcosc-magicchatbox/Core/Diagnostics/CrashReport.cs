using System;
using System.Globalization;
using System.Text;

namespace vrcosc_magicchatbox.Core.Diagnostics;

public static class CrashReport
{
    public static string Format(
        string? appVersion,
        string? message,
        string? stackTrace,
        string? logPath,
        string osDescription,
        DateTimeOffset occurredAt)
    {
        var report = new StringBuilder();

        report.Append("MagicChatbox ").AppendLine(Fallback(appVersion, "version unknown"));
        report.Append("Windows: ").AppendLine(Fallback(osDescription, "unknown"));
        report.Append("When: ").AppendLine(occurredAt.ToUniversalTime().ToString("u", CultureInfo.InvariantCulture));

        if (!string.IsNullOrWhiteSpace(logPath))
        {
            report.Append("Log: ").AppendLine(logPath.Trim());
        }

        report.AppendLine();
        report.AppendLine("Error");
        report.AppendLine(Fallback(message, "(no message)"));

        report.AppendLine();
        report.AppendLine("Stack trace");
        report.AppendLine(Fallback(stackTrace, "(no stack trace)"));

        return report.ToString().TrimEnd() + Environment.NewLine;
    }

    private static string Fallback(string? value, string whenEmpty) =>
        string.IsNullOrWhiteSpace(value) ? whenEmpty : value.Trim();
}
