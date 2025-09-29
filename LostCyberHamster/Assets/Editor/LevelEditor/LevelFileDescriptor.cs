using System;
using Assets.Scripts.Common.Models;

/// <summary>
/// Immutable description of a level json file, keeping both file system information
/// and semantic metadata such as the part of day it belongs to.
/// </summary>
public readonly struct LevelFileDescriptor : IEquatable<LevelFileDescriptor>
{
    public LevelFileDescriptor(
        string absolutePath,
        string relativePath,
        PartOfDayEnum? partOfDay,
        string displayName)
    {
        AbsolutePath = absolutePath ?? throw new ArgumentNullException(nameof(absolutePath));
        RelativePath = relativePath ?? throw new ArgumentNullException(nameof(relativePath));
        PartOfDay = partOfDay;
        DisplayName = string.IsNullOrWhiteSpace(displayName)
            ? System.IO.Path.GetFileNameWithoutExtension(relativePath)
            : displayName;
    }

    /// <summary>
    /// Full path on disk to the level json file.
    /// </summary>
    public string AbsolutePath { get; }

    /// <summary>
    /// Path relative to the location's <c>levels</c> folder.
    /// </summary>
    public string RelativePath { get; }

    /// <summary>
    /// Optional semantic label used for UI display.
    /// </summary>
    public string DisplayName { get; }

    /// <summary>
    /// Time-of-day classification if the file belongs to a daypart folder.
    /// </summary>
    public PartOfDayEnum? PartOfDay { get; }

    public bool TryGetPartOfDay(out PartOfDayEnum result)
    {
        if (PartOfDay.HasValue)
        {
            result = PartOfDay.Value;
            return true;
        }

        result = default;
        return false;
    }

    public override string ToString() => DisplayName;

    public bool Equals(LevelFileDescriptor other)
    {
        return string.Equals(AbsolutePath, other.AbsolutePath, StringComparison.OrdinalIgnoreCase);
    }

    public override bool Equals(object obj)
    {
        return obj is LevelFileDescriptor other && Equals(other);
    }

    public override int GetHashCode()
    {
        return AbsolutePath != null
            ? StringComparer.OrdinalIgnoreCase.GetHashCode(AbsolutePath)
            : 0;
    }
}
