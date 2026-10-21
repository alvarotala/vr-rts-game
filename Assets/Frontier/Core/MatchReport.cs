using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Frontier.Core
{
    public enum BuildOrderStatus { Pending, Completed, Cancelled, Destroyed }

    public sealed class BuildOrderEntry
    {
        public readonly int Tick, Supply, EndTick;
        public readonly string Definition;
        public readonly CommandKind Kind;
        public readonly BuildOrderStatus Status;
        internal BuildOrderEntry(int tick, int supply, string definition, CommandKind kind, BuildOrderStatus status, int endTick)
        { Tick=tick; Supply=supply; Definition=definition; Kind=kind; Status=status; EndTick=endTick; }
    }

    public sealed class PlayerMatchStats
    {
        public readonly string Faction;
        public readonly int Workers, Army, Structures, PeakWorkers, PeakArmy;
        public readonly int WorkersProduced, ArmyProduced, StructuresCompleted, WorkersLost, ArmyLost, StructuresLost;
        public readonly int Minerals, Catalyst, MineralsCollected, CatalystCollected, MineralsSpent, CatalystSpent, MineralsRefunded, CatalystRefunded;
        public readonly int Actions, PeakActionsPerSecond;
        public readonly float AverageApm, RecentApm;
        internal PlayerMatchStats(PlayerStatsData saved)
        {
            Faction=saved.Faction;
            Workers=saved.Workers;
            Army=saved.Army;
            Structures=saved.Structures;
            PeakWorkers=saved.PeakWorkers;
            PeakArmy=saved.PeakArmy;
            WorkersProduced=saved.WorkersProduced;
            ArmyProduced=saved.ArmyProduced;
            StructuresCompleted=saved.StructuresCompleted;
            WorkersLost=saved.WorkersLost;
            ArmyLost=saved.ArmyLost;
            StructuresLost=saved.StructuresLost;
            Minerals=saved.Minerals;
            Catalyst=saved.Catalyst;
            MineralsCollected=saved.MineralsCollected;
            CatalystCollected=saved.CatalystCollected;
            MineralsSpent=saved.MineralsSpent;
            CatalystSpent=saved.CatalystSpent;
            MineralsRefunded=saved.MineralsRefunded;
            CatalystRefunded=saved.CatalystRefunded;
            Actions=saved.Actions;
            PeakActionsPerSecond=saved.PeakActionsPerSecond;
            AverageApm=saved.AverageApm;
            RecentApm=saved.RecentApm;
        }
        internal PlayerMatchStats(string faction, int workers, int army, int structures, int minerals, int catalyst, MatchCounters c, int tick)
        {
            Faction=faction; Workers=workers; Army=army; Structures=structures; Minerals=minerals; Catalyst=catalyst;
            PeakWorkers=c.peakWorkers; PeakArmy=c.peakArmy; WorkersProduced=c.workersProduced; ArmyProduced=c.armyProduced;
            StructuresCompleted=c.structuresCompleted; WorkersLost=c.workersLost; ArmyLost=c.armyLost; StructuresLost=c.structuresLost;
            MineralsCollected=c.mineralsCollected; CatalystCollected=c.catalystCollected; MineralsSpent=c.mineralsSpent; CatalystSpent=c.catalystSpent;
            MineralsRefunded=c.mineralsRefunded; CatalystRefunded=c.catalystRefunded; Actions=c.actions; PeakActionsPerSecond=c.peakActionsPerSecond;
            float seconds=Math.Max(1,tick/(float)Simulation.TickRate);
            AverageApm=Actions*60f/seconds; RecentApm=c.recentActions.Count*60f/Math.Min(60,seconds);
        }
    }

    public sealed class MatchSample
    {
        public readonly int Tick;
        public readonly ReadOnlyCollection<PlayerMatchStats> Players;
        internal MatchSample(int tick, PlayerMatchStats[] players) { Tick=tick; Players=Array.AsReadOnly(players); }
    }

    public sealed class MatchReport
    {
        public readonly int Tick, Winner;
        public readonly ReadOnlyCollection<MatchSample> Samples;
        public readonly ReadOnlyCollection<PlayerMatchStats> Players;
        public readonly ReadOnlyCollection<ReadOnlyCollection<BuildOrderEntry>> BuildOrders;
        internal MatchReport(int tick, int winner, MatchSample[] samples, BuildOrderEntry[][] orders)
        {
            Tick=tick; Winner=winner; Samples=Array.AsReadOnly(samples); Players=samples[samples.Length-1].Players;
            BuildOrders=Array.AsReadOnly(orders.Select(Array.AsReadOnly).ToArray());
        }
    }

    internal sealed class MatchCounters
    {
        internal int peakWorkers=6, peakArmy, workersProduced, armyProduced, structuresCompleted, workersLost, armyLost, structuresLost;
        internal int mineralsCollected, catalystCollected, mineralsSpent, catalystSpent, mineralsRefunded, catalystRefunded;
        internal int actions, peakActionsPerSecond;
        internal readonly Queue<int> recentActions=new Queue<int>(), burstActions=new Queue<int>();
        internal readonly List<BuildOrderEntry> orders=new List<BuildOrderEntry>();
        internal readonly Dictionary<int,int> production=new Dictionary<int,int>(), construction=new Dictionary<int,int>();
    }

    public sealed partial class Simulation
    {
        private readonly MatchCounters[] matchCounters={new MatchCounters(),new MatchCounters()};
        private readonly List<MatchSample> matchSamples=new List<MatchSample>();
        private int sampleInterval=TickRate*5;
        // Opponent statistics never enter PlayerView or the AI's live observation.
        public MatchReport Report { get; private set; }

        private void RecordCommand(int player)
        {
            var c=matchCounters[player]; c.actions++; c.recentActions.Enqueue(Tick); c.burstActions.Enqueue(Tick);
            TrimActions(c);
            c.peakActionsPerSecond=Math.Max(c.peakActionsPerSecond,c.burstActions.Count);
        }
        private void TrimActions(MatchCounters c)
        {
            while(c.recentActions.Count>0&&c.recentActions.Peek()<=Tick-TickRate*60)c.recentActions.Dequeue();
            while(c.burstActions.Count>0&&c.burstActions.Peek()<=Tick-TickRate)c.burstActions.Dequeue();
        }
        private MatchSample CaptureMatchSample()
        {
            var players=new PlayerMatchStats[2];
            for(int p=0;p<2;p++)
            {
                var c=matchCounters[p]; TrimActions(c);
                int workers=0,army=0,structures=0;
                foreach(var e in Entities.Values)
                {
                    if(e.owner!=p||e.health<=0)continue;
                    var type=Type(e);
                    if(type.building){if(e.complete)structures++;}
                    else if(type.role=="worker")workers++;else army++;
                }
                c.peakWorkers=Math.Max(c.peakWorkers,workers); c.peakArmy=Math.Max(c.peakArmy,army);
                players[p]=new PlayerMatchStats(Players[p].faction,workers,army,structures,Players[p].minerals,Players[p].catalyst,c,Tick);
            }
            return new MatchSample(Tick,players);
        }
        private void SampleMatch(bool final=false)
        {
            if(Report!=null)return;
            if(!final&&Tick%sampleInterval!=0)return;
            var sample=CaptureMatchSample();
            if(matchSamples.Count>0&&matchSamples[matchSamples.Count-1].Tick==Tick)matchSamples[matchSamples.Count-1]=sample;
            else matchSamples.Add(sample);
            if(final)
                Report=new MatchReport(Tick,Winner,matchSamples.ToArray(),matchCounters.Select(c=>c.orders.ToArray()).ToArray());
            else if(matchSamples.Count>=2048)
            {
                // Keep the start and bounded graph storage even for very long matches.
                for(int i=matchSamples.Count-1;i>0;i--)if(i%2==1)matchSamples.RemoveAt(i);
                sampleInterval*=2;
            }
        }
        private void RecordBuildOrder(int player, int id, string definition, CommandKind kind)
        {
            var c=matchCounters[player];
            var pending=kind==CommandKind.Build?c.construction:c.production;
            pending.Add(id,c.orders.Count);
            c.orders.Add(new BuildOrderEntry(Tick,Units(player)+Reserved(player),definition,kind,BuildOrderStatus.Pending,-1));
        }
        private void FinishBuildOrder(int player, int id, bool construction, BuildOrderStatus status)
        {
            var c=matchCounters[player];var pending=construction?c.construction:c.production;
            if(!pending.TryGetValue(id,out int index))return;
            var entry=c.orders[index];
            c.orders[index]=new BuildOrderEntry(entry.Tick,entry.Supply,entry.Definition,entry.Kind,status,Tick);
            pending.Remove(id);
        }
        private void RecordCompletion(EntityState e)
        {
            var c=matchCounters[e.owner];var t=Type(e);
            if(t.building)c.structuresCompleted++;
            else if(t.role=="worker")c.workersProduced++;else c.armyProduced++;
            int workers=Entities.Values.Count(x=>x.owner==e.owner&&x.health>0&&Type(x).role=="worker");
            int army=Entities.Values.Count(x=>x.owner==e.owner&&x.health>0&&!Type(x).building&&Type(x).role!="worker");
            c.peakWorkers=Math.Max(c.peakWorkers,workers); c.peakArmy=Math.Max(c.peakArmy,army);
        }
        private void RecordLoss(EntityState e)
        {
            var c=matchCounters[e.owner];var type=Type(e);
            if(type.building)
            {
                c.structuresLost++;
                FinishBuildOrder(e.owner,e.id,true,BuildOrderStatus.Destroyed);
                foreach(var order in e.queue)FinishBuildOrder(e.owner,order.id,false,BuildOrderStatus.Destroyed);
            }
            else if(type.role=="worker")c.workersLost++;else c.armyLost++;
        }
    }
}
