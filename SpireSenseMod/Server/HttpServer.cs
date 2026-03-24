using System;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Godot;

namespace SpireSenseMod;

/// <summary>
/// Lightweight HTTP server exposing game state at localhost:8080.
/// Handles CORS for browser access from the SpireSense web app.
/// </summary>
public class HttpServer : IDisposable
{
    private readonly HttpListener _listener;
    private readonly GameStateTracker _stateTracker;
    private readonly CancellationTokenSource _cts = new();
    private readonly int _port;
    private bool _disposed;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    public HttpServer(int port, GameStateTracker stateTracker)
    {
        _port = port;
        _stateTracker = stateTracker;
        _listener = new HttpListener();
        _listener.Prefixes.Add($"http://localhost:{port}/");
    }

    public void Start()
    {
        _listener.Start();
        Task.Run(() => ListenLoop(_cts.Token));
    }

    public void Stop()
    {
        _cts.Cancel();
        _listener.Stop();
        _cts.Dispose();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Stop();
    }

    private async Task ListenLoop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var context = await _listener.GetContextAsync();
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await Task.Run(() => HandleRequest(context)).WaitAsync(TimeSpan.FromSeconds(5));
                    }
                    catch (TimeoutException)
                    {
                        GD.PrintErr("[SpireSense HTTP] Request timed out");
                        try { context.Response.StatusCode = 504; context.Response.Close(); } catch { }
                    }
                }, ct);
            }
            catch (HttpListenerException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                GD.PrintErr($"[SpireSense HTTP] Error: {ex}");
            }
        }
    }

    private void HandleRequest(HttpListenerContext context)
    {
        var request = context.Request;
        var response = context.Response;

        // CORS headers for browser access
        response.Headers.Add("Access-Control-Allow-Origin", "*");
        response.Headers.Add("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
        response.Headers.Add("Access-Control-Allow-Headers", "Content-Type");

        if (request.HttpMethod == "OPTIONS")
        {
            response.StatusCode = 204;
            response.Close();
            return;
        }

        try
        {
            var path = request.Url?.AbsolutePath ?? "/";

            switch (path)
            {
                case "/api/state":
                    HandleGetState(response);
                    break;
                case "/api/health":
                    HandleHealth(response);
                    break;
                case "/api/version":
                    HandleVersion(response);
                    break;
                case "/api/deck":
                    HandleGetDeck(response);
                    break;
                case "/api/combat":
                    HandleGetCombat(response);
                    break;
                case "/api/relics":
                    HandleGetRelics(response);
                    break;
                case "/api/map":
                    HandleGetMap(response);
                    break;
                case "/api/events":
                    HandleGetEvents(request, response);
                    break;
                default:
                    HandleNotFound(response);
                    break;
            }
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[SpireSense HTTP] Request error: {ex}");
            SendJson(response, new { error = "Internal server error" }, 500);
        }
    }

    private void HandleGetState(HttpListenerResponse response)
    {
        // Use pre-serialized snapshot — avoids double serialization and race conditions
        SendRawJson(response, _stateTracker.GetSerializedState());
    }

    private void HandleHealth(HttpListenerResponse response)
    {
        SendJson(response, new
        {
            status = "ok",
            mod = "SpireSense",
            version = "0.1.0",
            port = _port,
        });
    }

    private static void HandleVersion(HttpListenerResponse response)
    {
        SendJson(response, new
        {
            mod = "SpireSense",
            version = "0.1.0",
            api = "v1",
            game = "Slay the Spire 2",
        });
    }

    private void HandleGetDeck(HttpListenerResponse response)
    {
        var state = _stateTracker.GetCurrentState();
        SendJson(response, new { deck = state.Deck, count = state.Deck.Count });
    }

    private void HandleGetCombat(HttpListenerResponse response)
    {
        var state = _stateTracker.GetCurrentState();
        if (state.Combat == null)
        {
            SendJson(response, new { error = "Not in combat" }, 404);
            return;
        }
        SendJson(response, state.Combat);
    }

    private void HandleGetRelics(HttpListenerResponse response)
    {
        var state = _stateTracker.GetCurrentState();
        SendJson(response, new { relics = state.Relics, count = state.Relics.Count });
    }

    private void HandleGetMap(HttpListenerResponse response)
    {
        var state = _stateTracker.GetCurrentState();
        SendJson(response, new { map = state.Map, currentFloor = state.Floor });
    }

    private void HandleGetEvents(HttpListenerRequest request, HttpListenerResponse response)
    {
        var sinceParam = request.QueryString["since"];
        long since = 0;
        if (!string.IsNullOrEmpty(sinceParam))
        {
            long.TryParse(sinceParam, out since);
        }

        var events = _stateTracker.GetEventsSince(since);
        SendJson(response, new { events, count = events.Count });
    }

    private static void HandleNotFound(HttpListenerResponse response)
    {
        SendJson(response, new
        {
            error = "Not found",
            endpoints = new[]
            {
                "GET /api/state — Full game state",
                "GET /api/health — Server health check",
                "GET /api/version — Mod version info",
                "GET /api/deck — Current deck",
                "GET /api/combat — Current combat state",
                "GET /api/relics — Current relic collection",
                "GET /api/map — Current map state",
                "GET /api/events?since={timestamp} — Buffered events since timestamp",
                "WS /ws — WebSocket real-time events",
            },
        }, 404);
    }

    private static void SendJson(HttpListenerResponse response, object data, int statusCode = 200)
    {
        var json = JsonSerializer.Serialize(data, JsonOptions);
        SendRawJson(response, json, statusCode);
    }

    private static void SendRawJson(HttpListenerResponse response, string json, int statusCode = 200)
    {
        response.StatusCode = statusCode;
        response.ContentType = "application/json";

        var bytes = Encoding.UTF8.GetBytes(json);

        response.ContentLength64 = bytes.Length;
        using (var output = response.OutputStream)
        {
            output.Write(bytes, 0, bytes.Length);
            output.Flush();
        }
        response.Close();
    }
}
