using Newtonsoft.Json;

namespace Aero.Languages;

internal sealed class InitializeResult
{
    [JsonProperty("capabilities")]
    public ServerCapabilities? Capabilities { get; set; }
}