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
            copyCsvButton = new Button();
            rbDev = new RadioButton();
            rbSpead = new RadioButton();
            splitContainer1 = new SplitContainer();
            panel4 = new Panel();
            toolTip1 = new ToolTip(components);
            statusStrip1.SuspendLayout();
            panel1.SuspendLayout();
            panel3.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)splitContainer1).BeginInit();
            splitContainer1.Panel1.SuspendLayout();
            splitContainer1.Panel2.SuspendLayout();
            splitContainer1.SuspendLayout();
            panel4.SuspendLayout();
            SuspendLayout();
            // 
            // statusStrip1
            // 
            statusStrip1.Items.AddRange(new ToolStripItem[] { statusLabel, CalStatusLabel });
            statusStrip1.Location = new Point(0, 404);
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
            portComboBox.Location = new Point(4, 3);
            portComboBox.Name = "portComboBox";
            portComboBox.Size = new Size(69, 23);
            portComboBox.TabIndex = 1;
            portComboBox.Text = "COM14";
            // 
            // ConnectButton
            // 
            ConnectButton.Location = new Point(0, 32);
            ConnectButton.Name = "ConnectButton";
            ConnectButton.Size = new Size(75, 23);
            ConnectButton.TabIndex = 2;
            ConnectButton.Text = "Connect";
            ConnectButton.UseVisualStyleBackColor = true;
            ConnectButton.Click += ConnectButton_Click;
            // 
            // textBox1
            // 
            textBox1.Location = new Point(90, 3);
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
            timelineView1.Location = new Point(3, 3);
            timelineView1.Name = "timelineView1";
            timelineView1.Size = new Size(891, 356);
            timelineView1.TabIndex = 4;
            // 
            // textBox2
            // 
            textBox2.Location = new Point(39, 2);
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
            label1.Location = new Point(3, 5);
            label1.Name = "label1";
            label1.Size = new Size(36, 15);
            label1.TabIndex = 6;
            label1.Text = "WPM";
            // 
            // calibrateButton
            // 
            calibrateButton.Location = new Point(90, 1);
            calibrateButton.Name = "calibrateButton";
            calibrateButton.Size = new Size(75, 23);
            calibrateButton.TabIndex = 7;
            calibrateButton.Text = "Cal WPM";
            calibrateButton.UseVisualStyleBackColor = true;
            calibrateButton.Click += calibrateButton_Click;
            // 
            // decodedTextBox
            // 
            decodedTextBox.Location = new Point(3, 365);
            decodedTextBox.Name = "decodedTextBox";
            decodedTextBox.Size = new Size(889, 23);
            decodedTextBox.TabIndex = 8;
            decodedTextBox.WordWrap = false;
            // 
            // buttonClearText
            // 
            buttonClearText.Location = new Point(189, 0);
            buttonClearText.Name = "buttonClearText";
            buttonClearText.Size = new Size(75, 23);
            buttonClearText.TabIndex = 9;
            buttonClearText.Text = "Clear text";
            toolTip1.SetToolTip(buttonClearText, "Left click - clear text. Right click - clear view");
            buttonClearText.UseVisualStyleBackColor = true;
            buttonClearText.MouseUp += clearButton_MouseUp;
            // 
            // paretoChartControl1
            // 
            paretoChartControl1.Location = new Point(3, 61);
            paretoChartControl1.Name = "paretoChartControl1";
            paretoChartControl1.Size = new Size(261, 261);
            paretoChartControl1.TabIndex = 10;
            toolTip1.SetToolTip(paretoChartControl1, "Right click to update");
            paretoChartControl1.Click += paretoChartControl1_Click;
            // 
            // panel1
            // 
            panel1.Controls.Add(panel2);
            panel1.Controls.Add(rbElements);
            panel1.Controls.Add(rbChar);
            panel1.Location = new Point(3, 328);
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
            panel3.Controls.Add(copyCsvButton);
            panel3.Controls.Add(rbDev);
            panel3.Controls.Add(rbSpead);
            panel3.Location = new Point(142, 328);
            panel3.Name = "panel3";
            panel3.Size = new Size(122, 31);
            panel3.TabIndex = 12;
            // 
            // copyCsvButton
            // 
            copyCsvButton.Location = new Point(91, 1);
            copyCsvButton.Name = "copyCsvButton";
            copyCsvButton.Size = new Size(28, 23);
            copyCsvButton.TabIndex = 2;
            copyCsvButton.Text = "X";
            toolTip1.SetToolTip(copyCsvButton, "CSV to Clipboard");
            copyCsvButton.UseVisualStyleBackColor = true;
            copyCsvButton.Click += copyCsvButton_Click;
            // 
            // rbDev
            // 
            rbDev.Location = new Point(44, 3);
            rbDev.Margin = new Padding(3, 3, 0, 3);
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
            // splitContainer1
            // 
            splitContainer1.Location = new Point(12, 5);
            splitContainer1.Name = "splitContainer1";
            // 
            // splitContainer1.Panel1
            // 
            splitContainer1.Panel1.Controls.Add(panel4);
            splitContainer1.Panel1.Controls.Add(paretoChartControl1);
            splitContainer1.Panel1.Controls.Add(panel3);
            splitContainer1.Panel1.Controls.Add(panel1);
            splitContainer1.Panel1.Controls.Add(textBox1);
            splitContainer1.Panel1.Controls.Add(portComboBox);
            splitContainer1.Panel1.Controls.Add(ConnectButton);
            // 
            // splitContainer1.Panel2
            // 
            splitContainer1.Panel2.Controls.Add(decodedTextBox);
            splitContainer1.Panel2.Controls.Add(timelineView1);
            splitContainer1.Size = new Size(1169, 394);
            splitContainer1.SplitterDistance = 271;
            splitContainer1.TabIndex = 13;
            splitContainer1.SplitterMoved += splitContainer1_SplitterMoved;
            // 
            // panel4
            // 
            panel4.Controls.Add(textBox2);
            panel4.Controls.Add(buttonClearText);
            panel4.Controls.Add(label1);
            panel4.Controls.Add(calibrateButton);
            panel4.Location = new Point(0, 362);
            panel4.Name = "panel4";
            panel4.Size = new Size(267, 32);
            panel4.TabIndex = 14;
            // 
            // MainForm
            // 
            ClientSize = new Size(1193, 426);
            Controls.Add(splitContainer1);
            Controls.Add(statusStrip1);
            Icon = (Icon)resources.GetObject("$this.Icon");
            Name = "MainForm";
            Text = "CW Bug Key Trainer";
            Load += MainForm_Load;
            Shown += MainForm_Shown;
            Resize += MainForm_Resize;
            statusStrip1.ResumeLayout(false);
            statusStrip1.PerformLayout();
            panel1.ResumeLayout(false);
            panel1.PerformLayout();
            panel3.ResumeLayout(false);
            panel3.PerformLayout();
            splitContainer1.Panel1.ResumeLayout(false);
            splitContainer1.Panel1.PerformLayout();
            splitContainer1.Panel2.ResumeLayout(false);
            splitContainer1.Panel2.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)splitContainer1).EndInit();
            splitContainer1.ResumeLayout(false);
            panel4.ResumeLayout(false);
            panel4.PerformLayout();
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
        private SplitContainer splitContainer1;
        private Panel panel4;
        private Button copyCsvButton;
        private ToolTip toolTip1;
    }
}
