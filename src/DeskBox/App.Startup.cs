// Copyright (c) DeskBox. All rights reserved.

using DeskBox.Helpers;

namespace DeskBox;

/// <summary>
/// Startup resilience. The single-instance mutex is taken in the App
/// constructor, long before any window exists, so a startup that fails or
/// hangs must never leave the process running: it would own the lock while
/// offering no UI, and every later launch would just signal it and exit.
/// Fatal failures therefore always terminate the process, and a dedicated
/// watchdog covers the case where startup stops making progress instead of
/// throwing.
/// </summary>
public partial class App
{
    /// <summary>Startup must reach a usable tray surface within this window.</summary>
    private const int StartupWatchdogDeadlineMs = 90_000;

    /// <summary>How long the fatal dialog may stay up before the process exits anyway.</summary>
    private const int StartupFailureGraceMs = 20_000;

    private static int s_startupLifelineEstablished;
    private static int s_startupFailureHandled;
    private static int s_startupWatchdogArmed;

    /// <summary>
    /// True once the tray surface is usable. Before that point the process has
    /// nothing the user can act on, so exceptions and stalls are fatal.
    /// </summary>
    private static bool IsStartupLifelineEstablished =>
        Volatile.Read(ref s_startupLifelineEstablished) != 0;

    /// <summary>
    /// Arms the watchdog thread that terminates a startup which never reaches
    /// the lifeline. A dedicated thread, not the pool: startup work (snapshot
    /// zip, schtasks, WMI queries) can saturate the pool, and the whole point
    /// is to fire when the rest of the process is stuck.
    /// </summary>
    private static void StartStartupWatchdog()
    {
        if (Interlocked.Exchange(ref s_startupWatchdogArmed, 1) != 0)
        {
            return;
        }

        var watchdog = new Thread(() =>
        {
            int waitedMs = 0;
            while (waitedMs < StartupWatchdogDeadlineMs)
            {
                if (IsStartupLifelineEstablished)
                {
                    return;
                }

                Thread.Sleep(500);
                waitedMs += 500;
            }

            if (!IsStartupLifelineEstablished)
            {
                FailStartup(
                    $"startup did not reach a usable tray surface within {StartupWatchdogDeadlineMs} ms",
                    exception: null);
            }
        })
        {
            IsBackground = true,
            Name = "DeskBox startup watchdog"
        };

        watchdog.Start();
    }

    private static void MarkStartupLifelineEstablished()
    {
        Volatile.Write(ref s_startupLifelineEstablished, 1);
    }

    /// <summary>
    /// Reports a fatal startup failure and terminates the process. The
    /// single-instance mutex is deliberately left to the kernel: releasing it
    /// early would let a second instance start while this one is still writing
    /// logs and settings.
    /// </summary>
    private static void FailStartup(string reason, Exception? exception)
    {
        // The message box pumps messages, so the fatal path can be re-entered
        // from an unhandled exception while the dialog is up. Only the first
        // entry reports and exits.
        if (Interlocked.Exchange(ref s_startupFailureHandled, 1) != 0)
        {
            return;
        }

        Log($"[Startup] Fatal: {reason}" +
            (exception is null ? string.Empty : $": {exception}"));

        var exitThread = new Thread(() =>
        {
            Thread.Sleep(StartupFailureGraceMs);
            DrainLogQueue();
            Environment.Exit(1);
        })
        {
            IsBackground = true,
            Name = "DeskBox fatal startup exit"
        };

        exitThread.Start();

        try
        {
            Win32Helper.ShowFatalError(
                "DeskBox could not finish starting and is about to close.\n\n" +
                $"Details were written to the log:\n{LogPath}\n\n" +
                "You can start DeskBox again in a moment.",
                "DeskBox");
        }
        catch (Exception dialogFailure)
        {
            Log($"[Startup] Fatal dialog failed: {dialogFailure.Message}");
        }

        DrainLogQueue();
        Environment.Exit(1);
    }

    /// <summary>
    /// The process must end startup owning at least one surface the user can
    /// act on: the tray icon, or a widget window (widget windows legitimately
    /// exist while hidden, so visibility is not the criterion).
    /// </summary>
    private void EnsureStartupProducedUsableSurface()
    {
        if (IsStartupLifelineEstablished || WidgetManager?.LoadedWidgetCount > 0)
        {
            return;
        }

        FailStartup("no usable tray or widget surface was created", exception: null);
    }

    /// <summary>
    /// Runs a startup step that is not part of the lifeline. A failure degrades
    /// to a log line so the app stays usable instead of refusing to start.
    /// </summary>
    private static async Task RunOptionalStartupStepAsync(string name, Func<Task> step)
    {
        try
        {
            await step();
        }
        catch (Exception ex)
        {
            Log($"[Startup] Optional step '{name}' failed: {ex}");
        }
    }

    private static void RunOptionalStartupStep(string name, Action step)
    {
        try
        {
            step();
        }
        catch (Exception ex)
        {
            Log($"[Startup] Optional step '{name}' failed: {ex}");
        }
    }
}
