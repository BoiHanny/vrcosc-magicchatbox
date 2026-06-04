using System;
using System.Collections.Generic;
using System.Linq;

public class ReproBug
{
    public const int MaxOscLength = 144;

    public static string AssembleMessage(IEnumerable<string> segments, string separator, string prefix, string suffix)
    {
        string message = string.Join(separator, segments);
        if (!string.IsNullOrEmpty(message))
            message = $"{prefix}{message}{suffix}";
        return message;
    }

    public static string ClampToOscLimit(string message)
    {
        if (string.IsNullOrEmpty(message) || message.Length <= MaxOscLength)
            return message ?? string.Empty;

        int cut = MaxOscLength;
        if (cut > 0 && char.IsHighSurrogate(message[cut - 1]))
            cut--;

        return message.Substring(0, cut);
    }

    public static string Build(List<(string Text, int Priority)> collected, string separator, string prefix, string suffix)
    {
        var trimmed = new List<string>();
        while (collected.Count > 0)
        {
            string message = AssembleMessage(collected.Select(c => c.Text), separator, prefix, suffix);
            if (message.Length <= MaxOscLength)
                break;

            int worstIdx = 0;
            for (int i = 1; i < collected.Count; i++)
            {
                if (collected[i].Priority > collected[worstIdx].Priority)
                    worstIdx = i;
            }

            trimmed.Add(collected[worstIdx].Text);
            collected.RemoveAt(worstIdx);
        }

        string finalMessage = collected.Count > 0
            ? AssembleMessage(collected.Select(c => c.Text), separator, prefix, suffix)
            : string.Empty;

        return ClampToOscLimit(finalMessage);
    }

    public static void Main()
    {
        // Scenario: Prefix + Suffix + Single Segment > 144
        string prefix = new string('P', 100);
        string suffix = new string('S', 50);
        string separator = " | ";
        var collected = new List<(string Text, int Priority)>
        {
            ("Important Segment", 0)
        };

        // Combined length: 100 + 16 + 50 = 166 > 144
        string result = Build(collected, separator, prefix, suffix);
        Console.WriteLine($"Result: '{result}'");
        Console.WriteLine($"Result Length: {result.Length}");

        if (result == string.Empty)
        {
            Console.WriteLine("BUG REPRODUCED: Result is empty even though there was an important segment!");
        }
        else
        {
            Console.WriteLine("Bug not reproduced.");
        }
    }
}
