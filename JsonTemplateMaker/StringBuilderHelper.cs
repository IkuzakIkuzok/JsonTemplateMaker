
// (c) 2022-2024 Kazuki KOHZUKI

using System.Text;

namespace JsonTemplateMaker;

internal static class StringBuilderHelper
{
    internal static void WriteDocComments(this StringBuilder sb, bool write, string indent, params string[] comments)
    {
        if (!write) return;
        foreach (var line in comments)
            sb.AppendLine($"{indent}/// {line}".TrimEnd());
    } // internal static void WriteDocComments (this StringBuilder, bool, string, params string[])
} // internal static class StringBuilderHelper
