using System.Reflection;
using HarmonyLib;
using UnityModManagerNet;

namespace FirstDVMod
{
    // The name of this class + method must match "EntryMethod" in Info.json:
    //   "FirstDVMod.Main.Load"
    public static class Main
    {
        public static UnityModManager.ModEntry ModEntry { get; private set; }
        public static Settings Settings { get; private set; }
        private static Harmony _harmony;

        // UMM calls this once when the mod is first loaded at game startup.
        // Return true on success; returning false marks the mod as failed in UMM.
        public static bool Load(UnityModManager.ModEntry modEntry)
        {
            ModEntry = modEntry;

            // Load saved settings from Settings.xml, or defaults on first run.
            Settings = UnityModManager.ModSettings.Load<Settings>(modEntry);

            // Wire up UMM callbacks.
            modEntry.OnGUI = OnGUI;           // draw the settings panel
            modEntry.OnSaveGUI = OnSaveGUI;   // persist settings when the panel closes
            modEntry.OnToggle = OnToggle;     // handle the enable/disable checkbox

            modEntry.Logger.Log("Load() called - mod code is running!");
            return true;
        }

        // Called when the user ticks/unticks the mod's checkbox in the UMM menu
        // (and once at startup with the mod's saved enabled state).
        private static bool OnToggle(UnityModManager.ModEntry modEntry, bool enabled)
        {
            if (enabled)
            {
                // Apply every [HarmonyPatch] class in this assembly.
                _harmony = new Harmony(modEntry.Info.Id);
                _harmony.PatchAll(Assembly.GetExecutingAssembly());

                if (Settings.logGreeting)
                    modEntry.Logger.Log(Settings.greeting);

                modEntry.Logger.Log("Mod ENABLED - Harmony patches applied.");
            }
            else
            {
                // Cleanly remove our patches so the mod can be toggled off at runtime.
                _harmony?.UnpatchAll(modEntry.Info.Id);
                _harmony = null;
                modEntry.Logger.Log("Mod DISABLED - Harmony patches removed.");
            }

            return true; // report the toggle succeeded
        }

        private static void OnGUI(UnityModManager.ModEntry modEntry)
        {
            Settings.Draw(modEntry);
        }

        private static void OnSaveGUI(UnityModManager.ModEntry modEntry)
        {
            Settings.Save(modEntry);
        }
    }
}
