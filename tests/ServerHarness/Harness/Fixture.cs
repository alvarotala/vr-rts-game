using System;
using System.IO;
using System.Linq;
using Frontier.Core;
using Frontier.Networking;
using UnityEngine;

namespace Frontier.Harness
{
    // Writes C#-produced protocol bytes for the web client's parity tests: compatibility
    // values, a real compressed and chunked PlayerView, a final result envelope and the
    // C# enum order. Generated from the current sources on every test run.
    public static class Fixture
    {
        [Serializable] private sealed class Output
        {
            public int protocol, rules, frame, rawLength, ticks;
            public string contentHash, compatibilityKey, viewJson, resultRawJson;
            public string[] chunks, resultChunks, commandKinds;
            public int resultRawLength;
            public string[] placementChecks;
        }

        public static int Write(string path)
        {
            var catalog = new Catalog(JsonUtility.FromJson<CatalogDefinition>(Resources.Load<TextAsset>("catalog").text));
            var map = JsonUtility.FromJson<MapDefinition>(Resources.Load<TextAsset>("Maps/rift").text);
            var match = new Simulation(map, catalog, "industrial", "biological");
            var bot = new Strategist(1, AiDifficulty.Normal, catalog, map);
            var own = new Strategist(0, AiDifficulty.Normal, catalog, map);
            for (int i = 0; i < 20 * 60; i++)
            {
                if (match.Tick % 20 == 0)
                {
                    foreach (var command in bot.Decide(match.View(1)).ToArray()) match.Submit(command);
                    foreach (var command in own.Decide(match.View(0)).ToArray()) match.Submit(command);
                }
                match.Step();
            }
            byte[] raw = MatchProtocol.Encode(match.View(0), NetworkWire.MaxSnapshotBytes);
            byte[] packed = NetworkWire.Compress(raw, NetworkWire.MaxSnapshotBytes, NetworkWire.MaxSnapshotCompressed);
            const long frame = 4242;
            var chunks = Enumerable.Range(0, NetworkWire.ChunkCount(packed.Length)).Select(i => Convert.ToBase64String(NetworkWire.Chunk(frame, packed, raw.Length, i))).ToArray();
            match.Submit(new GameCommand { player = 1, kind = CommandKind.Surrender, sequence = 100000 });
            byte[] finalView = MatchProtocol.Encode(match.View(0), NetworkWire.MaxSnapshotBytes);
            byte[] report = MatchProtocol.Encode(MatchReportData.From(match.Report), NetworkWire.MaxResultBytes);
            byte[] result = NetworkWire.PackResult(finalView, report, out int resultRaw, out _);
            var resultChunks = Enumerable.Range(0, NetworkWire.ChunkCount(result.Length)).Select(i => Convert.ToBase64String(NetworkWire.Chunk(1, result, resultRaw, i))).ToArray();
            var output = new Output
            {
                protocol = MatchProtocol.Version, rules = Simulation.ReplayRulesVersion, contentHash = MatchProtocol.ContentHash(),
                compatibilityKey = MatchCompatibility.Key, frame = (int)frame, rawLength = raw.Length, chunks = chunks, ticks = match.Tick,
                viewJson = System.Text.Encoding.UTF8.GetString(raw), resultChunks = resultChunks, resultRawLength = resultRaw,
                resultRawJson = System.Text.Encoding.UTF8.GetString(NetworkWire.Decompress(result, resultRaw, NetworkWire.MaxResultBytes)),
                commandKinds = Enum.GetNames(typeof(CommandKind)),
            };
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
            File.WriteAllText(path, JsonUtility.ToJson(output));
            Console.WriteLine("FRONTIER_FIXTURE " + path + " chunks=" + chunks.Length + " raw=" + raw.Length + " tick=" + match.Tick);
            return 0;
        }
    }
}
