using System;
using System.Collections.Generic;
using System.Windows.Forms;
using CwTrainer.Serial;

namespace CwTrainer
{
    /// <summary>
    /// Minimal example showing how to wire KeyEventSerialPort into a Form,
    /// including the logic that pairs consecutive KeyEvents into Elements
    /// (the (isMark, durationMs) tuples the rest of the trainer's analysis
    /// will consume).
    /// </summary>
    public partial class MainForm : Form
    {
        // Tracks the previous event so we can compute a duration once the
        // NEXT event arrives (an Element's duration is the gap between two
        // consecutive transitions, not something present in a single event).
        private KeyEvent? _previousEvent;

        public MainForm()
        {
            InitializeComponent();

            // Constructed here (UI thread) so SynchronizationContext capture
            // inside KeyEventSerialPort is correct.
            _serial = new KeyEventSerialPort();
            _serial.KeyEventReceived += OnKeyEventReceived;
            _serial.ConnectionStateChanged += OnConnectionStateChanged;
            _serial.UnparsedLineReceived += OnUnparsedLine;
        }

        private void RefreshPortListButton_Click(object sender, EventArgs e)
        {
            portComboBox.Items.Clear();
            portComboBox.Items.AddRange(KeyEventSerialPort.GetAvailablePorts());
            if (portComboBox.Items.Count > 0)
                portComboBox.SelectedIndex = 0;
        }

        private void ConnectButton_Click(object sender, EventArgs e)
        {
            if (portComboBox.SelectedItem is string portName)
            {
                _previousEvent = null; // discard any partial element from a prior session
                _serial.Connect(portName);
            }
        }

        private void DisconnectButton_Click(object sender, EventArgs e)
        {
            _serial.Disconnect();
        }

        // Fires already marshaled onto the UI thread - safe to touch
        // controls directly here.
        private void OnConnectionStateChanged(object? sender, SerialConnectionState state)
        {
            statusLabel.Text = state switch
            {
                SerialConnectionState.Disconnected => "Disconnected",
                SerialConnectionState.Connecting => "Connecting...",
                SerialConnectionState.Connected => $"Connected ({_serial.PortName})",
                SerialConnectionState.Reconnecting => "Reconnecting...",
                _ => state.ToString(),
            };
        }

        private void OnUnparsedLine(object? sender, string line)
        {
            // Useful during development/debugging - e.g. log to a debug
            // panel. Shouldn't happen in normal operation once the firmware
            // wire format is stable.
            System.Diagnostics.Debug.WriteLine($"Unparsed line: {line}");
        }

        private void OnKeyEventReceived(object? sender, KeyEvent evt)
        {
            if (_previousEvent is KeyEvent prev)
            {
                double durationMs = (evt.TimestampUs - prev.TimestampUs) / 1000.0;

                // The element that just ENDED is described by the PREVIOUS
                // event's state (prev.KeyDown) and the gap until THIS event.
                var element = new Element(prev.KeyDown, durationMs, DateTime.Now);
                _session.Add(element);

                OnElementCompleted(element);
            }

            _previousEvent = evt;
        }

        /// <summary>
        /// Called each time a full mark or space duration is known. This is
        /// the hook point for live feedback (timing histograms, decoded
        /// character display, etc.) - analysis logic belongs in a separate
        /// class consuming _session or this callback, not crammed into the
        /// form itself as the trainer grows.
        /// </summary>
        private void OnElementCompleted(Element element)
        {
            // Placeholder - wire up live UI updates / analysis here.
            System.Diagnostics.Debug.WriteLine(element.ToString());
            textBox1.AppendText(element.ToString() + Environment.NewLine);
            timelineView1.AddElement(element);
        }

        

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            _serial?.Dispose();
            base.OnFormClosing(e);
        }

        private void textBox2_TextChanged(object sender, EventArgs e)
        {
            timelineView1.DitLengthMs = 1200.0 / ((double)Convert.ToInt32(textBox2.Text));
        }
    }
}
