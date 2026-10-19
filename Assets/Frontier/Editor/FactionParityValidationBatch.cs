using UnityEditor;
using UnityEngine;

namespace Frontier.Editor
{
    public static class FactionParityValidationBatch
    {
        [MenuItem("Frontier/Check faction parity UI")]
        public static void Run()
        {
            QueuedInputChecks.Run();
            ActionHelpPreview.Render();
            Debug.Log("FRONTIER_FACTION_PARITY_UI passed; input, help, production, action wheel and menus validated; no player build");
        }
    }
}
