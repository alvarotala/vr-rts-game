using System;
using System.Collections.Generic;
using System.Linq;

namespace Frontier.Core
{
    public enum AiDifficulty { Easy, Normal, Hard }

    // Decisions only consume the same information projection delivered to a player.
    // Difficulty changes planning and execution, never resources, stats or vision.
    public sealed partial class Strategist
    {
        private readonly int player;
        private sealed class Pace
        {
            public int reaction, workers, army, recruit, build, barracks, extractor, tech, academy, defense, research, expansion;
            public int firstWave, waveSize, waveLimit, waveRest, waveDuration, scoutStart;
            public float attackShare, retreatHealth;
        }
        private readonly Pace pace;
        public AiDifficulty Difficulty { get; }
        private readonly Catalog catalog;
        private readonly GridMap terrain;
        private int sequence;
        public Strategist(int player, AiDifficulty difficulty, Catalog catalog, MapDefinition map)
        {
            this.player=player; Difficulty=difficulty; this.catalog=catalog; terrain=new GridMap(map);
            pace=difficulty==AiDifficulty.Easy
                ? new Pace { reaction=3, workers=16, army=28, recruit=8, build=8, barracks=2, extractor=30, tech=100, academy=160,
                    defense=150, research=240, expansion=420, firstWave=300, waveSize=6, waveLimit=22, waveRest=65, waveDuration=180,
                    scoutStart=90, attackShare=.65f, retreatHealth=.22f }
                : difficulty==AiDifficulty.Hard
                ? new Pace { reaction=1, workers=22, army=38, recruit=2, build=2, barracks=4, extractor=12, tech=45, academy=80,
                    defense=65, research=110, expansion=180, firstWave=150, waveSize=10, waveLimit=38, waveRest=20, waveDuration=300,
                    scoutStart=35, attackShare=.95f, retreatHealth=.32f }
                : new Pace { reaction=2, workers=20, army=36, recruit=4, build=4, barracks=3, extractor=18, tech=70, academy=110,
                    defense=100, research=160, expansion=270, firstWave=210, waveSize=9, waveLimit=32, waveRest=35, waveDuration=240,
                    scoutStart=60, attackShare=.8f, retreatHealth=.28f };
        }
        private static int Seconds(int seconds)=>seconds*Simulation.TickRate;
        private Archetype Type(VisibleEntity entity)=>catalog[entity.definition];
        private Point Center(VisibleEntity entity)
        {
            var type=Type(entity);
            return type.building?new Point(entity.position.x+type.footprint*.5f,entity.position.y+type.footprint*.5f):entity.position;
        }
        public IEnumerable<GameCommand> Decide(PlayerView view)
        {
            if(view.winner!=-1 || view.tick%Seconds(pace.reaction)!=0)yield break;
            var own=view.entities.Where(e=>e.owner==player&&!e.remembered).ToArray();
            if(!own.Any(e=>Type(e).role=="hq"))yield break;
            foreach(var command in DecideEconomy(view,own))yield return command;
            foreach(var command in DecideTactics(view,own))yield return command;
        }
        private GameCommand Command(CommandKind kind, int entity, string definition = null, int target = 0, Point position = default)
            => new GameCommand { player = player, sequence = ++sequence, kind = kind, entities = new[] { entity }, definition = definition, target = target, position = position };
        private GameCommand Group(CommandKind kind, VisibleEntity[] entities, Point position)
            => new GameCommand { player = player, sequence = ++sequence, kind = kind, entities = entities.Select(e => e.id).ToArray(), position = position };
    }
}
