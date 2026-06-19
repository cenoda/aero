using System;
using System.IO;

namespace Aero.Tests.Languages.Helpers;

internal sealed class InMemoryDuplex : IDisposable
{
    private readonly ChannelStream _firstToSecond = new();
    private readonly ChannelStream _secondToFirst = new();

    private InMemoryDuplex()
    {
        ClientStream = new DuplexEndpointStream(_secondToFirst, _firstToSecond);
        ServerStream = new DuplexEndpointStream(_firstToSecond, _secondToFirst);
    }

    public Stream ClientStream { get; }

    public Stream ServerStream { get; }

    public static InMemoryDuplex CreatePair()
    {
        return new InMemoryDuplex();
    }

    public void Dispose()
    {
        ClientStream.Dispose();
        ServerStream.Dispose();
    }
}
