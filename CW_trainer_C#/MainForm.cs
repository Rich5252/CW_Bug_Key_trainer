#define USE_PARIS_BURST_CALIBRATION         //use the full PARIS burst (marks and spaces) for calibration, instead of just the marks.
                                            // See WpmCalibrator.cs for details.

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

        private readonly ElementHistory _history = new ElementHistory();

        public MainForm()
        {
            InitializeComponent();

            timelineView1.AttachHistory(_history);

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

            _history.AddElement(element);
        }



        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            _serial?.Dispose();
            _history?.Dispose();
            base.OnFormClosing(e);
        }

        private void textBox2_TextChanged(object sender, EventArgs e)
        {
            if (double.TryParse(textBox2.Text, out double wpm) && wpm > 0)
            {
                double ditMs = 1200.0 / wpm;
                timelineView1.DitLengthMs = ditMs;
                _history.DitLengthMs = ditMs;               // calibartion data
            }
        }

        private void calibrateButton_Click(object sender, EventArgs e)
        {
            var lastChar = _history.LastCompletedCharacter;
            if (lastChar == null)
            {
                MessageBox.Show("No completed character yet - send a character first (e.g. \"5\" for five dits), then press Calibrate.",
                    "Calibration", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // DIAGNOSTIC: show exactly what calibration is seeing, to compare
            // against what the timeline view displays for the same character.
            var marks = lastChar.MarkDurationsMs;
            string marksDebug = string.Join(", ", marks.Select(m => m.ToString("F1")));
            System.Diagnostics.Debug.WriteLine($"[Calibration] Last character marks (ms): {marksDebug}");
            System.Diagnostics.Debug.WriteLine($"[Calibration] Total elements in this character: {lastChar.Elements.Count}");
            foreach (var el in lastChar.Elements)
                System.Diagnostics.Debug.WriteLine($"  {(el.IsMark ? "MARK " : "SPACE")} {el.DurationMs:F1}ms");

#if USE_PARIS_BURST_CALIBRATION
            var result = WpmCalibrator.Calibrate(lastChar.Elements);
#else
            var result = WpmCalibrator.Calibrate(lastChar.MarkDurationsMs);
#endif

            if (!result.Success)
            {
                MessageBox.Show(result.FailureReason, "Calibration",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Update the WPM textbox - this in turn fires textBox2_TextChanged,
            // which propagates DitLengthMs to both the timeline view and the
            // history. Rounded to 0.1 WPM resolution as requested.
            double roundedWpm = Math.Round(result.Wpm, 1);
            textBox2.Text = roundedWpm.ToString("F1");

            string logLine = $"Calibrated: {roundedWpm:F1} WPM from {result.DitsUsed} dits " +
                  $"(avg {result.DitLengthMs:F1}ms/dit, variance {result.VarianceFraction:P1})";

            CalStatusLabel.Text = logLine;
            textBox1.AppendText(logLine + Environment.NewLine);
        }

        private void MainForm_Resize(object sender, EventArgs e)
        {
            timelineView1.Width = this.ClientSize.Width - timelineView1.Left - 10;
            timelineView1.Height = this.ClientSize.Height - timelineView1.Top - statusStrip1.Height - 10;
        }
    }
}
