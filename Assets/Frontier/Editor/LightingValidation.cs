using UnityEditor;

namespace Frontier.Editor
{
    public static class LightingValidation
    {
        [MenuItem("Frontier/Validate experimental lighting and settings")]
        public static void Run()
        {
            LightingPreview.Render();
            SettingsAndFog();
        }
        public static void SettingsAndFog()
        {
            LocalizationPreview.Run();
            FogPreview.Run();
            ProjectBuilder.RetainRuntimeShaders();
        }
    }
}
