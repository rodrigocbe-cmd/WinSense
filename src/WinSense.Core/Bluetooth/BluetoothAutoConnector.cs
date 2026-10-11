namespace WinSense.Core.Bluetooth;

/// <param name="Time">When the attempt finished.</param>
public sealed record ConnectAttempt(DateTime Time, PairedDevice Device, ConnectResult Result, string? Error = null);

/// <summary>
/// While running, every few seconds tries to connect each chosen paired device that is not connected.
/// One device at a time: each attempt keeps the radio busy for a few seconds, which can make
/// Bluetooth headphones stutter.
/// </summary>
/// <param name="shouldConnect">The user's choice for each device.</param>
public sealed class BluetoothAutoConnector(IBluetoothDevices bluetooth, Func<PairedDevice, bool> shouldConnect) : IDisposable
{
    public static readonly TimeSpan Interval = TimeSpan.FromSeconds(8);

    private CancellationTokenSource? _cts;

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
        _ = Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                await RunOnceAsync(token);
                try { await Task.Delay(Interval, token); }
                catch (OperationCanceledException) { break; }
            }
        }, token);
    }

    public void Stop()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
    }

    /// <summary>One round: list paired devices and try the chosen ones that are disconnected.</summary>
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

        foreach (var device in devices.Where(d => !d.IsConnected && d.CanAutoConnect && shouldConnect(d)))
        {
            if (cancellationToken.IsCancellationRequested)
                return;

            ConnectAttempt attempt;
            try
            {
                var result = await bluetooth.TryConnectAsync(device, cancellationToken);
                attempt = new ConnectAttempt(DateTime.Now, device, result);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                attempt = new ConnectAttempt(DateTime.Now, device, ConnectResult.Failed, ex.Message);
            }
            Attempted?.Invoke(attempt);
        }
    }

    public void Dispose() => Stop();
}
