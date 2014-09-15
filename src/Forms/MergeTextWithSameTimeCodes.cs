﻿using Nikse.SubtitleEdit.Logic;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace Nikse.SubtitleEdit.Forms
{
    public partial class MergeTextWithSameTimeCodes : Form
    {
        private Subtitle _subtitle;
        private Subtitle _mergedSubtitle;
        private bool _loading = true;
        private Timer previewTimer = new Timer();
        public int NumberOfMerges { get; private set; }
        private string _language;
        private Dictionary<int, bool> _isFixAllowedList = new Dictionary<int, bool>();

        private void FixLargeFonts()
        {
            Graphics graphics = this.CreateGraphics();
            SizeF textSize = graphics.MeasureString(buttonOK.Text, this.Font);
            if (textSize.Height > buttonOK.Height - 4)
            {
                int newButtonHeight = (int)(textSize.Height + 7 + 0.5);
                Utilities.SetButtonHeight(this, newButtonHeight, 1);
            }
        }

        public Subtitle MergedSubtitle
        {
            get { return _mergedSubtitle; }
        }

        public MergeTextWithSameTimeCodes()
        {
            InitializeComponent();

            previewTimer.Tick += previewTimer_Tick;
            previewTimer.Interval = 250;
            FixLargeFonts();
        }

        public void Initialize(Subtitle subtitle)
        {
            if (subtitle.Paragraphs.Count > 0)
                subtitle.Renumber(subtitle.Paragraphs[0].Number);

            Text = Configuration.Settings.Language.MergeTextWithSameTimeCodes.Title;
            labelMaxDifferenceMS.Text = Configuration.Settings.Language.MergeTextWithSameTimeCodes.MaxDifferenceMilliseconds;
            checkBoxAutoBreakOn.Text = Configuration.Settings.Language.MergeTextWithSameTimeCodes.ReBreakLines;
            listViewFixes.Columns[0].Text = Configuration.Settings.Language.General.Apply;
            listViewFixes.Columns[1].Text = Configuration.Settings.Language.General.LineNumber;
            listViewFixes.Columns[2].Text = Configuration.Settings.Language.MergeTextWithSameTimeCodes.MergedText;

            buttonOK.Text = Configuration.Settings.Language.General.Ok;
            buttonCancel.Text = Configuration.Settings.Language.General.Cancel;
            SubtitleListview1.InitializeLanguage(Configuration.Settings.Language.General, Configuration.Settings);
            Utilities.InitializeSubtitleFont(SubtitleListview1);
            SubtitleListview1.AutoSizeAllColumns(this);
            NumberOfMerges = 0;
            _subtitle = subtitle;
            MergeTextWithSameTimeCodes_ResizeEnd(null, null);
            _language = Utilities.AutoDetectGoogleLanguage(subtitle);
        }

        private void previewTimer_Tick(object sender, EventArgs e)
        {
            previewTimer.Stop();
            Cursor = Cursors.WaitCursor;
            GeneratePreview();
            Cursor = Cursors.Default;
        }

        private void AddToListView(Paragraph p, string lineNumbers, string newText)
        {
            var item = new ListViewItem(string.Empty) { Tag = p, Checked = true };

            var subItem = new ListViewItem.ListViewSubItem(item, lineNumbers.TrimEnd(','));
            item.SubItems.Add(subItem);
            subItem = new ListViewItem.ListViewSubItem(item, newText.Replace(Environment.NewLine, Configuration.Settings.General.ListViewLineSeparatorString));
            item.SubItems.Add(subItem);

            listViewFixes.Items.Add(item);

            foreach (string number in lineNumbers.TrimEnd(',').Split(",".ToCharArray(), StringSplitOptions.RemoveEmptyEntries))
            {
                _isFixAllowedList.Add(Convert.ToInt32(number), true);
            }
        }

        private void GeneratePreview()
        {
            if (_subtitle == null)
                return;

            var mergedIndexes = new List<int>();

            NumberOfMerges = 0;
            SubtitleListview1.Items.Clear();
            SubtitleListview1.BeginUpdate();
            int count;
            _mergedSubtitle = MergeLinesWithSameTimeCodes(_subtitle, mergedIndexes, out count, true, checkBoxAutoBreakOn.Checked, (int)numericUpDownMaxMillisecondsBetweenLines.Value);
            NumberOfMerges = count;

            SubtitleListview1.Fill(_subtitle);
            foreach (var index in mergedIndexes)
            {
                SubtitleListview1.SetBackgroundColor(index, Color.Green);
            }
            SubtitleListview1.EndUpdate();
            groupBoxLinesFound.Text = string.Format(Configuration.Settings.Language.MergeTextWithSameTimeCodes.NumberOfMergesX, NumberOfMerges);
        }

        private bool IsFixAllowed(Paragraph p)
        {
            if (_isFixAllowedList.ContainsKey(p.Number))
                return _isFixAllowedList[p.Number];
            return true;
        }

        private void listViewFixes_ItemChecked(object sender, ItemCheckedEventArgs e)
        {
            if (_loading)
                return;

            foreach (string number in e.Item.SubItems[1].Text.Split(",".ToCharArray(), StringSplitOptions.RemoveEmptyEntries))
            {
                int no = Convert.ToInt32(number);
                if (_isFixAllowedList.ContainsKey(no))
                    _isFixAllowedList[no] = e.Item.Checked;
            }

            var mergedIndexes = new List<int>();

            NumberOfMerges = 0;
            Cursor = Cursors.WaitCursor;
            SubtitleListview1.Items.Clear();
            SubtitleListview1.BeginUpdate();
            int count;
            _mergedSubtitle = MergeLinesWithSameTimeCodes(_subtitle, mergedIndexes, out count, false, checkBoxAutoBreakOn.Checked, (int)numericUpDownMaxMillisecondsBetweenLines.Value);
            NumberOfMerges = count;
            SubtitleListview1.Fill(_subtitle);
            foreach (var index in mergedIndexes)
            {
                SubtitleListview1.SetBackgroundColor(index, Color.Green);
            }
            SubtitleListview1.EndUpdate();
            Cursor = Cursors.Default;
            groupBoxLinesFound.Text = string.Format(Configuration.Settings.Language.MergeTextWithSameTimeCodes.NumberOfMergesX, NumberOfMerges);
        }

        public Subtitle MergeLinesWithSameTimeCodes(Subtitle subtitle, List<int> mergedIndexes, out int numberOfMerges, bool clearFixes, bool reBreak, int maxMsBetween)
        {
            listViewFixes.BeginUpdate();
            List<int> removed = new List<int>();
            if (!_loading)
                listViewFixes.ItemChecked -= listViewFixes_ItemChecked;
            if (clearFixes)
            {
                listViewFixes.Items.Clear();
                _isFixAllowedList = new Dictionary<int, bool>();
            }
            numberOfMerges = 0;
            var mergedSubtitle = new Subtitle();
            bool lastMerged = false;
            Paragraph p = null;
            var lineNumbers = new StringBuilder();
            for (int i = 1; i < subtitle.Paragraphs.Count; i++)
            {
                if (!lastMerged)
                {
                    p = new Paragraph(subtitle.GetParagraphOrDefault(i - 1));
                    mergedSubtitle.Paragraphs.Add(p);
                }
                Paragraph next = subtitle.GetParagraphOrDefault(i);
                Paragraph afterNext = subtitle.GetParagraphOrDefault(i + 1);
                if (next != null)
                {
                    if (QualifiesForMerge(p, next, maxMsBetween) && IsFixAllowed(p))
                    {
                        if (p.Text.StartsWith("<i>") && p.Text.EndsWith("</i>") && next.Text.StartsWith("<i>") && next.Text.EndsWith("</i>"))
                        {
                            p.Text = p.Text.Remove(p.Text.Length - 4) + Environment.NewLine + next.Text.Remove(0, 3);
                        }
                        else
                        {
                            p.Text = p.Text + Environment.NewLine + next.Text;
                        }
                        if (reBreak)
                            p.Text = Utilities.AutoBreakLine(p.Text, _language);
                        lastMerged = true;
                        removed.Add(i);
                        numberOfMerges++;
                        if (!mergedIndexes.Contains(i))
                            mergedIndexes.Add(i);
                        if (!mergedIndexes.Contains(i - 1))
                            mergedIndexes.Add(i - 1);

                        lineNumbers.Append(p.Number.ToString() + ", ");
                        lineNumbers.Append(next.Number.ToString());
                    }
                    else
                    {
                        lastMerged = false;
                    }
                }
                else
                {
                    lastMerged = false;
                }

                if (!removed.Contains(i) && lineNumbers.Length > 0 && clearFixes)
                {
                    AddToListView(p, lineNumbers.ToString(), p.Text);
                    lineNumbers = new StringBuilder();
                }
            }
            if (lineNumbers.Length > 0 && clearFixes)
            {
                AddToListView(p, lineNumbers.ToString(), p.Text);
            }
            if (!lastMerged)
                mergedSubtitle.Paragraphs.Add(new Paragraph(subtitle.GetParagraphOrDefault(subtitle.Paragraphs.Count - 1)));

            listViewFixes.EndUpdate();
            if (!_loading)
                listViewFixes.ItemChecked += listViewFixes_ItemChecked;

            mergedSubtitle.Renumber(1);
            return mergedSubtitle;
        }

        private static bool QualifiesForMerge(Paragraph p, Paragraph next, int maxMsBetween)
        {
            if (p == null || next == null)
                return false;

            return Math.Abs(next.StartTime.TotalMilliseconds - p.StartTime.TotalMilliseconds) <= maxMsBetween &&
                   Math.Abs(next.EndTime.TotalMilliseconds - p.EndTime.TotalMilliseconds) <= maxMsBetween;
        }

        private void MergeTextWithSameTimeCodes_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
                DialogResult = DialogResult.Cancel;
        }

        private void buttonOK_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.OK;
        }

        private void buttonCancel_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
        }

        private void MergeTextWithSameTimeCodes_ResizeEnd(object sender, EventArgs e)
        {
            columnHeaderText.Width = -2;
        }

        private void MergeTextWithSameTimeCodes_Shown(object sender, EventArgs e)
        {
            GeneratePreview();
            listViewFixes.Focus();
            if (listViewFixes.Items.Count > 0)
                listViewFixes.Items[0].Selected = true;
            _loading = false;
            listViewFixes.ItemChecked += listViewFixes_ItemChecked;
        }

        private void checkBoxAutoBreakOn_CheckedChanged(object sender, EventArgs e)
        {
            Cursor = Cursors.WaitCursor;
            GeneratePreview();
            Cursor = Cursors.Default;
        }

        private void numericUpDownMaxMillisecondsBetweenLines_ValueChanged(object sender, EventArgs e)
        {
            Cursor = Cursors.WaitCursor;
            GeneratePreview();
            Cursor = Cursors.Default;
        }

    }
}
