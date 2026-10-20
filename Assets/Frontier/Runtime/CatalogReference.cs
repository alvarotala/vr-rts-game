using System.Collections.Generic;
using System.Globalization;
using Frontier.Core;

namespace Frontier.Runtime
{
    // Reference pages deliberately use definitions only, never a player's live view or match state.
    public static class CatalogReference
    {
        public readonly struct Stat
        {
            public readonly string Label,Value;
            public Stat(string label,string value){Label=label;Value=value;}
        }

        public static List<Archetype> Entries(Catalog catalog,string faction,bool structures)
        {
            var result=new List<Archetype>();
            foreach(var type in catalog.Definition.archetypes)
                if(type.faction==faction&&type.building==structures)result.Add(type);
            return result;
        }

        public static string Number(float value)=>value.ToString("0.##",CultureInfo.InvariantCulture);
        public static string Seconds(int ticks)=>Number((float)ticks/Simulation.TickRate)+" s";

        public static string Role(Archetype type)
        {
            switch(type.role)
            {
                case "worker":return "Worker";
                case "scout":return "Scout";
                case "melee":return "Frontline fighter";
                case "ranged":return "Ranged fighter";
                case "siege":return "Siege artillery";
                case "caster":return "Support caster";
                case "hq":return "Headquarters";
                case "extractor":return "Gas harvesting";
                case "supply":return "Supply support";
                case "barracks":return "Army production";
                case "tech":return "Advanced production";
                case "academy":return "Caster production";
                case "defense":return "Base defense";
                default:return type.role;
            }
        }

        public static string Description(Catalog catalog,Archetype type)
        {
            switch(type.role)
            {
                case "worker":return catalog.Faction(type.faction).regenerates
                    ?"Gathers minerals and gas. Builds structures. Its faction regenerates health outside combat."
                    :"Gathers minerals and gas. Builds structures and repairs allies. Keep workers protected.";
                case "scout":return "Fast, lightly armed explorer. Find resources and spot enemy movements before committing your army.";
                case "melee":return "Durable close-range fighter. Lead the charge and protect your ranged units.";
                case "ranged":return "Ranged attacker. Fight behind your frontline and keep enemies at a distance.";
                case "siege":return "Slow, long-range artillery with area damage. Protect it from enemies that close the gap.";
                case "caster":return "Support specialist with energy abilities. Stay behind your frontline and aim spells at clustered units.";
                case "hq":return "Trains workers and receives gathered resources. Establish new bases near resource deposits.";
                case "extractor":return "Enables gas harvesting on a gas deposit. Assign workers once construction is complete.";
                case "supply":return "Raises your army's supply capacity when complete. Build ahead of your next wave of units.";
                case "barracks":return "Trains the core of your army and unlocks advanced structures. Keep production running.";
                case "tech":return "Trains siege units and researches army upgrades. Unlocks caster production.";
                case "academy":return "Trains support casters. Add targeted damage and healing to a balanced army.";
                case "defense":return "Stationary weapon that fires automatically. Protect workers, base entrances and narrow passages.";
                default:return "";
            }
        }

        public static List<Stat> Stats(Archetype type)
        {
            var stats=new List<Stat>{
                new Stat("MINERALS",Number(type.minerals)),
                new Stat("GAS",Number(type.catalyst)),
                new Stat(type.building?"BUILD TIME":"TRAIN TIME",Seconds(type.duration)),
                new Stat(type.building?"SUPPLY ADDED":"SUPPLY USED",Number(type.building?type.capacity:type.supply)),
                new Stat("HEALTH",Number(type.health))
            };
            if(type.damage>0)
            {
                stats.Add(new Stat("DAMAGE / HIT",Number(type.damage)));
                stats.Add(new Stat("RANGE · CELLS",Number(type.range)));
                stats.Add(new Stat("SHOT INTERVAL",Seconds(type.attackPeriod)));
            }
            stats.Add(type.building
                ?new Stat("FOOTPRINT",type.footprint+" × "+type.footprint)
                :new Stat("SPEED · CELLS/S",Number(type.speed)));
            stats.Add(new Stat("VISION · CELLS",Number(type.vision)));
            if(type.damage>0)stats.Add(new Stat("DAMAGE / SECOND",Number(type.damage*Simulation.TickRate/type.attackPeriod)));
            else stats.Add(new Stat("WEAPON","Unarmed"));
            if(type.energyMax>0)stats.Add(new Stat("ENERGY · REGEN/S",Number(type.energyMax)+" · +"+Number(type.energyRegen)));
            return stats;
        }

        public static string Requirements(Catalog catalog,Archetype type)
        {
            string source=type.building?catalog.Role(type.faction,"worker").name:catalog.Role(type.faction,type.producer).name;
            string requires=string.IsNullOrEmpty(type.requires)?"None":catalog.Role(type.faction,type.requires).name;
            return (type.building?"<b>Built by</b>  ":"<b>Trained at</b>  ")+source+"\n<b>Requires</b>  "+requires;
        }

        public static string ExtraTitle(Archetype type)=>type.abilities.Length>0?"ABILITIES":type.building?"PRODUCTION & USE":"FIELD NOTES";

        public static string ExtraDetails(Catalog catalog,Archetype type)
        {
            if(type.abilities.Length>0)
            {
                var lines=new List<string>();
                foreach(string id in type.abilities)
                {
                    var ability=catalog.Ability(id);
                    lines.Add("<b>"+ability.name+"</b> · "+ability.energyCost+" energy · "+Seconds(ability.cooldown)+" cooldown\n"
                        +(ability.effect=="heal"?"Up to "+Number(ability.amount)+" health":Number(ability.amount)+" damage")
                        +" · radius "+Number(ability.radius)+" · range "+Number(ability.range)
                        +(ability.effect=="heal"?" · allied units":" · visible enemies"));
                }
                return string.Join("\n",lines);
            }
            if(type.building)
            {
                var produced=new List<string>();var unlocked=new List<string>();var lines=new List<string>();
                foreach(var other in catalog.Definition.archetypes)
                {
                    if(other.faction!=type.faction)continue;
                    if(!other.building&&other.producer==type.role)produced.Add(other.name);
                    if(other.building&&other.requires==type.role)unlocked.Add(other.name);
                }
                if(produced.Count>0)lines.Add("<b>Trains</b>  "+string.Join(" · ",produced));
                if(unlocked.Count>0)lines.Add("<b>Unlocks</b>  "+string.Join(" · ",unlocked));
                if(type.role=="tech")foreach(var upgrade in catalog.Definition.upgrades)
                    lines.Add("<b>"+upgrade.name+"</b>  "+upgrade.minerals+" minerals · "+upgrade.catalyst+" gas · "+Seconds(upgrade.duration));
                if(type.role=="hq")lines.Add("Workers return gathered minerals and gas here.");
                if(type.role=="extractor")lines.Add("Build over a gas deposit. Best efficiency: "+Harvesting.OptimalWorkers("catalyst")+" workers per deposit.");
                if(type.role=="supply")lines.Add("Capacity is added when construction finishes. Army limit: "+Simulation.UnitLimit+" units.");
                if(type.role=="defense")lines.Add("Attacks automatically when an enemy enters weapon range.");
                return string.Join("\n",lines);
            }
            switch(type.role)
            {
                case "worker":return "Assign a resource to gather. "+(catalog.Faction(type.faction).regenerates?"Allies regenerate outside combat.":"Repairing spends minerals.")
                    +"\n"+(catalog.Faction(type.faction).grows?"Structures grow on their own after a worker starts construction.":"Workers stay at construction sites until the structure is complete.");
                case "scout":return "Use its speed and vision to explore safely.\nAvoid trading shots with heavier frontline units.";
                case "melee":return "Close the distance while your ranged units fire from behind.\nGood at keeping enemies away from artillery and casters.";
                case "ranged":return "Keep a frontline between this unit and close-range attackers.\nAttack-Move engages nearby threats along the route.";
                case "siege":return "Shots also damage nearby enemies.\nLong range rewards scouting and protection from faster units.";
                default:return "";
            }
        }
    }
}
