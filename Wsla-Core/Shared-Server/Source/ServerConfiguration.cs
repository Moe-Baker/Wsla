using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Wsla
{
    public static class ServerConfigurationLoader
    {
        static JsonSerializerOptions Options;

        public static T Load<T>() => Load<T>("Configuration.json");
        public static T Load<T>(string path)
        {
            using (var file = File.OpenRead(path))
            {
                return JsonSerializer.Deserialize<T>(file, Options);
            }
        }

        static ServerConfigurationLoader()
        {
            Options = new JsonSerializerOptions(JsonSerializerDefaults.General)
            {
                ReadCommentHandling = JsonCommentHandling.Skip,
                IncludeFields = true,
            };

            Options.Converters.Add(new JsonStringEnumConverter<ServerRegion>());
            Options.Converters.Add(new MatchMakingValueJsonConverter());
        }
    }

    public abstract class ServerConfigurationData { }
}