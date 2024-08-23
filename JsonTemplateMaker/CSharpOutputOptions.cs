
// (c) 2022-2024 Kazuki KOHZUKI

using System.Text.Json.Serialization;

namespace JsonTemplateMaker;

internal readonly struct CSharpOutputOptions
{
    internal bool FileScopedNamespaces { get; init; } = true;

    internal bool Nullable { get; init; } = true;

    internal bool DocumentationComment { get; init; } = true;

    internal bool EndOfBlockComment { get; init; } = false;

    internal JsonLoaderOptions JsonLoader { get; init; } = new();

    internal JsonNumberHandling NumberHandlingAttr { get; init; } = JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.AllowNamedFloatingPointLiterals;

    public CSharpOutputOptions() { }
} // internal readonly struct CSharpOutputOptions
