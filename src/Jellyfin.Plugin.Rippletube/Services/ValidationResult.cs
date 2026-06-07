using System.Collections.Generic;

namespace Jellyfin.Plugin.Rippletube.Services;

/// <summary>
/// Configuration or dependency validation result.
/// </summary>
public sealed class ValidationResult
{
    public bool IsValid => Errors.Count == 0;

    public List<string> Errors { get; } = [];

    public List<string> Warnings { get; } = [];
}
