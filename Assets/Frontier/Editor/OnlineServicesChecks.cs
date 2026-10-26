using System;
using Frontier.Runtime;
using UnityEngine;

namespace Frontier.Editor
{
    public static class OnlineServicesChecks
    {
        public static void Run()
        {
            PublicMatchmakingChecks.Run();
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
    }
}
