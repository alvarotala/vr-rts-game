using System;
using Frontier.Networking;
using UnityEngine;

namespace Frontier.Dedicated
{
    // Copied into the generated server project. It deliberately has no client, XR or room setup.
    public static class ServerBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Launch()
        {
            string[] args = Environment.GetCommandLineArgs();
            if (!ushort.TryParse(Argument(args, "-frontier-port", "7777"), out ushort port) || port == 0)
            {
                Debug.LogError("Invalid -frontier-port (1..65535 required)."); Application.Quit(2); return;
            }
            // Browser clients join over TCP; 0 disables the websocket listener.
            if (!ushort.TryParse(Argument(args, "-frontier-ws-port", (port + NgoMatchServer.WebSocketPortOffset).ToString()), out ushort webSocketPort))
            {
                Debug.LogError("Invalid -frontier-ws-port (0..65535 required)."); Application.Quit(2); return;
            }
            Application.runInBackground = true;
            Application.targetFrameRate = 60;
            var server = new GameObject("Dedicated match server").AddComponent<NgoMatchServer>();
            server.EnableNetworkMetrics = Array.IndexOf(args, "-frontier-network-metrics") >= 0;
            if (Array.IndexOf(args, "-frontier-smoke") >= 0) server.SimulationSpeed = 20;
            if (Array.IndexOf(args, "-frontier-allocated") >= 0)
            {
                try { AllocatedServerAgent.Launch(server, port); }
                catch (Exception error) { Debug.LogError(error.Message); Application.Quit(2); return; }
            }
            if (!server.StartServer(port, Argument(args, "-frontier-map", "rift"), Argument(args, "-frontier-listen", "0.0.0.0"), webSocketPort))
                Application.Quit(1);
        }

        private static string Argument(string[] args, string name, string fallback)
        {
            int index = Array.IndexOf(args, name);
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : fallback;
        }
    }
}
