
// (c) 2022-2024 Kazuki KOHZUKI

using System.Text.Json;
using System.Text.Json.Serialization;

namespace JsonTemplateMaker;

public class CSharpOutputOptions
{
    private static readonly JsonSerializerOptions _options = new() { WriteIndented = true };

    [JsonPropertyName("file-scoped-namespaces")]
    public bool FileScopedNamespaces { get; set; } = true;

    [JsonPropertyName("nullable")]
    public bool Nullable { get; set; } = true;

    [JsonPropertyName("documentation-comment")]
    public bool DocumentationComment { get; set; } = true;

    [JsonPropertyName("end-of-block-comment")]
    public bool EndOfBlockComment { get; set; } = false;

    [JsonPropertyName("json-loader")]
    public JsonLoaderOptions JsonLoader { get; set; } = new();

    [JsonPropertyName("number-handling")]
    public JsonNumberHandling NumberHandlingAttr { get; set; } = JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.AllowNamedFloatingPointLiterals;

    public CSharpOutputOptions() { }

    internal static CSharpOutputOptions? LoadFromFile(string filename)
    {
        var json = File.ReadAllText(filename);
        return JsonSerializer.Deserialize<CSharpOutputOptions>(json);
    } // internal static CSharpOutputOptions LoadFromFile (string)

    override public string ToString()
        => JsonSerializer.Serialize(this, _options);
} // public readonly struct CSharpOutputOptions
