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
            decodedTextBox = new TextBox();
            buttonClearText = new Button();
            paretoChartControl1 = new CwTrainer.Display.ParetoChartControl();
            panel1 = new Panel();
            panel2 = new Panel();
            rbElements = new RadioButton();
            rbChar = new RadioButton();
            panel3 = new Panel();
            rbDev = new RadioButton();
            rbSpead = new RadioButton();
            statusStrip1.SuspendLayout();
            panel1.SuspendLayout();
            panel3.SuspendLayout();
            SuspendLayout();
            // 
            // statusStrip1
            // 
            statusStrip1.Items.AddRange(new ToolStripItem[] { statusLabel, CalStatusLabel });
            statusStrip1.Location = new Point(0, 399);
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
            portComboBox.Location = new Point(16, 15);
            portComboBox.Name = "portComboBox";
            portComboBox.Size = new Size(69, 23);
            portComboBox.TabIndex = 1;
            portComboBox.Text = "COM14";
            // 
            // ConnectButton
            // 
            ConnectButton.Location = new Point(12, 44);
            ConnectButton.Name = "ConnectButton";
            ConnectButton.Size = new Size(75, 23);
            ConnectButton.TabIndex = 2;
            ConnectButton.Text = "Connect";
            ConnectButton.UseVisualStyleBackColor = true;
            ConnectButton.Click += ConnectButton_Click;
            // 
            // textBox1
            // 
            textBox1.Location = new Point(102, 15);
            textBox1.Multiline = true;
            textBox1.Name = "textBox1";
            textBox1.ScrollBars = ScrollBars.Vertical;
            textBox1.Size = new Size(174, 52);
            textBox1.TabIndex = 3;
            // 
            // timelineView1
            // 
            timelineView1.AutoScroll = true;
            timelineView1.AutoScrollMinSize = new Size(0, 42);
            timelineView1.BackColor = Color.FromArgb(24, 24, 28);
            timelineView1.DitLengthMs = 54.545454545454547D;
            timelineView1.Location = new Point(289, 10);
            timelineView1.Name = "timelineView1";
            timelineView1.Size = new Size(895, 354);
            timelineView1.TabIndex = 4;
            // 
            // textBox2
            // 
            textBox2.Location = new Point(51, 367);
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
            label1.Location = new Point(9, 370);
            label1.Name = "label1";
            label1.Size = new Size(36, 15);
            label1.TabIndex = 6;
            label1.Text = "WPM";
            // 
            // calibrateButton
            // 
            calibrateButton.Location = new Point(102, 370);
            calibrateButton.Name = "calibrateButton";
            calibrateButton.Size = new Size(75, 23);
            calibrateButton.TabIndex = 7;
            calibrateButton.Text = "Cal WPM";
            calibrateButton.UseVisualStyleBackColor = true;
            calibrateButton.Click += calibrateButton_Click;
            // 
            // decodedTextBox
            // 
            decodedTextBox.Location = new Point(289, 370);
            decodedTextBox.Name = "decodedTextBox";
            decodedTextBox.Size = new Size(892, 23);
            decodedTextBox.TabIndex = 8;
            decodedTextBox.WordWrap = false;
            // 
            // buttonClearText
            // 
            buttonClearText.Location = new Point(201, 369);
            buttonClearText.Name = "buttonClearText";
            buttonClearText.Size = new Size(75, 23);
            buttonClearText.TabIndex = 9;
            buttonClearText.Text = "Clear text";
            buttonClearText.UseVisualStyleBackColor = true;
            buttonClearText.MouseUp += clearButton_MouseUp;
            // 
            // paretoChartControl1
            // 
            paretoChartControl1.Location = new Point(12, 73);
            paretoChartControl1.Name = "paretoChartControl1";
            paretoChartControl1.Size = new Size(264, 253);
            paretoChartControl1.TabIndex = 10;
            paretoChartControl1.Click += paretoChartControl1_Click;
            // 
            // panel1
            // 
            panel1.Controls.Add(panel2);
            panel1.Controls.Add(rbElements);
            panel1.Controls.Add(rbChar);
            panel1.Location = new Point(13, 330);
            panel1.Name = "panel1";
            panel1.Size = new Size(135, 31);
            panel1.TabIndex = 11;
            // 
            // panel2
            // 
            panel2.Location = new Point(138, 0);
            panel2.Name = "panel2";
            panel2.Size = new Size(125, 31);
            panel2.TabIndex = 12;
            // 
            // rbElements
            // 
            rbElements.AutoSize = true;
            rbElements.Location = new Point(59, 3);
            rbElements.Name = "rbElements";
            rbElements.Size = new Size(73, 19);
            rbElements.TabIndex = 1;
            rbElements.Text = "Elements";
            rbElements.UseVisualStyleBackColor = true;
            // 
            // rbChar
            // 
            rbChar.AutoSize = true;
            rbChar.Checked = true;
            rbChar.Location = new Point(3, 3);
            rbChar.Name = "rbChar";
            rbChar.Size = new Size(55, 19);
            rbChar.TabIndex = 0;
            rbChar.TabStop = true;
            rbChar.Text = "Chars";
            rbChar.UseVisualStyleBackColor = true;
            rbChar.CheckedChanged += rbChar_CheckedChanged;
            // 
            // panel3
            // 
            panel3.Controls.Add(rbDev);
            panel3.Controls.Add(rbSpead);
            panel3.Location = new Point(151, 330);
            panel3.Name = "panel3";
            panel3.Size = new Size(125, 31);
            panel3.TabIndex = 12;
            // 
            // rbDev
            // 
            rbDev.AutoSize = true;
            rbDev.Location = new Point(50, 3);
            rbDev.Name = "rbDev";
            rbDev.Size = new Size(45, 19);
            rbDev.TabIndex = 1;
            rbDev.Text = "Dev";
            rbDev.UseVisualStyleBackColor = true;
            // 
            // rbSpead
            // 
            rbSpead.AutoSize = true;
            rbSpead.Checked = true;
            rbSpead.Location = new Point(3, 3);
            rbSpead.Name = "rbSpead";
            rbSpead.Size = new Size(35, 19);
            rbSpead.TabIndex = 0;
            rbSpead.TabStop = true;
            rbSpead.Text = "%";
            rbSpead.UseVisualStyleBackColor = true;
            rbSpead.CheckedChanged += rbSpead_CheckedChanged;
            // 
            // MainForm
            // 
            ClientSize = new Size(1193, 421);
            Controls.Add(panel3);
            Controls.Add(panel1);
            Controls.Add(paretoChartControl1);
            Controls.Add(buttonClearText);
            Controls.Add(decodedTextBox);
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
            panel1.ResumeLayout(false);
            panel1.PerformLayout();
            panel3.ResumeLayout(false);
            panel3.PerformLayout();
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
        private TextBox decodedTextBox;
        private Button buttonClearText;
        private Display.ParetoChartControl paretoChartControl1;
        private Panel panel1;
        private RadioButton rbElements;
        private RadioButton rbChar;
        private Panel panel2;
        private Panel panel3;
        private RadioButton rbDev;
        private RadioButton rbSpead;
    }
}
