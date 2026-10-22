using TMPro;
using System;
using System.IO;
using System.Linq;
using Frontier.Core;
using Frontier.Runtime;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Text = TMPro.TMP_Text;

namespace Frontier.Editor
{
    public static class ReplayInputChecks
    {
        [MenuItem("Frontier/Check replay input and controller hints")]
        public static void Run()
        {
            string path=Path.Combine(Path.GetTempPath(),"frontier-replay-input-"+Guid.NewGuid().ToString("N"));
            try
            {
                using(var f=new UnifiedWheelFixture("ReplayInput"))
                {
                    var store=new MatchArchiveStore(path);var source=new Simulation(f.Session.Map,f.Session.Catalog);
                    var writer=store.Begin(source,"easy");writer.Flush();
                    for(int i=0;i<Simulation.TickRate*90;i++)source.Step();
                    source.Submit(new GameCommand{player=1,kind=CommandKind.Surrender});writer.Finish();
                    var files=Directory.GetFiles(path,"*",SearchOption.AllDirectories).ToDictionary(p=>p,File.ReadAllText);
                    typeof(GameSession).GetField("history",UnifiedWheelFixture.Private).SetValue(f.Session,store);
                    f.Board.Placed=true;
                    f.Check(f.Session.StartReplay(store.List().Single().Id),"Replay opens from a stored recording for controller checks");
                    f.Check(f.Panel.AcceptsReplayInput&&!f.Panel.AcceptsGameInput,"Replay accepts transport input and blocks gameplay routing");
                    f.Session.Replay.Paused=true;f.Session.SetReplaySpeed(1);
                    Apply(Vector2.zero);Apply(Vector2.right);
                    f.Check(f.Session.Replay.Seeking&&f.Session.Replay.SeekTarget==Simulation.TickRate*15,"A fresh right tilt seeks forward fifteen seconds");
                    Apply(Vector2.right);Apply(Vector2.left);
                    f.Check(f.Session.Replay.SeekTarget==Simulation.TickRate*15,"Holding or crossing directions cannot repeatedly seek without returning to center");
                    Apply(Vector2.zero);Apply(Vector2.left);
                    f.Check(f.Session.Replay.SeekTarget==0,"Center then left adjusts the active seek target instead of stale displayed time");
                    while(f.Session.Replay.Seeking)f.Session.Replay.Advance(0,120);
                    Apply(Vector2.zero);Apply(Vector2.up);Apply(Vector2.up);
                    f.Check(f.Session.Replay.Speed==2,"One up tilt changes playback speed once while held");
                    Apply(Vector2.zero);Apply(Vector2.down);Apply(Vector2.zero);Apply(Vector2.down);Apply(Vector2.zero);Apply(Vector2.down);
                    f.Check(f.Session.Replay.Speed==.5f,"Down steps through playback speeds and clamps at half speed");
                    bool paused=f.Session.Replay.Paused;Apply(Vector2.zero,play:true);
                    f.Check(f.Session.Replay.Paused!=paused,"The replay action button toggles play and pause");Apply(Vector2.zero,play:true);
                    float zoom=f.Board.Zoom;Apply(Vector2.zero,play:true,grip:true);
                    f.Check(f.Board.Zoom>zoom&&f.Session.Replay.Paused==paused,"Grip plus action zooms without changing playback pause");
                    Vector2 pan=f.Board.Pan;Apply(Vector2.right,grip:true,delta:1);
                    f.Check(f.Board.Pan!=pan&&f.Session.Replay.Paused==paused,"Grip and stick pan the board even while the replay is paused");
                    int target=f.Session.Replay.SeekTarget;Apply(Vector2.right);
                    f.Check(f.Session.Replay.SeekTarget==target,"Releasing grip with a tilted stick does not accidentally seek");
                    zoom=f.Board.Zoom;Apply(Vector2.zero,back:true,grip:true);
                    f.Check(f.Board.Zoom<zoom&&f.Session.IsReplay,"Grip plus back zooms out without exiting the replay");
                    Apply(Vector2.zero,zoom:.3f,touch:false);pan=f.Board.Pan;Apply(Vector2.zero,move:Vector2.up,delta:1,touch:false);
                    f.Check(f.Board.Pan!=pan,"Desktop navigation remains available during paused replay");
                    f.Wheel.TickHint(f.Ray,true);
                    f.Check(f.Wheel.HintVisible&&!f.Wheel.CanOpen,"Replay controller labels are visible without enabling the command wheel");
                    Apply(Vector2.zero,hint:true);f.Wheel.TickHint(f.Ray,true);
                    f.Check(!f.Wheel.HintVisible&&!f.Wheel.HintsEnabled,"Stick click hides replay hints");
                    Apply(Vector2.zero,hint:true);
                    foreach(bool left in new[]{false,true})
                    {
                        f.Panel.Bindings.SelectHand(left);f.Wheel.TickHint(f.Ray,true);
                        var labels=f.Wheel.GetComponentsInChildren<Text>().Select(t=>t.text).ToArray();
                        f.Check(labels.Contains("↑↓ Speed")&&labels.Contains("→ +15 seconds")&&labels.Contains("← −15 seconds"),"Directional hints describe replay transport for "+(left?"left":"right")+" hand");
                        f.Check(labels.Any(s=>s.StartsWith((left?"X":"A")+" · Play / pause"))&&labels.Any(s=>s.StartsWith((left?"Y":"B")+" · Exit replay")),"Physical action/back labels match the active hand");
                        f.Check(!labels.Any(s=>s.Contains("Attack Move")||s.Contains("Groups")||s.Contains("Selection")),"Replay hints contain no gameplay commands");
                        f.Capture(left?"left-replay-hints":"right-replay-hints","hintRoot",1700,700);
                    }
                    f.Panel.Bindings.Assign(true,"order","touch:trigger",true,out _);f.Wheel.TickHint(f.Ray,true);
                    f.Check(f.Wheel.GetComponentsInChildren<Text>().Any(t=>t.text.StartsWith("X · Play / pause")),"Rebinding gameplay commands never changes fixed replay controls");
                    float speed=f.Session.Replay.Speed;bool wasPaused=f.Session.Replay.Paused;
                    f.Session.Pause("focus",true);Apply(Vector2.zero,play:true);Apply(Vector2.up);f.Wheel.TickHint(f.Ray,true);
                    f.Check(f.Session.Replay.Speed==speed&&f.Session.Replay.Paused==wasPaused&&!f.Wheel.HintVisible,"System interruption blocks replay transport and controller hints");
                    f.Session.Pause("focus",false);
                    f.Panel.Back();f.Check(!f.Session.IsReplay&&f.Panel.Page==CommandPanel.MenuPage.History,"Back exits playback to history");
                    f.Check(files.All(pair=>File.ReadAllText(pair.Key)==pair.Value)&&Directory.GetFiles(path,"*",SearchOption.AllDirectories).Length==files.Count,"Spectator controls never alter the saved recording or create new archives");
                    f.Report("REPLAY_INPUT");

                    void Apply(Vector2 stick,bool play=false,bool back=false,bool grip=false,bool hint=false,Vector2 move=default,float zoom=0,float delta=.05f,bool touch=true)
                        =>UnifiedWheelFixture.Call(f.Input,"ApplyReplayControls",stick,play,back,grip,hint,move,zoom,delta,touch);
                }
            }
            finally{if(Directory.Exists(path))Directory.Delete(path,true);}
        }
    }
}
