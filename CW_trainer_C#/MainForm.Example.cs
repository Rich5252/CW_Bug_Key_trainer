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
        private KeyEventSerialPort _serial;
        private readonly List<Element> _session = new List<Element>();
        private StatusStrip statusStrip1;
        private ToolStripStatusLabel statusLabel;
        private ComboBox portComboBox;
        private System.Windows.Forms.Timer Timer;
        private System.ComponentModel.IContainer components;
        private Button ConnectButton;
        private TextBox textBox1;
        private Display.TimelineView timelineView1;
        private TextBox textBox2;
        private Label label1;

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

        private void InitializeComponent()
        {
            components = new System.ComponentModel.Container();
            statusStrip1 = new StatusStrip();
            statusLabel = new ToolStripStatusLabel();
            portComboBox = new ComboBox();
            Timer = new System.Windows.Forms.Timer(components);
            ConnectButton = new Button();
            textBox1 = new TextBox();
            timelineView1 = new CwTrainer.Display.TimelineView();
            textBox2 = new TextBox();
            label1 = new Label();
            statusStrip1.SuspendLayout();
            SuspendLayout();
            // 
            // statusStrip1
            // 
            statusStrip1.Items.AddRange(new ToolStripItem[] { statusLabel });
            statusStrip1.Location = new Point(0, 402);
            statusStrip1.Name = "statusStrip1";
            statusStrip1.Size = new Size(1193, 22);
            statusStrip1.TabIndex = 0;
            statusStrip1.Text = "statusStrip1";
            // 
            // statusLabel
            // 
            statusLabel.Name = "statusLabel";
            statusLabel.Size = new Size(66, 17);
            statusLabel.Text = "statusLabel";
            // 
            // portComboBox
            // 
            portComboBox.FormattingEnabled = true;
            portComboBox.Items.AddRange(new object[] { "COM14" });
            portComboBox.Location = new Point(23, 15);
            portComboBox.Name = "portComboBox";
            portComboBox.Size = new Size(69, 23);
            portComboBox.TabIndex = 1;
            portComboBox.Text = "COM14";
            // 
            // ConnectButton
            // 
            ConnectButton.Location = new Point(12, 92);
            ConnectButton.Name = "ConnectButton";
            ConnectButton.Size = new Size(75, 23);
            ConnectButton.TabIndex = 2;
            ConnectButton.Text = "Connect";
            ConnectButton.UseVisualStyleBackColor = true;
            ConnectButton.Click += ConnectButton_Click;
            // 
            // textBox1
            // 
            textBox1.Location = new Point(12, 121);
            textBox1.Multiline = true;
            textBox1.Name = "textBox1";
            textBox1.ScrollBars = ScrollBars.Vertical;
            textBox1.Size = new Size(109, 164);
            textBox1.TabIndex = 3;
            // 
            // timelineView1
            // 
            timelineView1.AutoScroll = true;
            timelineView1.AutoScrollMinSize = new Size(0, 42);
            timelineView1.BackColor = Color.FromArgb(24, 24, 28);
            timelineView1.DitLengthMs = 54.545454545454547D;
            timelineView1.Location = new Point(130, 10);
            timelineView1.Name = "timelineView1";
            timelineView1.Size = new Size(1054, 371);
            timelineView1.TabIndex = 4;
            // 
            // textBox2
            // 
            textBox2.Location = new Point(58, 309);
            textBox2.Name = "textBox2";
            textBox2.Size = new Size(34, 23);
            textBox2.TabIndex = 5;
            textBox2.Text = "22";
            textBox2.TextAlign = HorizontalAlignment.Right;
            textBox2.TextChanged += textBox2_TextChanged;
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Location = new Point(16, 312);
            label1.Name = "label1";
            label1.Size = new Size(36, 15);
            label1.TabIndex = 6;
            label1.Text = "WPM";
            // 
            // MainForm
            // 
            ClientSize = new Size(1193, 424);
            Controls.Add(label1);
            Controls.Add(textBox2);
            Controls.Add(timelineView1);
            Controls.Add(textBox1);
            Controls.Add(ConnectButton);
            Controls.Add(portComboBox);
            Controls.Add(statusStrip1);
            Name = "MainForm";
            Text = "MainForm";
            statusStrip1.ResumeLayout(false);
            statusStrip1.PerformLayout();
            ResumeLayout(false);
            PerformLayout();

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
