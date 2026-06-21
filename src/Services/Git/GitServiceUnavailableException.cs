using System;

namespace Aero.Services.Git;

/// <summary>
/// Exception thrown when the Git service (native library) cannot be loaded.
/// </summary>
public sealed class GitServiceUnavailableException : Exception
{
    public GitServiceUnavailableException(string message)
        : base(message)
    {
    }

    public GitServiceUnavailableException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
