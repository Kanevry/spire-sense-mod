using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Godot;

namespace SpireSenseMod;

/// <summary>
/// WebSocket server for real-time game event streaming.
/// Clients connect at ws://localhost:8081 to receive live updates.
/// </summary>
public class WebSocketServer
{
    private readonly HttpListener _listener;
    private readonly GameStateTracker _stateTracker;
    private readonly CancellationTokenSource _cts = new();
    private readonly ConcurrentDictionary<string, WebSocket> _clients = new();
    private readonly ConcurrentDictionary<string, int> _pendingSends = new();
    private Task? _pingTask;

    private static readonly TimeSpan PingInterval = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan SendTimeout = TimeSpan.FromSeconds(5);
    private const int MaxPendingSends = 10;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    public WebSocketServer(int port, GameStateTracker stateTracker)
    {
        _stateTracker = stateTracker;
        _listener = new HttpListener();
        _listener.Prefixes.Add($"http://localhost:{port + 1}/");

        // Subscribe to game events
        _stateTracker.OnGameEvent += BroadcastEvent;
    }

    public void Start()
    {
        _listener.Start();
        Task.Run(() => AcceptLoop(_cts.Token));
        _pingTask = Task.Run(() => PingLoop(_cts.Token));
    }

    public void Stop()
    {
        _cts.Cancel();
        _stateTracker.OnGameEvent -= BroadcastEvent;

        foreach (var client in _clients.Values)
        {
            try
            {
                client.CloseAsync(WebSocketCloseStatus.NormalClosure, "Server shutting down", CancellationToken.None)
                    .GetAwaiter().GetResult();
            }
            catch { /* ignore close errors */ }
        }
        _clients.Clear();
        _pendingSends.Clear();
        _listener.Stop();
        _cts.Dispose();
    }

    private async Task AcceptLoop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var context = await _listener.GetContextAsync();

                if (context.Request.IsWebSocketRequest)
                {
                    var wsContext = await context.AcceptWebSocketAsync(null);
                    var clientId = Guid.NewGuid().ToString();
                    _clients.TryAdd(clientId, wsContext.WebSocket);

                    GD.Print($"[SpireSense WS] Client connected: {clientId}");

                    // Send current state immediately
                    var state = _stateTracker.GetCurrentState();
                    var stateEvent = new GameEvent { Type = "state_update", Data = state };
                    await SendToClient(clientId, wsContext.WebSocket, stateEvent);

                    _ = Task.Run(() => ReceiveLoop(clientId, wsContext.WebSocket, ct), ct);
                }
                else
                {
                    context.Response.StatusCode = 400;
                    context.Response.Close();
                }
            }
            catch (HttpListenerException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                GD.PrintErr($"[SpireSense WS] Accept error: {ex.Message}");
            }
        }
    }

    private async Task ReceiveLoop(string clientId, WebSocket ws, CancellationToken ct)
    {
        var buffer = new byte[4096];

        try
        {
            while (ws.State == WebSocketState.Open && !ct.IsCancellationRequested)
            {
                var result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), ct);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Client disconnected", ct);
                    break;
                }

                // Respond to ping frames with pong (keeps connection alive)
                // Note: System.Net.WebSockets handles ping/pong at the protocol level
                // automatically, but we still need to read frames to keep the connection active.
                // No additional action needed — ReceiveAsync consumes ping frames and the
                // runtime sends pong responses automatically.
            }
        }
        catch (WebSocketException)
        {
            // Client disconnected
        }
        finally
        {
            _clients.TryRemove(clientId, out _);
            _pendingSends.TryRemove(clientId, out _);
            GD.Print($"[SpireSense WS] Client disconnected: {clientId}");
        }
    }

    /// <summary>
    /// Periodically sends ping frames to all connected clients to detect dead connections.
    /// Clients that fail to respond will be cleaned up on the next broadcast or ping cycle.
    /// </summary>
    private async Task PingLoop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(PingInterval, ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            var disconnected = new System.Collections.Generic.List<string>();

            foreach (var (clientId, ws) in _clients)
            {
                try
                {
                    if (ws.State == WebSocketState.Open)
                    {
                        // Send a small text message as an application-level ping.
                        // System.Net.WebSockets does not expose raw ping frame sending,
                        // so we use a lightweight text message that clients can ignore.
                        var heartbeatEvent = new GameEvent { Type = "heartbeat", Data = null };
                        await SendToClient(clientId, ws, heartbeatEvent);
                    }
                    else
                    {
                        disconnected.Add(clientId);
                    }
                }
                catch
                {
                    disconnected.Add(clientId);
                }
            }

            foreach (var id in disconnected)
            {
                _clients.TryRemove(id, out _);
                GD.Print($"[SpireSense WS] Dead connection removed: {id}");
            }
        }
    }

    private void BroadcastEvent(GameEvent gameEvent)
    {
        _ = Task.Run(async () =>
        {
            var disconnected = new System.Collections.Generic.List<string>();

            foreach (var (clientId, ws) in _clients)
            {
                try
                {
                    // Backpressure: skip clients with too many pending sends
                    var pending = _pendingSends.GetOrAdd(clientId, 0);
                    if (pending >= MaxPendingSends)
                    {
                        GD.Print($"[SpireSense WS] Backpressure: skipping client {clientId} ({pending} pending)");
                        continue;
                    }

                    if (ws.State == WebSocketState.Open)
                    {
                        await SendToClient(clientId, ws, gameEvent);
                    }
                    else
                    {
                        disconnected.Add(clientId);
                    }
                }
                catch (Exception ex)
                {
                    GD.PrintErr($"[SpireSense WS] Broadcast error for {clientId}: {ex.Message}");
                    disconnected.Add(clientId);
                }
            }

            foreach (var id in disconnected)
            {
                _clients.TryRemove(id, out _);
                _pendingSends.TryRemove(id, out _);
            }
        });
    }

    private async Task SendToClient(string clientId, WebSocket ws, GameEvent gameEvent)
    {
        _pendingSends.AddOrUpdate(clientId, 1, (_, v) => v + 1);
        try
        {
            var json = JsonSerializer.Serialize(gameEvent, JsonOptions);
            var bytes = Encoding.UTF8.GetBytes(json);

            using var timeoutCts = new CancellationTokenSource(SendTimeout);
            await ws.SendAsync(
                new ArraySegment<byte>(bytes),
                WebSocketMessageType.Text,
                endOfMessage: true,
                timeoutCts.Token
            );
        }
        finally
        {
            _pendingSends.AddOrUpdate(clientId, 0, (_, v) => Math.Max(0, v - 1));
        }
    }
}
