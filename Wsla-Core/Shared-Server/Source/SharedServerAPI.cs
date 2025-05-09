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

            options.Converters.Add(new FixedStringJsonConverter<FixedString<FS20>>());
            options.Converters.Add(new FixedStringJsonConverter<FixedString<FS40>>());
            options.Converters.Add(new FixedStringJsonConverter<FixedString<FS60>>());
            options.Converters.Add(new FixedStringJsonConverter<FixedString<FS80>>());
        }

        static SharedServerAPI()
        {
            JsonOptions = new JsonSerializerOptions(JsonSerializerOptions.Web);
            ConfigureJsonOptions(JsonOptions);
        }
    }
}