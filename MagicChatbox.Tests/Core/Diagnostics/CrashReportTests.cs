using System;
using vrcosc_magicchatbox.Core.Diagnostics;
using Xunit;

namespace MagicChatbox.Tests.Core.Diagnostics;

/// <summary>
/// The dialog tells people to include the version, the message and the log. This is the text that
/// promise turns into, so it has to carry all three even when the exception is threadbare.
/// </summary>
public class CrashReportTests
{
    private static readonly DateTimeOffset When =
        new(2026, 8, 21, 18, 30, 0, TimeSpan.FromHours(2));

    private static string Report(
        string? version = "0.9.222",
        string? message = "Object reference not set to an instance of an object.",
        string? stack = "   at vrcosc_magicchatbox.Osc.Build()",
        string? log = @"C:\logs\2026-08-21.log") =>
        CrashReport.Format(version, message, stack, log, "Microsoft Windows 10.0.26200", When);

    [Fact]
    public void The_report_carries_everything_a_bug_report_needs()
    {
        string report = Report();

        Assert.Contains("MagicChatbox 0.9.222", report);
        Assert.Contains("Microsoft Windows 10.0.26200", report);
        Assert.Contains(@"C:\logs\2026-08-21.log", report);
        Assert.Contains("Object reference not set to an instance of an object.", report);
        Assert.Contains("at vrcosc_magicchatbox.Osc.Build()", report);
    }

    [Fact]
    public void The_timestamp_is_written_in_utc_so_reports_from_anywhere_line_up()
    {
        string report = Report();

        Assert.Contains("2026-08-21 16:30:00Z", report);
    }

    [Fact]
    public void A_missing_version_says_so_rather_than_leaving_a_blank()
    {
        string report = Report(version: null);

        Assert.Contains("MagicChatbox version unknown", report);
    }

    [Fact]
    public void A_missing_stack_trace_says_so_rather_than_leaving_a_blank()
    {
        string report = Report(stack: "   ");

        Assert.Contains("(no stack trace)", report);
    }

    [Fact]
    public void A_missing_message_says_so_rather_than_leaving_a_blank()
    {
        string report = Report(message: null);

        Assert.Contains("(no message)", report);
    }

    [Fact]
    public void The_log_line_is_dropped_entirely_when_there_is_no_log()
    {
        string report = Report(log: null);

        Assert.DoesNotContain("Log:", report);
    }

    [Fact]
    public void Both_sections_are_labelled_so_the_paste_is_readable()
    {
        string report = Report();

        Assert.Contains("Error", report);
        Assert.Contains("Stack trace", report);
        Assert.True(
            report.IndexOf("Error", StringComparison.Ordinal) < report.IndexOf("Stack trace", StringComparison.Ordinal),
            "the message should come before the stack trace");
    }

    [Fact]
    public void The_report_ends_with_exactly_one_newline_so_pasting_does_not_add_blank_lines()
    {
        string report = Report();

        Assert.EndsWith(Environment.NewLine, report);
        Assert.DoesNotContain(Environment.NewLine + Environment.NewLine, report[^4..]);
    }

    [Fact]
    public void Surrounding_whitespace_in_the_inputs_is_trimmed()
    {
        string report = CrashReport.Format(
            "  0.9.222  ",
            "  boom  ",
            "  at Thing()  ",
            "  C:\\logs\\x.log  ",
            "  Windows  ",
            When);

        Assert.Contains("MagicChatbox 0.9.222" + Environment.NewLine, report);
        Assert.Contains("Log: C:\\logs\\x.log" + Environment.NewLine, report);
        Assert.Contains("boom" + Environment.NewLine, report);
    }
}
