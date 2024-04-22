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
            ((EnvMan.IsAfternoon() || EnvMan.IsNight()) || SleepoverPlugin.SleepAnyTime.Value == SleepoverPlugin.Toggle.On) &&
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

        string ownerName = __instance.GetOwnerName();
        string ownerText = ownerName + "'s $piece_bed\n";

        __result = ownerName != "" ? ownerText : "$piece_bed_unclaimed\n";

        if (Util.MaySleep(__instance, ownerName))
        {
            __result += Util.SleepHover;
        }

        if (Util.MayClaim(__instance))
        {
            __result += Util.ClaimHover;
        }

        if (Util.MaySetSpawn(__instance))
        {
            __result += Util.SetSpawnHover;
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

        Player? thePlayer = human as Player;
        long playerID = Game.instance.GetPlayerProfile().GetPlayerID();
        bool isClaimIntent = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        bool isSetSpawnIntent = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
        bool isSleepIntent = !isClaimIntent && !isSetSpawnIntent;

        string ownerName = __instance.GetOwnerName();

        if (isClaimIntent && Util.MayClaim(__instance))
        {
            if (!__instance.CheckExposure(thePlayer))
            {
                __result = false;
                return false;
            }

            __instance.SetOwner(playerID, Game.instance.GetPlayerProfile().GetName());
        }

        if (isSetSpawnIntent && Util.MaySetSpawn(__instance))
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
        if (isSleepIntent && Util.MaySleep(__instance, ownerName))
        {
            if (SleepoverPlugin.SleepAnyTime.Value == SleepoverPlugin.Toggle.Off && !EnvMan.IsAfternoon() && !EnvMan.IsNight())
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
        if (SleepoverPlugin.IgnoreExposure.Value != SleepoverPlugin.Toggle.On) return true;
        __result = true;
        return false;
    }
}

[HarmonyPatch(typeof(Bed), nameof(Bed.CheckEnemies))]
static class BedCheckEnemiesPatch
{
    static bool Prefix(ref bool __result)
    {
        if (SleepoverPlugin.IgnoreEnemies.Value != SleepoverPlugin.Toggle.On) return true;
        __result = true;
        return false;
    }
}

[HarmonyPatch(typeof(Bed), nameof(Bed.CheckFire))]
static class BedCheckFirePatch
{
    static bool Prefix(ref bool __result)
    {
        if (SleepoverPlugin.IgnoreFire.Value != SleepoverPlugin.Toggle.On) return true;
        __result = true;
        return false;
    }
}

[HarmonyPatch(typeof(Bed), nameof(Bed.CheckWet))]
static class BedCheckWetPatch
{
    static bool Prefix(ref bool __result)
    {
        if (SleepoverPlugin.IgnoreWet.Value != SleepoverPlugin.Toggle.On) return true;
        __result = true;
        return false;
    }
}