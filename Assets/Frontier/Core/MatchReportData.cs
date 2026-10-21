using System;
using System.Linq;

namespace Frontier.Core
{
    // Explicit serializable DTOs preserve report data independently of the live simulation.
    [Serializable] public sealed class PlayerStatsData
    {
        public string Faction;
        public int Workers;
        public int Army;
        public int Structures;
        public int PeakWorkers;
        public int PeakArmy;
        public int WorkersProduced;
        public int ArmyProduced;
        public int StructuresCompleted;
        public int WorkersLost;
        public int ArmyLost;
        public int StructuresLost;
        public int Minerals;
        public int Catalyst;
        public int MineralsCollected;
        public int CatalystCollected;
        public int MineralsSpent;
        public int CatalystSpent;
        public int MineralsRefunded;
        public int CatalystRefunded;
        public int Actions;
        public int PeakActionsPerSecond;
        public float AverageApm;
        public float RecentApm;
        public static PlayerStatsData From(PlayerMatchStats p)=>new PlayerStatsData
        {
            Faction=p.Faction,
            Workers=p.Workers,
            Army=p.Army,
            Structures=p.Structures,
            PeakWorkers=p.PeakWorkers,
            PeakArmy=p.PeakArmy,
            WorkersProduced=p.WorkersProduced,
            ArmyProduced=p.ArmyProduced,
            StructuresCompleted=p.StructuresCompleted,
            WorkersLost=p.WorkersLost,
            ArmyLost=p.ArmyLost,
            StructuresLost=p.StructuresLost,
            Minerals=p.Minerals,
            Catalyst=p.Catalyst,
            MineralsCollected=p.MineralsCollected,
            CatalystCollected=p.CatalystCollected,
            MineralsSpent=p.MineralsSpent,
            CatalystSpent=p.CatalystSpent,
            MineralsRefunded=p.MineralsRefunded,
            CatalystRefunded=p.CatalystRefunded,
            Actions=p.Actions,
            PeakActionsPerSecond=p.PeakActionsPerSecond,
            AverageApm=p.AverageApm,
            RecentApm=p.RecentApm,
        };
    }
    [Serializable] public sealed class BuildOrderData
    {
        public int tick,supply,endTick;
        public string definition;
        public CommandKind kind;
        public BuildOrderStatus status;
        public static BuildOrderData From(BuildOrderEntry e)=>new BuildOrderData{tick=e.Tick,supply=e.Supply,endTick=e.EndTick,definition=e.Definition,kind=e.Kind,status=e.Status};
        public BuildOrderEntry Restore()=>new BuildOrderEntry(tick,supply,definition,kind,status,endTick);
    }
    [Serializable] public sealed class PlayerBuildOrderData { public BuildOrderData[] entries; }
    [Serializable] public sealed class MatchSampleData { public int tick; public PlayerStatsData[] players; }
    [Serializable] public sealed class MatchReportData
    {
        public int schemaVersion=1,tick,winner;
        public MatchSampleData[] samples;
        public PlayerBuildOrderData[] buildOrders;
        public static MatchReportData From(MatchReport report)=>new MatchReportData
        {
            tick=report.Tick,winner=report.Winner,
            samples=report.Samples.Select(s=>new MatchSampleData{tick=s.Tick,players=s.Players.Select(PlayerStatsData.From).ToArray()}).ToArray(),
            buildOrders=report.BuildOrders.Select(o=>new PlayerBuildOrderData{entries=o.Select(BuildOrderData.From).ToArray()}).ToArray()
        };
        public MatchReport Restore()
        {
            if(schemaVersion!=1||tick<0||winner<0||winner>2||samples==null||samples.Length==0||samples.Length>2049
                ||buildOrders==null||buildOrders.Length!=2||samples[0]==null||samples[0].tick!=0||samples[samples.Length-1]==null||samples[samples.Length-1].tick!=tick)
                throw new ArgumentException("Invalid saved match report.");
            int previous=-1;
            foreach(var sample in samples)
            {
                if(sample==null||sample.tick<=previous||sample.tick>tick||sample.players==null||sample.players.Length!=2)
                    throw new ArgumentException("Invalid report timeline.");
                previous=sample.tick;
                foreach(var p in sample.players)
                    if(p==null||string.IsNullOrEmpty(p.Faction)||float.IsNaN(p.AverageApm)||float.IsInfinity(p.AverageApm)||p.AverageApm<0
                        ||float.IsNaN(p.RecentApm)||float.IsInfinity(p.RecentApm)||p.RecentApm<0)
                        throw new ArgumentException("Invalid saved player statistics.");
            }
            foreach(var order in buildOrders)
            {
                if(order?.entries==null)throw new ArgumentException("Missing saved build order.");
                previous=-1;
                foreach(var entry in order.entries)
                {
                    if(entry==null||entry.tick<previous||entry.tick<0||entry.tick>tick||entry.endTick>tick||entry.endTick< -1
                        ||string.IsNullOrEmpty(entry.definition)||!Enum.IsDefined(typeof(BuildOrderStatus),entry.status))
                        throw new ArgumentException("Invalid saved build order.");
                    previous=entry.tick;
                }
            }
            return new MatchReport(tick,winner,samples.Select(s=>new MatchSample(s.tick,s.players.Select(p=>new PlayerMatchStats(p)).ToArray())).ToArray(),
                buildOrders.Select(p=>p.entries.Select(e=>e.Restore()).ToArray()).ToArray());
        }
    }
}
