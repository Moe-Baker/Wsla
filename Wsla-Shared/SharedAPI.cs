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
        }
    }
}