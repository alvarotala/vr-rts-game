using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Frontier.Core;
using Frontier.Runtime;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Frontier.Editor
{
    public static class UnitFacingPreview
    {
        [MenuItem("Frontier/Check unit facing")]
        public static void Render()
        {
            int checks=0;
            void Check(bool condition,string label){if(!condition)throw new Exception(label);checks++;}
            const float frame=1f/72;
            var facing=new UnitFacing();var position=new Vector3(20,0,20);
            facing.Reset(position,Quaternion.identity,0);
            facing.FaceDestination(position,position+Vector3.right*10);
            var first=facing.Step(position,0,frame,false);
            Check(Quaternion.Angle(Quaternion.identity,first)>1,"MOVE does not start turning before the first movement tick");
            Check(Quaternion.Angle(Quaternion.identity,first)<=UnitFacing.TurnDegreesPerSecond*frame+.001f,"New MOVE snaps the model instead of turning");
            for(int i=1;i<18;i++)facing.Step(position,0,frame,false);
            Check(Vector3.Dot(facing.Rotation*Vector3.forward,Vector3.right)>.999f,"Stationary unit never faces its requested MOVE");
            var before=facing.Rotation;facing.FaceDestination(position,position+Vector3.left*10);
            var reversed=facing.Step(position,0,frame,false);
            Check(Quaternion.Angle(before,reversed)<=UnitFacing.TurnDegreesPerSecond*frame+.001f,"Reissued opposite order causes an abrupt turn");
            before=facing.Rotation;facing.Step(position,0,1,true);
            Check(Quaternion.Angle(before,facing.Rotation)<.001f,"Paused unit keeps turning");

            // Alternating avoidance offsets should not alternate the rendered model's yaw.
            facing.Reset(position,Quaternion.LookRotation(Vector3.right),0);
            float largestHeadingError=0;
            for(int tick=1;tick<=40;tick++)
            {
                position+=new Vector3(.2f,0,tick%2==0?.1f:-.1f);
                facing.Step(position,tick,.05f,false);
                if(tick>10)largestHeadingError=Mathf.Max(largestHeadingError,Vector3.Angle(facing.Rotation*Vector3.forward,Vector3.right));
            }
            Check(largestHeadingError<10,"Small alternating avoidance steps make the model zigzag");
            for(int tick=41;tick<=70;tick++){position+=Vector3.forward*.2f;facing.Step(position,tick,.05f,false);}
            Check(Vector3.Dot(facing.Rotation*Vector3.forward,Vector3.forward)>.995f,"Smoothed orientation ignores a real route corner");
            before=facing.Rotation;
            for(int tick=71;tick<=80;tick++)facing.Step(position,tick,.05f,false);
            Check(Quaternion.Angle(before,facing.Rotation)<.001f,"Stopped unit drifts toward a stale direction");
            facing.Reset(position,Quaternion.identity,100);
            Check(Quaternion.Angle(facing.Rotation,Quaternion.identity)<.001f,"A reused model inherits its previous unit's orientation");
            before=facing.Rotation;facing.Step(position,100,frame,false,Vector3.left);
            Check(Quaternion.Angle(before,facing.Rotation)>1&&Quaternion.Angle(before,facing.Rotation)<=5.001f,"Combat aiming snaps rather than turns");

            Quaternion TurnAt(int frames)
            {
                var sample=new UnitFacing();sample.Reset(Vector3.zero,Quaternion.identity,0);sample.FaceDestination(Vector3.zero,Vector3.right);
                for(int i=0;i<frames;i++)sample.Step(Vector3.zero,0,.2f/frames,false);
                return sample.Rotation;
            }
            Check(Quaternion.Angle(TurnAt(15),TurnAt(30))<.001f,"Facing speed depends on display frame rate");

            // Artillery still moves east for four ticks while braking an order west.
            // Render at 72 Hz between those 20 Hz samples: yaw must not bounce back east.
            var reversing=new UnitFacing();var reversePosition=Vector3.zero;
            reversing.Reset(reversePosition,Quaternion.LookRotation(Vector3.right),0);
            for(int tick=1;tick<=5;tick++){reversePosition+=Vector3.right*.125f;reversing.Step(reversePosition,tick,.05f,false);}
            reversing.FaceDestination(reversePosition,reversePosition+Vector3.left*10);
            float velocityX=2.5f,priorError=180;int physicsTick=5;
            bool monotonic=true,bounded=true;
            for(int i=1;i<=50;i++)
            {
                int nextTick=5+Mathf.FloorToInt(i*frame*Simulation.TickRate+.00001f);
                while(physicsTick<nextTick)
                {
                    velocityX=Mathf.MoveTowards(velocityX,-2.5f,(velocityX>0?12:8)/(float)Simulation.TickRate);
                    reversePosition.x+=velocityX/Simulation.TickRate;physicsTick++;
                }
                var old=reversing.Rotation;reversing.Step(reversePosition,physicsTick,frame,false);
                float error=Quaternion.Angle(reversing.Rotation,Quaternion.LookRotation(Vector3.left));
                monotonic&=error<=priorError+.01f;priorError=error;
                bounded&=Quaternion.Angle(old,reversing.Rotation)<=UnitFacing.TurnDegreesPerSecond*frame+.01f;
            }
            Check(monotonic,"Reverse MOVE turns back toward residual momentum before braking finishes");
            Check(bounded&&priorError<.1f,"Reverse MOVE fails to finish a smooth turn toward its new destination");

            // A wall can require continuing west even after an eastward order. The
            // braking grace period must end and allow the real route to control facing.
            var detour=new UnitFacing();var detourPosition=Vector3.zero;
            detour.Reset(detourPosition,Quaternion.LookRotation(Vector3.left),0);
            detourPosition.x-=.2f;detour.Step(detourPosition,1,.05f,false);
            detour.FaceDestination(detourPosition,detourPosition+Vector3.right*10);
            for(int tick=2;tick<=30;tick++){detourPosition.x-=.2f;detour.Step(detourPosition,tick,.05f,false);}
            Check(Vector3.Dot(detour.Rotation*Vector3.forward,Vector3.left)>.995f,"Reverse-facing grace period ignores a legitimate obstacle detour indefinitely");

            // Exercise the accepted-command event and BoardView integration as well as the helper.
            const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
            var sessionObject=new GameObject("Facing check session");var boardObject=new GameObject("Facing check board");
            var bounds=Shader.GetGlobalVector("_FrontierBounds");var matrix=Shader.GetGlobalMatrix("_FrontierWorldToBoard");
            void Invoke(object value,string method)=>value.GetType().GetMethod(method,flags).Invoke(value,null);
            try
            {
                var session=sessionObject.AddComponent<GameSession>();if(session.Catalog==null)Invoke(session,"Awake");session.StartMatch();
                var board=boardObject.AddComponent<BoardView>();board.Session=session;Invoke(board,"Start");Invoke(board,"LateUpdate");
                var worker=session.View.entities.First(e=>e.owner==0&&!session.Catalog[e.definition].building);
                session.Select(worker.id,false);int events=0;Point destination=default;
                session.UnitMoveOrdered+=(id,target)=>{Check(id==worker.id,"Move intent exposed a unit outside the local selection");events++;destination=target;};
                var pending=(Dictionary<int,Point>)typeof(BoardView).GetField("pendingMoveFacing",flags).GetValue(board);
                Check(session.Send(CommandKind.Move,position:new Point(25.5f,20.5f)),"Facing check MOVE was rejected");
                Check(events==1&&pending.ContainsKey(worker.id),"Accepted MOVE did not reach BoardView immediately");
                Check(destination.Distance(session.Match.Entities[worker.id].destination)<.001f,"Facing intent ignores the unit's assigned formation slot");
                Check(session.Match.Tick==0,"Facing command advances the simulation");
                Invoke(board,"LateUpdate");Check(!pending.ContainsKey(worker.id),"BoardView never consumes facing intent");
                Check(!session.Send(CommandKind.Move,position:new Point(-5,-5))&&events==1,"Rejected MOVE changes unit facing");
                Check(session.Send(CommandKind.AttackMove,position:new Point(24.5f,20.5f))&&events==2,"Attack Move does not publish local facing intent");
                Check(session.Send(CommandKind.Move,position:new Point(25.5f,20.5f)),"Queued facing setup rejected");
                int beforeQueue=events;
                Check(session.Send(CommandKind.Move,position:worker.position,append:true)&&events==beforeQueue,"Future waypoint turns the unit before activation");
                int nextSequence=session.Match.Entities[worker.id].pendingOrders[0].sourceSequence;
                Invoke(board,"LateUpdate");
                for(int i=0;i<800&&session.Match.Entities[worker.id].activeOrderSequence!=nextSequence;i++)
                {typeof(GameSession).GetField("accumulator",flags).SetValue(session,.05f);Invoke(session,"Update");}
                Check(session.Match.Entities[worker.id].activeOrderSequence==nextSequence,"Queued reverse waypoint never activates");
                Check(events==beforeQueue+1&&pending.ContainsKey(worker.id),"Queued waypoint activation waits for displacement before publishing heading");
                Check(destination.Distance(session.Match.Entities[worker.id].destination)<.001f,"Queued heading ignores its formation destination");
                Invoke(session,"RefreshView");Check(events==beforeQueue+1,"Refreshing the same active waypoint repeats heading intent");
                session.LeaveMatch();Check(pending.Count==0,"Facing intent survives leaving the match");
                Debug.Log($"FRONTIER_FACING_CHECKS passed: {checks} checks; immediate MOVE intent, bounded turns, avoidance filtering, real corners, idle/pause/pool reset, combat aim, frame rate, accepted/rejected commands, exit; no player build");
            }
            finally
            {
                Object.DestroyImmediate(boardObject);Object.DestroyImmediate(sessionObject);
                Shader.SetGlobalVector("_FrontierBounds",bounds);Shader.SetGlobalMatrix("_FrontierWorldToBoard",matrix);
            }
        }
    }
}
