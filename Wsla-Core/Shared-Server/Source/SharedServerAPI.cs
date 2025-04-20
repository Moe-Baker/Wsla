using System.Text.Json;

namespace Wsla
{
    public static class SharedServerAPI
    {
        public static JsonSerializerOptions JsonOptions { get; }

        public static void ConfigureJsonOptions(JsonSerializerOptions options)
        {
            options.IncludeFields = true;
            options.Converters.Add(new IPAddressJsonConverter());
        }

        static SharedServerAPI()
        {
            JsonOptions = new JsonSerializerOptions(JsonSerializerOptions.Web);
            ConfigureJsonOptions(JsonOptions);
        }
    }
}