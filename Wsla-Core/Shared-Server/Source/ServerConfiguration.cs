using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Wsla
{
    public static class ServerConfigurationLoader
    {
        static JsonSerializerOptions Options = new JsonSerializerOptions(JsonSerializerDefaults.General)
        {
            ReadCommentHandling = JsonCommentHandling.Skip,
            Converters = { new JsonStringEnumConverter<ServerRegion>(), new IPAddressJsonConverter(), }
        };

        public static T Load<T>() where T : ServerConfigurationData => Load<T>("Configuration.json");
        public static T Load<T>(string path)
            where T : ServerConfigurationData
        {
            using (var file = File.OpenRead(path))
            {
                return JsonSerializer.Deserialize<T>(file, Options);
            }
        }
    }

    public abstract class ServerConfigurationData
    {

    }
}