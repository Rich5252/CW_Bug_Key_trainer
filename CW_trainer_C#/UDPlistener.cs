using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CwTrainer.Serial;

/// <summary>
/// Joins the same multicast group as UdpTimingSender and receives DOWN/UP
/// keying events. Multiple independent listener processes can each construct
/// one of these against the same address/port and all receive every message -
/// this is the piece that makes fan-out to several analysis tools trivial.
///
/// Threading contract matches KeyEventSerialPort exactly: construct this on
/// your UI thread so it can capture the SynchronizationContext, and every
/// public event is marshalled onto that context via Post (never Send, so the
/// receive loop is never blocked waiting on a UI handler). This means an
/// analyzer coded against IKeyEventSource can be pointed at either source
/// with no threading surprises.
///
/// Usage:
///   var listener = new UdpTimingListener(); // construct on the UI thread
///   listener.KeyEventReceived += (sender, evt) => Console.WriteLine($"{evt} received");
///   listener.Start();
///   ...
///   listener.Stop();
/// </summary>
public sealed class UdpTimingListener : IKeyEventSource, IDisposable
{
    private readonly SynchronizationContext _syncContext;
    private readonly UdpClient _udpClient;
    private CancellationTokenSource _cts;
    private Task _listenTask;
    private volatile bool _disposed;

    /// <summary>Raised whenever a complete, parsed key event line is received.</summary>
    public event EventHandler<KeyEvent> KeyEventReceived;

    /// <summary>Raised when a datagram arrives that couldn't be parsed - useful for diagnostics/logging, not fatal.</summary>
    public event EventHandler<string> UnparsedLineReceived;

    /// <param name="multicastAddress">
    /// Must be in the 224.0.0.0-239.255.255.255 range. 239.255.x.x is part of the
    /// "administratively scoped" (site-local) range, a safe default for
    /// machine-local use. Must match UdpTimingSender's address.
    /// </param>
    /// <param name="port">UDP port for the group. Must match UdpTimingSender's port.</param>
    public UdpTimingListener(string multicastAddress = "239.255.42.99", int port = 6789)
    {
        _syncContext = SynchronizationContext.Current
            ?? throw new InvalidOperationException(
                "UdpTimingListener must be constructed on a thread with a " +
                "SynchronizationContext (e.g. the UI thread), matching " +
                "KeyEventSerialPort's threading contract. Construct it from " +
                "your Form's constructor or Load event handler.");

        _udpClient = new UdpClient();

        // Let multiple processes bind the same port on this machine.
        _udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        _udpClient.ExclusiveAddressUse = false;

        _udpClient.Client.Bind(new IPEndPoint(IPAddress.Any, port));
        _udpClient.JoinMulticastGroup(IPAddress.Parse(multicastAddress));
    }

    /// <summary>Starts the receive loop on a background task. Safe to call once.</summary>
    public void Start()
    {
        if (_listenTask != null) return;

        _cts = new CancellationTokenSource();
        _listenTask = Task.Run(() => ListenLoopAsync(_cts.Token));
    }

    /// <summary>Stops the receive loop. Safe to call even if not started.</summary>
    public void Stop()
    {
        _cts?.Cancel();
    }

    private async Task ListenLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            UdpReceiveResult result;
            try
            {
                result = await _udpClient.ReceiveAsync(token);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch (SocketException)
            {
                continue; // transient - keep listening
            }

            string text = Encoding.ASCII.GetString(result.Buffer).Trim();
            if (text.Length == 0) continue;

            if (KeyEventLineParser.TryParse(text, out KeyEvent evt))
                RaiseOnUiThread(() => KeyEventReceived?.Invoke(this, evt));
            else
                RaiseOnUiThread(() => UnparsedLineReceived?.Invoke(this, text));
        }
    }

    private void RaiseOnUiThread(Action action)
    {
        if (_disposed) return;
        // Post (not Send) so the receive loop never blocks waiting for the
        // UI thread - same reasoning as KeyEventSerialPort.
        _syncContext.Post(_ => action(), null);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Stop();
        _udpClient?.Dispose();
    }
}