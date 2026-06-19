using System;

namespace Aero.Languages;

public sealed class LSPLogMessageEventArgs : EventArgs
{
    public LSPLogMessageEventArgs(LogMessageParams message)
    {
        Message = message ?? throw new ArgumentNullException(nameof(message));
    }

    public LogMessageParams Message { get; }
}