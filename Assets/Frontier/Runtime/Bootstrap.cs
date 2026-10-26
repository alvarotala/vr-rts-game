using System;
using Frontier.Networking;
using UnityEngine;

namespace Frontier.Runtime
{
    public static class Bootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Launch()
        {
            var args=Environment.GetCommandLineArgs();
            bool server=Array.IndexOf(args,"-frontier-server")>=0;
#if UNITY_SERVER
            server=true;
#endif
            string address=Argument(args,"-frontier-client","127.0.0.1");
            if(!ushort.TryParse(Argument(args,"-frontier-port","7777"),out ushort port)||port==0)
            {Debug.LogError("Invalid -frontier-port (1..65535 required).");Application.Quit(2);return;}
            bool smoke=Array.IndexOf(args,"-frontier-smoke")>=0;
            if(server)
            {
                Application.runInBackground=true;Application.targetFrameRate=60;
                var dedicated=new GameObject("Dedicated match server").AddComponent<NgoMatchServer>();
                dedicated.EnableNetworkMetrics=Array.IndexOf(args,"-frontier-network-metrics")>=0;
                if(smoke)dedicated.SimulationSpeed=20;
                if(!dedicated.StartServer(port,Argument(args,"-frontier-map","rift"),Argument(args,"-frontier-listen","0.0.0.0")))Application.Quit(1);
                return;
            }
            if(smoke)
            {MultiplayerSmokeClient.Launch(address,port,Argument(args,"-frontier-faction","industrial"));return;}
            if(UnityEngine.Object.FindFirstObjectByType<GameSession>()!=null)return;
            QualitySettings.vSyncCount=0;Application.targetFrameRate=72;
            Application.runInBackground=Application.platform!=RuntimePlatform.Android;
            var session=new GameObject("Game session").AddComponent<GameSession>();
            var board=new GameObject("Physical board").AddComponent<BoardView>();board.Session=session;
            var quest=new GameObject("Platform adapter").AddComponent<QuestBridge>();quest.Session=session;quest.Board=board;
            board.Viewer=quest.Camera;
            var panel=new GameObject("Interface").AddComponent<CommandPanel>();panel.Session=session;panel.Board=board;panel.Quest=quest;
            var wheel=new GameObject("Controller actions").AddComponent<ActionWheel>();wheel.Session=session;wheel.Panel=panel;wheel.Quest=quest;
            var input=new GameObject("Commander input").AddComponent<CommanderInput>();input.Session=session;input.Board=board;input.Panel=panel;input.Quest=quest;input.Wheel=wheel;
            var audio=session.gameObject.AddComponent<AudioFeedback>();audio.Session=session;audio.Board=board;
            session.gameObject.AddComponent<FrameMetrics>().Session=session;
            var light=new GameObject("Light").AddComponent<Light>();light.type=LightType.Directional;light.transform.rotation=Quaternion.Euler(50,-30,0);light.intensity=.8f;light.shadows=LightShadows.None;
            RenderSettings.ambientLight=new Color(.45f,.55f,.65f);
            if(Array.IndexOf(args,"-frontier-client")>=0)
            {
                session.ServerAddress=address;session.ServerPort=port;
                session.Faction=Argument(args,"-frontier-faction","industrial");
                session.StartCoroutine(ConnectAfterStart(quest));
            }
        }
        private static string Argument(string[] args,string key,string fallback)
        {int index=Array.IndexOf(args,key);return index>=0&&index+1<args.Length?args[index+1]:fallback;}
        private static System.Collections.IEnumerator ConnectAfterStart(QuestBridge quest)
        {yield return null;quest.RequestOnlineMatch();}
    }
}
