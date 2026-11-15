using System;
using System.Threading.Tasks;

namespace R3Config.Svc;

// The one place that reaches the WPF Application's dispatcher.
//
// A background loop in a ViewModel must come back to the UI thread before it
// touches bound state. Asking here instead of reaching Application.Current
// keeps the ViewModel layer free of the WPF application object, so a ViewModel
// stays constructible and verifiable without a live Application, and the
// no-UI-thread case is handled in one place instead of at each call site.
public static class UiThread
{
    // False before the Application is created and after it shuts down. A
    // background loop that finds no UI thread has no bound state left to
    // update, so it stops rather than continuing blind.
    public static bool IsAvailable => Current() is not null;

    // Runs the asynchronous work on the UI thread and awaits it. Returns false
    // without running anything when there is no UI thread; the work itself is
    // never swallowed, so its exceptions still reach the caller.
    public static async Task<bool> InvokeAsync(Func<Task> work)
    {
        ArgumentNullException.ThrowIfNull(work);

        if (Current() is not { } dispatcher)
        {
            return false;
        }

        await dispatcher.InvokeAsync(work)
            .Task
            .Unwrap()
            .ConfigureAwait(false);

        return true;
    }

    private static System.Windows.Threading.Dispatcher? Current() =>
        System.Windows.Application.Current?.Dispatcher;
}
