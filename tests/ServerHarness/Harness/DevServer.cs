using System;
using System.Threading;
using Frontier.Core;
using Frontier.Harness.Shim;
using Frontier.Networking;
using UnityEngine;

namespace Frontier.Harness
{
    // Local development server for the web client: the real match server code with
    // its websocket listener, optionally with an AI opponent joining through the
    // NGO code path (as a Quest player would) or through a websocket.
    public static class DevServer
    {
        public static int Run(string[] args)
        {
            ushort port = ushort.Parse(Program.Option(args, "--port", "7777"));
            ushort webSocketPort = ushort.Parse(Program.Option(args, "--ws-port", (port + NgoMatchServer.WebSocketPortOffset).ToString()));
            string map = Program.Option(args, "--map", "rift");
            string listen = Program.Option(args, "--listen", "0.0.0.0");
            float speed = float.Parse(Program.Option(args, "--speed", "1"), System.Globalization.CultureInfo.InvariantCulture);
            string bot = Program.Option(args, "--bot", "none");
            string transport = Program.Option(args, "--bot-transport", "ngo");
            string level = Program.Option(args, "--bot-difficulty", "normal");
            bool allocated = Array.IndexOf(args, "--allocated") >= 0;
            // In allocated mode, an AI player can search through Nakama like a Quest and join over NGO.
            string publicBot = Program.Option(args, "--public-bot", "none");
            string backend = Environment.GetEnvironmentVariable("FRONTIER_BACKEND_URL");
            if (allocated && bot != "none") throw new ArgumentException("--bot is only available for direct servers; use --public-bot.");
            if (publicBot != "none" && !allocated) throw new ArgumentException("--public-bot requires --allocated.");
            if (bot != "none" && bot != "industrial" && bot != "biological") throw new ArgumentException("--bot must be industrial, biological or none.");
            var difficulty = level == "hard" ? AiDifficulty.Hard : level == "easy" ? AiDifficulty.Easy : AiDifficulty.Normal;

            Application.runInBackground = true;
            var server = new GameObject("Dedicated match server").AddComponent<NgoMatchServer>();
            server.SimulationSpeed = speed;
            if (allocated) AllocatedServerAgent.Launch(server, port);
            if (!server.StartServer(port, map, listen, webSocketPort)) return 1;
            Console.CancelKeyPress += (sender, e) => { e.Cancel = true; ShimRuntime.RequestQuit(0); };
            Console.WriteLine("FRONTIER_DEV_SERVER ready · websocket ws://" + (listen == "0.0.0.0" ? "127.0.0.1" : listen) + ":" + webSocketPort + "/ · map " + map
                + (bot == "none" ? "" : " · AI opponent " + bot + " (" + level + ", via " + transport + ")"));

            StrategyBot opponent = null;
            double rejoinAt = 0;
            System.Threading.Tasks.Task<NakamaBot.Connection> search = null;
            while (ShimRuntime.Tick())
            {
                if (publicBot != "none")
                {
                    if (opponent != null && (opponent.Passed || opponent.Failed))
                    {
                        Console.WriteLine("FRONTIER_DEV_SERVER public AI finished: " + (opponent.Passed ? "winner=" + opponent.Winner : opponent.FailReason));
                        opponent.Link.Leave(); opponent = null; rejoinAt = Time.realtimeSinceStartupAsDouble + 3;
                    }
                    if (opponent == null && search == null && Time.realtimeSinceStartupAsDouble >= rejoinAt && server.AllocationId == null)
                    {
                        Console.WriteLine("FRONTIER_DEV_SERVER public AI searching through " + backend);
                        search = new NakamaBot(backend).FindAsync(publicBot, System.Threading.CancellationToken.None);
                    }
                    if (search != null && search.IsCompleted)
                    {
                        if (search.IsFaulted) { Console.WriteLine("FRONTIER_DEV_SERVER public AI search failed: " + search.Exception?.GetBaseException().Message); rejoinAt = Time.realtimeSinceStartupAsDouble + 3; }
                        else
                        {
                            var c = search.Result;
                            Console.WriteLine("FRONTIER_DEV_SERVER public AI allocated match=" + c.matchId + " udp=" + c.port + " ws=" + c.wsPort);
                            opponent = new StrategyBot("Public AI", new NgoLink("127.0.0.1", (ushort)c.port, c.faction, c.matchId, c.joinTicket), difficulty)
                            { ConcedeAtSeconds = -1, RequireFullEconomy = false, RequireReport = false };
                        }
                        search = null;
                    }
                }
                if (bot != "none")
                {
                    if (opponent != null && (opponent.Passed || opponent.Failed))
                    {
                        if (opponent.Failed) Console.WriteLine("FRONTIER_DEV_SERVER AI opponent left: " + opponent.FailReason);
                        opponent.Link.Leave(); opponent = null; rejoinAt = Time.realtimeSinceStartupAsDouble + 2;
                    }
                    if (opponent == null && Time.realtimeSinceStartupAsDouble >= rejoinAt && server.Match == null && server.ConnectedPlayers < 2)
                    {
                        IMatchLink link = transport == "websocket"
                            ? (IMatchLink)new WebSocketLink("ws://127.0.0.1:" + webSocketPort + "/", bot)
                            : new NgoLink("127.0.0.1", port, bot);
                        opponent = new StrategyBot("AI opponent", link, difficulty) { ConcedeAtSeconds = -1, RequireFullEconomy = false, RequireReport = false };
                    }
                }
                Thread.Sleep(4);
            }
            server.StopServer();
            ShimRuntime.DestroyAll();
            return ShimRuntime.QuitCode ?? 0;
        }
    }
}
