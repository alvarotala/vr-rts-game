using UnityEditor;
using UnityEngine;

namespace Frontier.Editor
{
    public static class UnifiedWheelValidationBatch
    {
        [MenuItem("Frontier/Check unified controller wheel")]
        public static void Run()
        {
            UnifiedWheelPreview.Render();
            HotkeyValidationBatch.Run();
            RallyPointInputChecks.Run();
            // Its attack-pointer regression also exercises all wheel commands.
            CasterAbilityPreview.Render();
            Debug.Log("FRONTIER_UNIFIED_WHEEL passed; hint, navigation, groups, commands, bindings and pointer handoffs validated; no player build");
        }
    }
}
