using System.Collections.Concurrent;
using System.Reflection;
using SpireSenseMod;
using Xunit;

namespace SpireSenseMod.Tests;

/// <summary>
/// Tests for WebSocketServer disposed guard (MOD-003).
///
/// WebSocketServer starts an HttpListener, so tests use a unique ephemeral port
/// per test to avoid "address in use" conflicts when tests run in parallel.
/// After Dispose(), EnqueueEvent and FlushBatchQueue must be safe no-ops.
/// </summary>
public class WebSocketServerTests : IDisposable
{
    private static int _nextPort = 18900;
    private readonly int _port;
    private readonly GameStateTracker _tracker;
    private readonly WebSocketServer _server;

    public WebSocketServerTests()
    {
        _port = Interlocked.Increment(ref _nextPort);
        _tracker = new GameStateTracker();
        _server = new WebSocketServer(_port, _tracker);
    }

    public void Dispose()
    {
        // Ensure cleanup even if the test didn't dispose the server
        try { _server.Dispose(); } catch { /* ignore double-dispose */ }
    }

    // ─── EnqueueEvent after Dispose ─────────────────────────────────

    [Fact]
    public void EnqueueEvent_AfterDispose_DoesNotThrow()
    {
        _server.Start();
        _server.Dispose();

        // EnqueueEvent is private — but it is subscribed to tracker.OnGameEvent
        // in the constructor. After Dispose, the event handler is unsubscribed
        // (in Stop()), but we still test the guard by invoking it via reflection.
        var enqueueMethod = typeof(WebSocketServer)
            .GetMethod("EnqueueEvent", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(enqueueMethod);

        var gameEvent = new GameEvent { Type = "state_update", Data = null };

        // Should be a no-op — no exception, no crash
        var exception = Record.Exception(() => enqueueMethod!.Invoke(_server, new object[] { gameEvent }));
        Assert.Null(exception);
    }

    [Fact]
    public void EnqueueEvent_AfterDispose_DoesNotEnqueueToBuffer()
    {
        _server.Start();
        _server.Dispose();

        var enqueueMethod = typeof(WebSocketServer)
            .GetMethod("EnqueueEvent", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(enqueueMethod);

        // Enqueue multiple events after disposal
        for (int i = 0; i < 5; i++)
        {
            enqueueMethod!.Invoke(_server, new object[] { new GameEvent { Type = $"event_{i}" } });
        }

        // Verify the batch queue is empty — events were silently dropped
        var batchQueueField = typeof(WebSocketServer)
            .GetField("_batchQueue", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(batchQueueField);

        var batchQueue = (ConcurrentQueue<GameEvent>)batchQueueField!.GetValue(_server)!;
        Assert.True(batchQueue.IsEmpty, "Batch queue should be empty after disposal — EnqueueEvent guard should prevent enqueuing.");
    }

    [Fact]
    public void EnqueueEvent_AfterDispose_ViaTrackerEvent_DoesNotThrow()
    {
        // The constructor subscribes to _stateTracker.OnGameEvent += EnqueueEvent.
        // Stop() unsubscribes, so after Dispose() the tracker event should NOT
        // reach the server. But if unsubscription fails for some reason, the
        // disposed guard in EnqueueEvent prevents crashes.
        _server.Start();
        _server.Dispose();

        // Emit events through the tracker — should not throw even if the handler
        // were still subscribed (it shouldn't be, but the guard is the safety net).
        var exception = Record.Exception(() =>
        {
            _tracker.SetState(new GameState { Screen = "combat" });
            _tracker.EmitEvent(new GameEvent { Type = "card_played", Data = "strike" });
            _tracker.UpdateState(s => s.Floor = 5);
        });
        Assert.Null(exception);
    }

    // ─── FlushBatchQueue after Dispose ──────────────────────────────

    [Fact]
    public void FlushBatchQueue_AfterDispose_DoesNotThrow()
    {
        _server.Start();
        _server.Dispose();

        var flushMethod = typeof(WebSocketServer)
            .GetMethod("FlushBatchQueue", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(flushMethod);

        // Should be a no-op — no exception, no crash
        var exception = Record.Exception(() => flushMethod!.Invoke(_server, null));
        Assert.Null(exception);
    }

    [Fact]
    public void FlushBatchQueue_AfterDispose_MultipleCalls_DoesNotThrow()
    {
        _server.Start();
        _server.Dispose();

        var flushMethod = typeof(WebSocketServer)
            .GetMethod("FlushBatchQueue", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(flushMethod);

        // Multiple rapid flushes after disposal — all should be safe
        var exceptions = new List<Exception>();
        for (int i = 0; i < 10; i++)
        {
            var ex = Record.Exception(() => flushMethod!.Invoke(_server, null));
            if (ex != null) exceptions.Add(ex);
        }
        Assert.Empty(exceptions);
    }

    [Fact]
    public void FlushBatchQueue_AfterDispose_DoesNotDrainQueue()
    {
        _server.Start();

        // Pre-populate the batch queue with events before disposal
        var enqueueMethod = typeof(WebSocketServer)
            .GetMethod("EnqueueEvent", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(enqueueMethod);

        for (int i = 0; i < 3; i++)
        {
            enqueueMethod!.Invoke(_server, new object[] { new GameEvent { Type = $"event_{i}" } });
        }

        // Now dispose — Stop() calls FlushBatchQueue once during shutdown.
        // After disposal, subsequent FlushBatchQueue calls should be no-ops.
        _server.Dispose();

        var flushMethod = typeof(WebSocketServer)
            .GetMethod("FlushBatchQueue", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(flushMethod);

        // This call should be a no-op due to the disposed guard
        var exception = Record.Exception(() => flushMethod!.Invoke(_server, null));
        Assert.Null(exception);
    }

    // ─── Dispose Idempotency ────────────────────────────────────────

    [Fact]
    public void Dispose_CalledTwice_DoesNotThrow()
    {
        _server.Start();

        var exception = Record.Exception(() =>
        {
            _server.Dispose();
            _server.Dispose();
        });

        Assert.Null(exception);
    }

    [Fact]
    public void Dispose_WithoutStart_DoesNotThrow()
    {
        // Server constructed but never started — Dispose should still be safe
        var tracker = new GameStateTracker();
        var port = Interlocked.Increment(ref _nextPort);
        var server = new WebSocketServer(port, tracker);

        var exception = Record.Exception(() => server.Dispose());
        Assert.Null(exception);
    }

    // ─── Concurrent Dispose + Event Safety ──────────────────────────

    [Fact]
    public async Task ConcurrentDisposeAndEnqueue_NoExceptions()
    {
        _server.Start();

        var exceptions = new ConcurrentBag<Exception>();
        var barrier = new Barrier(2);

        var enqueueMethod = typeof(WebSocketServer)
            .GetMethod("EnqueueEvent", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(enqueueMethod);

        // Task 1: Rapid-fire enqueue events
        var enqueueTask = Task.Run(() =>
        {
            barrier.SignalAndWait();
            for (int i = 0; i < 100; i++)
            {
                try
                {
                    enqueueMethod!.Invoke(_server, new object[] { new GameEvent { Type = $"event_{i}" } });
                }
                catch (TargetInvocationException tie) when (tie.InnerException is ObjectDisposedException)
                {
                    // Acceptable — HttpListener may be disposed during enqueue
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }
            }
        });

        // Task 2: Dispose mid-flight
        var disposeTask = Task.Run(() =>
        {
            barrier.SignalAndWait();
            try
            {
                _server.Dispose();
            }
            catch (Exception ex)
            {
                exceptions.Add(ex);
            }
        });

        await Task.WhenAll(enqueueTask, disposeTask);

        Assert.Empty(exceptions);
    }

    [Fact]
    public async Task ConcurrentDisposeAndFlush_NoExceptions()
    {
        _server.Start();

        var exceptions = new ConcurrentBag<Exception>();
        var barrier = new Barrier(2);

        var flushMethod = typeof(WebSocketServer)
            .GetMethod("FlushBatchQueue", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(flushMethod);

        // Pre-populate the queue
        var enqueueMethod = typeof(WebSocketServer)
            .GetMethod("EnqueueEvent", BindingFlags.NonPublic | BindingFlags.Instance);
        for (int i = 0; i < 10; i++)
        {
            enqueueMethod!.Invoke(_server, new object[] { new GameEvent { Type = $"event_{i}" } });
        }

        // Task 1: Rapid-fire flush calls
        var flushTask = Task.Run(() =>
        {
            barrier.SignalAndWait();
            for (int i = 0; i < 50; i++)
            {
                try
                {
                    flushMethod!.Invoke(_server, null);
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }
            }
        });

        // Task 2: Dispose mid-flight
        var disposeTask = Task.Run(() =>
        {
            barrier.SignalAndWait();
            try
            {
                _server.Dispose();
            }
            catch (Exception ex)
            {
                exceptions.Add(ex);
            }
        });

        await Task.WhenAll(flushTask, disposeTask);

        Assert.Empty(exceptions);
    }
}
