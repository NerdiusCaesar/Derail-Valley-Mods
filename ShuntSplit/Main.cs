using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;

using UnityEngine;
using UnityEngine.UI;
using UnityModManagerNet;

using HarmonyLib;

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
            FieldInfo unloadColorField = typeof(C).GetField(
                "SHUNTING_UNLOAD_JOB_TYPE_COLOR",
                BindingFlags.Public | BindingFlags.Static);
            // Overwrite it. First arg is null because it's static (no instance).
            unloadColorField.SetValue(null, new Color(0.847f, 0.62f, 0.4117f)); // e.g. orange

            // Leaving load color alone, but leaving this here just in case
            // FieldInfo loadColorField = typeof(C).GetField(
            //     "SHUNTING_LOAD_JOB_TYPE_COLOR",
            //     BindingFlags.Public | BindingFlags.Static);
            // loadColorField.SetValue(null, new Color(0.847f, 0.4117f, 0.4117f)); // original red

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

    // Harmony Patch to modify the stripe
    // [HarmonyPatch(typeof(FrontPageTemplatePaper), nameof(FrontPageTemplatePaper.FillInData))]
    // static class FrontPageStripePatch
    // {
    //     private static void Postfix(FrontPageTemplatePaper __instance)
    //     {
    //         // GOTCHA 1: FillInData bails early when data == null, but a postfix ALWAYS
    //         // runs afterward regardless. So guard, or the next line throws.
    //         if (__instance.data == null) return;

    //         // GOTCHA 2: this runs for EVERY front page. Pick the stripe by which shunt
    //         // job it is, and bail for anything that isn't ours. Comparing the color
    //         // (rather than the title string) keeps this decoupled from the title patch.
    //         Color bg = __instance.data.jobTypeColor;
    //         Color stripeColor;
    //         if (bg == C.SHUNTING_LOAD_JOB_TYPE_COLOR)        stripeColor = C.HAUL_JOB_TYPE_COLOR;       // green
    //         else if (bg == C.SHUNTING_UNLOAD_JOB_TYPE_COLOR) stripeColor = C.EMPTY_HAUL_JOB_TYPE_COLOR; // yellow
    //         else return;

    //         // Build the stripe as a child of the banner image.
    //         var stripe = new GameObject("ShuntSplit.Stripe", typeof(RectTransform), typeof(Image));
    //         var rt = stripe.GetComponent<RectTransform>();
    //         rt.SetParent(__instance.jobTypeBgColor.rectTransform, worldPositionStays: false);

    //         // Anchor to a band across the bottom. anchorMax.y is the stripe's height as a
    //         // fraction of the banner (tune this). offsets zero => it exactly fills the band,
    //         // full width, and scales with the banner automatically.
    //         rt.anchorMin = new Vector2(0f, 0f);
    //         rt.anchorMax = new Vector2(1f, 0.28f);   // <-- tune the 0.18
    //         rt.offsetMin = Vector2.zero;
    //         rt.offsetMax = Vector2.zero;

    //         stripe.GetComponent<Image>().color = stripeColor;

    //         // GOTCHA 3 (draw order): a child renders on top of its parent's fill, which is
    //         // what we want over the red. If you find it clipping the title's descenders,
    //         // rt.SetAsFirstSibling() pushes it behind sibling elements while staying above
    //         // the banner fill.

    //         // GOTCHA 4 (cleanup): register it in the protected dynamicallyCreatedObjects list
    //         // so CleanUp() destroys it — otherwise reused paper objects stack duplicate stripes.
    //         Traverse.Create(__instance)
    //                 .Field("dynamicallyCreatedObjects")
    //                 .GetValue<List<GameObject>>()
    //                 .Add(stripe);
    //     }
    // }
}
