using Newtonsoft.Json;

namespace Aero.Languages;

public sealed class LogMessageParams
{
    [JsonProperty("type")]
    public int Type { get; set; }

    [JsonProperty("message")]
    public string Message { get; set; } = string.Empty;
}