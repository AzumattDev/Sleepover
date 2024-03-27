using HarmonyLib;
using UnityEngine;

namespace Sleepover;

[HarmonyPatch(typeof(Game), nameof(Game.UpdateSleeping))]
static class GameUpdateSleepingPatch
{
    static bool Prefix(ref Game __instance)
    {
        if (SleepoverPlugin.ModEnabled.Value == SleepoverPlugin.Toggle.Off)
        {
            return true;
        }

        // Only patch game code if we are worrying about sleep time
        if (SleepoverPlugin.SleepAnyTime.Value == SleepoverPlugin.Toggle.Off)
        {
            return true;
        }

        if (!ZNet.instance.IsServer())
        {
            return false;
        }

        if (__instance.m_sleeping)
        {
            if (!EnvMan.instance.IsTimeSkipping())
            {
                __instance.m_sleeping = false;
                ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, "SleepStop");
                return false;
            }
        }
        else if (
            !EnvMan.instance.IsTimeSkipping() &&
            ((EnvMan.instance.IsAfternoon() || EnvMan.instance.IsNight()) || SleepoverPlugin.SleepAnyTime.Value == SleepoverPlugin.Toggle.On) &&
            __instance.EverybodyIsTryingToSleep()
        )
        {
            EnvMan.instance.SkipToMorning();
            __instance.m_sleeping = true;
            ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, "SleepStart");
        }

        return false;
    }
}

[HarmonyPatch(typeof(Bed), nameof(Bed.GetHoverText))]
static class BedGetHoverTextPatch
{
    static bool Prefix(Bed __instance, ref string __result)
    {
        if (SleepoverPlugin.ModEnabled.Value == SleepoverPlugin.Toggle.Off)
        {
            return true;
        }

        string sleepHover = "[<color=yellow><b>$KEY_Use</b></color>] $piece_bed_sleep\n";
        string claimHover = "[<color=yellow><b>shift + $KEY_Use</b></color>] $piece_bed_claim\n";
        string setSpawnHover = "[<color=yellow><b>alt + $KEY_Use</b></color>] $piece_bed_setspawn\n";
        bool maySleep;
        bool mayClaim;
        bool maySetSpawn;
        string ownerName = __instance.GetOwnerName();
        string ownerText = ownerName + "'s $piece_bed\n";

        // Sleep rules
        maySleep = (
            (__instance.IsMine() && __instance.IsCurrent()) || // Default - it's my claimed spawn bed
            ((!__instance.IsMine() && ownerName != "" && SleepoverPlugin.EnableMultipleBedfellows.Value == SleepoverPlugin.Toggle.On)) || // Many sleepers
            (ownerName == "" && SleepoverPlugin.SleepWithoutClaiming.Value == SleepoverPlugin.Toggle.On) || // Ignore claim rules
            (!__instance.IsCurrent() && SleepoverPlugin.SleepWithoutSpawnpoint.Value == SleepoverPlugin.Toggle.On) // Ignore spawn rules
        );

        // Claim rules
        mayClaim = (
            (ownerName == "")
        );

        // Set spawn rules
        maySetSpawn = (
            (!__instance.IsCurrent() && __instance.IsMine()) || // Default - it's my bed, but not currently spawn
            (!__instance.IsCurrent() && (!__instance.IsMine() && SleepoverPlugin.MultipleSpawnpointsPerBed.Value == SleepoverPlugin.Toggle.On)) // Allow multiple spawns
        );

        __result = ownerName != "" ? ownerText : "$piece_bed_unclaimed\n";

        if (maySleep)
        {
            __result += sleepHover;
        }

        if (mayClaim)
        {
            __result += claimHover;
        }

        if (maySetSpawn)
        {
            __result += setSpawnHover;
        }

        __result = Localization.instance.Localize(__result);
        return false;
    }
}

[HarmonyPatch(typeof(Bed), nameof(Bed.Interact))]
static class BedInteractPatch
{
    [HarmonyPriority(Priority.VeryHigh)]
    static bool Prefix(Bed __instance, ref bool __result, ref Humanoid human, ref bool repeat)
    {
        if (SleepoverPlugin.ModEnabled.Value == SleepoverPlugin.Toggle.Off)
        {
            return true;
        }

        if (repeat)
        {
            return false;
        }

        Player thePlayer = human as Player;
        long playerID = Game.instance.GetPlayerProfile().GetPlayerID();
        bool isClaimIntent = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        bool isSetSpawnIntent = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
        bool isSleepIntent = !isClaimIntent && !isSetSpawnIntent;

        bool maySleep;
        bool mayClaim;
        bool maySetSpawn;
        string ownerName = __instance.GetOwnerName();

        // Sleep rules
        maySleep = (
            (__instance.IsMine() && __instance.IsCurrent()) || // Default - it's my claimed spawn bed
            ((!__instance.IsMine() && ownerName != "" && SleepoverPlugin.EnableMultipleBedfellows.Value == SleepoverPlugin.Toggle.On)) || // Many sleepers
            (ownerName == "" && SleepoverPlugin.SleepWithoutClaiming.Value == SleepoverPlugin.Toggle.On) || // Ignore claim rules
            (!__instance.IsCurrent() && SleepoverPlugin.SleepWithoutSpawnpoint.Value == SleepoverPlugin.Toggle.On) // Ignore spawn rules
        );

        // Claim rules
        mayClaim = (
            (ownerName == "")
        );

        // Set spawn rules
        maySetSpawn = (
            (!__instance.IsCurrent() && __instance.IsMine()) || // Default - it's my bed, but not currently spawn
            (!__instance.IsCurrent() && (!__instance.IsMine() && SleepoverPlugin.MultipleSpawnpointsPerBed.Value == SleepoverPlugin.Toggle.On)) // Allow multiple spawns
        );

        if (isClaimIntent && mayClaim)
        {
            if (!__instance.CheckExposure(thePlayer))
            {
                __result = false;
                return false;
            }

            __instance.SetOwner(playerID, Game.instance.GetPlayerProfile().GetName());
        }

        if (isSetSpawnIntent && maySetSpawn)
        {
            if (!__instance.CheckExposure(thePlayer))
            {
                __result = false;
                return false;
            }

            // My bed, not current spawnpoint. Normal behaviour
            Game.instance.GetPlayerProfile().SetCustomSpawnPoint(__instance.GetSpawnPoint());
            human.Message(MessageHud.MessageType.Center, "$msg_spawnpointset", 0, null);
            return false;
        }

        // Triggering "sleep" hover actions
        if (isSleepIntent && maySleep)
        {
            if (SleepoverPlugin.SleepAnyTime.Value == SleepoverPlugin.Toggle.Off && !EnvMan.instance.IsAfternoon() && !EnvMan.instance.IsNight())
            {
                human.Message(MessageHud.MessageType.Center, "$msg_cantsleep", 0, null);
                __result = false;
                return false;
            }

            if (!__instance.CheckEnemies(thePlayer))
            {
                __result = false;
                return false;
            }

            if (!__instance.CheckExposure(thePlayer))
            {
                __result = false;
                return false;
            }

            if (!__instance.CheckFire(thePlayer))
            {
                __result = false;
                return false;
            }

            if (!__instance.CheckWet(thePlayer))
            {
                __result = false;
                return false;
            }

            human.AttachStart(__instance.m_spawnPoint, human.gameObject, true, true, false, "attach_bed", new Vector3(0f, 0.5f, 0f));
        }

        __result = false;
        return false;
    }
}

[HarmonyPatch(typeof(Bed), nameof(Bed.IsCurrent))]
static class BedIsCurrentPatch
{
    static bool Prefix(Bed __instance, ref bool __result)
    {
        if (!__instance.IsMine() && SleepoverPlugin.MultipleSpawnpointsPerBed.Value == SleepoverPlugin.Toggle.Off)
        {
            __result = false;
            return false;
        }

        __result = Vector3.Distance(__instance.GetSpawnPoint(), Game.instance.GetPlayerProfile().GetCustomSpawnPoint()) < 1f;
        return false;
    }
}

[HarmonyPatch(typeof(Bed), nameof(Bed.CheckExposure))]
static class BedCheckExposurePatch
{
    static bool Prefix(ref bool __result)
    {
        if (SleepoverPlugin.IgnoreExposure.Value == SleepoverPlugin.Toggle.On)
        {
            __result = true;
            return false;
        }

        return true;
    }
}

[HarmonyPatch(typeof(Bed), nameof(Bed.CheckEnemies))]
static class BedCheckEnemiesPatch
{
    static bool Prefix(ref bool __result)
    {
        if (SleepoverPlugin.IgnoreEnemies.Value == SleepoverPlugin.Toggle.On)
        {
            __result = true;
            return false;
        }

        return true;
    }
}

[HarmonyPatch(typeof(Bed), nameof(Bed.CheckFire))]
static class BedCheckFirePatch
{
    static bool Prefix(ref bool __result)
    {
        if (SleepoverPlugin.IgnoreFire.Value == SleepoverPlugin.Toggle.On)
        {
            __result = true;
            return false;
        }

        return true;
    }
}

[HarmonyPatch(typeof(Bed), nameof(Bed.CheckWet))]
static class BedCheckWetPatch
{
    static bool Prefix(ref bool __result)
    {
        if (SleepoverPlugin.IgnoreWet.Value == SleepoverPlugin.Toggle.On)
        {
            __result = true;
            return false;
        }

        return true;
    }
}