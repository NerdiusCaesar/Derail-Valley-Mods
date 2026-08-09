using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using HarmonyLib;
using UnityEngine;
using UnityModManagerNet;
using DV.Booklets;
using DV.RenderTextureSystem.BookletRender;
using DV.Logic.Job;
using DV.ThingTypes;

namespace ShuntSplit
{
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

            // Find the field on the C class. BindingFlags say "a public, static member".
            // FieldInfo loadColorField = typeof(C).GetField(
            //     "SHUNTING_LOAD_JOB_TYPE_COLOR",
            //     BindingFlags.Public | BindingFlags.Static);

            FieldInfo unloadColorField = typeof(C).GetField(
                "SHUNTING_UNLOAD_JOB_TYPE_COLOR",
                BindingFlags.Public | BindingFlags.Static);

            // Overwrite it. First arg is null because it's static (no instance).
            // loadColorField.SetValue(null, new Color(0.847f, 0.4117f, 0.4117f)); // original red
            unloadColorField.SetValue(null, new Color(0.847f, 0.62f, 0.4117f)); // e.g. orange

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

    [HarmonyPatch(typeof(BookletCreator_JobOverview), nameof(BookletCreator_JobOverview.GetJobOverviewTemplateData))]
    static class JobOverviewTitlePatch
    {
        private static void Postfix(Job_data job, List<TemplatePaperData> __result)
        {
            if (__result == null || __result.Count == 0)
                return;

            switch (job.type) {
                case JobType.ShuntingLoad:
                    (__result[0] as FrontPageTemplatePaperData).jobType = "Shunting Load";
                    break;
                case JobType.ShuntingUnload:
                    (__result[0] as FrontPageTemplatePaperData).jobType = "Shunting Unload";
                    break;
                default: 
                    break;
            }
        }
    }
}
