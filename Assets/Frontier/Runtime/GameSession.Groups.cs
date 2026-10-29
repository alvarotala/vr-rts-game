using System.Linq;
using Frontier.Core;

namespace Frontier.Runtime
{
    public sealed partial class GameSession
    {
        public void Group(int index, bool save)
        {
            if(IsCountingDown)return;
            if(View==null||index<0||index>=groups.Length)return;
            if(IsTutorial && (!CanIssueOrders || index!=0 || (save?Tutorial.Step!=TutorialStep.SaveGroup:Tutorial.Step!=TutorialStep.RecallGroup)))
            { TutorialRejectOrder(); return; }
            if (save) { groups[index].Clear(); groups[index].AddRange(Selection.Where(OwnGroupMember)); Announce("Group " + (index + 1) + " saved."); }
            else { Selection.Clear(); SelectedResource = 0; foreach (int id in groups[index]) if (OwnGroupMember(id)) Selection.Add(id);
                Announce("Group " + (index + 1) + (Selection.Count>0?" selected.":" is empty."),Selection.Count==0); }
            if(IsTutorial)
            {
                Tutorial.NotifyGroup(index,save); UpdateTutorial();
                if(save && Tutorial.Step==TutorialStep.RecallGroup)Selection.Clear();
            }
            ValidateAbilitySelection();Feedback?.Invoke(save||Selection.Count>0);Changed?.Invoke();
        }
        private bool OwnGroupMember(int id)=>View!=null&&View.entities.Any(e=>e.id==id&&e.owner==View.player&&!e.remembered&&e.health>0);
        public int GroupSize(int index) => index>=0&&index<groups.Length?groups[index].Count(OwnGroupMember):0;
    }
}
