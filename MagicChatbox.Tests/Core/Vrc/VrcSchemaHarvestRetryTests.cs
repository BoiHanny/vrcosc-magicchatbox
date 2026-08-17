using MagicChatbox.Osc;
using MagicChatbox.Osc.Query;
using MagicChatbox.Vrc;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace MagicChatbox.Tests.Core.Vrc;

// A harvest is the only way the app learns what an avatar can do. It used to be asked for exactly once
// per avatar change or peer handshake, and a single dropped fetch therefore left the schema empty for as
// long as somebody kept wearing that avatar -- with every surface built on the schema reporting the
// avatar as having no parameters at all rather than as unread.
public class VrcSchemaHarvestRetryTests
{
    [Fact]
    public void The_backoff_doubles_from_one_second_and_stops_at_thirty()
    {
        Assert.Equal(TimeSpan.FromSeconds(1), VrcSchemaHarvester.DelayFor(1));
        Assert.Equal(TimeSpan.FromSeconds(2), VrcSchemaHarvester.DelayFor(2));
        Assert.Equal(TimeSpan.FromSeconds(4), VrcSchemaHarvester.DelayFor(3));
        Assert.Equal(TimeSpan.FromSeconds(8), VrcSchemaHarvester.DelayFor(4));
        Assert.Equal(TimeSpan.FromSeconds(16), VrcSchemaHarvester.DelayFor(5));
        Assert.Equal(TimeSpan.FromSeconds(30), VrcSchemaHarvester.DelayFor(6));
        Assert.Equal(TimeSpan.FromSeconds(30), VrcSchemaHarvester.DelayFor(40));
    }

    [Fact]
    public void The_backoff_never_returns_a_delay_that_would_spin()
    {
        Assert.True(VrcSchemaHarvester.DelayFor(0) > TimeSpan.Zero);
        Assert.True(VrcSchemaHarvester.DelayFor(-5) > TimeSpan.Zero);
    }

    [Fact]
    public async Task The_tree_is_re_read_on_a_timer_even_when_nothing_asks()
    {
        // The reported bug: put a different avatar on and the page keeps showing the old one forever.
        // Both events this class listens to are one-shot in that situation -- the handshake has already
        // happened, and the epoch only moves on an inbound /avatar/change, which never arrives on a
        // client that did not pick up our advertisement. Without a poll the tree is read once a session.
        using var harness = new Harness(pollFast: true);

        await harness.WaitForPollsAsync(3);

        Assert.True(harness.Harvester.Polled >= 3, "the tree was not re-read on its own");
    }

    [Fact]
    public async Task A_poll_that_finds_no_peer_does_not_spend_the_failure_budget()
    {
        // The poll runs on a clock whether or not VRChat is there. Counting each tick as a failure would
        // exhaust the backoff on nothing having gone wrong, and then a real failure would get no retry.
        using var harness = new Harness(pollFast: true);

        await harness.WaitForPollsAsync(4);

        Assert.Equal(0, harness.Harvester.Retried);
    }

    [Fact]
    public void The_two_accounts_of_which_avatar_is_loaded_only_disagree_when_both_are_readable()
    {
        // The peer's tree and /avatar/change are separate reports. Either being silent is ordinary; only
        // both speaking and disagreeing means the tree is stale.
        var epoch = new VrcAvatarEpoch();

        Assert.False(VrcSchemaHarvester.TreeLagsBehind(epoch, "avtr_one"));

        epoch.AdvanceToAvatar("avtr_one");

        Assert.False(VrcSchemaHarvester.TreeLagsBehind(epoch, "avtr_one"));
        Assert.False(VrcSchemaHarvester.TreeLagsBehind(epoch, string.Empty));
        Assert.False(VrcSchemaHarvester.TreeLagsBehind(epoch, null));
        Assert.True(VrcSchemaHarvester.TreeLagsBehind(epoch, "avtr_previous"));
    }

    [Fact]
    public async Task A_peer_that_never_answers_is_re_asked_and_then_left_alone()
    {
        // No peer has handshaked, so every fetch returns null immediately. Without a retry this ends after
        // one attempt; with an unbounded one it would ask forever at a peer that has gone away for good.
        using var harness = new Harness();

        harness.Epoch.AdvanceToAvatar("avtr_one");
        await harness.RunUntilQuietAsync();

        Assert.Equal(VrcSchemaHarvester.MaxRetryAttempts, harness.Harvester.Retried);
        Assert.Equal(VrcSchemaHarvester.MaxRetryAttempts + 1, harness.Harvester.Failed);
        Assert.Equal(0, harness.Sink.Harvests);
    }

    [Fact]
    public async Task Each_re_ask_waits_longer_than_the_last()
    {
        using var harness = new Harness();

        harness.Epoch.AdvanceToAvatar("avtr_one");
        await harness.RunUntilQuietAsync();

        Assert.Equal(
            new[]
            {
                TimeSpan.FromSeconds(1),
                TimeSpan.FromSeconds(2),
                TimeSpan.FromSeconds(4),
                TimeSpan.FromSeconds(8),
                TimeSpan.FromSeconds(16),
                TimeSpan.FromSeconds(30),
            },
            harness.Waits);
    }

    [Fact]
    public async Task A_fresh_event_starts_the_count_again()
    {
        // The ceiling exists so a peer that has gone away is not polled forever. It must not also mean
        // that the next avatar somebody puts on inherits an exhausted budget and is never re-asked for.
        using var harness = new Harness();

        harness.Epoch.AdvanceToAvatar("avtr_one");
        await harness.RunUntilQuietAsync();
        long afterFirst = harness.Harvester.Retried;

        harness.Epoch.AdvanceToAvatar("avtr_two");
        await harness.RunUntilQuietAsync();

        Assert.Equal(VrcSchemaHarvester.MaxRetryAttempts, afterFirst);
        Assert.Equal(VrcSchemaHarvester.MaxRetryAttempts * 2, harness.Harvester.Retried);
    }

    [Fact]
    public async Task A_backoff_in_flight_at_shutdown_never_re_asks()
    {
        // The wait outlives the drain loop by design, so it is the one place a disposed harvester could
        // still write to a completed channel or wake a torn-down transport.
        var harness = new Harness(holdTheWait: true);

        harness.Epoch.AdvanceToAvatar("avtr_one");
        await harness.WaitForFirstBackoffAsync();

        Assert.Equal(0, harness.Harvester.Retried);

        harness.Dispose();
        await Task.Delay(60);

        Assert.Equal(0, harness.Harvester.Retried);
    }

    private sealed class Harness : IDisposable
    {
        private readonly CancellationTokenSource _cts = new();
        private readonly Task _pump;
        private readonly ConcurrentQueue<TimeSpan> _waits = new();
        private readonly bool _holdTheWait;
        private int _waitsObserved;

        internal Harness(bool holdTheWait = false, bool pollFast = false)
        {
            _holdTheWait = holdTheWait;
            Query = new FakeQueryService();
            Epoch = new VrcAvatarEpoch();
            Sink = new CountingSink();
            Harvester = new VrcSchemaHarvester(
                Query.Service,
                Epoch,
                Sink,
                RecordAndSkipAsync,
                pollFast ? TimeSpan.FromMilliseconds(15) : TimeSpan.FromMinutes(10));
            _pump = Harvester.RunAsync(_cts.Token);
        }

        internal async Task WaitForPollsAsync(long target)
        {
            var deadline = DateTime.UtcNow.AddSeconds(5);

            while (Harvester.Polled < target && DateTime.UtcNow < deadline)
            {
                await Task.Delay(5);
            }
        }

        internal FakeQueryService Query { get; }

        internal VrcAvatarEpoch Epoch { get; }

        internal CountingSink Sink { get; }

        internal VrcSchemaHarvester Harvester { get; }

        internal IReadOnlyList<TimeSpan> Waits => _waits.ToArray();

        // The delay is what the production path spends real seconds on, so the test replaces it with a
        // recording no-op. Everything else -- the counting, the ceiling, the cancellation -- is the real
        // class.
        private Task RecordAndSkipAsync(TimeSpan delay, CancellationToken token)
        {
            if (token.IsCancellationRequested)
            {
                return Task.FromCanceled(token);
            }

            _waits.Enqueue(delay);
            Interlocked.Increment(ref _waitsObserved);

            // Held open so the test can assert on a backoff that is still in flight when shutdown lands.
            return _holdTheWait ? Task.Delay(Timeout.Infinite, token) : Task.CompletedTask;
        }

        internal async Task WaitForFirstBackoffAsync()
        {
            var deadline = DateTime.UtcNow.AddSeconds(5);
            while (Volatile.Read(ref _waitsObserved) < 1 && DateTime.UtcNow < deadline)
            {
                await Task.Delay(5);
            }

            Assert.Equal(1, Volatile.Read(ref _waitsObserved));
        }

        internal async Task RunUntilQuietAsync(int? stopAfter = null)
        {
            int target = stopAfter ?? VrcSchemaHarvester.MaxRetryAttempts;
            var deadline = DateTime.UtcNow.AddSeconds(5);

            while (Volatile.Read(ref _waitsObserved) < target && DateTime.UtcNow < deadline)
            {
                await Task.Delay(5);
            }

            await Task.Delay(30);
            Interlocked.Exchange(ref _waitsObserved, 0);
        }

        public void Dispose()
        {
            _cts.Cancel();
            Harvester.Dispose();
            try
            {
                _pump.Wait(TimeSpan.FromSeconds(2));
            }
            catch (AggregateException)
            {
            }

            Query.Dispose();
            _cts.Dispose();
        }
    }

    private sealed class CountingSink : IVrcSchemaSink
    {
        private long _harvests;

        internal long Harvests => Interlocked.Read(ref _harvests);

        public void OnSchemaHarvested(VrcAvatarSchemaHarvest harvest) => Interlocked.Increment(ref _harvests);
    }

    // A real OscQueryService with no peer ever handshaked: TryFetchPeerSnapshotAsync short-circuits to
    // null without touching the network, which is exactly the failure the retry exists for.
    private sealed class FakeQueryService : IDisposable
    {
        private readonly SilentDiscovery _discovery = new();
        private readonly SilentReceiver _receiver = new();
        private readonly HttpClient _http = new();

        internal FakeQueryService()
        {
            Service = new OscQueryService(
                _receiver,
                _discovery,
                new OscQueryClient(_http),
                new DiscoveredOscEndpointProvider(),
                new SilentStatus());
        }

        internal OscQueryService Service { get; }

        public void Dispose()
        {
            Service.Dispose();
            _http.Dispose();
            _discovery.Dispose();
            _receiver.Dispose();
        }
    }

    private sealed class SilentDiscovery : IOscQueryDiscovery
    {
        public event Action<OscQueryAdvertisement>? AdvertisementReceived;

        public void Start()
        {
        }

        public void Advertise(string instanceName, IPAddress address, int httpPort, int oscPort)
        {
        }

        public void Query()
        {
        }

        public void Dispose() => AdvertisementReceived = null;
    }

    private sealed class SilentReceiver : IOscReceiver
    {
        public int Port => 9000;

        public OscDecodeCounters Counters => default;

        public int Bind() => 9000;

        public Task RunAsync(CancellationToken cancellationToken) =>
            Task.Delay(Timeout.Infinite, cancellationToken);

        public void Dispose()
        {
        }
    }

    private sealed class SilentStatus : IOscTransportStatusSink
    {
        public void OnStatus(OscTransportStatus status)
        {
        }
    }
}
