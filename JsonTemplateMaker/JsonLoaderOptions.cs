
// (c) 2022-2024 Kazuki KOHZUKI

using System.Text.Json.Serialization;

namespace JsonTemplateMaker;

public sealed class JsonLoaderOptions
{
    [JsonPropertyName("from-string")]
    public bool LoadFromString { get; init; } = true;

    [JsonPropertyName("from-stream")]
    public bool LoadFromStream { get; init; } = true;

    [JsonPropertyName("from-read-only-span")]
    public bool LoadFromReadOnlySpan { get; init; } = true;

    public JsonLoaderOptions() { }
} // public sealed class JsonLoaderOptions
