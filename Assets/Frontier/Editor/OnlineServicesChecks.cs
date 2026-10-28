using System;
using System.Linq;
using System.Reflection;
using Frontier.Runtime;
using UnityEngine;

namespace Frontier.Editor
{
    public static class OnlineServicesChecks
    {
        public static void RunWithRankingPreview()
        {
            Run();
            RankingPreview.Run();
        }

        public static void Run()
        {
            CheckClientDefaults();
            PublicMatchmakingChecks.Run();
            RankingPresentationChecks.Run();
            AllocatedServerChecks.Run();
            OnlinePresentationChecks.RunRegressionChecks();
            int checks = 0;
            foreach (string endpoint in new[] { "http://127.0.0.1:7350", "http://192.168.100.157:7350", "http://10.2.3.4:7350", "http://172.16.2.3:7350", "https://game.example.com" })
            { NakamaMatchmaker.ValidateEndpoint(endpoint); checks++; }
            foreach (string endpoint in new[] { "http://8.8.8.8:7350", "http://game.example.com", "https://name:password@game.example.com", "https://game.example.com/path", "https://game.example.com/?key=secret", "file:///tmp/server", "garbage" })
            {
                bool rejected = false;
                try { NakamaMatchmaker.ValidateEndpoint(endpoint); } catch (ArgumentException) { rejected = true; }
                if (!rejected) throw new Exception("Unsafe/malformed service endpoint accepted.");
                checks++;
            }
            Debug.Log($"FRONTIER_ONLINE_SERVICES passed: {checks} endpoint checks plus public search, allocation and existing online/offline regressions; no cloud/hardware proof");
        }

        private static void CheckClientDefaults()
        {
            const string legacy="frontier.multiplayer.service.v1",current="frontier.multiplayer.service.v2";
            const string address="frontier.multiplayer.address.v1",port="frontier.multiplayer.port.v1";
            var saved=new[]{legacy,current,address}.Select(key=>(key,exists:PlayerPrefs.HasKey(key),value:PlayerPrefs.GetString(key))).ToArray();
            bool hadPort=PlayerPrefs.HasKey(port);int savedPort=PlayerPrefs.GetInt(port);
            var fixture=new GameObject("Production endpoint checks");int checks=0;
            try
            {
                var session=fixture.AddComponent<GameSession>();
                var panel=fixture.AddComponent<CommandPanel>();panel.Session=session;
                var load=typeof(CommandPanel).GetMethod("LoadOnlineEndpoint",BindingFlags.Instance|BindingFlags.NonPublic);
                Check(session.PublicBackendUrl=="https://server-us-1.littlelegion.app","New clients select the production service.");
                Check(NakamaMatchmaker.ValidateEndpoint(session.PublicBackendUrl).Port==443,"Production uses a valid HTTPS endpoint on port 443.");
                PlayerPrefs.DeleteKey(current);
                PlayerPrefs.SetString(legacy,"http://192.168.100.157:7350");
                PlayerPrefs.SetString(address,"192.168.50.2");PlayerPrefs.SetInt(port,7788);
                Load();
                Check(session.PublicBackendUrl==GameSession.DefaultPublicBackendUrl,"A legacy saved backend cannot redirect an upgraded client away from production.");
                Check(session.ServerAddress=="192.168.50.2"&&session.ServerPort==7788,"Direct LAN preferences survive the public backend change.");
                Check(PlayerPrefs.GetString(legacy)=="http://192.168.100.157:7350","The previous service preference is retained without deleting account data.");
                PlayerPrefs.SetString(current,"http://10.0.0.2:7350");Load();
                Check(session.PublicBackendUrl=="http://10.0.0.2:7350","A later explicit service selection is retained.");
                session.PublicBackendUrl="http://127.0.0.1:7350";
                Load("-frontier-backend","http://127.0.0.1:7350");
                Check(session.PublicBackendUrl=="http://127.0.0.1:7350","The development launch override wins over stored preferences.");
                PlayerPrefs.SetString(current,"invalid endpoint");session.PublicBackendUrl=GameSession.DefaultPublicBackendUrl;Load();
                Check(session.PublicBackendUrl==GameSession.DefaultPublicBackendUrl,"Invalid stored preferences leave the production default intact.");
                session.ServerAddress="10.0.0.8";session.ServerPort=7799;Load("-frontier-client");
                Check(session.ServerAddress=="10.0.0.8"&&session.ServerPort==7799,"The direct-client launch override still wins over LAN preferences.");
                Debug.Log($"FRONTIER_PRODUCTION_ENDPOINT passed: {checks} default, preference migration and launch override checks; no network requests");
                void Load(params string[] args)=>load.Invoke(panel,new object[]{args});
                void Check(bool valid,string message){if(!valid)throw new Exception(message);checks++;}
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(fixture);
                foreach(var entry in saved)
                    if(entry.exists)PlayerPrefs.SetString(entry.key,entry.value);else PlayerPrefs.DeleteKey(entry.key);
                if(hadPort)PlayerPrefs.SetInt(port,savedPort);else PlayerPrefs.DeleteKey(port);
                PlayerPrefs.Save();
            }
        }
    }
}
