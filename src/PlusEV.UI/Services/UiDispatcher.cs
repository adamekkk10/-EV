using Avalonia.Threading;

namespace PlusEV.UI.Services;

/// <summary>Thin wrapper around Avalonia's dispatcher for easy testing.</summary>
public sealed class UiDispatcher
{
    public void Post(Action action) => Dispatcher.UIThread.Post(action);
    public Task InvokeAsync(Func<Task> asyncAction) => Dispatcher.UIThread.InvokeAsync(asyncAction);
    public Task InvokeAsync(Action action) => Dispatcher.UIThread.InvokeAsync(action).GetTask();
}
