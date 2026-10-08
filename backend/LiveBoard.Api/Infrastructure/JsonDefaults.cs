using System.Text.Json;
using System.Text.Json.Serialization;

namespace LiveBoard.Api.Infrastructure;

public static class JsonDefaults
{
    /// <summary>Matches the API's wire format: camelCase properties, camelCase enum strings.</summary>
    public static readonly JsonSerializerOptions Options = Configure(new JsonSerializerOptions(JsonSerializerDefaults.Web));

    public static JsonSerializerOptions Configure(JsonSerializerOptions options)
    {
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        return options;
    }
}
