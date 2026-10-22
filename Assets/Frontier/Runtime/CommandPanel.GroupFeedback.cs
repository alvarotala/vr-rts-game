using System;
using System.Linq;
using UnityEngine;

namespace Frontier.Runtime
{
    public sealed partial class CommandPanel
    {
        private const float GroupConfirmationDuration=1.2f;
        private readonly float[] groupConfirmedUntil=new float[GameSession.ControlGroupCount];
        private Target groupGestureTarget;
        private bool groupGestureAssign;
        private object groupGestureMatch,groupConfirmationMatch;
        private int[] groupGestureSelection=Array.Empty<int>();

        private void CreateGroupFeedback(int index,Target target)
        {
            // Group assignment is a modifier click, never a duration-based action.
            target.hold=null;
        }

        private void BeginGroupGesture(Target target,bool assign)
        {
            ResetGroupGesture();
            int index=target==null?-1:Array.IndexOf(groupTargets,target);
            if(index<0)return;
            if(!AcceptsGameInput||Quest.Placing){if(pressed==target)pressed=null;return;}
            groupConfirmedUntil[index]=0;
            groupGestureTarget=target;groupGestureAssign=assign;
            groupGestureMatch=Session.SessionIdentity;groupGestureSelection=Session.Selection.ToArray();
            RenderGroupFeedback();
        }

        private bool GroupGestureValid()=>groupGestureTarget!=null&&pressed==groupGestureTarget&&hovered==groupGestureTarget&&AcceptsGameInput&&!Quest.Placing
            &&groupGestureTarget.rect.gameObject.activeInHierarchy&&groupGestureMatch==Session.SessionIdentity
            &&Session.Selection.SetEquals(groupGestureSelection);

        private bool HandleGroupGesture(Target hit,bool up)
        {
            if(groupGestureTarget==null)return false;
            var target=groupGestureTarget;
            if(hit!=target||!GroupGestureValid())
            {
                if(pressed==target)pressed=null;
                ResetGroupGesture();return true;
            }
            if(up)
            {
                // The modifier belongs to the press. Changing it before release
                // cannot turn a recall into an assignment, or the reverse.
                bool assign=groupGestureAssign;int index=Array.IndexOf(groupTargets,target);
                pressed=null;ResetGroupGesture();
                if(assign)AssignControlGroup(index);
                else
                {
                    Session.GetComponent<AudioFeedback>()?.Navigation(true);
                    target.click?.Invoke();
                }
            }
            return true;
        }

        private void UpdateGroupFeedback()
        {
            if(groupConfirmationMatch!=Session.SessionIdentity)
            {
                Array.Clear(groupConfirmedUntil,0,groupConfirmedUntil.Length);
                groupConfirmationMatch=Session.SessionIdentity;
            }
            if(groupGestureTarget!=null&&!GroupGestureValid())
            {
                if(pressed==groupGestureTarget)pressed=null;
                ResetGroupGesture();
            }
            RenderGroupFeedback();
        }

        private void AssignControlGroup(int index)
        {
            if(!AcceptsGameInput||Quest.Placing||index<0||index>=groupTargets.Length)return;
            Session.Group(index,true); // Existing Feedback event drives the active controller's bounded haptic pulse.
            groupConfirmationMatch=Session.SessionIdentity;
            groupConfirmedUntil[index]=Time.unscaledTime+GroupConfirmationDuration;
            Session.GetComponent<AudioFeedback>()?.Navigation(true);
            RenderGroupFeedback();
        }

        private void ResetGroupGesture()
        {
            groupGestureTarget=null;groupGestureAssign=false;groupGestureMatch=null;groupGestureSelection=Array.Empty<int>();
            RenderGroupFeedback();
        }

        private void RenderGroupFeedback()
        {
            if(Session==null)return;
            for(int index=0;index<groupTargets.Length;index++)
            {
                var target=groupTargets[index];if(target==null||target.image==null)continue;
                bool confirmed=groupConfirmationMatch==Session.SessionIdentity&&Time.unscaledTime<groupConfirmedUntil[index];
                target.image.color=confirmed?QueueActiveTint:hovered==target?ConsoleTheme.Focus:target.normal;
                int count=Session.GroupSize(index);
                groupCounts[index].text=confirmed?"Saved · "+count:count==0?"Empty":count+" assigned";
            }
        }
    }
}
