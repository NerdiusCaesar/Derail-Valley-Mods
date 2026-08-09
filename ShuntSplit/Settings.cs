using UnityModManagerNet;

namespace ShuntSplit
{
    // Deriving from UnityModManager.ModSettings gives us Save/Load to an XML file
    // inside the mod's folder. Implementing IDrawable lets UMM auto-render any field
    // marked with [Draw(...)] as a control in the mod's settings panel.
    public class Settings : UnityModManager.ModSettings, IDrawable
    {
        // Called by UMM when the settings panel is closed / the user hits Save.
        public override void Save(UnityModManager.ModEntry modEntry)
        {
            Save(this, modEntry);
        }

        // Called by UMM whenever a [Draw] control changes. No-op for now.
        public void OnChange() { }
    }
}
