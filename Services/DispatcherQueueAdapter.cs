using Microsoft.UI.Dispatching;

namespace SwitchCast.Services;

/// <summary>
/// Adapts Microsoft.UI.Dispatching.DispatcherQueue to the IUIThreadDispatcher interface.
/// </summary>
public sealed class DispatcherQueueAdapter : IUIThreadDispatcher
{
    private readonly DispatcherQueue _dispatcherQueue;

    public DispatcherQueueAdapter(DispatcherQueue dispatcherQueue)
    {
        _dispatcherQueue = dispatcherQueue ?? throw new ArgumentNullException(nameof(dispatcherQueue));
    }

    public DispatcherQueue DispatcherQueue => _dispatcherQueue;

    public bool HasThreadAccess => _dispatcherQueue.HasThreadAccess;

    public bool TryEnqueue(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        return _dispatcherQueue.TryEnqueue(() => action());
    }
}

/// <summary>
/// Immediate execution dispatcher for synchronous headless unit tests.
/// </summary>
public sealed class ImmediateUIThreadDispatcher : IUIThreadDispatcher
{
    public static ImmediateUIThreadDispatcher Instance { get; } = new();

    public bool HasThreadAccess => true;

    public bool TryEnqueue(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        action();
        return true;
    }
}

/// <summary>
/// Central resolver for UI thread dispatchers across ViewModels and services.
/// </summary>
public static class UIThreadDispatcherProvider
{
    private static IUIThreadDispatcher? _testDispatcher;

    /// <summary>
    /// Gets or sets an explicit test dispatcher for test environments.
    /// </summary>
    public static IUIThreadDispatcher? TestDispatcher
    {
        get => _testDispatcher;
        set => _testDispatcher = value;
    }

    /// <summary>
    /// Resolves the appropriate UI thread dispatcher.
    /// In production, uses the current thread's DispatcherQueue if present.
    /// In headless unit test runners (where Application.Current is null), defaults to ImmediateUIThreadDispatcher.
    /// </summary>
    public static IUIThreadDispatcher? Resolve(IUIThreadDispatcher? explicitDispatcher = null)
    {
        if (explicitDispatcher is not null)
        {
            return explicitDispatcher;
        }

        if (_testDispatcher is not null)
        {
            return _testDispatcher;
        }

        if (DispatcherQueue.GetForCurrentThread() is { } dq)
        {
            return new DispatcherQueueAdapter(dq);
        }

        // In test environments where DispatcherQueue is unavailable, default to ImmediateUIThreadDispatcher.
        // In production runtime, windows explicitly configure their owning DispatcherQueue via SetDispatcherQueue.
        return ImmediateUIThreadDispatcher.Instance;
    }
}
