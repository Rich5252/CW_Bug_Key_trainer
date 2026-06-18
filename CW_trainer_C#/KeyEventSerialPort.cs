using System;
using System.Globalization;
using System.IO.Ports;
using System.Text;
using System.Threading;

namespace CwTrainer.Serial
{
    /// <summary>
    /// Connection state, exposed so the UI can show status without polling.
    /// </summary>
    public enum SerialConnectionState
    {
        Disconnected,
        Connecting,
        Connected,
        Reconnecting,
    }

    /// <summary>
    /// Wraps a System.IO.Ports.SerialPort to read the ESP32 CW trainer's
    /// "DOWN,&lt;us&gt;" / "UP,&lt;us&gt;" event stream.
    ///
    /// Design notes:
    /// - DataReceived fires on a thread-pool thread, not the UI thread. This
    ///   class captures the SynchronizationContext at construction time (call
    ///   it from your UI thread, e.g. in the form's constructor or Load
    ///   handler) and marshals all public events through it via Post, so
    ///   consumers can update UI controls directly in event handlers without
    ///   their own Invoke/BeginInvoke calls.
    /// - Incoming bytes are accumulated in a StringBuilder and split on \n,
    ///   NOT read via SerialPort.ReadLine() - DataReceived's timing has known
    ///   quirks combined with ReadLine's blocking behavior. Manual buffering
    ///   is more robust.
    /// - Basic reconnection: if the port closes unexpectedly (cable unplugged,
    ///   device reset), this class detects the exception typically thrown,
    ///   transitions to Reconnecting, and polls SerialPort.GetPortNames()
    ///   looking for the port to reappear.
    /// </summary>
    public sealed class KeyEventSerialPort : IDisposable
    {
        private readonly SynchronizationContext _syncContext;
        private readonly StringBuilder _lineBuffer = new StringBuilder();
        private readonly object _portLock = new object();

        private SerialPort? _port;
        private System.Threading.Timer? _reconnectTimer;
        private string? _targetPortName;
        private int _baudRate = 115200; // CDC ignores actual baud, but a value is required
        private volatile bool _disposed;
        private volatile SerialConnectionState _state = SerialConnectionState.Disconnected;

        /// <summary>Raised whenever a complete, parsed key event line is received.</summary>
        public event EventHandler<KeyEvent> KeyEventReceived;

        /// <summary>Raised when connection state changes (Disconnected/Connecting/Connected/Reconnecting).</summary>
        public event EventHandler<SerialConnectionState> ConnectionStateChanged;

        /// <summary>Raised when a line arrives that couldn't be parsed - useful for diagnostics/logging, not fatal.</summary>
        public event EventHandler<string> UnparsedLineReceived;

        public SerialConnectionState State => _state;
        public string? PortName => _targetPortName;

        /// <summary>
        /// Construct on your UI thread (e.g. in the Form constructor) so the
        /// SynchronizationContext capture is correct.
        /// </summary>
        public KeyEventSerialPort()
        {
            _syncContext = SynchronizationContext.Current
                ?? throw new InvalidOperationException(
                    "KeyEventSerialPort must be constructed on a thread with a " +
                    "SynchronizationContext (e.g. the UI thread). Construct it " +
                    "from your Form's constructor or Load event handler.");
        }

        /// <summary>
        /// Returns the currently available serial port names, for populating
        /// a connection dropdown. Call this fresh each time you show/refresh
        /// a port picker - port lists can change as devices connect/disconnect.
        /// </summary>
        public static string[] GetAvailablePorts() => SerialPort.GetPortNames();

        /// <summary>
        /// Opens the named port and begins listening. If the port later
        /// disconnects unexpectedly, this class will automatically attempt
        /// to reconnect to the same port name when it reappears.
        /// </summary>
        public void Connect(string portName, int baudRate = 115200)
        {
            _targetPortName = portName;
            _baudRate = baudRate;
            OpenPort();
        }

        /// <summary>
        /// Closes the port and stops any reconnection attempts. Safe to call
        /// even if not currently connected.
        /// </summary>
        public void Disconnect()
        {
            StopReconnectTimer();
            ClosePortQuiet();
            SetState(SerialConnectionState.Disconnected);
        }

        private void OpenPort()
        {
            if (_disposed) return;

            SetState(SerialConnectionState.Connecting);

            lock (_portLock)
            {
                ClosePortQuiet();

                try
                {
                    _port = new SerialPort(_targetPortName, _baudRate)
                    {
                        NewLine = "\n",
                        Encoding = Encoding.ASCII,
                        ReadTimeout = SerialPort.InfiniteTimeout,
                        WriteTimeout = 1000,
                        // DtrEnable matters for CDC-ACM devices - some firmware
                        // (including TinyUSB's tud_cdc_connected()) checks DTR
                        // to decide whether a host is "really" listening.
                        DtrEnable = true,
                        RtsEnable = true,
                    };
                    _port.DataReceived += OnDataReceived;
                    _port.ErrorReceived += OnErrorReceived;
                    _port.Open();

                    _lineBuffer.Clear();
                    StopReconnectTimer();
                    SetState(SerialConnectionState.Connected);
                }
                catch (Exception)
                {
                    // Port may not exist yet, or be in use, or briefly
                    // unavailable right after a USB re-enumeration. Fall
                    // back to polling for it rather than throwing - this
                    // keeps Connect() usable as a "try to connect, and keep
                    // trying" call rather than requiring the caller to
                    // retry manually.
                    ClosePortQuiet();
                    BeginReconnectPolling();
                }
            }
        }

        private void OnErrorReceived(object sender, SerialErrorReceivedEventArgs e)
        {
            // Framing/overrun errors etc. Treat as a sign the connection is
            // unhealthy and worth re-establishing, same as a hard disconnect.
            HandleUnexpectedDisconnect();
        }

        private void OnDataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            SerialPort port;
            lock (_portLock) { port = _port; }
            if (port == null) return;

            string chunk;
            try
            {
                // ReadExisting grabs whatever's currently buffered by the
                // OS/driver without blocking and without assuming line
                // boundaries align with this callback's firing - the
                // buffering/splitting below handles partial lines.
                chunk = port.ReadExisting();
            }
            catch (Exception)
            {
                HandleUnexpectedDisconnect();
                return;
            }

            if (string.IsNullOrEmpty(chunk)) return;

            ProcessIncomingText(chunk);
        }

        private void ProcessIncomingText(string chunk)
        {
            _lineBuffer.Append(chunk);

            // Extract and process every complete line currently in the
            // buffer; leave any trailing partial line for the next call.
            while (true)
            {
                string buffered = _lineBuffer.ToString();
                int newlineIndex = buffered.IndexOf('\n');
                if (newlineIndex < 0) break;

                string line = buffered.Substring(0, newlineIndex);
                _lineBuffer.Remove(0, newlineIndex + 1);

                line = line.TrimEnd('\r'); // strip CR left over from \r\n
                if (line.Length == 0) continue;

                ParseAndRaise(line);
            }
        }

        private void ParseAndRaise(string line)
        {
            // Expected format: "DOWN,<int64>" or "UP,<int64>"
            int commaIndex = line.IndexOf(',');
            if (commaIndex < 0)
            {
                RaiseUnparsedLine(line);
                return;
            }

            string keyword = line.Substring(0, commaIndex);
            string numberPart = line.Substring(commaIndex + 1);

            bool isDown;
            if (keyword == "DOWN") isDown = true;
            else if (keyword == "UP") isDown = false;
            else
            {
                RaiseUnparsedLine(line);
                return;
            }

            if (!long.TryParse(numberPart, NumberStyles.Integer, CultureInfo.InvariantCulture, out long timestampUs))
            {
                RaiseUnparsedLine(line);
                return;
            }

            var evt = new KeyEvent(isDown, timestampUs);
            RaiseOnUiThread(() => KeyEventReceived?.Invoke(this, evt));
        }

        private void RaiseUnparsedLine(string line)
        {
            RaiseOnUiThread(() => UnparsedLineReceived?.Invoke(this, line));
        }

        private void HandleUnexpectedDisconnect()
        {
            lock (_portLock)
            {
                ClosePortQuiet();
            }
            SetState(SerialConnectionState.Reconnecting);
            BeginReconnectPolling();
        }

        private void BeginReconnectPolling()
        {
            if (_disposed) return;
            SetState(_state == SerialConnectionState.Connecting
                ? SerialConnectionState.Connecting
                : SerialConnectionState.Reconnecting);

            StopReconnectTimer();
            _reconnectTimer = new System.Threading.Timer(_ => TryReconnectOnce(), null,
                dueTime: 1000, period: 1000);
        }

        private void TryReconnectOnce()
        {
            if (_disposed || string.IsNullOrEmpty(_targetPortName)) return;

            bool portIsPresent = Array.IndexOf(SerialPort.GetPortNames(), _targetPortName) >= 0;
            if (!portIsPresent) return; // keep waiting, timer will fire again

            OpenPort();
        }

        private void StopReconnectTimer()
        {
            _reconnectTimer?.Dispose();
            _reconnectTimer = null;
        }

        private void ClosePortQuiet()
        {
            if (_port == null) return;
            try
            {
                _port.DataReceived -= OnDataReceived;
                _port.ErrorReceived -= OnErrorReceived;
                if (_port.IsOpen) _port.Close();
            }
            catch
            {
                // Closing an already-faulted port can itself throw - this is
                // a best-effort cleanup, never let it propagate.
            }
            finally
            {
                _port.Dispose();
                _port = null;
            }
        }

        private void SetState(SerialConnectionState newState)
        {
            if (_state == newState) return;
            _state = newState;
            RaiseOnUiThread(() => ConnectionStateChanged.Invoke(this, newState));
        }

        private void RaiseOnUiThread(Action action)
        {
            if (_disposed) return;
            // Post (not Send) so the serial/timer thread never blocks
            // waiting for the UI thread - important since UI handlers might
            // do nontrivial work (updating charts, etc.).
            _syncContext.Post(_ => action(), null);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            StopReconnectTimer();
            lock (_portLock)
            {
                ClosePortQuiet();
            }
        }
    }
}
