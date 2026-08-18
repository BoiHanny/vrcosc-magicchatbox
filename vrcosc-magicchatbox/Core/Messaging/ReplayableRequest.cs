using System;

namespace vrcosc_magicchatbox.Core.Messaging;

public sealed class ReplayableRequest<T>
{
    private Action<T>? _handlers;
    private T? _pending;
    private bool _hasPending;

    public event Action<T>? Requested
    {
        add
        {
            _handlers += value;

            if (_hasPending)
            {
                T target = _pending!;
                _hasPending = false;
                _pending = default;
                value?.Invoke(target);
            }
        }
        remove => _handlers -= value;
    }

    public void Raise(T value)
    {
        if (_handlers == null)
        {
            _pending = value;
            _hasPending = true;
            return;
        }

        _handlers.Invoke(value);
    }
}
