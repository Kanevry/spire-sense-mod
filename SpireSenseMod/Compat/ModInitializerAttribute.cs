using System;

namespace SpireSenseMod;

/// <summary>
/// Stub for the STS2 ModInitializer attribute.
/// At runtime, the game's mod loader provides the real implementation.
/// This stub allows the project to compile without game DLLs in CI.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
internal sealed class ModInitializerAttribute : Attribute
{
    public string MethodName { get; }

    public ModInitializerAttribute(string methodName)
    {
        MethodName = methodName;
    }
}
