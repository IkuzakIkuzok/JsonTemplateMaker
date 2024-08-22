
// (c) 2022 Kazuki KOHZUKI

using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using PlainJson = System.Collections.Generic.Dictionary<string, object>;

namespace JsonTemplateMaker;

[DebuggerDisplay("TypeName = {name}")]
internal class JsonObject : IEqualityComparer<JsonObject>
{
    private static readonly Regex re_separator = new(@"[^a-zA-Z0-9]+[a-zA-Z0-9]");

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
        isPrimitive = true;
        var element = (JsonElement)value;
        if (element.ValueKind == JsonValueKind.Number)
        {
            if (int.TryParse(value.ToString(), out var _)) return "int";
            if (long.TryParse(value.ToString(), out var _)) return "long";
            return "float";
        }
        if (element.ValueKind == JsonValueKind.String) return "string";

        object[]? array;
        if ((array = GetArray(value)) != null)
        {
            if (array.Length > 0)
            {
                var types = new HashSet<object>();
                foreach (var e in array)
                {
                    var elementType = GetTypeName(key + "Element", e, out isPrimitive);
                    if (isPrimitive)
                    {
                        types.Add(elementType);
                    }
                    else
                    {
                        var jsonObj = GetJsonObject(e);
                        //if (jsonObj != null) types.Add(new JsonObject(jsonObj, this.depth + 1, $"Json{GetPropertyName(key)}ElementObject"));
                        if (jsonObj != null) types.Add(new JsonObject(jsonObj, this.depth + 1, elementType));
                        else types.Add(elementType);
                    }
                }

                if (types.Count == 1)
                {
                    if (isPrimitive) return types.First() + "[]";
                    else
                    {
                        var t = types.First();
                        if (t is string s) return s + "[]";
                        return ((JsonObject)t).name + "[]";
                    }
                }
                else
                {
                    if (types.All(o => o is JsonObject))
                    {
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
                    }
                    else
                    {
                        isPrimitive = true;
                        return GetMinimumObject(types) + "[]";
                    }
                }
            }
            else
            {
                return "object[]";
            }
        }

        PlainJson? obj;
        if ((obj = GetJsonObject(value)) != null)
        {
            isPrimitive = false;
            var type = $"Json{GetPropertyName(key)}Object";
            if ((obj?.Count ?? 0) == 0)
                type = "object";
            else
            {
                var subClass = new JsonObject(obj, this.depth + 1, type);
                var registered = this.subClasses.FirstOrDefault(sc => sc.name == subClass.name);
                if (registered != default && !registered.Equals(subClass))
                {
                    var keys = this.properties.Where(kt => kt.Value == registered.name).Select(kt => kt.Key);
                    foreach (var k in keys)
                        this.properties[k] = "object";

                    this.subClasses.Remove(registered);
                    type = "object";
                }
                else
                {
                    var sameClass = this.subClasses.FirstOrDefault(sc => sc.HasSameMembers(subClass));
                    if (sameClass != default) type = sameClass.name;
                    else if (registered == default)
                        this.subClasses.Add(subClass);
                }
            }

            return type;
        }
        
        return "object";
    } // private string GetTypeName (object, out bool)

    private static object[]? GetArray(object value)
    {
        if (value is not JsonElement element) return null;
        if (element.ValueKind != JsonValueKind.Array) return null;
        return element.EnumerateArray().Cast<object>().ToArray();
    } // private static object[]? GetArray (object)

    private static JsonObject GetMinimumObject(IEnumerable<JsonObject> objects)
    {
        var obj = objects.First();

        #region properties

        foreach ((var k, var v) in obj.properties)
        {
            if (!objects.All(o => o.properties.TryGetValue(k, out var t) && t == v))
            {
                if (objects.All(o => o.properties.TryGetValue(k, out var _)))
                {
                    var propTypes = objects.Select(o => o.properties[k]);
                    var arrayDepth = propTypes.Select(t => t.Split("[").Length);
                    var depth = new HashSet<int>(arrayDepth);
                    if (depth.Count > 1)
                    {
                        obj.properties[k] = "object";
                    }
                    else if (propTypes.All(t => !CheckPrimitive(t)))
                    {
                        var propType = GetMinimumObject(objects.Select(o => o.subClasses.First(t => t.name == string.Join("", o.properties[k].TakeWhile(c => c != '[')))));
                        if (propType.properties.Count == 0)
                        {
                            obj.properties[k] = "object";
                        }
                        else
                        {
                            obj.subClasses.Add(propType);
                            obj.properties[k] = propType.name;
                        }
                    }
                    else
                    {
                        obj.properties[k] = GetMinimumObject(propTypes);
                    }
                }
                else
                {
                    obj.properties.Remove(k);
                }
                obj.subClasses.RemoveAll(o => o.name == v);
            }
        }

        #endregion properties

        #region sub classes

        var removeList = new List<JsonObject>();
        var addList = new List<JsonObject>();
        foreach (var sub in obj.subClasses)
        {
            if (!objects.All(o => o.subClasses.Contains(sub)))
            {
                var types = objects.SelectMany(o => o.subClasses).Where(t => t.name == sub.name);
                var prop = obj.properties.FirstOrDefault(kt => kt.Value.StartsWith(sub.name)).Key;
                if (!string.IsNullOrEmpty(prop))
                {
                    var minimum = GetMinimumObject(types);
                    if (minimum.properties.Count == 0)
                    {
                        foreach (var o in objects)
                            o.properties[prop] = "object";
                    }
                    else
                    {
                        addList.Add(minimum);
                        foreach (var o in objects)
                            o.properties[prop] = o.properties[prop].Replace(sub.name, minimum.name);
                    }
                }
                removeList.Add(sub);
            }
        }

        foreach (var sub in removeList) obj.subClasses.Remove(sub);
        foreach (var sub in addList)
        {
            if (!obj.subClasses.Contains(sub))
                obj.subClasses.Add(sub);
        }

        #endregion sub classes

        return obj;
    } // private static JsonObject GetMinimumObject (IEnumerable<JsonObject>)

    private static string GetMinimumObject(IEnumerable<object> objects)
    {
        if (objects.Contains("object")) return "object";

        if (objects.OfType<JsonObject>().Any()) return "object";

        if (objects.Contains("float")) return "float";
        if (objects.Contains("long")) return "long";

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
    }

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

    private static string GetPropertyName(string name)
    {
        var camel = re_separator.Replace(name, s => s.Value.Last().ToString().ToUpper());
        return string.Concat(camel[0].ToString().ToUpper(), camel.AsSpan(1));
    } // private static string GetPropertyName (string)

    override public string ToString()
        => ToString($"Represents {this.name}.");

    private string ToString(string classSummary)
    {
        var sb = new StringBuilder();
        if (this.depth == 0)
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
            sb.AppendLine($"namespace {this.ns}");
            sb.AppendLine("{");
        }

        var indent = new string('\t', this.depth + 1);

        sb.AppendLine($"{indent}/// <summary>");
        sb.AppendLine($"{indent}/// {classSummary}");
        sb.AppendLine($"{indent}/// </summary>");
        sb.AppendLine($"{indent}public sealed class {this.name}");
        sb.AppendLine($"{indent}{{");

        foreach ((var name, var type) in this.properties.Items())
        {
            sb.AppendLine($"{indent}\t/// <summary>");
            sb.AppendLine($"{indent}\t/// Gets or sets the {name}.");
            sb.AppendLine($"{indent}\t/// </summary>");
            sb.AppendLine($"{indent}\t[JsonPropertyName(\"{name}\")]");

            var propName = GetPropertyName(name);
            sb.AppendLine($"{indent}\tpublic {type}{(CheckStruct(type) ? "" : "?")} {propName} {{ get; set; }}");
            sb.AppendLine();
        }

        sb.AppendLine($"{indent}\t/// <summary>");
        sb.AppendLine($"{indent}\t/// Initializes a new instance of the <see cref=\"{this.name}\"/> class.");
        sb.AppendLine($"{indent}\t/// </summary>");
        sb.AppendLine($"{indent}\tpublic {this.name}() {{ }}");

        if (this.depth == 0)
        {
            sb.AppendLine();
            sb.AppendLine($"{indent}\t/// <summary>");
            sb.AppendLine($"{indent}\t/// Creates a new instance of the <see cref=\"{this.name}\"/> class from JSON string.");
            sb.AppendLine($"{indent}\t/// </summary>");
            sb.AppendLine($"{indent}\t/// <param name=\"jsonString\">JSON text to parse.</param>");
            sb.AppendLine($"{indent}\t/// <returns>A <see cref=\"{this.name}\"/> instance representing the JSON value.</returns>");
            sb.AppendLine($"{indent}\tpublic static {this.name}? LoadJson(string jsonString)");
            sb.AppendLine($"{indent}\t\t=> JsonSerializer.Deserialize<{this.name}>(jsonString);");

            sb.AppendLine();
            sb.AppendLine($"{indent}\t/// <inheritdoc/>");
            sb.AppendLine($"{indent}\tpublic override string ToString()");
            sb.AppendLine($"{indent}\t\t=> JsonSerializer.Serialize(this);");
        }

        foreach (var sub in this.subClasses)
        {
            var prop = GetPropertyName(this.properties.First(kt => kt.Value.StartsWith(sub.name)).Key);

            var summary = sub.name.EndsWith("ElementObject")
                ? $"Represents an element of <see cref=\"{prop}\"/>."
                : $"Represents a <see cref=\"{prop}\"/>.";

            sb.AppendLine();
            sb.AppendLine(sub.ToString(summary));
        }

        sb.Append($"{indent}}}");

        if (this.depth == 0)
        {
            sb.AppendLine();
            sb.AppendLine("}");
        }
        return sb.ToString();
    } // private string ToString (string)

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
    }

    override public int GetHashCode()
        => base.GetHashCode();

    public bool Equals(JsonObject? x, JsonObject? y)
    {
        if (x == null) return y == null;
        return x.Equals(y);
    } // public bool Equals (JsonObject?, JsonObject?)

    public int GetHashCode(JsonObject obj)
        => obj.GetHashCode();
} // internal class JsonObject : IEqualityComparer<JsonObject>
