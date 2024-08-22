
// (c) 2022-2024 Kazuki KOHZUKI

namespace JsonTemplateMaker;

internal static class EnumHelper
{
    internal static string GetFlagsString<TEnum>(this TEnum value) where TEnum : Enum
    {
        var enumName = typeof(TEnum).Name;
        var flags =
            Enum.GetValues(typeof(TEnum))
                .Cast<TEnum>()
                .Where(v => !v.Equals(default(TEnum)))
                .Where(v => value.HasFlag(v))
                .ToArray();
        return flags.Length == 0 ? $"{enumName}.{default(TEnum)}" : string.Join(" | ", flags.Select(f => $"{enumName}.{f}"));
    } // internal static string GetFlagsString ()
} // internal static class EnumHelper
