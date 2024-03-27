using System.Linq;
using BepInEx.Configuration;
using UnityEngine;

namespace Sleepover;

public class Util
{
    public const string SleepHover = "[<color=yellow><b>$KEY_Use</b></color>] $piece_bed_sleep\n";
    public const string ClaimHover = "[<color=yellow><b>shift + $KEY_Use</b></color>] $piece_bed_claim\n";
    public const string SetSpawnHover = "[<color=yellow><b>alt + $KEY_Use</b></color>] $piece_bed_setspawn\n";

    public static bool MayClaim(Bed bed)
    {
        return bed.GetOwnerName() == "";
    }

    public static bool MaySleep(Bed bed, string ownerName)
    {
        return (bed.IsMine() && bed.IsCurrent()) || // Default - it's my claimed spawn bed
               ((!bed.IsMine() && ownerName != "" && SleepoverPlugin.EnableMultipleBedfellows.Value == SleepoverPlugin.Toggle.On)) || // Many sleepers
               (ownerName == "" && SleepoverPlugin.SleepWithoutClaiming.Value == SleepoverPlugin.Toggle.On) || // Ignore claim rules
               (!bed.IsCurrent() && SleepoverPlugin.SleepWithoutSpawnpoint.Value == SleepoverPlugin.Toggle.On);
    }

    public static bool MaySetSpawn(Bed bed)
    {
        return (!bed.IsCurrent() && bed.IsMine()) || // Default - it's my bed, but not currently spawn
               (!bed.IsCurrent() && (!bed.IsMine() && SleepoverPlugin.MultipleSpawnpointsPerBed.Value == SleepoverPlugin.Toggle.On)); // Allow multiple spawns
    }
}

public static class KeyboardExtensions
{
    public static bool IsKeyDown(this KeyboardShortcut shortcut)
    {
        return shortcut.MainKey != KeyCode.None && Input.GetKeyDown(shortcut.MainKey) && shortcut.Modifiers.All(Input.GetKey);
    }

    public static bool IsKeyHeld(this KeyboardShortcut shortcut)
    {
        return shortcut.MainKey != KeyCode.None && Input.GetKey(shortcut.MainKey) && shortcut.Modifiers.All(Input.GetKey);
    }
}