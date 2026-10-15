namespace SwitchCast.Services;

/// <summary>
/// Abstraction for marshaling UI-bound property notifications and actions to the owning window's UI thread.
/// Decouples ViewModels and services from hard dependencies on Microsoft.UI.Dispatching in headless tests.
/// </summary>
public interface IUIThreadDispatcher
{
    /// <summary>
    /// Gets a value indicating whether the current thread is the UI thread associated with this dispatcher.
    /// </summary>
    bool HasThreadAccess { get; }

    /// <summary>
    /// Enqueues an action to be executed asynchronously on the owning UI thread.
    /// Returns true if enqueued successfully; false otherwise.
    /// </summary>
    bool TryEnqueue(Action action);
}
