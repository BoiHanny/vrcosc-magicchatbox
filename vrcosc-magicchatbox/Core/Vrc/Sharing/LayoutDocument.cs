using System;
using System.Collections.Generic;

namespace vrcosc_magicchatbox.Core.Vrc.Sharing;

public sealed class LayoutRequirement
{
    public string Name { get; set; } = string.Empty;

    public string Type { get; set; } = "Bool";

    public bool Optional { get; set; }

    public string Purpose { get; set; } = string.Empty;
}

public sealed class LayoutDocument
{
    public const string ExpectedKind = "mcb.layout";
    public const int CurrentSchema = 1;

    public string Kind { get; set; } = ExpectedKind;

    public int Schema { get; set; } = CurrentSchema;

    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string Author { get; set; } = string.Empty;

    public string License { get; set; } = string.Empty;

    public List<string> Tags { get; set; } = new();

    public List<LayoutRequirement> Requires { get; set; } = new();
}
