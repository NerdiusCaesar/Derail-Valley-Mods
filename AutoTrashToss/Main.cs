using UnityEngine;
using UnityModManagerNet;

namespace AutoTrashToss
{
    // Entry point referenced by Info.json: "AutoTrashToss.Main.Load"
    // [EnableReloading] makes UMM show a "Reload" button so a fresh build can be
    // loaded in-game (Ctrl+F10 -> Mods -> Reload) without restarting the game.
    [EnableReloading]
    public static class Main
    {
        public static UnityModManager.ModEntry ModEntry { get; private set; }
        public static Settings Settings { get; private set; }

        // The GameObject that hosts our per-frame TrashTosser logic.
        private static GameObject runner;

        public static bool Load(UnityModManager.ModEntry modEntry)
        {
            ModEntry = modEntry;
            Settings = UnityModManager.ModSettings.Load<Settings>(modEntry);

            modEntry.OnGUI = (e) => Settings.Draw(e);
            modEntry.OnSaveGUI = (e) => Settings.Save(e);
            modEntry.OnToggle = OnToggle;
            modEntry.OnUnload = OnUnload;

            return true;
        }

        // Called by UMM right before it reloads (or unloads) the mod. Clean up so the
        // fresh copy starts from scratch.
        private static bool OnUnload(UnityModManager.ModEntry modEntry)
        {
            if (runner != null)
            {
                Object.Destroy(runner);
                runner = null;
            }
            return true; // true = safe to unload/reload
        }

        private static bool OnToggle(UnityModManager.ModEntry modEntry, bool enabled)
        {
            if (enabled)
            {
                if (runner == null)
                {
                    runner = new GameObject("AutoTrashToss.Runner");
                    Object.DontDestroyOnLoad(runner);
                    runner.AddComponent<TrashTosser>();
                }
            }
            else if (runner != null)
            {
                Object.Destroy(runner);
                runner = null;
            }

            return true;
        }

        public static void Log(string msg) => ModEntry?.Logger.Log(msg);
    }
}
