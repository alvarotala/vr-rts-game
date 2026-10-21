using UnityEditor;
using UnityEngine;

namespace Frontier.Editor
{
    public static class ReplayValidation
    {
        [MenuItem("Frontier/Validate replay playback and spectator controls")]
        public static void Run()
        {
            ReplayPlaybackChecks.Run();
            ReplayInputChecks.Run();
            RunPresentation();
            Debug.Log("FRONTIER_REPLAY_VALIDATION passed; no player build");
        }
        public static void RunPresentation()
        {
            ReplaySessionChecks.Run();
            SpectatorPanelChecks.Run();
            MatchHistoryPreview.Run();
            MenuFlowPreview.Render();
            Debug.Log("FRONTIER_REPLAY_PRESENTATION passed; no player build");
        }
    }
}
