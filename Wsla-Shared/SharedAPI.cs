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
            };
            JsonOptions.Converters.Add(new IPAddressJsonConverter());
        }
    }
}