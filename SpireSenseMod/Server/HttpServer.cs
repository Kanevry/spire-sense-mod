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
public class HttpServer
{
    private readonly HttpListener _listener;
    private readonly GameStateTracker _stateTracker;
    private readonly CancellationTokenSource _cts = new();
    private readonly int _port;

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
        var state = _stateTracker.GetCurrentState();
        SendJson(response, state);
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
                "WS /ws — WebSocket real-time events",
            },
        }, 404);
    }

    private static void SendJson(HttpListenerResponse response, object data, int statusCode = 200)
    {
        response.StatusCode = statusCode;
        response.ContentType = "application/json";

        var json = JsonSerializer.Serialize(data, JsonOptions);
        var bytes = Encoding.UTF8.GetBytes(json);

        response.ContentLength64 = bytes.Length;
        response.OutputStream.Write(bytes, 0, bytes.Length);
        response.OutputStream.Flush();
        response.Close();
    }
}
