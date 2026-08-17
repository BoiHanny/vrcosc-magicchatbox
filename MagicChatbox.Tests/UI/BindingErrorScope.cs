using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Windows;

namespace MagicChatbox.Tests.UI;

/// <summary>
/// Collects the binding failures WPF reports while something is being built.
/// </summary>
/// <remarks>
/// A binding to a property that does not exist does not throw and does not render an error - it
/// renders nothing, and the control looks like a control whose value happens to be empty. The shipped
/// app bound `{Binding Description}` against a record whose member is called `Notes`, and the only
/// symptom was a blank line under every control parameter. WPF does report it, but only to this trace
/// source, which nothing was listening to.
/// </remarks>
internal sealed class BindingErrorScope : IDisposable
{
    private readonly CollectingListener _listener = new();
    private readonly SourceLevels _previousLevel;
    private bool _disposed;

    public BindingErrorScope()
    {
        _previousLevel = PresentationTraceSources.DataBindingSource.Switch.Level;

        PresentationTraceSources.Refresh();
        PresentationTraceSources.DataBindingSource.Listeners.Add(_listener);
        PresentationTraceSources.DataBindingSource.Switch.Level = SourceLevels.Warning;
    }

    public IReadOnlyList<string> Errors => _listener.Snapshot();

    /// <summary>
    /// The failures with the noise removed - see <see cref="CollectingListener.IsNoise"/>.
    /// </summary>
    public IReadOnlyList<string> RealErrors
        => Errors.Where(e => !CollectingListener.IsNoise(e)).ToList();

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        PresentationTraceSources.DataBindingSource.Listeners.Remove(_listener);
        PresentationTraceSources.DataBindingSource.Switch.Level = _previousLevel;
    }

    private sealed class CollectingListener : TraceListener
    {
        private readonly Lock _gate = new();
        private readonly List<string> _messages = [];

        public IReadOnlyList<string> Snapshot()
        {
            lock (_gate) return _messages.ToArray();
        }

        public override void Write(string? message) => Record(message);

        public override void WriteLine(string? message) => Record(message);

        public override void TraceEvent(
            TraceEventCache? eventCache, string source, TraceEventType eventType, int id, string? message)
        {
            if (eventType is TraceEventType.Error or TraceEventType.Warning)
                Record(message);
        }

        private void Record(string? message)
        {
            if (string.IsNullOrWhiteSpace(message))
                return;

            lock (_gate) _messages.Add(message);
        }

        /// <summary>
        /// Failures that say nothing about the markup being tested.
        /// </summary>
        /// <remarks>
        /// A page built outside the real window tree has no inherited DataContext at the moment its
        /// own template is applied, and a design-time-only d: binding is not a runtime binding at all.
        /// Both report through the same channel as a genuine typo, so they are named here rather than
        /// left to make the whole check unusable.
        /// </remarks>
        public static bool IsNoise(string message)
            => message.Contains("DataContext=null", StringComparison.Ordinal)
                || message.Contains("target element is not a FrameworkElement", StringComparison.Ordinal);
    }
}
