using System.Reflection;
using HarmonyLib;
using UnityModManagerNet;
using UnityEngine;
using System.Linq;
using System.Collections.Generic;
using DV.Logic.Job;
using DV.RenderTextureSystem.BookletRender;

namespace AnyDestTrack
{
    // The name of this class + method must match "EntryMethod" in Info.json:
    //   "AnyDestTrack.Main.Load"
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

    [HarmonyPatch(typeof(TransportTask), nameof(TransportTask.UpdateTaskState))]
    static class YardDestinationPatch
    {
        // FieldRef lets us read AND write the private fields; cache them once.
        private static readonly AccessTools.FieldRef<TransportTask, Track> DestRef =
            AccessTools.FieldRefAccess<TransportTask, Track>("destinationTrack");
        private static readonly AccessTools.FieldRef<TransportTask, List<Car>> CarsRef =
            AccessTools.FieldRefAccess<TransportTask, List<Car>>("cars");
        private static readonly HashSet<string> DestTrackTypes = new HashSet<string> { "I", "O", "S" }; // inbound, outbound, siding

        // __state carries the stashed real destination from Prefix to Postfix.
        private static void Prefix(TransportTask __instance, out Track __state)
        {
            __state = null;
            Track dest = DestRef(__instance);
            var cars = CarsRef(__instance);

            // break early if...
            if (dest == null) return; // No destination
            if (cars == null || cars.Count == 0) return; // No cars
            if (!DestTrackTypes.Contains(GetTrackType(dest.ID))) return; // Not accepted dest track

            // If all cars are present on the same track,
            // and that track is in the same yard as the destination track,
            // then fake the current location of the cars to be the destination track
            Track where = cars[0].CurrentTrack;                  // the track car[0] is on
            if (where == null || where == dest) return;          // null = straddling; ==dest = normal path
            if (!SameYardAndType(where, dest)) return;           // must be same yard + same type
            foreach (var c in cars)                              // ALL cars on that same track
                if (c.CurrentTrack != where) return;
            
            __state = dest;              // stash the real destination
            DestRef(__instance) = where; // redirect so the original check passes
        }

        private static void Finalizer(TransportTask __instance, Track __state)
        {
            if (__state != null) DestRef(__instance) = __state;  // always restore
        }

        private static string GetStation(TrackID trackId) => trackId.yardId; // e.g. "SM" for "Steel Mill"
        private static string GetYard(TrackID trackId) => trackId.SignIDSubYardPart; // e.g. "B" for "Yard B"
        private static string GetOrderNumber(TrackID trackId) => trackId.FullID.Split('-')[2]; // e.g. "7" for "Track 7"
        private static string GetTrackType(TrackID trackId) => trackId.FullID.Split('-')[3]; // e.g. "I" for "Inbound"
            
        private static bool SameYardAndType(Track a, Track b)
        {
            if (a == null || b == null) return false;

            return GetStation(a.ID) == GetStation(b.ID)
                && GetYard(a.ID) == GetYard(b.ID)
                && GetTrackType(a.ID) == GetTrackType(b.ID);
        }
    }

    [HarmonyPatch]
    static class TaskTrackIdPatch
    {
        // There's exactly one constructor; grab it directly.
        static MethodBase TargetMethod() => typeof(TaskTemplatePaperData).GetConstructors()[0];

        private static void Postfix(TaskTemplatePaperData __instance)
        {
            if (string.IsNullOrEmpty(__instance.trackId)) return;
            // __instance.trackId is public; strip the digits: "B3I" -> "BI"
            __instance.trackId = new string(__instance.trackId.Where(c => !char.IsDigit(c)).ToArray());
        }
    }
}