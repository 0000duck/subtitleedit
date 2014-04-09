﻿using System;
using System.Text.RegularExpressions;

namespace Nikse.SubtitleEdit.Logic
{
    public class NoBreakAfterItem : IComparable 
    {
        public Regex Regex;
        public string Text;

        public NoBreakAfterItem(Regex regex, string text)
        {
            Regex = regex;
            Text = text;
        }

        public NoBreakAfterItem(string text)
        {
            Text = text;
        }

        public bool IsMatch(string line)
        {
            if (Regex != null)
                return Regex.IsMatch(line);

            if (!string.IsNullOrEmpty(Text) && line.EndsWith(Text))
                return true;

            return false;
               
        }

        public override string ToString()
        {
            return Text;
        }    

        public int CompareTo(object obj)
        {
            if (obj == null)
                return -1;

            var o = obj as NoBreakAfterItem;
            if (o == null)
                return -1;

            if (o.Text == null && this.Text == null)
                return 0;

            if (o.Text == null)
                return -1;

            return this.Text.CompareTo(o.Text);
        }
    }
}
