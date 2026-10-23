using Frontier.Core;

static class ResearchBuildingChecks
{
    public static void Run(Catalog catalog,MapDefinition map,Action<bool,string> check)
    {
        foreach(string faction in new[]{"industrial","biological"})
        {
            var sim=new Simulation(map,catalog,faction);
            sim.Players[0].minerals=5000;sim.Players[0].catalyst=5000;
            var type=catalog.Role(faction,"research");
            var worker=sim.Entities.Values.First(e=>e.owner==0&&sim.Type(e).role=="worker");
            Check(type.building&&type.minerals==150&&type.catalyst==50&&type.duration==30*Simulation.TickRate&&type.requires=="barracks",
                "approved cost, time and prerequisite");
            Check(!catalog.CanProduceUnits(type.id),"research does not train units or qualify for a rally point");
            int minerals=sim.Players[0].minerals,gas=sim.Players[0].catalyst;
            Check(!Build(new Point(18,8))&&sim.Players[0].minerals==minerals&&sim.Players[0].catalyst==gas,"missing barracks rejects construction without charge");
            var barracks=Add("barracks",1000,new Point(22,8));barracks.complete=false;
            Check(!Build(new Point(18,8)),"unfinished barracks does not unlock research building");barracks.complete=true;
            bool built=false;
            for(int y=2;y<30&&!built;y++)for(int x=2;x<30&&!built;x++)built=Build(new Point(x,y));
            Check(built&&sim.Players[0].minerals==minerals-150&&sim.Players[0].catalyst==gas-50,"valid research foundation charges the approved cost once");
            var lab=sim.Entities.Values.Single(e=>e.owner==0&&e.definition==type.id);
            Check(!Send(CommandKind.Research,lab,"attack"),"unfinished lab rejects upgrades");
            for(int i=0;i<1800&&!lab.complete;i++)sim.Step();
            Check(lab.complete,"worker completes the new structure through normal simulation");
            var tech=Add("tech",1001,new Point(22,18));
            Check(!Send(CommandKind.Research,tech,"attack")&&!Send(CommandKind.Research,barracks,"attack"),"existing production buildings cannot research");
            Check(!Send(CommandKind.Train,lab,catalog.Role(faction,"siege").id),"research building cannot train artillery");
            Check(!sim.Submit(new GameCommand{player=1,kind=CommandKind.Research,entities=new[]{lab.id},definition="attack"}).accepted,"opponent cannot research at this lab");
            Check(!sim.Submit(new GameCommand{player=0,kind=CommandKind.SetRallyPoint,entities=new[]{lab.id},position=new Point(20,20)}).accepted,"dedicated lab has no rally point");
            Check(Send(CommandKind.Research,lab,"attack")&&Send(CommandKind.Research,lab,"armor")&&Send(CommandKind.Train,tech,catalog.Role(faction,"siege").id),"research queue and artillery run independently");
            int attack=lab.queue[0].remaining,siege=tech.queue[0].remaining;sim.Step();
            Check(lab.queue[0].remaining==attack-1&&tech.queue[0].remaining==siege-1,"both buildings advance on the same tick");
            Check(!Send(CommandKind.Research,lab,"attack"),"duplicate queued upgrades are rejected");
            foreach(var order in lab.queue)order.remaining=1;
            sim.Step();sim.Step();
            Check(sim.Players[0].attackLevel==1&&sim.Players[0].armorLevel==1&&lab.queue.Count==0,"both upgrades retain their faction-wide effects");
            Check(!Send(CommandKind.Research,lab,"armor"),"completed upgrades cannot be researched again");
            foreach(string stat in new[]{"attack","armor"})
            {
                int beforeMinerals=sim.Players[0].minerals,beforeGas=sim.Players[0].catalyst;
                Check(!Send(CommandKind.Research,lab,stat+"3")&&sim.Players[0].minerals==beforeMinerals&&sim.Players[0].catalyst==beforeGas,
                    stat+": level III cannot skip level II or charge resources");
                for(int level=2;level<=3;level++)
                {
                    var upgrade=catalog.Upgrade(stat+level);var view=sim.View(0);
                    Check(upgrade.level==level&&upgrade.Stat==stat&&upgrade.minerals==(level==2?225:300)&&upgrade.catalyst==(level==2?150:200)
                        &&upgrade.duration==35*Simulation.TickRate,stat+": approved progressive cost and unchanged duration at level "+level);
                    Check(catalog.CanResearch(upgrade,view.researched),stat+": completed previous level unlocks the next option");
                    beforeMinerals=sim.Players[0].minerals;beforeGas=sim.Players[0].catalyst;
                    Check(Send(CommandKind.Research,lab,upgrade.id)&&lab.queue.Single().definition==upgrade.id
                        &&sim.Players[0].minerals==beforeMinerals-upgrade.minerals&&sim.Players[0].catalyst==beforeGas-upgrade.catalyst,
                        stat+": exact tier is enqueued and paid once");
                    Check(!Send(CommandKind.Research,lab,upgrade.id),stat+": queued tier cannot be duplicated");
                    if(level==2)Check(!Send(CommandKind.Research,lab,stat+"3"),stat+": next level waits for completion, not just enqueue");
                    Check(Send(CommandKind.Cancel,lab,null)&&lab.queue.Count==0&&sim.Players[0].minerals==beforeMinerals&&sim.Players[0].catalyst==beforeGas,
                        stat+": cancellation refunds this tier's full cost");
                    Check(Send(CommandKind.Research,lab,upgrade.id),stat+": cancelled tier can be researched again");
                    for(int tick=0;tick<upgrade.duration;tick++)sim.Step();
                    Check((stat=="attack"?sim.Players[0].attackLevel:sim.Players[0].armorLevel)==level&&lab.queue.Count==0,
                        stat+": tier completes after its actual duration");
                    if(stat=="attack")Check(sim.Players[0].armorLevel==1,"Firepower progresses independently from Armor");
                    Check(sim.View(0).researched.Contains(upgrade.id)&&!sim.View(1).researched.Contains(upgrade.id),stat+": only the owner receives its completed tier");
                    Check(!Send(CommandKind.Research,lab,upgrade.id)&&!catalog.CanResearch(upgrade,sim.View(0).researched),stat+": completed tier cannot be repeated");
                }
            }
            Check(sim.Players[0].attackLevel==3&&sim.Players[0].armorLevel==3&&sim.View(0).researched.Length==6
                &&!catalog.Definition.upgrades.Any(u=>catalog.CanResearch(u,sim.View(0).researched)),"both independent progressions finish at III");
            Check(!Send(CommandKind.Research,lab,"attack4")&&!Send(CommandKind.Research,lab,"armor4"),"there is no fourth level");
            void Check(bool ok,string message)=>check(ok,"Research building "+faction+": "+message);
            bool Build(Point point)=>sim.Submit(new GameCommand{player=0,kind=CommandKind.Build,entities=new[]{worker.id},definition=type.id,position=point}).accepted;
            bool Send(CommandKind kind,EntityState entity,string definition)=>sim.Submit(new GameCommand{player=0,kind=kind,entities=new[]{entity.id},definition=definition}).accepted;
            EntityState Add(string role,int id,Point position)
            {
                var definition=catalog.Role(faction,role);var entity=new EntityState{id=id,owner=0,definition=definition.id,position=position,health=definition.health};
                sim.Entities.Add(id,entity);sim.Map.Occupy(position,definition.footprint,id);return entity;
            }
        }
    }
}
