﻿
// (c) 2022-2024 Kazuki KOHZUKI

namespace JsonTemplateMaker;

internal static class ToolStripMenuHelper
{
    internal static void SetChecked<T>(this ToolStripMenuItem parent, T tag)
    {
        foreach (var child in parent.DropDownItems)
        {
            if (child is not ToolStripMenuItem item) continue;
            item.Checked = item.Tag is T t && t.Equals(tag);
        }
    } // internal static void SetChecked<T> (this ToolStripMenuItem, T)
} // internal static class ToolStripMenuHelper