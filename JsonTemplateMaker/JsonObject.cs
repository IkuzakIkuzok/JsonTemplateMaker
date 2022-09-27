
// (c) 2022 Kazuki KOHZUKI

using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using PlainJson = System.Collections.Generic.Dictionary<string, object>;

namespace JsonTemplateMaker
{
    [DebuggerDisplay("TypeName = {name}")]
    internal class JsonObject : IEqualityComparer<JsonObject>
    {
        private static readonly Regex re_separator = new(@"[^a-zA-Z0-9]+[a-zA-Z0-9]");

        private readonly Dictionary<string, string> properties = new();
        private readonly List<JsonObject> subClasses = new();

        private readonly int depth;
        private readonly string ns = string.Empty, name;

        internal JsonObject(string json, string ns, string className) : this(JsonSerializer.Deserialize<PlainJson>(json), 0, className)
        {
            this.ns = ns;
        } // ctor (string, string, string)

        internal JsonObject(PlainJson? json, int depth, string className)
        {
            this.depth = depth;
            this.name = className;
            if (json == null) return;

            foreach ((var key, var value) in json.Items())
                RegisterProperty(key, value);
        } // internal JsonObject (PlainJson?, int, string)

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
                            if (jsonObj != null) types.Add(new JsonObject(jsonObj, depth + 1, $"Json{GetPropertyName(key)}ElementType"));
                            else types.Add(elementType);
                        }
                    }

                    if (key.Contains("license"))
                    {
                        Console.WriteLine();
                    }

                    if (types.Count == 1)
                    {
                        if (isPrimitive) return types.First() + "[]";
                        else return ((JsonObject)types.First()).name + "[]";
                    }
                    else
                    {
                        if (types.All(o => o is JsonObject))
                        {
                            var objectType = (JsonObject)types.First();
                            foreach (var t in types.Skip(1).Cast<JsonObject>())
                            {
                                if (!objectType.Equals(t))
                                {
                                    isPrimitive = false;
                                    foreach (JsonObject registered in types)
                                        this.subClasses.Remove(registered);
                                    return "object[]";
                                }
                            }

                            return ((JsonObject)types.First()).name + "[]";
                        }
                        else
                        {
                            isPrimitive = false;
                            return "object[]";
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
                var type = $"Json{GetPropertyName(key)}Type";
                if ((obj?.Count ?? 0) == 0)
                    type = "object";
                else
                {
                    var subClass = new JsonObject(obj, depth + 1, type);
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
                        if (registered == default)
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
                sb.AppendLine($"{indent}\tpublic {type} {propName} {{ get; set; }}");
                sb.AppendLine();
            }

            sb.AppendLine($"{indent}\t/// <summary>");
            sb.AppendLine($"{indent}\t/// Initializes a new instance of the <see cref=\"{this.name}\"/> class.");
            sb.AppendLine($"{indent}\t/// </summary>");
            sb.AppendLine($"{indent}\tpublic {this.name}() {{ }}");

            foreach (var sub in this.subClasses)
            {
                var prop = this.properties.First(kt => kt.Value.StartsWith(sub.name)).Key;

                var summary = sub.name.EndsWith("ElementType")
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

            if (this.properties.Count != json.properties.Count) return false;
            if (this.subClasses.Count != json.subClasses.Count) return false;

            foreach ((var key, var xType) in this.properties.Items())
            {
                if (!json.properties.TryGetValue(key, out var yType)) return false;
                if (xType != yType) return false;
            }

            foreach (var sub in this.subClasses)
            {
                if (!json.subClasses.Contains(sub)) return false;
            }

            return true;
        } // override public bool Equals (object?)

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
} // namespace JsonTemplateMaker
