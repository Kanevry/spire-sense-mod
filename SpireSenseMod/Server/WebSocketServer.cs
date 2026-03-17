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
/// Clients connect at ws://localhost:8080/ws to receive live updates.
/// </summary>
public class WebSocketServer
{
    private readonly HttpListener _listener;
    private readonly GameStateTracker _stateTracker;
    private readonly CancellationTokenSource _cts = new();
    private readonly ConcurrentDictionary<string, WebSocket> _clients = new();

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
        _listener.Stop();
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
                    await SendToClient(wsContext.WebSocket, stateEvent);

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
                // We don't process incoming messages — this is a broadcast-only server
            }
        }
        catch (WebSocketException)
        {
            // Client disconnected
        }
        finally
        {
            _clients.TryRemove(clientId, out _);
            GD.Print($"[SpireSense WS] Client disconnected: {clientId}");
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
                    if (ws.State == WebSocketState.Open)
                    {
                        await SendToClient(ws, gameEvent);
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
            }
        });
    }

    private static async Task SendToClient(WebSocket ws, GameEvent gameEvent)
    {
        var json = JsonSerializer.Serialize(gameEvent, JsonOptions);
        var bytes = Encoding.UTF8.GetBytes(json);
        await ws.SendAsync(
            new ArraySegment<byte>(bytes),
            WebSocketMessageType.Text,
            endOfMessage: true,
            CancellationToken.None
        );
    }
}
