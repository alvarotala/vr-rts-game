using System;
using System.Reflection;
using Frontier.Core;
using Frontier.Runtime;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

namespace Frontier.Editor
{
    public static class OnlinePlacementChecks
    {
        private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        private static object Call(object target,string method,params object[] args)=>target.GetType().GetMethod(method,Private).Invoke(target,args);
        private static void Property(object target,string name,object value)=>target.GetType().GetProperty(name).SetValue(target,value);
        private static void Field(object target,string name,object value)=>target.GetType().GetField(name,Private).SetValue(target,value);

        [MenuItem("Frontier/Multiplayer/Check Quest online placement")]
        public static void Run()
        {
            // Synthetic decoded views and head poses; no transport, anchor persistence,
            // or physical Quest APIs are exercised by this editor regression check.
            var fixture=new GameObject("Online placement checks");
            var boardObject=new GameObject("Board");boardObject.transform.SetParent(fixture.transform);
            Camera camera=null;QuestBridge quest=null;int checks=0;
            try
            {
                var session=fixture.AddComponent<GameSession>();if(session.Catalog==null)Call(session,"Awake");
                var board=boardObject.AddComponent<BoardView>();board.Session=session;
                quest=fixture.AddComponent<QuestBridge>();quest.Session=session;quest.Board=board;
                if(quest.Camera==null)Call(quest,"Awake");camera=quest.Camera;
                // Subscribe the real lifecycle handlers without restoring a device anchor.
                Property(quest,"Active",false);Call(quest,"Start");Property(quest,"Active",true);
                var authority=new Simulation(session.Map,session.Catalog,session.Faction,session.EnemyFaction);

                Reset();Call(quest,"TrackingLost");Call(quest,"TrackingAcquired");
                Check(!board.Placed&&!quest.Placing&&!session.Running&&!session.HasSystemPause,"Tracking recovery while waiting leaves placement to match start.");
                StartRemote();Check(quest.Placing&&session.IsPaused&&!board.Placed,"First server view requires placement immediately after a waiting-room tracking loss.");
                quest.CancelPlacement();Call(quest,"Update");
                Check(!quest.Placing&&session.IsPaused,"Cancel allows menus but cannot enable orders on an unconfirmed board or reopen placement every frame.");
                session.LeaveMatch();Check(!session.IsPaused,"Leaving releases the online placement input barrier.");

                Reset();board.Placed=false;Field(quest,"busy",true);StartRemote();
                Check(!quest.Placing&&session.IsPaused,"Anchor save in progress delays placement while local input remains paused.");
                Field(quest,"busy",false);Call(quest,"Update");
                Check(quest.Placing,"Placement retries when anchor saving finishes.");

                Reset();board.Placed=false;session.Pause("vrfocus",true);StartRemote();
                Check(!quest.Placing&&session.IsPaused,"Match start during lost focus waits for focus recovery.");
                session.Pause("vrfocus",false);Call(quest,"Update");
                Check(quest.Placing&&session.IsPaused,"Focus recovery opens placement and retains the local input barrier.");

                Reset();board.Placed=false;Property(quest,"Camera",null);StartRemote();
                Check(!quest.Placing&&session.IsPaused,"Match start without a usable head pose waits for tracking.");
                Property(quest,"Camera",camera);Call(quest,"Update");Check(quest.Placing,"A usable head pose resumes pending placement.");
                var endedDuringPlacement=authority.View(0);endedDuringPlacement.winner=1;Call(session,"OnlineState",endedDuringPlacement);Call(quest,"Update");
                Check(!quest.Placing&&!session.IsPaused,"A match ending during placement releases placement controls for the result menu.");

                Reset();StartRemote();Check(board.Placed&&!quest.Placing&&!session.IsPaused,"A confirmed board starts online without an extra placement step.");
                Reset();board.Placed=false;var terminal=authority.View(0);terminal.winner=1;Call(session,"OnlineState",terminal);
                Check(!quest.Placing&&!session.IsPaused,"A terminal result does not force board placement.");
                Reset();Property(session,"IsOnline",false);Property(session,"IsConnecting",false);Property(session,"Running",true);board.Placed=false;Call(quest,"Update");
                Check(!quest.Placing&&!session.IsPaused,"Offline lifecycle remains unchanged.");
                Debug.Log($"FRONTIER_ONLINE_PLACEMENT passed: {checks} checks; synthetic editor lifecycle, no physical Quest validation");

                void StartRemote()=>Call(session,"OnlineState",authority.View(0));
                void Reset()
                {
                    Property(quest,"Camera",camera);Field(quest,"busy",false);quest.CancelPlacement();session.LeaveMatch();
                    session.Pause("vrfocus",false);session.Pause("tracking",false);board.Placed=true;
                    Property(session,"IsOnline",true);Property(session,"IsConnecting",true);
                }
                void Check(bool valid,string message){if(!valid)throw new Exception(message);checks++;Debug.Log("ONLINE_PLACEMENT_PASS: "+message);}
            }
            finally
            {
                if(quest!=null)quest.CancelPlacement();
                if(boardObject!=null)Object.DestroyImmediate(boardObject);
                Object.DestroyImmediate(fixture);
                if(camera!=null)Object.DestroyImmediate(camera.gameObject);
            }
        }
    }
}
