
// (c) 2022-2024 Kazuki KOHZUKI

namespace JsonTemplateMaker;

internal readonly struct CSharpOutputOptions
{
    internal bool FileScopedNamespaces { get; init; } = true;

    internal bool Nullable { get; init; } = true;

    internal bool DocumentationComment { get; init; } = true;

    internal bool EndOfBlockComment { get; init; } = false;

    public CSharpOutputOptions() { }
} // internal readonly struct CSharpOutputOptions
