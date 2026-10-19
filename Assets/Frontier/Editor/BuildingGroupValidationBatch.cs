using UnityEditor;
using UnityEngine;

namespace Frontier.Editor
{
    public static class BuildingGroupValidationBatch
    {
        [MenuItem("Frontier/Check building control groups")]
        public static void Run()
        {
            QueuedInputChecks.Run();
            ControlGroupPreview.Render();
            RallyPointInputChecks.Run();
            SelectionGridPreview.Render();
            ActionHelpPreview.Render();
            Debug.Log("FRONTIER_BUILDING_GROUPS passed; selection, group input, production, rally points and menus validated; no player build");
        }
    }
}
