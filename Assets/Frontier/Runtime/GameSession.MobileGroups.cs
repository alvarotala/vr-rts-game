#if FRONTIER_BROWSER
using System;
using System.Linq;
using Frontier.Core;

namespace Frontier.Runtime
{
    public sealed partial class GameSession
    {
        // Group membership is still owned by the shared session. Touch only adds
        // an explicit create/replace flow, without silently discarding a slot.
        public bool CanSaveMobileGroup=>CanIssueOrders&&Selection.Count>0&&Selection.All(OwnGroupMember)
            &&(!IsTutorial||Tutorial.Step==TutorialStep.SaveGroup);

        public int SaveMobileSelectionGroup()
        {
            if(!CanSaveMobileGroup)return -1;
            // Saving the same live squad twice reuses its existing icon.
            if(!IsTutorial)
                for(int slot=0;slot<groups.Length;slot++)
                    if(MobileGroupMatchesSelection(slot)){CancelMode();return slot;}
            for(int slot=0;slot<groups.Length;slot++)
            {
                if(IsTutorial&&slot!=0)continue;
                if(GroupSize(slot)!=0)continue;
                return ReplaceMobileSelectionGroup(slot)?slot:-1;
            }
            return -1;
        }

        public bool ReplaceMobileSelectionGroup(int slot)
        {
            if(!CanSaveMobileGroup||slot<0||slot>=groups.Length||IsTutorial&&slot!=0)return false;
            CancelMode();
            Group(slot,true);
            return true;
        }

        public bool RecallMobileGroup(int slot)
        {
            if(!CanIssueOrders||slot<0||slot>=groups.Length||GroupSize(slot)==0
                ||IsTutorial&&(slot!=0||Tutorial.Step!=TutorialStep.RecallGroup))return false;
            CancelMode();
            Group(slot,false);
            return true;
        }

        public bool MobileGroupMatchesSelection(int slot)=>slot>=0&&slot<groups.Length&&GroupSize(slot)>0
            &&Selection.SetEquals(groups[slot].Where(OwnGroupMember));

        public string MobileGroupDefinition(int slot)
        {
            if(slot<0||slot>=groups.Length||View==null)return null;
            // Deterministic representative for mixed squads, using only live,
            // visible members. Hidden enemies can never enter an icon or count.
            return View.entities.Where(e=>groups[slot].Contains(e.id)&&OwnGroupMember(e.id))
                .GroupBy(e=>e.definition).OrderByDescending(g=>g.Count())
                .ThenBy(g=>g.Key,StringComparer.Ordinal).Select(g=>g.Key).FirstOrDefault();
        }
    }
}
#endif
