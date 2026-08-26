using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Shapes;

namespace vrcosc_magicchatbox.Core.Diagnostics;

/// <summary>
/// Walks a live visual tree and counts the things that cost render and layout time, so a claim about a
/// page can be checked against what WPF actually built rather than against the XAML source.
/// </summary>
public static class VisualTreeCensus
{
    public static Result Take(DependencyObject root)
    {
        var result = new Result();
        Walk(root, 0, result);
        return result;
    }

    private static void Walk(DependencyObject node, int depth, Result result)
    {
        result.Total++;
        result.MaxDepth = Math.Max(result.MaxDepth, depth);

        string typeName = node.GetType().Name;
        result.ByType[typeName] = result.ByType.TryGetValue(typeName, out int count) ? count + 1 : 1;

        if (node is UIElement element)
        {
            if (element.Effect != null)
            {
                result.Effects++;
                result.EffectOwners.Add($"{typeName} ({element.Effect.GetType().Name})");
            }

            if (element.Visibility == Visibility.Hidden)
                result.HiddenNotCollapsed++;

            if (element.Opacity is > 0 and < 1)
                result.PartialOpacity++;

            if (element.CacheMode != null)
                result.CacheModes++;
        }

        if (node is Shape shapeLike && shapeLike.Fill is Brush fill && !fill.IsFrozen)
            result.UnfrozenBrushes++;

        if (node is Control control)
        {
            if (control.Background is { IsFrozen: false })
                result.UnfrozenBrushes++;

            if (control.Foreground is { IsFrozen: false })
                result.UnfrozenBrushes++;
        }

        if (node is Border border && border.Background is { IsFrozen: false })
            result.UnfrozenBrushes++;

        if (node is ItemsControl items)
            RecordItemsControl(items, typeName, result);

        int children = VisualTreeHelper.GetChildrenCount(node);
        for (int i = 0; i < children; i++)
            Walk(VisualTreeHelper.GetChild(node, i), depth + 1, result);
    }

    private static void RecordItemsControl(ItemsControl items, string typeName, Result result)
    {
        // The attached property says what was asked for; only the realised items host says what was got.
        Panel? host = FindItemsHost(items);

        result.ItemsControls.Add(new ItemsControlInfo(
            typeName,
            items.Name,
            items.Items.Count,
            host is VirtualizingPanel,
            items.ItemsSource != null,
            VirtualizingPanel.GetVirtualizationMode(items).ToString(),
            ScrollViewer.GetCanContentScroll(items),
            host?.GetType().Name ?? "(not realised)"));
    }

    private static Panel? FindItemsHost(DependencyObject node)
    {
        int children = VisualTreeHelper.GetChildrenCount(node);

        for (int i = 0; i < children; i++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(node, i);

            if (child is Panel { IsItemsHost: true } panel)
                return panel;

            // Stop at a nested ItemsControl so an outer list does not claim an inner list's panel.
            if (child is ItemsControl)
                continue;

            if (FindItemsHost(child) is { } found)
                return found;
        }

        return null;
    }

    public sealed class Result
    {
        public int Total { get; set; }

        public int MaxDepth { get; set; }

        public int Effects { get; set; }

        public int HiddenNotCollapsed { get; set; }

        public int PartialOpacity { get; set; }

        public int CacheModes { get; set; }

        public int UnfrozenBrushes { get; set; }

        public Dictionary<string, int> ByType { get; } = new(StringComparer.Ordinal);

        public List<string> EffectOwners { get; } = new();

        public List<ItemsControlInfo> ItemsControls { get; } = new();

        public string Describe(string label, int topTypes = 15)
        {
            var sb = new StringBuilder();

            sb.Append("[Perf] Visual tree '").Append(label).Append("': ")
                .Append(Total).Append(" elements, depth ").Append(MaxDepth)
                .Append(", effects ").Append(Effects)
                .Append(", Hidden-not-Collapsed ").Append(HiddenNotCollapsed)
                .Append(", partial opacity ").Append(PartialOpacity)
                .Append(", unfrozen brushes ").Append(UnfrozenBrushes)
                .Append(", CacheMode ").Append(CacheModes)
                .AppendLine();

            sb.Append("  types: ").AppendLine(string.Join(", ", ByType
                .OrderByDescending(p => p.Value)
                .Take(topTypes)
                .Select(p => $"{p.Key}={p.Value}")));

            if (EffectOwners.Count > 0)
                sb.Append("  effects on: ").AppendLine(string.Join(", ", EffectOwners.Distinct().Take(20)));

            foreach (ItemsControlInfo info in ItemsControls.OrderByDescending(i => i.ItemCount))
            {
                sb.Append("  ").Append(info.TypeName);
                if (!string.IsNullOrEmpty(info.Name))
                    sb.Append(" x:Name=").Append(info.Name);

                sb.Append(": items ").Append(info.ItemCount)
                    .Append(", panel ").Append(info.ItemsHostType)
                    .Append(", virtualizing ").Append(info.IsVirtualizing)
                    .Append(", bound ").Append(info.HasItemsSource)
                    .Append(", mode ").Append(info.VirtualizationMode)
                    .Append(", canContentScroll ").Append(info.CanContentScroll)
                    .AppendLine();
            }

            return sb.ToString();
        }
    }

    public readonly record struct ItemsControlInfo(
        string TypeName,
        string? Name,
        int ItemCount,
        bool IsVirtualizing,
        bool HasItemsSource,
        string VirtualizationMode,
        bool CanContentScroll,
        string ItemsHostType);
}
