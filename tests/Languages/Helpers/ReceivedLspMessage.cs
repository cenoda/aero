using Newtonsoft.Json.Linq;

namespace Aero.Tests.Languages.Helpers;

/// <summary>
/// A notification received by the fake LSP server.
/// </summary>
/// <param name="Method">The JSON-RPC method name.</param>
/// <param name="Params">The notification parameters.</param>
public record ReceivedLspMessage(string Method, JToken? Params);
