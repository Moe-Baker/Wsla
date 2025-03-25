using System.Text.Json;

namespace Wsla
{
    public static class SharedAPI
    {
        public static JsonSerializerOptions JsonOptions { get; }

        static SharedAPI()
        {
            JsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.General)
            {
                IncludeFields = true,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault,
            };

            JsonOptions.Converters.Add(new IPAddressJsonConverter());
            JsonOptions.Converters.Add(new FixedStringJsonConverter<FixedString20>());
            JsonOptions.Converters.Add(new FixedStringJsonConverter<FixedString40>());
            JsonOptions.Converters.Add(new FixedStringJsonConverter<FixedString60>());
            JsonOptions.Converters.Add(new FixedStringJsonConverter<FixedString80>());
        }
    }
}