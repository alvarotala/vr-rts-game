using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;

namespace UnityEngine
{
    // Emulates the parts of Unity's JsonUtility that the match protocol relies on:
    // public (or [SerializeField]) instance fields, no properties, [NonSerialized] and
    // readonly fields skipped, enums as integers, null strings written as "", null
    // arrays as [], null [Serializable] objects as default instances, byte[] as numbers,
    // unknown JSON members ignored and missing members left at their defaults.
    public static class JsonUtility
    {
        private const int MaxDepth = 10;
        private static readonly Dictionary<Type, FieldInfo[]> fieldCache = new Dictionary<Type, FieldInfo[]>();

        public static string ToJson(object obj) => ToJson(obj, false);
        public static string ToJson(object obj, bool prettyPrint)
        {
            if (obj == null) return "";
            var output = new StringBuilder();
            WriteObject(output, obj, obj.GetType(), 0);
            return output.ToString();
        }

        public static T FromJson<T>(string json) => (T)FromJson(json, typeof(T));

        public static object FromJson(string json, Type type)
        {
            if (string.IsNullOrEmpty(json)) return type.IsValueType ? Activator.CreateInstance(type) : null;
            JsonDocument document;
            try { document = JsonDocument.Parse(json); }
            catch (JsonException error) { throw new ArgumentException("JSON parse error: " + error.Message); }
            using (document)
            {
                if (document.RootElement.ValueKind != JsonValueKind.Object) throw new ArgumentException("JSON must represent an object type.");
                object instance = Activator.CreateInstance(type, true);
                Populate(instance, type, document.RootElement, 0);
                return instance;
            }
        }

        private static FieldInfo[] Fields(Type type)
        {
            if (fieldCache.TryGetValue(type, out var cached)) return cached;
            var chain = new List<Type>();
            for (var t = type; t != null && t != typeof(object) && t != typeof(ValueType); t = t.BaseType) chain.Insert(0, t);
            var fields = chain.SelectMany(t => t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                .Where(f => (f.IsPublic || f.GetCustomAttribute<SerializeField>() != null) && !f.IsInitOnly && !f.IsLiteral && !f.IsNotSerialized && Supported(f.FieldType))
                .ToArray();
            fieldCache[type] = fields; return fields;
        }

        private static bool Supported(Type type)
        {
            if (type.IsPrimitive || type == typeof(string) || type.IsEnum) return true;
            if (type.IsArray) return type.GetArrayRank() == 1 && Element(type) != null && !Element(type).IsArray && Supported(Element(type));
            if (IsList(type)) { var e = Element(type); return !e.IsArray && !IsList(e) && Supported(e); }
            if (type.IsGenericType || type.IsInterface || type.IsAbstract || type == typeof(object) || typeof(Delegate).IsAssignableFrom(type)) return false;
            return type.GetCustomAttribute<SerializableAttribute>() != null || type.IsSerializable;
        }

        private static bool IsList(Type type) => type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>);
        private static Type Element(Type type) => type.IsArray ? type.GetElementType() : IsList(type) ? type.GetGenericArguments()[0] : null;

        private static void WriteObject(StringBuilder output, object value, Type type, int depth)
        {
            output.Append('{');
            bool first = true;
            foreach (var field in Fields(type))
            {
                if (depth >= MaxDepth && !(field.FieldType.IsPrimitive || field.FieldType == typeof(string) || field.FieldType.IsEnum)) continue;
                if (!first) output.Append(',');
                first = false;
                WriteString(output, field.Name); output.Append(':');
                WriteValue(output, value == null ? null : field.GetValue(value), field.FieldType, depth + 1);
            }
            output.Append('}');
        }

        private static void WriteValue(StringBuilder output, object value, Type type, int depth)
        {
            if (type == typeof(string)) { WriteString(output, (string)value ?? ""); return; }
            if (type == typeof(bool)) { output.Append((bool)value ? "true" : "false"); return; }
            if (type.IsEnum) { output.Append(Convert.ToInt64(value, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture)); return; }
            if (type == typeof(float)) { output.Append(Number((float)value)); return; }
            if (type == typeof(double)) { output.Append(Number((double)value)); return; }
            if (type.IsPrimitive) { output.Append(Convert.ToString(value, CultureInfo.InvariantCulture)); return; }
            var element = Element(type);
            if (element != null)
            {
                output.Append('[');
                if (value is IEnumerable items)
                {
                    bool first = true;
                    foreach (var item in items)
                    {
                        if (!first) output.Append(',');
                        first = false;
                        WriteValue(output, item, element, depth);
                    }
                }
                output.Append(']');
                return;
            }
            // Unity cannot represent a null serializable object; it writes default values.
            WriteObject(output, value ?? Activator.CreateInstance(type, true), type, depth);
        }

        private static string Number(float value) => float.IsNaN(value) || float.IsInfinity(value) ? "0" : value.ToString("R", CultureInfo.InvariantCulture);
        private static string Number(double value) => double.IsNaN(value) || double.IsInfinity(value) ? "0" : value.ToString("R", CultureInfo.InvariantCulture);

        private static void WriteString(StringBuilder output, string text)
        {
            output.Append('"');
            foreach (char c in text)
            {
                switch (c)
                {
                    case '"': output.Append("\\\""); break;
                    case '\\': output.Append("\\\\"); break;
                    case '\n': output.Append("\\n"); break;
                    case '\r': output.Append("\\r"); break;
                    case '\t': output.Append("\\t"); break;
                    case '\b': output.Append("\\b"); break;
                    case '\f': output.Append("\\f"); break;
                    default:
                        if (c < 0x20) output.Append("\\u").Append(((int)c).ToString("x4"));
                        else output.Append(c);
                        break;
                }
            }
            output.Append('"');
        }

        private static void Populate(object instance, Type type, JsonElement json, int depth)
        {
            var fields = Fields(type);
            foreach (var property in json.EnumerateObject())
            {
                var field = Array.Find(fields, f => f.Name == property.Name);
                if (field == null) continue;
                field.SetValue(instance, Read(property.Value, field.FieldType, field.GetValue(instance), depth + 1));
            }
        }

        private static object Read(JsonElement json, Type type, object current, int depth)
        {
            try
            {
                if (type == typeof(string)) return json.ValueKind == JsonValueKind.String ? json.GetString() : json.ValueKind == JsonValueKind.Null ? "" : json.GetRawText();
                if (type == typeof(bool)) return json.ValueKind == JsonValueKind.True || json.ValueKind == JsonValueKind.Number && json.GetDouble() != 0;
                if (type.IsEnum) return Enum.ToObject(type, json.ValueKind == JsonValueKind.Number ? json.GetInt64() : 0);
                if (type == typeof(float)) return json.ValueKind == JsonValueKind.Number ? (float)json.GetDouble() : 0f;
                if (type == typeof(double)) return json.ValueKind == JsonValueKind.Number ? json.GetDouble() : 0d;
                if (type.IsPrimitive) return json.ValueKind == JsonValueKind.Number ? Convert.ChangeType(json.GetDecimal(), type, CultureInfo.InvariantCulture) : Activator.CreateInstance(type);
                var element = Element(type);
                if (element != null)
                {
                    var values = json.ValueKind == JsonValueKind.Array ? json.EnumerateArray().Select(v => Read(v, element, null, depth)).ToList() : new List<object>();
                    if (type.IsArray)
                    {
                        var array = Array.CreateInstance(element, values.Count);
                        for (int i = 0; i < values.Count; i++) array.SetValue(values[i], i);
                        return array;
                    }
                    var list = (IList)Activator.CreateInstance(type);
                    foreach (var v in values) list.Add(v);
                    return list;
                }
                object target = Activator.CreateInstance(type, true);
                if (json.ValueKind == JsonValueKind.Object && depth < MaxDepth) Populate(target, type, json, depth);
                return target;
            }
            catch (Exception error) when (!(error is ArgumentException))
            {
                throw new ArgumentException("JSON value does not match field type " + type.Name + ": " + error.Message);
            }
        }
    }
}
