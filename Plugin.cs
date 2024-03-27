using System;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using JetBrains.Annotations;
using UnityEngine;

namespace Sleepover
{
    [BepInPlugin(ModGUID, ModName, ModVersion)]
    public class SleepoverPlugin : BaseUnityPlugin
    {
        internal const string ModName = "Sleepover";
        internal const string ModVersion = "1.1.2";
        internal const string Author = "Azumatt";
        private const string ModGUID = $"{Author}.{ModName}";
        private static string ConfigFileName = $"{ModGUID}.cfg";
        private static string ConfigFileFullPath = Paths.ConfigPath + Path.DirectorySeparatorChar + ConfigFileName;
        internal static string ConnectionError = "";
        private readonly Harmony _harmony = new(ModGUID);
        public static readonly ManualLogSource SleepoverLogger = BepInEx.Logging.Logger.CreateLogSource(ModName);

        public enum Toggle
        {
            On = 1,
            Off = 0
        }

        public void Awake()
        {
            ModEnabled = config("1 - General", "Enabled", Toggle.On, "Enable this mod");
            EnableMultipleBedfellows = config("1 - General", "Multiple sleepers", Toggle.On, "Allow multiple people to use this bed simultaneously. Not tested on public servers.");
            SleepAnyTime = config("1 - General", "Ignore time restrictions", Toggle.On, "Sleep at any time of day, not just at night.");
            IgnoreExposure = config("1 - General", "Ignore exposure restrictions", Toggle.On, "Ignore restrictions for walls and a roof. Sleep under a starry sky.");
            IgnoreEnemies = config("1 - General", "Ignore nearby enemies", Toggle.On, "Enemies no longer prevent you from sleeping.");
            IgnoreFire = config("1 - General", "Ignore fire requirement", Toggle.On, "Sleep without a nearby fire.");
            IgnoreWet = config("1 - General", "Ignore wet restrictions", Toggle.On, "Sleep while wet.");
            SleepWithoutClaiming = config("1 - General", "Do not automatically claim beds", Toggle.On, "Sleep without claiming a bed first.");
            SleepWithoutSpawnpoint = config("1 - General", "Do not set spawnpoint", Toggle.On, "Sleep without setting a spawnpoint first.");
            MultipleSpawnpointsPerBed = config("1 - General", "Multiple spawnpoints per bed", Toggle.On, "Any number of players can use the same bed as a spawnpoint.");
            if (ModEnabled.Value == SleepoverPlugin.Toggle.Off)
            {
                return;
            }

            Assembly assembly = Assembly.GetExecutingAssembly();
            _harmony.PatchAll(assembly);
            SetupWatcher();
        }

        private void OnDestroy()
        {
            Config.Save();
        }

        private void SetupWatcher()
        {
            FileSystemWatcher watcher = new(Paths.ConfigPath, ConfigFileName);
            watcher.Changed += ReadConfigValues;
            watcher.Created += ReadConfigValues;
            watcher.Renamed += ReadConfigValues;
            watcher.IncludeSubdirectories = true;
            watcher.SynchronizingObject = ThreadingHelper.SynchronizingObject;
            watcher.EnableRaisingEvents = true;
        }

        private void ReadConfigValues(object sender, FileSystemEventArgs e)
        {
            if (!File.Exists(ConfigFileFullPath)) return;
            try
            {
                SleepoverLogger.LogDebug("ReadConfigValues called");
                Config.Reload();
            }
            catch
            {
                SleepoverLogger.LogError($"There was an issue loading your {ConfigFileName}");
                SleepoverLogger.LogError("Please check your config entries for spelling and format!");
            }
        }


        #region ConfigOptions

        /*private static ConfigEntry<Toggle> _serverConfigLocked = null!;*/
        public static ConfigEntry<Toggle> ModEnabled = null!;
        public static ConfigEntry<Toggle> EnableMultipleBedfellows = null!;
        public static ConfigEntry<Toggle> SleepAnyTime = null!;
        public static ConfigEntry<Toggle> IgnoreExposure = null!;
        public static ConfigEntry<Toggle> IgnoreEnemies = null!;
        public static ConfigEntry<Toggle> IgnoreFire = null!;
        public static ConfigEntry<Toggle> IgnoreWet = null!;
        public static ConfigEntry<Toggle> SleepWithoutSpawnpoint = null!;
        public static ConfigEntry<Toggle> MultipleSpawnpointsPerBed = null!;
        public static ConfigEntry<Toggle> SleepWithoutClaiming = null!;

        private ConfigEntry<T> config<T>(string group, string name, T value, ConfigDescription description)
        {
            ConfigEntry<T> configEntry = Config.Bind(group, name, value, description);
            return configEntry;
        }

        private ConfigEntry<T> config<T>(string group, string name, T value, string description)
        {
            return config(group, name, value, new ConfigDescription(description));
        }

        private class ConfigurationManagerAttributes
        {
            [UsedImplicitly] public int? Order = null!;
            [UsedImplicitly] public bool? Browsable = null!;
            [UsedImplicitly] public string? Category = null!;
            [UsedImplicitly] public Action<ConfigEntryBase>? CustomDrawer = null!;
        }

        class AcceptableShortcuts : AcceptableValueBase
        {
            public AcceptableShortcuts() : base(typeof(KeyboardShortcut))
            {
            }

            public override object Clamp(object value) => value;
            public override bool IsValid(object value) => true;

            public override string ToDescriptionString() => $"# Acceptable values: {string.Join(", ", UnityInput.Current.SupportedKeyCodes)}";
        }

        #endregion
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
}