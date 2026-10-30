#if FRONTIER_BROWSER
using System.Collections.Generic;

namespace Frontier.Runtime
{
    public sealed partial class GameSession
    {
        // The lasso supplies screen-tested IDs, but the session still validates
        // the current filtered view before changing selection in one operation.
        public bool SelectVisibleEntities(IEnumerable<int> entityIds)
        {
            if(!CanIssueOrders||View==null||entityIds==null)return false;
            var requested=new HashSet<int>(entityIds);
            Selection.Clear();SelectedResource=0;
            foreach(var entity in View.entities)
                if(requested.Contains(entity.id)&&entity.owner==LocalPlayer&&!entity.remembered&&entity.health>0)Selection.Add(entity.id);
            rallySelection=null;BuildDefinition=null;AttackMoveMode=false;AbilityDefinitionId=null;
            ValidateAbilitySelection();UpdateTutorial();Feedback?.Invoke(true);Changed?.Invoke();return true;
        }
    }
}
#endif
