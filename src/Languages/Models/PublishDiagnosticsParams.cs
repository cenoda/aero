using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Aero.Languages;

public sealed class PublishDiagnosticsParams
{
    [JsonProperty("uri")]
    public string Uri { get; set; } = string.Empty;

    [JsonProperty("diagnostics")]
    public JToken Diagnostics { get; set; } = JArray.Parse("[]");
}