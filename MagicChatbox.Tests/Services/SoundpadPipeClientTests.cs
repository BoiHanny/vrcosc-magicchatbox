using System;
using System.IO.Pipes;
using System.Text;
using System.Threading.Tasks;
using vrcosc_magicchatbox.Services;
using Xunit;

namespace MagicChatbox.Tests.Services;

public sealed class SoundpadPipeClientTests
{
    private static string RandomPipeName() => "mcb-sp-test-" + Guid.NewGuid().ToString("N");

    private static NamedPipeServerStream CreateServer(string pipeName, PipeTransmissionMode mode)
        => new(pipeName, PipeDirection.InOut, NamedPipeServerStream.MaxAllowedServerInstances, mode, PipeOptions.Asynchronous);

    private static async Task<string> ServeOneRequestAsync(NamedPipeServerStream server, string response)
    {
        await server.WaitForConnectionAsync();
        var buffer = new byte[64 * 1024];
        int read = await server.ReadAsync(buffer);
        string request = Encoding.UTF8.GetString(buffer, 0, read);
        byte[] payload = Encoding.UTF8.GetBytes(response);
        await server.WriteAsync(payload);
        await server.FlushAsync();
        return request;
    }

    [Fact]
    public async Task SendRequestAsync_RoundTripsCommandAndResponse()
    {
        string pipeName = RandomPipeName();
        await using var server = CreateServer(pipeName, PipeTransmissionMode.Message);
        using var client = new SoundpadPipeClient(pipeName);

        Task<string> serverTask = ServeOneRequestAsync(server, "PLAYING");
        var reply = await client.SendRequestAsync("GetPlayStatus()", timeoutMs: 5000);

        Assert.Equal("PLAYING", reply.Response);
        Assert.True(reply.RequestDelivered);
        Assert.Equal("GetPlayStatus()", await serverTask);
        Assert.True(client.IsConnected);
    }

    [Fact]
    public async Task SendRequestAsync_ByteModeServer_StillRoundTrips()
    {
        string pipeName = RandomPipeName();
        await using var server = CreateServer(pipeName, PipeTransmissionMode.Byte);
        using var client = new SoundpadPipeClient(pipeName);

        Task<string> serverTask = ServeOneRequestAsync(server, "STOPPED");
        var reply = await client.SendRequestAsync("GetPlayStatus()", timeoutMs: 5000);

        Assert.Equal("STOPPED", reply.Response);
        Assert.Equal("GetPlayStatus()", await serverTask);
    }

    [Fact]
    public async Task SendRequestAsync_LargeResponse_IsReadCompletely()
    {
        string pipeName = RandomPipeName();
        await using var server = CreateServer(pipeName, PipeTransmissionMode.Message);
        using var client = new SoundpadPipeClient(pipeName);

        // Larger than the client's 64 KB read buffer, like a big GetSoundlist() reply.
        string bigResponse = "<Soundlist>" + new string('x', 300_000) + "</Soundlist>";
        Task<string> serverTask = ServeOneRequestAsync(server, bigResponse);
        var reply = await client.SendRequestAsync("GetSoundlist()", timeoutMs: 10000);

        Assert.Equal(bigResponse, reply.Response);
        await serverTask;
    }

    [Fact]
    public async Task SendRequestAsync_NoServer_ReturnsNullUndelivered()
    {
        using var client = new SoundpadPipeClient(RandomPipeName());
        var reply = await client.SendRequestAsync("GetPlayStatus()", timeoutMs: 200);
        Assert.Null(reply.Response);
        Assert.False(reply.RequestDelivered);
        Assert.False(client.IsConnected);
    }

    [Fact]
    public async Task TryConnectAsync_NoServer_ReturnsFalse()
    {
        using var client = new SoundpadPipeClient(RandomPipeName());
        Assert.False(await client.TryConnectAsync(timeoutMs: 200));
    }

    [Fact]
    public async Task SendRequestAsync_ServerGone_ReturnsNull_ThenReconnects()
    {
        string pipeName = RandomPipeName();
        using var client = new SoundpadPipeClient(pipeName);

        var firstServer = CreateServer(pipeName, PipeTransmissionMode.Message);
        Task<string> firstServe = ServeOneRequestAsync(firstServer, "R-200");
        Assert.Equal("R-200", (await client.SendRequestAsync("DoStopSound()", timeoutMs: 5000)).Response);
        await firstServe;
        await firstServer.DisposeAsync();

        // The connection is now broken; the request fails and drops the connection...
        var failed = await client.SendRequestAsync("GetPlayStatus()", timeoutMs: 1000);
        Assert.Null(failed.Response);

        // ...and the next request transparently reconnects to a fresh server.
        await using var secondServer = CreateServer(pipeName, PipeTransmissionMode.Message);
        Task<string> secondServe = ServeOneRequestAsync(secondServer, "PAUSED");
        Assert.Equal("PAUSED", (await client.SendRequestAsync("GetPlayStatus()", timeoutMs: 5000)).Response);
        await secondServe;
    }

    [Fact]
    public async Task SendRequestAsync_ServerClosesWithoutReply_ReturnsNullNotEmpty()
    {
        string pipeName = RandomPipeName();
        var server = CreateServer(pipeName, PipeTransmissionMode.Message);
        using var client = new SoundpadPipeClient(pipeName);

        // Server accepts, reads the request, then dies without replying: the client's read
        // sees a 0-byte EOF, which must surface as a broken pipe (null), not as response "".
        var serverTask = Task.Run(async () =>
        {
            await server.WaitForConnectionAsync();
            var buffer = new byte[1024];
            await server.ReadAsync(buffer);
            await server.DisposeAsync();
        });

        var reply = await client.SendRequestAsync("GetPlayStatus()", timeoutMs: 5000);
        Assert.Null(reply.Response);
        Assert.True(reply.RequestDelivered);
        Assert.False(client.IsConnected);
        await serverTask;
    }

    [Fact]
    public async Task SendRequestAsync_ServerReadsButNeverResponds_TimesOutDelivered()
    {
        string pipeName = RandomPipeName();
        await using var server = CreateServer(pipeName, PipeTransmissionMode.Message);
        using var client = new SoundpadPipeClient(pipeName);

        // Server consumes the request but never answers (Soundpad busy/hung).
        var serverTask = Task.Run(async () =>
        {
            await server.WaitForConnectionAsync();
            var buffer = new byte[1024];
            await server.ReadAsync(buffer);
        });

        var reply = await client.SendRequestAsync("GetPlayStatus()", timeoutMs: 500);

        // Delivered-but-unanswered: callers must NOT retry the command via another channel.
        Assert.Null(reply.Response);
        Assert.True(reply.RequestDelivered);
        Assert.False(client.IsConnected);
        await serverTask;
    }

    [Fact]
    public async Task SendRequestAsync_ServerNeverReadsRequest_TimesOutUndelivered()
    {
        string pipeName = RandomPipeName();
        await using var server = CreateServer(pipeName, PipeTransmissionMode.Message);
        using var client = new SoundpadPipeClient(pipeName);

        // Server accepts but never reads: with the pipe's zero-byte buffer the write itself
        // cannot complete, so the request is reported as undelivered (fallback is safe).
        Task accept = server.WaitForConnectionAsync();
        var reply = await client.SendRequestAsync("GetPlayStatus()", timeoutMs: 300);

        Assert.Null(reply.Response);
        Assert.False(reply.RequestDelivered);
        Assert.False(client.IsConnected);
        await accept;
    }

    [Fact]
    public async Task SendRequestAsync_AfterDispose_ReturnsNull()
    {
        var client = new SoundpadPipeClient(RandomPipeName());
        client.Dispose();
        var reply = await client.SendRequestAsync("GetPlayStatus()", timeoutMs: 200);
        Assert.Null(reply.Response);
        Assert.False(reply.RequestDelivered);
    }
}
