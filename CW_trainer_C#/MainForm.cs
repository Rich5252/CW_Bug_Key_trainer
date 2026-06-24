#define USE_PARIS_BURST_CALIBRATION         //use the full PARIS burst (marks and spaces) for calibration, instead of just the marks.
// See WpmCalibrator.cs for details.

using CwTrainer.Display;
using CwTrainer.Serial;
using System;
using System.Collections.Generic;
using System.Security.Policy;
using System.Windows.Forms;
using static System.Windows.Forms.Design.AxImporter;


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
        private readonly SessionStats _stats = new SessionStats();

        private ParetoMetric _currentMetric = ParetoMetric.SpreadFraction;
        private bool _showingCharacters = true;


        public MainForm()
        {
            InitializeComponent();

            timelineView1.AttachHistory(_history);
            _history.CharacterCompleted += OnCharacterCompleted;
            _history.CharacterCompleted += (s, group) => _stats.RecordCompletedCharacter(group, _history.DitLengthMs);

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
            ConnectPort();
        }

        private void ConnectPort()
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
            //System.Diagnostics.Debug.WriteLine(element.ToString());
            textBox1.AppendText(element.ToString() + Environment.NewLine);

            _history.AddElement(element);
        }

        private void OnCharacterCompleted(object sender, CharacterGroup group)
        {
            if (!string.IsNullOrEmpty(group.DecodedText))
            {
                decodedTextBox.AppendText(group.DecodedText);
            }
            if (group.WasWordSpace)
            {
                decodedTextBox.AppendText(" ");
            }
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


        bool freezeSpliter = true;

        private void MainForm_Resize(object sender, EventArgs e)
        {
            freezeSpliter = true;

            splitContainer1.Height = this.ClientSize.Height - statusStrip1.Height - 10;
            splitContainer1.Width = this.ClientSize.Width - splitContainer1.Left - 10;
            splitContainer1.SplitterDistance = paretoChartControl1.Width + 5;

            panel4.Top = splitContainer1.Bottom - panel4.Height;            //contains bottom left row of controls
            panel4.Width = splitContainer1.Panel1.Width;

            timelineView1.Width = splitContainer1.Panel2.Width;
            timelineView1.Height = splitContainer1.Height - decodedTextBox.Height - 10;

            decodedTextBox.Width = splitContainer1.Panel2.Width - 5;
            decodedTextBox.Top = panel4.Top;

            AdjustChartSize();
            freezeSpliter = false;
        }

        private void splitContainer1_SplitterMoved(object sender, SplitterEventArgs e)
        {
            if (freezeSpliter) return;          // don't allow user to move splitter - we control it in MainForm_Resize

            AdjustChartSize();
        }

        private void AdjustChartSize()
        {
            //fit timeline into the right panel of the split container, and keep the decoded text box below it
            timelineView1.Width = splitContainer1.Panel2.Width;
            decodedTextBox.Width = splitContainer1.Panel2.Width - 5;

            //move panel1 and panel3 radio buttons to above the panel4 buttons
            panel1.Top = splitContainer1.Panel2.Height - panel1.Height - panel3.Height;
            panel3.Top = panel1.Top;

            //chart control expanded into the left panel of the split container, so keep it sized to fill that panel
            paretoChartControl1.Width = splitContainer1.Panel1.Width - 5;
            paretoChartControl1.Height = panel1.Top - paretoChartControl1.Top - 10;

            panel4.Width = splitContainer1.Panel1.Width;
            buttonClearText.Left = splitContainer1.Panel1.Width - buttonClearText.Width - 5;
        }


        private void MainForm_Load(object sender, EventArgs e)
        {
            ConnectPort();
        }

        private void buttonClearText_Click(object sender, EventArgs e)
        {
            decodedTextBox.Text = "";
        }

        private void clearButton_MouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                decodedTextBox.Clear();
            }
            else if (e.Button == MouseButtons.Right)
            {
                _history.Reset();
                timelineView1.ClearSession();
                _stats.Reset();
                RefreshParetoChart();
            }
        }


        private void RefreshParetoChart()
        {
            string axisTitle = _currentMetric == ParetoMetric.MeanAbsoluteDeviation
                ? "Mean deviation (%)"
                : "Spread (%)";

            var entries = _showingCharacters
                ? ParetoDataBuilder.BuildByCharacter(_stats, _currentMetric)
                : ParetoDataBuilder.BuildByRole(_stats, _currentMetric);

            paretoChartControl1.SetData(entries, axisTitle);
        }

        private void rbChar_CheckedChanged(object sender, EventArgs e)
        {
            _showingCharacters = rbChar.Checked ? true : false;
            RefreshParetoChart();
        }

        private void paretoChartControl1_Click(object sender, EventArgs e)
        {
            RefreshParetoChart();
        }

        private void rbSpead_CheckedChanged(object sender, EventArgs e)
        {
            _currentMetric = rbSpead.Checked ? ParetoMetric.SpreadFraction : ParetoMetric.MeanAbsoluteDeviation;
            RefreshParetoChart();
        }




        // Wire your four buttons to set _currentMetric/_showingCharacters then call RefreshParetoChart()
    }
}
