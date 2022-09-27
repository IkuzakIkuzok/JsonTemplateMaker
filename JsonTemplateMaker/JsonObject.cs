
// (c) 2022 Kazuki KOHZUKI

using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using PlainJson = System.Collections.Generic.Dictionary<string, object>;

namespace JsonTemplateMaker
{
    internal class JsonObject
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

        private void RegisterProperty(string key, object value, int arrDepth = 0)
        {
            var type = GetTypeName(value, out var isPrimitive);
            if (!isPrimitive)
            {
                var array = GetArray(value);
                if (array == null)
                {
                    type = $"Json{GetPropertyName(key)}Type";
                    var obj = JsonSerializer.Deserialize<PlainJson>(value.ToString()!);
                    if ((obj?.Count ?? 0) == 0)
                        type = "object";
                    else
                        this.subClasses.Add(new JsonObject(obj, depth + 1, type));
                }
                else if (array.Length == 0)
                {
                    type = "object";
                }
                else
                {
                    type = RegisterArray(array, GetPropertyName(key), depth);
                }

            }
            this.properties.Add(key, type + string.Join(string.Empty, Enumerable.Repeat("[]", arrDepth)));
        } // private void RegisterProperty (string, object , [int])

        private string RegisterArray(object[] array, string key, int depth)
        {
            var element = array[0];
            var types = new HashSet<string>();
            var isPrimitive = true;
            foreach (var e in array)
                types.Add(GetTypeName(e, out isPrimitive));
            if (types.Count > 1) return "object[]";
            var type = types.First();
            if (isPrimitive)
                return type + "[]";

            var subArray = GetArray(element);
            if (subArray == null)
            {
                type = $"Json{key}ElementType";
                var obj = JsonSerializer.Deserialize<PlainJson>(element.ToString()!);
                if ((obj?.Count ?? 0) == 0)
                    type = "object";
                else
                    this.subClasses.Add(new JsonObject(obj, depth + 1, type));
                return type + "[]";
            }
            else if (subArray.Length == 0)
            {
                return "object[]";
            }
            else
            {
                return RegisterArray(subArray, $"{key}Element", depth + 1) + "[]";
            }
        } // private string RegisterArray (object[])

        private string GetTypeName(object value, out bool isPrimitive)
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
                    var types = new HashSet<string>();
                    foreach (var e in array)
                        types.Add(GetTypeName(e, out isPrimitive));

                    if (types.Count == 1)
                    {
                        return types.First() + "[]";
                    }
                    else
                    {
                        isPrimitive = false;
                        return "object[]";
                    }
                }
                else
                {
                    return "object[]";
                }
            }
            else if (IsJsonObject(value))
            {
                isPrimitive = false;
                return string.Empty;
            }
            
            return "object";
        } // private string GetTypeName (object, out bool)

        private static object[]? GetArray(object value)
        {
            if (value is not JsonElement element) return null;
            if (element.ValueKind != JsonValueKind.Array) return null;
            return element.EnumerateArray().Cast<object>().ToArray();
        } // private static object[]? GetArray (object)

        private static bool IsJsonObject(object value)
        {
            try
            {
                return JsonSerializer.Deserialize<PlainJson>(value.ToString()!) != null;
            }
            catch
            {
                return false;
            }
        } // private static bool IsJsonObject (object)

        private static string GetPropertyName(string name)
        {
            var camel = re_separator.Replace(name, s => s.Value.Last().ToString().ToUpper());
            return string.Concat(camel[0].ToString().ToUpper(), camel.AsSpan(1));
        } // private static string GetPropertyName (string)

        override public string ToString()
        {
            var sb = new StringBuilder();
            if (this.depth == 0)
            {
                sb.AppendLine();
                sb.AppendLine($"/* Generated by {nameof(JsonTemplateMaker)} */");
                sb.AppendLine();
                sb.AppendLine("using System.Text.Json.Serialization;");
                sb.AppendLine();
                sb.AppendLine($"namespace {this.ns}");
                sb.AppendLine("{");
            }

            var indent = new string('\t', this.depth + 1);

            sb.AppendLine($"{indent}public sealed class {this.name}");
            sb.AppendLine($"{indent}{{");

            foreach ((var name, var type) in this.properties.Items())
            {
                sb.AppendLine($"{indent}\t[JsonPropertyName(\"{name}\")]");

                var propName = GetPropertyName(name);
                sb.AppendLine($"{indent}\tpublic {type} {propName} {{ get; set; }}");
                sb.AppendLine();
            }

            sb.AppendLine($"{indent}\tpublic {this.name}() {{ }}");

            foreach (var sub in this.subClasses)
            {
                sb.AppendLine();
                sb.AppendLine(sub.ToString());
            }

            sb.Append($"{indent}}}");

            if (this.depth == 0)
            {
                sb.AppendLine();
                sb.AppendLine("}");
            }
            return sb.ToString();
        } // override public string ToString ()
    } // internal class JsonObject
} // namespace JsonTemplateMaker
