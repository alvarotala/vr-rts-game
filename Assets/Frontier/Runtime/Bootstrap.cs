using UnityEngine;

namespace Frontier.Runtime
{
    public static class Bootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Launch()
        {
            if(Object.FindFirstObjectByType<GameSession>()!=null)return;
            QualitySettings.vSyncCount=0;Application.targetFrameRate=72;
            Application.runInBackground=Application.platform!=RuntimePlatform.Android;
            var session=new GameObject("Game session").AddComponent<GameSession>();
            var board=new GameObject("Physical board").AddComponent<BoardView>();board.Session=session;
            var quest=new GameObject("Platform adapter").AddComponent<QuestBridge>();quest.Session=session;quest.Board=board;
            board.Viewer=quest.Camera;
            var panel=new GameObject("Interface").AddComponent<CommandPanel>();panel.Session=session;panel.Board=board;panel.Quest=quest;
            var wheel=new GameObject("Controller actions").AddComponent<ActionWheel>();wheel.Session=session;wheel.Panel=panel;wheel.Quest=quest;
            var input=new GameObject("Commander input").AddComponent<CommanderInput>();input.Session=session;input.Board=board;input.Panel=panel;input.Quest=quest;input.Wheel=wheel;
            session.gameObject.AddComponent<AudioFeedback>().Session=session;
            session.gameObject.AddComponent<FrameMetrics>().Session=session;
            var light=new GameObject("Light").AddComponent<Light>();light.type=LightType.Directional;light.transform.rotation=Quaternion.Euler(50,-30,0);light.intensity=.8f;light.shadows=LightShadows.None;
            RenderSettings.ambientLight=new Color(.45f,.55f,.65f);
        }
    }
}
