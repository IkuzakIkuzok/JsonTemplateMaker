
// (c) 2022-2024 Kazuki KOHZUKI

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using PlainJson = System.Collections.Generic.Dictionary<string, object>;

namespace JsonTemplateMaker;

[DebuggerDisplay("TypeName = {name}")]
internal partial class JsonObject : IEqualityComparer<JsonObject>
{
    private static readonly Regex re_separator = RegexSeparator();
    private static readonly Regex re_digits = RegexDigits();

    private readonly Dictionary<string, string> properties = [];
    private readonly List<JsonObject> subClasses = [];

    private readonly int depth;
    private readonly string ns = string.Empty, name;

    internal JsonObject(string json, string ns, string className) : this(JsonSerializer.Deserialize<PlainJson>(json), 0, className)
    {
        this.ns = ns;
    } // ctor (string, string, string)

    private JsonObject(PlainJson? json, int depth, string className)
    {
        this.depth = depth;
        this.name = className;
        if (json == null) return;

        foreach ((var key, var value) in json.Items())
            RegisterProperty(key, value);
    } // private JsonObject (PlainJson?, int, string)

    private void RegisterProperty(string key, object value)
    {
        var type = GetTypeName(key, value, out var _);
        this.properties.Add(key, type);
    } // private void RegisterProperty (string, object)

    private string GetTypeName(string key, object value, out bool isPrimitive)
    {
        // primitive
        isPrimitive = true;
        var element = (JsonElement)value;
        if (element.ValueKind == JsonValueKind.Number)
        {
            var s = value.ToString();
            if (int.TryParse(s, out var _)) return "int";
            if (long.TryParse(s, out var _)) return "long";
            return "float";
        }
        if (element.ValueKind == JsonValueKind.String) return "string";

        // array
        if (TryGetArray(value, out var array))
            return GetArrayTypeName(array, key, out isPrimitive);

        // unknown
        if (!TryGetJsonObject(value, out var obj)) return "object";
        if (obj.Count == 0) return "object";

        // other object
        isPrimitive = false;
        var type = $"Json{GetPropertyName(key)}Object";
        var subClass = new JsonObject(obj, this.depth + 1, type);
        var registered = this.subClasses.FirstOrDefault(sc => sc.name == subClass.name);

        if (registered != default && !registered.Equals(subClass))
        {
            var keys =
                this.properties.Where(kt => kt.Value == registered.name)
                               .Select(kt => kt.Key);

            foreach (var k in keys)
                this.properties[k] = "object";

            this.subClasses.Remove(registered);
            return "object";
        }

        var sameClass = this.subClasses.FirstOrDefault(sc => sc.HasSameMembers(subClass));
        if (sameClass != default) type = sameClass.name;
        else if (registered == default)
            this.subClasses.Add(subClass);

        return type;
    } // private string GetTypeName (object, out bool)

    #region array

    private static object[]? GetArray(object value)
    {
        if (value is not JsonElement element) return null;
        if (element.ValueKind != JsonValueKind.Array) return null;
        return element.EnumerateArray().Cast<object>().ToArray();
    } // private static object[]? GetArray (object)

    private static bool TryGetArray(object value, [NotNullWhen(true)] out object[]? array)
    {
        array = GetArray(value);
        return array is not null;
    } // private static bool TryGetArray (object, out object[]?)

    private string GetArrayTypeName(object[] array, string key, out bool isPrimitive)
    {
        isPrimitive = true;

        // If the array is empty, it is treated as an object array
        // because the type of the array cannot be determined.
        if (array.Length == 0) return "object[]";

        var types = new HashSet<object>();
        foreach (var e in array)
        {
            var elementType = GetTypeName(key + "Element", e, out isPrimitive);
            if (isPrimitive || !TryGetJsonObject(e, out var jsonObj))
                types.Add(elementType);
            else
                types.Add(new JsonObject(jsonObj, this.depth + 1, elementType));
        }

        if (types.Count == 1)
        {
            if (isPrimitive) return types.First() + "[]";

            var t = types.First();
            if (t is string s) return s + "[]";
            return ((JsonObject)t).name + "[]";
        }

        if (types.Any(o => o is not JsonObject))
        {
            isPrimitive = true;
            return GetMinimumObject(types) + "[]";
        }

        var jsonObjects = types.Cast<JsonObject>();
        var minimum = GetMinimumObject(jsonObjects);

        foreach (var registered in jsonObjects)
            this.subClasses.Remove(registered);

        if (minimum.properties.Count == 0)
        {
            isPrimitive = true;
            return "object[]";
        }

        this.subClasses.Add(minimum);
        return minimum.name + "[]";
    } // private string GetArrayTypeName (object[], string, out bool)

    #endregion array

    private static JsonObject GetMinimumObject(IEnumerable<JsonObject> objects)
    {
        Debug.Assert(objects.Any());

        var obj = objects.First();

        #region properties

        foreach ((var propName, var propType) in obj.properties)
            SimplifyProperty(obj, propName, propType, objects);

        #endregion properties

        #region sub classes

        // remove from or add to subClass classes in foreach loop causes InvalidOperationException
        // these lists contains objects to remove or add and refered after the loop
        var removeList = new List<JsonObject>();
        var addList = new List<JsonObject>();
        foreach (var sub in obj.subClasses)
        {
            // if all objects have the same subClass class, skip
            if (objects.All(o => o.subClasses.Contains(sub))) continue;

            // here, some objects does not have the subClass class
            // therefore, the subClass class is removed from the object
            removeList.Add(sub);
            
            var add = SimplifySubclass(obj, sub, objects);
            if (add is not null) addList.Add(add);
        } // for each subClass class

        foreach (var sub in removeList) obj.subClasses.Remove(sub);
        foreach (var sub in addList)
        {
            if (!obj.subClasses.Contains(sub))
                obj.subClasses.Add(sub);
        }

        #endregion sub classes

        return obj;
    } // private static JsonObject GetMinimumObject (IEnumerable<JsonObject>)

    private static void SimplifyProperty(JsonObject obj, string propName, string propType, IEnumerable<JsonObject> objects)
    {
        // if all objects have the same property with the same type, skip
        if (objects.All(o => o.properties.TryGetValue(propName, out var t) && t == propType)) return;

        // remove subClass classes that have the same name as the property type
        // because the property type is changed to the minimum type
        obj.subClasses.RemoveAll(o => o.name == propType);

        if (!objects.All(o => o.properties.ContainsKey(propName)))
        {
            // if any other object does not have the property, just remove the property
            obj.properties.Remove(propName);
            return;
        }

        var propTypes = objects.Select(o => o.properties[propName]);
        var arrayDepth = propTypes.Select(t => t.Split("[").Length);
        var depth = new HashSet<int>(arrayDepth);
        if (depth.Count > 1)
        {
            /*
             * some classes have array properties with different depths
             * e.g., some have "int[]" and others have "int[][]"
             * in this case, the common type is "object"
             */
            obj.properties[propName] = "object";
            return;
        }

        if (propTypes.Any(t => CheckPrimitive(t)))
        {
            // if any object has a primitive property, the property type is changed to the minimum type of the property
            obj.properties[propName] = GetMinimumObject(propTypes);
            return;
        }

        static string GetArrayElementTypeName(string typeName) => typeName.Split('[')[0];
        var propObj = GetMinimumObject(
            objects.Select(
                o => o.subClasses.First(
                    t => t.name == GetArrayElementTypeName(o.properties[propName])
                )
            )
        );
        if (propObj.properties.Count == 0)
        {
            // if the common type has no property, the type is "object"
            // because further analysis is impossible
            obj.properties[propName] = "object";
        }
        else
        {
            // the property type is replaced with the minimum type other than "object"
            obj.subClasses.Add(propObj);
            obj.properties[propName] = propObj.name;
        }

        return;
    } // private static void SimplifyProperty (JsonObject, string, string, IEnumerable<JsonObject>)

    private static JsonObject? SimplifySubclass(JsonObject obj, JsonObject subClass, IEnumerable<JsonObject> objects)
    {
        var prop = obj.properties.FirstOrDefault(kt => kt.Value.StartsWith(subClass.name)).Key;
        if (string.IsNullOrEmpty(prop)) return null;

        var types = objects.SelectMany(o => o.subClasses).Where(t => t.name == subClass.name);
        var minimum = GetMinimumObject(types);
        if (minimum.properties.Count == 0)
        {
            // the minimum object has no property, i.e., the type is "object"
            foreach (var o in objects)
                o.properties[prop] = "object";

            return null;
        }
        else
        {
            // do NOT simply replace the type with the minimum type
            // because the property type may be an array
            // e.g., "int[]" must not be changed to "object" but "object[]"
            foreach (var o in objects)
                o.properties[prop] = o.properties[prop].Replace(subClass.name, minimum.name);

            return minimum;
        }
    } // private static JsonObject? SimplifySubclass (JsonObject, JsonObject, IEnumerable<JsonObject>)

    private static string GetMinimumObject(IEnumerable<object> objects)
    {
        if (objects.Contains("object")) return "object";

        if (objects.OfType<JsonObject>().Any()) return "object";

        bool CheckPrimitive(params string[] typeNames)
            => objects.All(o => o is string s && typeNames.Contains(s));

        // long and int can be safely converted to float
        // and int can be safely converted to long
        if (CheckPrimitive("float", "long", "int")) return "float";
        if (CheckPrimitive("long", "int")) return "long";
        if (CheckPrimitive("int")) return "int";

        if (CheckPrimitive("string")) return "string";

        // all types can be converted to "object"
        return "object";
    } // private static string GetMinimumObject (IEnumerable<object>)

    private static bool CheckPrimitive(string typeName)
        => !typeName.StartsWith("Json");

    private static bool CheckStruct(string typeName)
    {
        if (!CheckPrimitive(typeName)) return false;
        if (typeName.EndsWith("[]")) return false;
        if (typeName == "object") return false;
        if (typeName == "string") return false;
        return true;
    } // private static bool CheckStruct (string)

    private static PlainJson? GetJsonObject(object value)
    {
        try
        {
            return JsonSerializer.Deserialize<PlainJson>(value.ToString()!);
        }
        catch
        {
            return null;
        }
    } // private static bool IsJsonObject (object)

    private static bool TryGetJsonObject(object value, [NotNullWhen(true)] out PlainJson? obj)
    {
        obj = GetJsonObject(value);
        return obj is not null;
    } // private static bool TryGetJsonObject (object, out PlainJson?)

    private static string GetPropertyName(string name)
    {
        name = re_digits.Replace(name, m => $"_{m.Value[1]}");
        var camel = re_separator.Replace(name, m => m.Value.Last().ToString().ToUpper());
        var propName = string.Concat(camel[0].ToString().ToUpper(), camel.AsSpan(1));

        if (char.IsDigit(propName[0]))
            propName = "Prop" + propName;

        return propName;
    } // private static string GetPropertyName (string)

    #region ToString

    override public string ToString()
        => ToString(new());

    internal string ToString(CSharpOutputOptions outputOptions, CancellationToken? cancellationToken = default)
        => ToString($"Represents {this.name}.", outputOptions, cancellationToken);

    private string ToString(string classSummary, CSharpOutputOptions outputOptions, CancellationToken? cancellationToken)
    {
        var fileScopedNamespace = outputOptions.FileScopedNamespaces;
        var nullable = outputOptions.Nullable;
        var docComment = outputOptions.DocumentationComment;

        var sb = new StringBuilder();
        if (this.depth == 0)
            WriteHeader(sb, fileScopedNamespace);

        var indent = new string('\t', this.depth + (fileScopedNamespace ? 0 : 1));

        if (docComment)
        {
            sb.AppendLine($"{indent}/// <summary>");
            sb.AppendLine($"{indent}/// {classSummary}");
            sb.AppendLine($"{indent}/// </summary>");
        }
        sb.AppendLine($"{indent}public sealed class {this.name}");
        sb.AppendLine($"{indent}{{");

        WriteProperties(sb, indent, nullable, docComment, cancellationToken);

        // constructor
        if (docComment)
        {
            sb.AppendLine($"{indent}\t/// <summary>");
            sb.AppendLine($"{indent}\t/// Initializes a new instance of the <see cref=\"{this.name}\"/> class.");
            sb.AppendLine($"{indent}\t/// </summary>");
        }
        sb.AppendLine($"{indent}\tpublic {this.name}() {{ }}");

        if (this.depth == 0)
            WriteTopClassMembers(sb, indent, docComment);

        WriteSubClasses(sb, outputOptions, cancellationToken);

        // end of class
        sb.Append($"{indent}}}");
        if (outputOptions.EndOfBlockComment)
            sb.Append($" // end of class {this.name}");

        if (this.depth == 0)
        {
            // end of namespace
            sb.AppendLine();
            if (!fileScopedNamespace)
            {
                if (outputOptions.EndOfBlockComment)
                    sb.Append($" // end of namespace {this.ns}");
                else
                    sb.AppendLine("}");
            }
        }

        cancellationToken?.ThrowIfCancellationRequested();

        return sb.ToString();
    } // private string ToString (string, CSharpOutputOptions, CancellationToken?)

    #region ToString.sub

    private void WriteHeader(StringBuilder sb, bool fileScopedNamespace)
    {
        sb.AppendLine();
        sb.AppendLine($"/*");
        sb.AppendLine($" * Generated by {nameof(JsonTemplateMaker)}");
        sb.AppendLine($" *");
        sb.AppendLine($" * The effect of the {nameof(JsonTemplateMaker)} license does not extend to this code,");
        sb.AppendLine($" * which is a deliverable of {nameof(JsonTemplateMaker)}. Therefore, no {nameof(JsonTemplateMaker)}");
        sb.AppendLine($" * license is required to use, copy, modify, merge, publish, distribute,");
        sb.AppendLine($" * sublicense, and/or sell this generated code.");
        sb.AppendLine($" */");
        sb.AppendLine();
        sb.AppendLine("using System.Text.Json;");
        sb.AppendLine("using System.Text.Json.Serialization;");
        sb.AppendLine();

        if (fileScopedNamespace)
        {
            sb.AppendLine($"namespace {this.ns};");
            sb.AppendLine();
        }
        else
        {
            sb.AppendLine($"namespace {this.ns}");
            sb.AppendLine("{");
        }
    } // private void WriteHeader (StringBuilder, bool)

    private void WriteProperties(StringBuilder sb, string indent, bool nullable, bool docComment, CancellationToken? cancellationToken)
    {
        foreach ((var name, var type) in this.properties.Items())
        {
            cancellationToken?.ThrowIfCancellationRequested();

            if (docComment)
            {
                sb.AppendLine($"{indent}\t/// <summary>");
                sb.AppendLine($"{indent}\t/// Gets or sets the {name}.");
                sb.AppendLine($"{indent}\t/// </summary>");
            }
            sb.AppendLine($"{indent}\t[JsonPropertyName(\"{name}\")]");

            var propName = GetPropertyName(name);
            sb.AppendLine($"{indent}\tpublic {type}{(CheckStruct(type) || !nullable ? "" : "?")} {propName} {{ get; set; }}");
            sb.AppendLine();
        }
    } // private void WriteProperties (StringBuilder, string, bool, bool, CancellationToken?)

    private void WriteTopClassMembers(StringBuilder sb, string indent, bool docComment)
    {
        sb.AppendLine();
        if (docComment)
        {
            sb.AppendLine($"{indent}\t/// <summary>");
            sb.AppendLine($"{indent}\t/// Creates a new instance of the <see cref=\"{this.name}\"/> class from JSON string.");
            sb.AppendLine($"{indent}\t/// </summary>");
            sb.AppendLine($"{indent}\t/// <param name=\"jsonString\">JSON text to parse.</param>");
            sb.AppendLine($"{indent}\t/// <returns>A <see cref=\"{this.name}\"/> instance representing the JSON value.</returns>");
        }
        sb.AppendLine($"{indent}\tpublic static {this.name}? LoadJson(string jsonString)");
        sb.AppendLine($"{indent}\t\t=> JsonSerializer.Deserialize<{this.name}>(jsonString);");

        sb.AppendLine();
        if (docComment) sb.AppendLine($"{indent}\t/// <inheritdoc/>");
        sb.AppendLine($"{indent}\tpublic override string ToString()");
        sb.AppendLine($"{indent}\t\t=> JsonSerializer.Serialize(this);");
    } // private void WriteTopClassMembers (StringBuilder, string, bool)

    private void WriteSubClasses(StringBuilder sb, CSharpOutputOptions outputOptions, CancellationToken? cancellationToken)
    {
        foreach (var sub in this.subClasses)
        {
            cancellationToken?.ThrowIfCancellationRequested();

            var prop = GetPropertyName(this.properties.First(kt => kt.Value.StartsWith(sub.name)).Key);

            var summary = sub.name.EndsWith("ElementObject")
                ? $"Represents an element of <see cref=\"{prop}\"/>."
                : $"Represents a <see cref=\"{prop}\"/>.";

            sb.AppendLine();
            sb.AppendLine(sub.ToString(summary, outputOptions, cancellationToken));
        }
    } // private void WriteSubClasses (StringBuilder, string, CancellationToken?)

    #endregion ToString.sub

    #endregion ToString

    override public bool Equals(object? obj)
    {
        if (obj is not JsonObject json) return false;

        if (this.name != json.name) return false;
        return HasSameMembers(json);
    } // override public bool Equals (object?)

    private bool HasSameMembers(JsonObject other)
    {
        if (this.properties.Count != other.properties.Count) return false;
        if (this.subClasses.Count != other.subClasses.Count) return false;

        foreach ((var key, var xType) in this.properties.Items())
        {
            if (!other.properties.TryGetValue(key, out var yType)) return false;
            if (xType != yType) return false;
        }

        foreach (var sub in this.subClasses)
        {
            if (!other.subClasses.Contains(sub)) return false;
        }

        return true;
    } // private bool HasSameMembers (JsonObject)

    override public int GetHashCode()
        => base.GetHashCode();

    public bool Equals(JsonObject? x, JsonObject? y)
    {
        if (x == null) return y == null;
        return x.Equals(y);
    } // public bool Equals (JsonObject?, JsonObject?)

    public int GetHashCode(JsonObject obj)
        => obj.GetHashCode();

    [GeneratedRegex(@"[^a-zA-Z0-9_]+[a-zA-Z0-9]", RegexOptions.Compiled)]
    private static partial Regex RegexSeparator();

    [GeneratedRegex(@"(?<=\d)[^a-zA-Z0-9](\d)", RegexOptions.Compiled)]
    private static partial Regex RegexDigits();
} // internal partial class JsonObject : IEqualityComparer<JsonObject>
