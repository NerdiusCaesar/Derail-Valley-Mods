using UnityModManagerNet;

namespace AutoTrashToss
{
    public class Settings : UnityModManager.ModSettings, IDrawable
    {
        [Draw("Highlight the bin when you can toss")]
        public bool enableHighlight = true;

        [Draw("Use loco-style glow", Tooltip = "On: the soft blue glow used by locomotives. Off: a white outline. Falls back to the outline if no glow source is found.")]
        public bool useLocoGlow = true;

        [Draw("Glow strength", Min = 1.0, Max = 10.0, Precision = 1, Tooltip = "How bright the loco-style glow is. Higher = more obvious.")]
        public float glowIntensity = 1.5f;

        [Draw("Show on-screen prompt", Tooltip = "Show a \"throw away\" hint, like the \"enter vehicle\" prompt on locomotives.")]
        public bool showPrompt = true;

        [Draw("Allow throwing away job booklets", Tooltip = "The vanilla bin refuses job booklets; enable this to trash them anyway (removes the paper, keeps the job).")]
        public bool allowJobBooklets = true;

        [Draw("Allow throwing away licenses", Tooltip = "The vanilla bin refuses licenses; enable this to trash them (removes the paper item, keeps the license ownership).")]
        public bool allowLicenses = true;

        [Draw("Toss with Left Mouse Button")]
        public bool useLeftClick = true;

        [Draw("Alternate toss key (optional)")]
        public KeyBinding altKey = new KeyBinding();

        [Draw("Max reach (metres)", Min = 1.0, Max = 6.0, Precision = 1)]
        public float maxDistance = 3f;

        public override void Save(UnityModManager.ModEntry modEntry) => Save(this, modEntry);

        public void OnChange() { }
    }
}
