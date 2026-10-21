using System.Linq;

namespace Frontier.Core
{
    public sealed partial class Simulation
    {
        // Explicit omniscient projection for an offline replay. Live sessions and Strategist
        // continue using View(player); no observer information is added to that contract.
        public PlayerView ObserverView(out PlayerView[] perspectives)
        {
            perspectives=new[]{View(0),View(1)};
            var own=perspectives[0];var enemy=perspectives[1];
            return new PlayerView
            {
                player=0,tick=Tick,winner=Winner,faction=own.faction,minerals=own.minerals,catalyst=own.catalyst,supply=own.supply,capacity=own.capacity,
                entities=own.entities.Where(e=>e.owner==0).Concat(enemy.entities.Where(e=>e.owner==1)).OrderBy(e=>e.id).ToArray(),
                resources=Resources.Select(r=>new ResourceState{id=r.id,definition=r.definition,kind=r.kind,remaining=r.remaining,position=r.position}).ToArray(),
                visibility=Enumerable.Repeat((byte)2,Map.Count).ToArray(),researched=own.researched,
                attacks=recentAttacks.Select(a=>a.shot).ToArray(),deaths=recentDeaths.Select(d=>d.death).ToArray(),
                abilityEvents=recentAbilities.Select(a=>a.effect).ToArray()
            };
        }
    }
}
