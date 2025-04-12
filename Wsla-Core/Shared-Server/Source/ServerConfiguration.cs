using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Schema;
using System.Text.Json.Serialization;

namespace Wsla
{
    public static class ServerConfigurationLoader
    {
        public static JsonSerializerOptions JsonOptions { get; }

        public static T Load<T>() => Load<T>("Configuration.json");
        public static T Load<T>(string path)
        {
            using (var file = File.OpenRead(path))
            {
                return JsonSerializer.Deserialize<T>(file, JsonOptions);
            }
        }

        public static class Schema
        {
            public static JsonNode Create<T>()
            {
                var SchemaOptions = new JsonSchemaExporterOptions()
                {
                    TreatNullObliviousAsNonNullable = true,
                    TransformSchemaNode = Transform,
                };

                return JsonOptions.GetJsonSchemaAsNode(typeof(T), SchemaOptions);
            }

            public static void Write<T>(string file)
            {
                var schema = Create<T>();

                var json = schema.ToString();

                var directory = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);

                File.WriteAllText(Path.Join(directory, file), json);
            }

            static JsonNode Transform(JsonSchemaExporterContext context, JsonNode node)
            {
                WriteDescription(ref node, context);

                ModifyMatchMakingValue(ref node, context);
                ModifyNullableMatchMakingValue(ref node, context);

                return node;
            }

            static void WriteDescription(ref JsonNode node, in JsonSchemaExporterContext context)
            {
                var attributes = GetAttributeProvider(in context);

                // Look up any description attributes.
                var target = attributes?
                    .GetCustomAttributes(inherit: true)
                    .Select(attr => attr as DescriptionAttribute)
                    .FirstOrDefault(attr => attr is not null);

                if (target is null)
                    return;

                var schema = EnsureSchemaObject(ref node);
                schema.Insert(0, "description", target.Description);
            }
            static void ModifyMatchMakingValue(ref JsonNode node, in JsonSchemaExporterContext context)
            {
                if (context.TypeInfo.Type != typeof(MatchMakingValue))
                    return;

                var schema = EnsureSchemaObject(ref node);

                schema["type"] = new JsonArray([JsonValue.Create("string"), JsonValue.Create("number")]);
            }
            static void ModifyNullableMatchMakingValue(ref JsonNode node, in JsonSchemaExporterContext context)
            {
                if (context.TypeInfo.Type != typeof(MatchMakingValue?))
                    return;

                var schema = EnsureSchemaObject(ref node);

                schema["type"] = new JsonArray([JsonValue.Create("null"), JsonValue.Create("string"), JsonValue.Create("number")]);
            }

            static ICustomAttributeProvider GetAttributeProvider(in JsonSchemaExporterContext context)
            {
                if (context.PropertyInfo is null)
                    return context.TypeInfo.Type;

                return context.PropertyInfo.AttributeProvider;
            }
            static JsonObject EnsureSchemaObject(ref JsonNode node)
            {
                if (node is JsonObject obj)
                    return obj;

                // Handle the case where the schema is a Boolean.
                var kind = node.GetValueKind();

                Debug.Assert(kind is JsonValueKind.True or JsonValueKind.False);

                node = obj = new JsonObject();

                if (kind is JsonValueKind.False)
                    obj.Add("not", true);

                return obj;
            }
        }

        static ServerConfigurationLoader()
        {
            JsonOptions = new JsonSerializerOptions(JsonSerializerOptions.Default)
            {
                ReadCommentHandling = JsonCommentHandling.Skip,
                IncludeFields = true,
            };

            JsonOptions.Converters.Add(new JsonStringEnumConverter<ServerRegion>());
            JsonOptions.Converters.Add(new MatchMakingValueJsonConverter());
        }
    }

    public abstract class ServerConfigurationData { }
}