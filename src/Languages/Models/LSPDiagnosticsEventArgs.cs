using System;

namespace Aero.Languages;

public sealed class LSPDiagnosticsEventArgs : EventArgs
{
    public LSPDiagnosticsEventArgs(PublishDiagnosticsParams diagnostics)
    {
        Diagnostics = diagnostics ?? throw new ArgumentNullException(nameof(diagnostics));
    }

    public PublishDiagnosticsParams Diagnostics { get; }
}
