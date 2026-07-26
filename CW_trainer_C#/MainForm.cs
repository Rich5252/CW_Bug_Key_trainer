#define USE_PARIS_BURST_CALIBRATION         //use the full PARIS burst (marks and spaces) for calibration, instead of just the marks.
// See WpmCalibrator.cs for details.

using CwTrainer.Display;
using CwTrainer.Serial;
using CwTrainer.Settings;
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
        private readonly TrainerSettings _settings = new TrainerSettings();

        private ParetoMetric _currentMetric = ParetoMetric.SpreadFraction;
        private bool _showingCharacters = true;

        private readonly IniFile _ini = new IniFile("CwTrainer.ini");


        //active timing data source switching
        private UdpTimingListener? _udpListener;
        private object? _activeSource;
        public event EventHandler<string>? ActiveSourceChanged;
        public enum KeyEventSourceMode { None, Serial, Udp }
        private KeyEventSourceMode _activeMode = KeyEventSourceMode.None;




        public MainForm()
        {
            InitializeComponent();

            timelineView1.AttachHistory(_history, _settings);
            _history.Settings = _settings;
            _history.CharacterCompleted += (s, group) => _stats.RecordCompletedCharacter(group, _history.DitLengthMs, _settings);
            _history.CharacterCompleted += OnCharacterCompleted;


            //set keyer as active source by default, and connect to the selected serial port
            SetActiveSource(KeyEventSourceMode.Serial, portComboBox.Text);
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
            //handle switch of source timing data
            if (!ReferenceEquals(sender, _activeSource))
            {
                _activeSource = sender;
                string name = sender is KeyEventSerialPort ? "Serial"
                             : sender is UdpTimingListener ? "UDP"
                             : "Unknown";
                ActiveSourceChanged?.Invoke(this, name);
            }

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
            /* // TEMP DIAGNOSTIC - dump element detail for specific characters
            if (group.DecodedText == "A" || group.DecodedText == "B")
            {
                System.Diagnostics.Debug.WriteLine($"--- Character: {group.DecodedText} ---");
                for (int i = 0; i < group.Elements.Count; i++)
                {
                    var el = group.Elements[i];
                    bool isLast = (i == group.Elements.Count - 1);
                    string role = el.IsMark
                        ? (group.Elements.Count > 2 && i == 0 ? "Dit" : "Dah")  // rough, just for display
                        : (isLast ? (group.WasWordSpace ? "WordSpace" : "InterChar") : "IntraChar");
                    double ditMs = _history.DitLengthMs;
                    double ratio = el.DurationMs / ditMs;
                    System.Diagnostics.Debug.WriteLine(
                        $"  [{i}] {(el.IsMark ? "MARK " : "SPACE")} {el.DurationMs:F1}ms  ratio={ratio:F2}  role={role}");
                }
            }

            // ... rest of existing handler unchanged
            */


            if (!string.IsNullOrEmpty(group.DecodedText))
            {
                if (group.DecodedText == "HH")
                {
                    ClearAllHistory();
                    decodedTextBox.Text = "";
                    return;
                }
                decodedTextBox.AppendText(group.DecodedText);
            }
            if (group.WasWordSpace)
            {
                decodedTextBox.AppendText(" ");
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (this.WindowState == FormWindowState.Normal)
            {
                _ini.WriteInt("Window", "Width", this.Width);
                _ini.WriteInt("Window", "Height", this.Height);
                _ini.WriteInt("Window", "Left", this.Left);
                _ini.WriteInt("Window", "Top", this.Top);
                _ini.WriteInt("Window", "SplitterDistance", splitContainer1.SplitterDistance);
            }

            _ini.WriteString("Settings", "WPM", textBox2.Text);

            // Mark classification windows
            _ini.WriteDouble("MarkWindows", "DitMinGood", _settings.DitMinGood);
            _ini.WriteDouble("MarkWindows", "DitMaxGood", _settings.DitMaxGood);
            _ini.WriteDouble("MarkWindows", "DitMinWarn", _settings.DitMinWarn);
            _ini.WriteDouble("MarkWindows", "DitMaxWarn", _settings.DitMaxWarn);
            _ini.WriteDouble("MarkWindows", "DahMinGood", _settings.DahMinGood);
            _ini.WriteDouble("MarkWindows", "DahMaxGood", _settings.DahMaxGood);
            _ini.WriteDouble("MarkWindows", "DahMinWarn", _settings.DahMinWarn);
            _ini.WriteDouble("MarkWindows", "DahMaxWarn", _settings.DahMaxWarn);

            // Boundary detection
            _ini.WriteDouble("Boundaries", "CharSpaceThresholdDits", _settings.CharSpaceThresholdDits);
            _ini.WriteDouble("Boundaries", "WordSpaceThresholdDits", _settings.WordSpaceThresholdDits);
            _ini.WriteDouble("Boundaries", "TimeoutMultiplier", _settings.TimeoutMultiplier);

            _serial?.Dispose();
            _udpListener?.Dispose();
            _history?.Dispose();
            base.OnFormClosing(e);
        }

        private void textBox2_TextChanged(object sender, EventArgs e)
        {
            if (double.TryParse(textBox2.Text, out double wpm) && wpm > 0)
            {
                double ditMs = 1200.0 / wpm;
                timelineView1.DitLengthMs = ditMs;
                _history.DitLengthMs = ditMs;
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
            ResizeMainForm();
        }

        private void MainForm_Shown(object sender, EventArgs e)
        {
            ResizeMainForm();
        }

        private void ResizeMainForm()
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
            panel1.Top = splitContainer1.Height - panel1.Height - panel4.Height;
            panel3.Top = panel1.Top;

            //chart control expanded into the left panel of the split container, so keep it sized to fill that panel
            paretoChartControl1.Width = splitContainer1.Panel1.Width - 5;
            paretoChartControl1.Height = panel1.Top - paretoChartControl1.Top - 5;
            errorRateChartControl1.Width = paretoChartControl1.Width;
            errorRateChartControl1.Height = paretoChartControl1.Height;

            panel4.Width = splitContainer1.Panel1.Width;
            buttonClearText.Left = splitContainer1.Panel1.Width - buttonClearText.Width - 5;
        }


        private void MainForm_Load(object sender, EventArgs e)
        {
            if (_ini.Exists)
            {
                this.StartPosition = FormStartPosition.Manual;
                this.Width = _ini.ReadInt("Window", "Width", this.Width);
                this.Height = _ini.ReadInt("Window", "Height", this.Height);
                this.Left = _ini.ReadInt("Window", "Left", this.Left);
                this.Top = _ini.ReadInt("Window", "Top", this.Top);
                splitContainer1.SplitterDistance = _ini.ReadInt("Window", "SplitterDistance", splitContainer1.SplitterDistance);

                textBox2.Text = _ini.ReadString("Settings", "WPM", textBox2.Text);

                // Mark classification windows - only override if present in INI
                _settings.DitMinGood = _ini.ReadDouble("MarkWindows", "DitMinGood", _settings.DitMinGood);
                _settings.DitMaxGood = _ini.ReadDouble("MarkWindows", "DitMaxGood", _settings.DitMaxGood);
                _settings.DitMinWarn = _ini.ReadDouble("MarkWindows", "DitMinWarn", _settings.DitMinWarn);
                _settings.DitMaxWarn = _ini.ReadDouble("MarkWindows", "DitMaxWarn", _settings.DitMaxWarn);
                _settings.DahMinGood = _ini.ReadDouble("MarkWindows", "DahMinGood", _settings.DahMinGood);
                _settings.DahMaxGood = _ini.ReadDouble("MarkWindows", "DahMaxGood", _settings.DahMaxGood);
                _settings.DahMinWarn = _ini.ReadDouble("MarkWindows", "DahMinWarn", _settings.DahMinWarn);
                _settings.DahMaxWarn = _ini.ReadDouble("MarkWindows", "DahMaxWarn", _settings.DahMaxWarn);

                // Boundary detection
                _settings.CharSpaceThresholdDits = _ini.ReadDouble("Boundaries", "CharSpaceThresholdDits", _settings.CharSpaceThresholdDits);
                _settings.WordSpaceThresholdDits = _ini.ReadDouble("Boundaries", "WordSpaceThresholdDits", _settings.WordSpaceThresholdDits);
                _settings.TimeoutMultiplier = _ini.ReadDouble("Boundaries", "TimeoutMultiplier", _settings.TimeoutMultiplier);
            }

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
                ClearAllHistory();
            }
        }

        private void ClearAllHistory()
        {
            _history.Reset();
            timelineView1.ClearSession();
            _stats.Reset();
            RefreshParetoChart();
            RefreshErrorRateChart();
        }

        private void RefreshErrorRateChart()
        {

            var entries = _showingCharacters
                ? ErrorRateDataBuilder.BuildByCharacter(_stats)
                : ErrorRateDataBuilder.BuildByRole(_stats);

            errorRateChartControl1.SetData(entries);
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
            RefreshErrorRateChart();
        }

        private void rbSpead_CheckedChanged(object sender, EventArgs e)
        {
            if (rbSpead.Checked)
            {
                //swap to ErrorRate chart
                paretoChartControl1.Left = -10000;
                errorRateChartControl1.Left = 3;
                errorRateChartControl1.Width = paretoChartControl1.Width;
                errorRateChartControl1.Height = paretoChartControl1.Height;
                _currentMetric = ParetoMetric.SpreadFraction;
                RefreshErrorRateChart();
            }
            else
            {
                //swap to Pareto chart
                errorRateChartControl1.Left = -10000;
                paretoChartControl1.Left = 3;
                paretoChartControl1.Width = splitContainer1.Panel1.Width - 5;
                _currentMetric = ParetoMetric.MeanAbsoluteDeviation;
                RefreshParetoChart();
            }


        }

        private void copyCsvButton_Click(object sender, EventArgs e)
        {
            //Copy stats to CSV on clipboard
            string csv = SessionStatsCsvExporter.BuildCsv(_stats);
            Clipboard.SetText(csv);
            statusLabel.Text = "Stats copied to clipboard";
        }

        private void errorRateChartControl1_MouseClick(object sender, MouseEventArgs e)
        {
            RefreshParetoChart();
            RefreshErrorRateChart();

            if (e.Button == MouseButtons.Right)
            {
                if (sender is ErrorRateChartControl errorRateChart)
                {
                    // Show context menu for error rate chart
                    ContextMenuStrip contextMenu = new ContextMenuStrip();
                    ToolStripMenuItem copyCsvItem = new ToolStripMenuItem("Copy Stats to CSV");
                    copyCsvItem.Click += (s, args) =>
                    {
                        string csv = SessionStatsCsvExporter.BuildCsv(_stats);
                        Clipboard.SetText(csv);
                        statusLabel.Text = "Stats copied to clipboard";
                    };
                    contextMenu.Items.Add(copyCsvItem);
                    contextMenu.Show(errorRateChart, e.Location);
                }
            }
        }

        private void paretoChartControl1_MouseClick(object sender, MouseEventArgs e)
        {
            RefreshParetoChart();
            RefreshErrorRateChart();

            if (e.Button == MouseButtons.Right)
            {
                if (sender is ParetoChartControl paretoChart)
                {
                    // Show context menu for pareto chart
                    ContextMenuStrip contextMenu = new ContextMenuStrip();
                    ToolStripMenuItem copyCsvItem = new ToolStripMenuItem("Copy Stats to CSV");
                    copyCsvItem.Click += (s, args) =>
                    {
                        string csv = SessionStatsCsvExporter.BuildCsv(_stats);
                        Clipboard.SetText(csv);
                        statusLabel.Text = "Stats copied to clipboard";
                    };
                    contextMenu.Items.Add(copyCsvItem);
                    contextMenu.Show(paretoChart, e.Location);
                }
            }
        }

        private void rbKey_CheckedChanged(object sender, EventArgs e)
        {
            if (rbKey.Checked)
            {
                SetActiveSource(KeyEventSourceMode.Serial, portComboBox.Text);
            }
            if (rbUDP.Checked)            {
                SetActiveSource(KeyEventSourceMode.Udp);
            }
        }

        private void SetActiveSource(KeyEventSourceMode mode, string serialPortName = null)
        {
            if (mode == _activeMode) return; // no-op if already on this source

            // Always tear down whichever is currently running first - never let
            // both be live, even momentarily, given what you just saw happen.
            _serial?.Dispose();
            _serial = null;
            _udpListener?.Dispose();
            _udpListener = null;

            switch (mode)
            {
                case KeyEventSourceMode.Serial:
                    _serial = new KeyEventSerialPort();
                    _serial.KeyEventReceived += OnKeyEventReceived;
                    _serial.ConnectionStateChanged += OnConnectionStateChanged;
                    _serial.UnparsedLineReceived += OnUnparsedLine;
                    _serial.Connect(serialPortName);
                    break;

                case KeyEventSourceMode.Udp:
                    _udpListener = new UdpTimingListener();
                    _udpListener.KeyEventReceived += OnKeyEventReceived;
                    _udpListener.UnparsedLineReceived += OnUnparsedLine;
                    _udpListener.Start();
                    break;
            }

            _activeMode = mode;
        }
    }
}