using Frontier.Runtime;
using UnityEditor;
using UnityEngine;

namespace Frontier.Editor
{
    public static class MapUpdateValidation
    {
        [MenuItem("Frontier/Validate themed maps and soundtracks")]
        public static void Run()
        {
            bool had=PlayerPrefs.HasKey(Localization.PreferenceKey);
            string saved=PlayerPrefs.GetString(Localization.PreferenceKey);
            try
            {
                Localization.SetPreference("en");
                ProjectBuilder.ValidateContent();
                MapThemePreview.Render();
                MapSelectionPreview.Run();
                MapAudioPreview.Run();
                FogPreview.Run();
                MenuFlowPreview.Render();
                Debug.Log("FRONTIER_MAP_UPDATE passed: themes, selector, music, fog and menu flow; no player build");
            }
            finally
            {
                if(had)PlayerPrefs.SetString(Localization.PreferenceKey,saved);else PlayerPrefs.DeleteKey(Localization.PreferenceKey);
                PlayerPrefs.Save();Localization.ReloadPreference();
            }
        }
    }
}
