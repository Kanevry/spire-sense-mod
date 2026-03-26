namespace Godot;

/// <summary>
/// Minimal shim for Godot.GD static methods used by WebSocketServer.
/// Allows compiling Server/ source files in the test project without the Godot SDK.
/// No-op implementations — test code does not need real Godot logging.
/// </summary>
internal static class GD
{
    internal static void Print(string message) { }
    internal static void PrintErr(string message) { }
}
