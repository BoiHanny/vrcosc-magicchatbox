using System;
using System.Threading.Tasks;

namespace vrcosc_magicchatbox.Core.Toast;

/// <summary>
/// An optional clickable action attached to a toast notification.
/// Using Func&lt;Task&gt; allows async navigation/dialog work without async void.
/// </summary>
public sealed record ToastAction(string Label, Func<Task> Execute);
