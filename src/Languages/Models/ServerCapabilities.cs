using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Aero.Languages;

internal sealed class ServerCapabilities
{
    [JsonProperty("textDocumentSync")]
    public JToken? TextDocumentSync { get; set; }
}