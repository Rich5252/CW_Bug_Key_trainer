using CwTrainer.Serial;
using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace CwTrainer
{
	/// <summary>
	/// Minimal example showing how to wire KeyEventSerialPort into a Form,
	/// including the logic that pairs consecutive KeyEvents into Elements
	/// (the (isMark, durationMs) tuples the rest of the trainer's analysis
	/// will consume).
	/// </summary>
	public partial class MainForm
	{
        private void InitializeComponent()
        {
            components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(MainForm));
            statusStrip1 = new StatusStrip();
            statusLabel = new ToolStripStatusLabel();
            CalStatusLabel = new ToolStripStatusLabel();
            portComboBox = new ComboBox();
            Timer = new System.Windows.Forms.Timer(components);
            ConnectButton = new Button();
            textBox1 = new TextBox();
            timelineView1 = new CwTrainer.Display.TimelineView();
            textBox2 = new TextBox();
            label1 = new Label();
            calibrateButton = new Button();
            statusStrip1.SuspendLayout();
            SuspendLayout();
            // 
            // statusStrip1
            // 
            statusStrip1.Items.AddRange(new ToolStripItem[] { statusLabel, CalStatusLabel });
            statusStrip1.Location = new Point(0, 390);
            statusStrip1.Name = "statusStrip1";
            statusStrip1.Size = new Size(1193, 22);
            statusStrip1.TabIndex = 0;
            statusStrip1.Text = "statusStrip1";
            // 
            // statusLabel
            // 
            statusLabel.Name = "statusLabel";
            statusLabel.Size = new Size(115, 17);
            statusLabel.Text = "COM not connected";
            // 
            // CalStatusLabel
            // 
            CalStatusLabel.Name = "CalStatusLabel";
            CalStatusLabel.Size = new Size(127, 17);
            CalStatusLabel.Text = "Cal Status - to be done";
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
            ConnectButton.Location = new Point(19, 44);
            ConnectButton.Name = "ConnectButton";
            ConnectButton.Size = new Size(75, 23);
            ConnectButton.TabIndex = 2;
            ConnectButton.Text = "Connect";
            ConnectButton.UseVisualStyleBackColor = true;
            ConnectButton.Click += ConnectButton_Click;
            // 
            // textBox1
            // 
            textBox1.Location = new Point(12, 83);
            textBox1.Multiline = true;
            textBox1.Name = "textBox1";
            textBox1.ScrollBars = ScrollBars.Vertical;
            textBox1.Size = new Size(109, 210);
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
            // calibrateButton
            // 
            calibrateButton.Location = new Point(19, 341);
            calibrateButton.Name = "calibrateButton";
            calibrateButton.Size = new Size(75, 23);
            calibrateButton.TabIndex = 7;
            calibrateButton.Text = "Cal WPM";
            calibrateButton.UseVisualStyleBackColor = true;
            calibrateButton.Click += calibrateButton_Click;
            // 
            // MainForm
            // 
            ClientSize = new Size(1193, 412);
            Controls.Add(calibrateButton);
            Controls.Add(label1);
            Controls.Add(textBox2);
            Controls.Add(timelineView1);
            Controls.Add(textBox1);
            Controls.Add(ConnectButton);
            Controls.Add(portComboBox);
            Controls.Add(statusStrip1);
            Icon = (Icon)resources.GetObject("$this.Icon");
            Name = "MainForm";
            Text = "CW Bug Key Trainer";
            Load += MainForm_Load;
            Resize += MainForm_Resize;
            statusStrip1.ResumeLayout(false);
            statusStrip1.PerformLayout();
            ResumeLayout(false);
            PerformLayout();

        }

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
        private Button calibrateButton;
        private ToolStripStatusLabel CalStatusLabel;
    }
}
