
// (c) 2022-2024 Kazuki KOHZUKI

namespace JsonTemplateMaker;

internal sealed class JsonLoaderOptions
{
    internal bool LoadFromString { get; init; } = true;

    internal bool LoadFromStream { get; init; } = false;

    internal bool LoadFromReadOnlySpan { get; init; } = false;

    internal JsonLoaderOptions() { }
} // internal sealed class JsonLoaderOptions
