using System.Text.Json;
using Frontier.Core;

static class DifficultyChecks
{
    public static void Run(Catalog catalog,MapDefinition map,MapDefinition training,Action<bool,string> check)
    {
        foreach(string faction in new[]{"industrial","biological"})
        {
            var sim=new Simulation(map,catalog,faction);int id=1000;
            EntityState Add(string role,Point position)
            {
                var t=catalog.Role(faction,role);var e=new EntityState{id=id++,owner=0,definition=t.id,health=t.health,position=position};sim.Entities.Add(e.id,e);return e;
            }
            var barracks=Add("barracks",new Point(20,12));Add("supply",new Point(20,8));
            Add("worker",new Point(16,16));Add("worker",new Point(17,16));
            var soldiers=Enumerable.Range(0,6).Select(i=>Add(i%2==0?"melee":"ranged",new Point(15+i,17))).ToArray();
            var hq=sim.Entities.Values.First(e=>e.owner==0&&sim.Type(e).role=="hq");
            PlayerView View(int tick){var v=sim.View(0);v.tick=tick;v.minerals=5000;v.catalyst=5000;return v;}
            var bot=new Strategist(0,AiDifficulty.VeryEasy,catalog,map);
            var orders=bot.Decide(View(120)).ToArray();
            check(orders.All(c=>c.kind!=CommandKind.Train&&c.kind!=CommandKind.Research),"Very Easy caps eight workers and six soldiers "+faction);
            check(orders.Where(c=>c.kind==CommandKind.Move).All(c=>c.position.Distance(hq.position)<12),"Very Easy rallies inside its base "+faction);
            check(!bot.Decide(View(60)).Any(),"Very Easy reacts only every six seconds "+faction);
            check(!bot.Decide(View(21480)).Any(c=>c.kind==CommandKind.AttackMove),"Very Easy does not raid before eighteen minutes "+faction);
            orders=bot.Decide(View(21600)).ToArray();
            check(orders.Single(c=>c.kind==CommandKind.AttackMove).entities.Length==3,"Very Easy sends three soldiers at eighteen minutes "+faction);
            check(orders.All(c=>c.kind!=CommandKind.Build),"Very Easy does not expand or build extra barracks, defenses or tech "+faction);
            check(!bot.Decide(View(21720)).Any(c=>c.kind==CommandKind.AttackMove),"Very Easy does not immediately resend a wave "+faction);
            check(bot.Decide(View(25200)).Single(c=>c.kind==CommandKind.AttackMove).entities.Length==3,"Very Easy can send its next wave after three minutes "+faction);

            var threat=View(120);threat.entities=threat.entities.Append(new VisibleEntity{id=999,owner=1,definition=catalog.Role(faction,"melee").id,position=new Point(16,14),health=100}).ToArray();
            check(new Strategist(0,AiDifficulty.VeryEasy,catalog,map).Decide(threat).Any(c=>c.kind==CommandKind.AttackMove&&c.position.Distance(new Point(16,14))<.01f),"Very Easy still defends its base during grace period "+faction);
            threat.entities.Last().remembered=true;
            check(!new Strategist(0,AiDifficulty.VeryEasy,catalog,map).Decide(threat).Any(c=>c.kind==CommandKind.AttackMove),"Very Easy ignores unseen remembered threats "+faction);
            threat.winner=0;check(!bot.Decide(threat).Any(),"Very Easy stops when the match ends "+faction);

            sim.Entities.Remove(soldiers[0].id);barracks.queue.Add(new ProductionOrder{definition=catalog.Role(faction,"ranged").id,total=200,remaining=100});
            var extraWorker=sim.Entities.Values.Last(e=>sim.Type(e).role=="worker"&&e.owner==0);sim.Entities.Remove(extraWorker.id);
            hq.queue.Add(new ProductionOrder{definition=catalog.Role(faction,"worker").id,total=200,remaining=100});
            check(!new Strategist(0,AiDifficulty.VeryEasy,catalog,map).Decide(View(120)).Any(c=>c.kind==CommandKind.Train),"Very Easy counts queued workers and soldiers toward its limits "+faction);
            barracks.queue.Clear();foreach(var soldier in soldiers)sim.Entities.Remove(soldier.id);
            var recruiter=new Strategist(0,AiDifficulty.VeryEasy,catalog,map);
            orders=recruiter.Decide(View(120)).ToArray();
            check(orders.Count(c=>c.kind==CommandKind.Train)==1&&orders.Where(c=>c.kind==CommandKind.Train).All(c=>catalog[c.definition].role=="ranged"||catalog[c.definition].role=="melee"),"Very Easy produces only basic infantry "+faction);
            check(!recruiter.Decide(View(240)).Any(c=>c.kind==CommandKind.Train),"Very Easy waits before recruiting again "+faction);
            check(recruiter.Decide(View(600)).Any(c=>c.kind==CommandKind.Train),"Very Easy resumes recruiting after twenty-four seconds "+faction);
            Add("tech",new Point(24,12));Add("scout",new Point(18,14));
            orders=new Strategist(0,AiDifficulty.VeryEasy,catalog,map).Decide(View(120)).ToArray();
            check(!orders.Any(c=>c.kind==CommandKind.Research||c.kind==CommandKind.Train&&catalog[c.definition].role=="siege"),"Very Easy skips upgrades and artillery even if technology exists "+faction);
            check(orders.Where(c=>c.kind==CommandKind.Move).All(c=>c.position.Distance(hq.position)<12),"Very Easy never sends an early scout to the opposing base "+faction);

            foreach(var difficulty in new[]{AiDifficulty.Easy,AiDifficulty.Normal})
            {
                var existing=new Strategist(0,difficulty,catalog,map);
                check(existing.Decide(View(difficulty==AiDifficulty.Easy?60:20)).Any(),difficulty+" retains its existing reaction interval "+faction);
                if(difficulty==AiDifficulty.Easy)check(!existing.Decide(View(20)).Any(),"Easy still waits three seconds "+faction);
            }
        }
    }

    public static void Practice(Catalog catalog,MapDefinition[] maps,Action<bool,string> check,string root)
    {
        var records=new List<object>();
        foreach(var map in maps)foreach(string faction in new[]{"industrial","biological"})
        {
            var sim=new Simulation(map,catalog,"industrial",faction);var bot=new Strategist(1,AiDifficulty.VeryEasy,catalog,map);
            var home=sim.Entities.Values.First(e=>e.owner==1&&sim.Type(e).role=="hq").position;
            int firstAttack=-1,maxWorkers=0,maxSoldiers=0,trained=0;bool advanced=false;
            for(int tick=0;tick<19*60*20&&sim.Winner==-1;tick++)
            {
                if(tick%20==0)foreach(var c in bot.Decide(sim.View(1)))
                {
                    if(c.kind==CommandKind.AttackMove&&c.position.Distance(home)>12&&firstAttack<0)firstAttack=tick;
                    advanced|=c.kind==CommandKind.Research||c.kind==CommandKind.Build&&new[]{"hq","tech","extractor","defense"}.Contains(catalog[c.definition].role);
                    if(sim.Submit(c).accepted&&c.kind==CommandKind.Train&&catalog[c.definition].role!="worker")trained++;
                }
                sim.Step();var own=sim.Entities.Values.Where(e=>e.owner==1).ToArray();
                maxWorkers=Math.Max(maxWorkers,own.Count(e=>sim.Type(e).role=="worker")+own.Sum(e=>e.queue.Count(q=>!q.research&&catalog[q.definition].role=="worker")));
                maxSoldiers=Math.Max(maxSoldiers,own.Count(e=>!sim.Type(e).building&&sim.Type(e).role!="worker")+own.Sum(e=>e.queue.Count(q=>!q.research&&catalog[q.definition].role!="worker")));
                if(sim.Players.Any(p=>p.minerals<0||p.catalyst<0))throw new Exception("Practice economy invariant violated");
            }
            check(maxWorkers<=8&&maxSoldiers<=6,"Practice population caps including reservations: "+map.id+" / "+faction);
            check(trained>=3&&firstAttack==21600,"Practice AI grows and launches first small wave at eighteen minutes: "+map.id+" / "+faction);
            check(!advanced&&sim.Players[1].attackLevel==0&&sim.Players[1].armorLevel==0,"Practice AI stays basic: "+map.id+" / "+faction);
            var record=new{map=map.id,faction,firstAttackSeconds=firstAttack/20,maxWorkers,maxSoldiers,trained,seconds=sim.Tick/20};records.Add(record);Console.WriteLine(JsonSerializer.Serialize(record));
        }
        Directory.CreateDirectory(Path.Combine(root,"TestResults"));File.WriteAllText(Path.Combine(root,"TestResults/very-easy-practice.json"),JsonSerializer.Serialize(records));
    }
}
