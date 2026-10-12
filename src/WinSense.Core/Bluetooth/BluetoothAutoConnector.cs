namespace WinSense.Core.Bluetooth;

/// <param name="Time">When the attempt finished.</param>
public sealed record ConnectAttempt(DateTime Time, PairedDevice Device, ConnectResult Result, string? Error = null);

/// <summary>
/// While running, every few seconds (and whenever a device calls the PC) tries to connect each chosen paired
/// device that is not connected and is available (see <see cref="PairedDevice.IsAvailable"/>), and pairs again
/// the chosen ones that forgot the PC and are in pairing mode.
/// One device at a time: each attempt keeps the radio busy for a few seconds, which can make
/// Bluetooth headphones stutter.
/// </summary>
/// <param name="shouldConnect">The user's choice for each device.</param>
/// <param name="shouldCall">Whether the user allowed WinSense to call (page) this device; it can turn a controller on.</param>
/// <param name="now">Clock, for tests.</param>
public sealed class BluetoothAutoConnector(
    IBluetoothDevices bluetooth,
    Func<PairedDevice, bool> shouldConnect,
    Func<PairedDevice, bool>? shouldCall = null,
    Func<DateTime>? now = null) : IDisposable
{
    public static readonly TimeSpan Interval = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Looking for devices in pairing mode takes the radio for ~3 s, so not every round. A DualShock 4 stays in
    /// pairing mode for well over this.
    /// </summary>
    public static readonly TimeSpan PairingModeScanInterval = TimeSpan.FromSeconds(10);

    private readonly Func<PairedDevice, bool> _shouldCall = shouldCall ?? (_ => false);
    private readonly Func<DateTime> _now = now ?? (() => DateTime.UtcNow);
    private DateTime _lastPairingModeScan = DateTime.MinValue;
    private CancellationTokenSource? _cts;
    private IDisposable? _watch;

    /// <summary>Raised from a background thread after every attempt.</summary>
    public event Action<ConnectAttempt>? Attempted;

    /// <summary>Raised from a background thread with the devices seen on each round.</summary>
    public event Action<IReadOnlyList<PairedDevice>>? DevicesRefreshed;

    public bool IsRunning => _cts is not null;

    public void Start()
    {
        if (_cts is not null)
            return;

        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        // A device calling the PC starts a round right away; the interval is the fallback.
        var wake = new SemaphoreSlim(0);
        try
        {
            _watch = bluetooth.WatchConnections(() => wake.Release());
        }
        catch (Exception)
        {
            // Radio off or missing: rounds still run on the interval.
        }

        _ = Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                await RunOnceAsync(token);
                try { await wake.WaitAsync(Interval, token); }
                catch (OperationCanceledException) { break; }
                while (wake.Wait(0)) { } // one round covers every wake-up so far
            }
        }, token);
    }

    public void Stop()
    {
        _watch?.Dispose();
        _watch = null;
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
    }

    /// <summary>
    /// One round: list paired devices, try the chosen ones that are disconnected and available, then pair again
    /// the chosen ones still disconnected that are in pairing mode.
    /// </summary>
    public async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<PairedDevice> devices;
        try
        {
            devices = await bluetooth.ListPairedAsync();
        }
        catch (Exception)
        {
            return; // Radio off or busy: try again next round.
        }
        DevicesRefreshed?.Invoke(devices);

        var missing = devices.Where(d => !d.IsConnected && d.CanAutoConnect && shouldConnect(d)).ToList();
        var connected = new HashSet<ulong>();
        foreach (var device in missing)
        {
            var call = _shouldCall(device);
            if (!device.IsAvailable && !call)
                continue;

            var attempt = await AttemptAsync(device, () => bluetooth.TryConnectAsync(device, call, cancellationToken), cancellationToken);
            if (attempt is null)
                return;
            if (attempt.Result is ConnectResult.Connected or ConnectResult.ConnectedAfterServiceRestart)
                connected.Add(device.Address);
        }

        missing.RemoveAll(d => connected.Contains(d.Address));
        if (missing.Count > 0 && _now() - _lastPairingModeScan >= PairingModeScanInterval)
            await RepairForgottenAsync(missing, cancellationToken);
    }

    private async Task RepairForgottenAsync(List<PairedDevice> missing, CancellationToken cancellationToken)
    {
        _lastPairingModeScan = _now();
        IReadOnlySet<ulong> inPairingMode;
        try
        {
            inPairingMode = await bluetooth.FindInPairingModeAsync(cancellationToken);
        }
        catch (Exception)
        {
            return; // Radio busy: next scan.
        }

        foreach (var device in missing.Where(d => inPairingMode.Contains(d.Address)))
        {
            if (await AttemptAsync(device, () => bluetooth.RepairAsync(device, cancellationToken), cancellationToken) is null)
                return;
        }
    }

    /// <summary>Runs one attempt and reports it. Null when cancelled.</summary>
    private async Task<ConnectAttempt?> AttemptAsync(PairedDevice device, Func<Task<ConnectResult>> attempt,
        CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
            return null;

        ConnectAttempt result;
        try
        {
            result = new ConnectAttempt(DateTime.Now, device, await attempt());
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch (Exception ex)
        {
            result = new ConnectAttempt(DateTime.Now, device, ConnectResult.Failed, ex.Message);
        }
        Attempted?.Invoke(result);
        return result;
    }

    public void Dispose() => Stop();
}
