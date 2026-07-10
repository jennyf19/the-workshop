using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

namespace WorkshopRoom.Tray;

/// <summary>
/// Owns the hidden WorkshopRoom.exe child: resolves its path, launches it on a
/// loopback URL with no console window, drains its stdout/stderr to a log file,
/// keeps it under a Job Object (so a tray crash reaps it), waits for it to
/// accept connections, and stops it. The web app has no graceful-shutdown
/// endpoint, so Stop kills the process tree — fine for a local dev dashboard.
/// </summary>
internal sealed class WebHostProcess : IDisposable
{
    private const string PreferredPortVar = "WORKSHOP_TRAY_PORT";
    private const string WebExeOverrideVar = "WORKSHOP_WEB_EXE";
    private const int PreferredPort = 5099;

    private readonly object _logLock = new();
    private readonly int _port;

    private Process? _process;
    private NativeJob? _job;
    private StreamWriter? _logWriter;
    private volatile bool _stopRequested;

    /// <summary>Loopback URL the web app listens on, e.g. http://localhost:5099.</summary>
    public string BaseUrl { get; }

    /// <summary>Resolved path of the web exe (may not exist yet — checked at Start).</summary>
    public string WebExePath { get; }

    /// <summary>File the child's console output is mirrored to.</summary>
    public string LogFilePath { get; }

    /// <summary>Raised (on a thread-pool thread) when the child exits without a
    /// Stop being requested.</summary>
    public event Action? ExitedUnexpectedly;

    public WebHostProcess()
    {
        _port = PickPort();
        BaseUrl = $"http://localhost:{_port}";
        WebExePath = ResolveWebExe();

        var logDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "the-workshop", "logs");
        Directory.CreateDirectory(logDir);
        LogFilePath = Path.Combine(logDir, $"web-{DateTime.Now:yyyyMMdd-HHmmss}.log");
    }

    /// <summary>Launches the hidden web child. Throws if the exe is missing.</summary>
    public void Start()
    {
        if (!File.Exists(WebExePath))
        {
            throw new FileNotFoundException(
                $"Could not find the workshop web server.\nExpected: {WebExePath}\n\n" +
                "Build the solution (dotnet build the-workshop.slnx) so WorkshopRoom.exe exists.",
                WebExePath);
        }

        _logWriter = new StreamWriter(LogFilePath, append: true) { AutoFlush = true };

        var psi = new ProcessStartInfo
        {
            FileName = WebExePath,
            WorkingDirectory = Path.GetDirectoryName(WebExePath)!,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        psi.Environment["ASPNETCORE_URLS"] = BaseUrl;

        var process = new Process { StartInfo = psi, EnableRaisingEvents = true };
        process.OutputDataReceived += (_, e) => WriteLog(e.Data);
        process.ErrorDataReceived += (_, e) => WriteLog(e.Data);
        process.Exited += OnProcessExited;

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        _process = process;

        // Supervise: reap the child if the tray dies abnormally.
        _job = NativeJob.Create();
        _job?.Assign(process);
    }

    /// <summary>Polls until the web accepts a loopback connection on its port, or
    /// the child exits, or the timeout elapses. (No /healthz on the web app, so a
    /// successful connect is our "ready" signal.)</summary>
    public async Task<bool> WaitForReadyAsync(TimeSpan timeout, CancellationToken ct = default)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (ct.IsCancellationRequested || _process is { HasExited: true })
            {
                return false;
            }

            try
            {
                using var client = new TcpClient();
                await client.ConnectAsync(IPAddress.Loopback, _port, ct).ConfigureAwait(false);
                if (client.Connected)
                {
                    return true;
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                return false;
            }
            catch
            {
                // Not listening yet — keep polling.
            }

            try
            {
                await Task.Delay(200, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return false;
            }
        }

        return false;
    }

    /// <summary>Stops the child (kill the process tree). Idempotent.</summary>
    public void Stop()
    {
        _stopRequested = true;
        var process = _process;
        if (process is not null && !process.HasExited)
        {
            try { process.Kill(entireProcessTree: true); }
            catch { /* already gone */ }
            try { process.WaitForExit(4000); }
            catch { /* best effort */ }
        }
        DisposeJob();
    }

    public void Dispose()
    {
        if (!_stopRequested)
        {
            Stop();
        }
        DisposeJob();
        _process?.Dispose();
        lock (_logLock)
        {
            _logWriter?.Dispose();
            _logWriter = null;
        }
    }

    private void OnProcessExited(object? sender, EventArgs e)
    {
        if (!_stopRequested)
        {
            ExitedUnexpectedly?.Invoke();
        }
    }

    private void WriteLog(string? line)
    {
        if (line is null)
        {
            return;
        }

        lock (_logLock)
        {
            _logWriter?.WriteLine(line);
        }
    }

    private void DisposeJob()
    {
        _job?.Dispose();
        _job = null;
    }

    private static int PickPort()
    {
        var preferred = PreferredPort;
        var configured = Environment.GetEnvironmentVariable(PreferredPortVar);
        if (int.TryParse(configured, out var parsed) && parsed is > 0 and < 65536)
        {
            preferred = parsed;
        }

        if (IsPortFree(preferred))
        {
            return preferred;
        }

        // Preferred port busy (e.g. a dev instance already running) — grab a
        // free ephemeral port. The tray owns the browser-open URL, so the exact
        // number doesn't need to be memorable.
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private static bool IsPortFree(int port)
    {
        try
        {
            var listener = new TcpListener(IPAddress.Loopback, port);
            listener.Start();
            listener.Stop();
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string ResolveWebExe()
    {
        const string exeName = "WorkshopRoom.exe";
        const string dllName = "WorkshopRoom.dll";

        // The apphost (.exe) is a shim that loads the matching managed .dll from
        // its own folder; a lone .exe is not runnable. Require the companion
        // .dll so we never select a dead shim.
        static bool Runnable(string exePath) =>
            File.Exists(exePath) &&
            File.Exists(Path.Combine(Path.GetDirectoryName(exePath)!, dllName));

        // 1. Explicit override (published / custom layouts).
        var overridePath = Environment.GetEnvironmentVariable(WebExeOverrideVar);
        if (!string.IsNullOrWhiteSpace(overridePath) && Runnable(overridePath))
        {
            return overridePath;
        }

        var baseDir = AppContext.BaseDirectory;

        // 2. Side-by-side (published single-folder layout: tray + web together).
        var sideBySide = Path.Combine(baseDir, exeName);
        if (Runnable(sideBySide))
        {
            return sideBySide;
        }

        // 3. Dev layout: derive the sibling web bin folder for the same config.
        //    baseDir = ...\src\WorkshopRoom.Tray\bin\<Config>\net10.0-windows\
        try
        {
            var tfmDir = new DirectoryInfo(baseDir.TrimEnd(Path.DirectorySeparatorChar));
            var configDir = tfmDir.Parent;                  // <Config>
            var srcDir = configDir?.Parent?.Parent?.Parent; // bin -> WorkshopRoom.Tray -> src
            if (configDir is not null && srcDir is not null)
            {
                // Returned whether or not it's runnable, so a missing build
                // yields a precise "expected here" message from Start().
                return Path.Combine(
                    srcDir.FullName, "WorkshopRoom", "bin", configDir.Name, "net10.0", exeName);
            }
        }
        catch
        {
            // Fall through to the side-by-side guess.
        }

        return sideBySide;
    }
}
