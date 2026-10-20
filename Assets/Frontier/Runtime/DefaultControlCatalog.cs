using System;
using System.Collections.Generic;

namespace Frontier.Runtime
{
    // Shared guide data. Gesture labels are resolved from the same profile as the input router.
    public static class DefaultControlCatalog
    {
        public const int SchemaVersion=1;
        public sealed class Command
        {
            public string Id { get; }
            public string Name { get; }
            public string Gesture { get; }
            public string Description { get; }
            public Command(string id,string name,string gesture,string description)
            {Id=id;Name=name;Gesture=gesture;Description=description;}
        }
        public sealed class Topic
        {
            public string Name { get; }
            public IReadOnlyList<Command> Commands { get; }
            public Topic(string name,params Command[] commands){Name=name;Commands=Array.AsReadOnly(commands);}
        }
        private static readonly IReadOnlyList<Topic> Touch=Array.AsReadOnly(Create(true)),Desktop=Array.AsReadOnly(Create(false));
        public static IReadOnlyList<Topic> For(bool touch)=>touch?Touch:Desktop;
        public static IReadOnlyList<Topic> For(bool touch,HotkeyBindings bindings)=>Array.AsReadOnly(Create(touch,bindings));

        private static Topic[] Create(bool touch,HotkeyBindings bindings=null)
        {
            bindings=bindings??new HotkeyBindings();
            string select=bindings.Name(touch,"select"),order=bindings.Name(touch,"order"),shift=bindings.Name(touch,"add"),back=bindings.Name(touch,"back");
            string wheel=touch?bindings.Name(true,"wheel"):"",pause=touch?back:bindings.Name(false,"pause");
            string groups=touch?"":string.Join(" / ",new[]{bindings.Name(false,"group1"),bindings.Name(false,"group2"),bindings.Name(false,"group3"),bindings.Name(false,"group4")});
            string click=touch?"Trigger / "+bindings.MenuConfirmName:"Left-click";
            string stick=bindings.HandName.ToLowerInvariant()+" stick";
            return new[]{
                new Topic("Selection",
                    new Command("selection.single","Select an item",select+" on map / "+click+" on icon","Select an allied unit, building or resource. A selection icon isolates that item."),
                    new Command("selection.area","Select an area","Hold "+select+" + drag","Enclose units in the flat rectangle. The drag can start outside the map."),
                    new Command("selection.add","Add to selection",shift+" + "+select,"Keep your selection and add more units, including by dragging an area."),
                    new Command("selection.remove","Remove one item",shift+" + hold "+select+" / hold icon","Map: hold still with "+shift+" for 0.5 s. Selection icon: hold for 0.65 s, without "+shift+"."),
                    new Command("selection.same-type","Select the same type","Map: double "+select+" · Icon: double click","Map: same-type units/buildings within 12 cells. Icon: same type from selection. "+shift+" adds on map."),
                    new Command("selection.toggle","Toggle one item",shift+" + short "+select+" on an ally","Add or remove one allied unit or building. Area selection with the modifier only adds units."),
                    new Command("selection.clear","Clear selection",select+" on empty terrain","Clear the current selection without the modifier. Finish or cancel an armed action first."),
                    new Command("selection.hover","Preview your target","Point at a unit, building or resource","The hover highlight identifies your target; the point on the map shows where you are aiming.")),
                new Topic("Orders",
                    new Command("orders.move","Move",order+" on terrain","Selected units move to that location using their movement and collision rules."),
                    new Command("orders.attack","Attack a target",order+" on a visible enemy","Focus attacks on that enemy. Double "+order+" over an enemy also attacks directly."),
                    new Command("orders.attack-move","Attack Move","Double "+order+" / Attack Move action","Double "+order+" issues at the cursor immediately. Menu action: choose a destination."),
                    new Command("orders.gather","Gather resources","Worker selected · "+order+" on minerals / gas","Assign workers automatically among nearby deposits. Gas needs a completed extractor."),
                    new Command("orders.repair","Repair / resume construction","Worker · "+order+" on damaged ally / construction","Both factions: repair allies with minerals or resume construction. Keep a worker on site."),
                    new Command("orders.queue","Queue orders",shift+" + "+order+" / double "+order,"Append movement, work or attack orders. Without "+shift+", a new order replaces the plan."),
                    new Command("orders.stop","Stop","Stop action","Clear current and queued orders and brake. Units can still engage nearby enemies."),
                    new Command("orders.hold","Hold Position","Hold Position action","Clear orders and keep units in place. Attack in range without chasing; resist allied pushes.")),
                new Topic("Menus",
                    new Command("menus.actions",touch?"Open unified wheel":"Open unit actions",touch?"Tilt "+stick+" from center":"Select units or a building",touch?"A small controller hint stays visible. Tilt to open; idle at center for 0.8 s to close.":"Use the selected item's actions. Workers have Build; producers have Train and Research."),
                    new Command("menus.choose-action",touch?"Choose a wheel category":"Choose a menu action",touch?"Up Groups · Right Selection · Down Assign · Left Orders":"Point at an action button",touch?"Tilt to enter. Keep the stick tilted across levels; press Trigger for each step.":"Available actions follow your selection. Hover to preview the button before clicking."),
                    new Command("menus.execute-action","Execute an action",touch?"Trigger in the wheel":"Left-click the action","Execute the highlighted action. Building and Attack Move then need a point on the map."),
                    new Command("menus.back-action",touch?"Back in the wheel":"Cancel a pending action",touch?"Down + Trigger / "+bindings.MenuBackName:back,touch?bindings.MenuBackName+" goes back or closes the root. Down returns from actions; in group pages it chooses group 3.":"Cancel an armed build or Attack Move, or open pause. Escape goes back inside menus."),
                    new Command("menus.activate","Use menu buttons",click,"Press and release on the same button. Empty menu areas never send orders through to the map."),
                    new Command("menus.pause","Pause / resume",touch?back+" / Pause button":back+" / "+pause+" / Pause button","Cancel an armed action first, or pause. Menus: "+(touch?bindings.MenuBackName:"Escape")+" goes back; Resume continues the match."),
                    new Command("menus.leave","Return to main menu","Pause > Return to main menu","Confirm Leave match to end the current match. Keep playing returns to your army."),
                    new Command("menus.match","Start / restart a match","New Match / Rematch","Set faction, opponent, difficulty and map before Start Match. Rematch is available after a match ends.")),
                new Topic("Groups",
                    new Command("groups.recall","Recall a group",touch?"Wheel up > Groups > Trigger":groups,touch?"Enter Groups, choose a direction and press Trigger. Centering never recalls a group.":"Replace the current selection with the surviving members of the chosen control group."),
                    new Command("groups.choose","Choose a group",touch?"1 up · 2 right · 3 down · 4 left":"Group 1 / 2 / 3 / 4 buttons",touch?"Tilt to choose. Trigger recalls in Groups, or replaces members in Assign group.":"The four buttons show how many members are currently assigned."),
                    new Command("groups.confirm-recall","Confirm recall",touch?"Trigger on a group":"Tap a Group button","Recall the group's surviving members. Empty groups show an empty message."),
                    new Command("groups.replace","Replace a group",touch?"Wheel down > Assign group > Trigger":bindings.Name(false,"save")+" + "+groups,touch?"Replace all members with your selection. Also in Groups: hold "+wheel+" + tilt; release.":"Replace ALL previous members with your current selection. This does not merge selections."),
                    new Command("groups.cancel","Cancel group choice",touch?bindings.MenuBackName:"Release mouse outside the Group button",touch?"Return to the root without recalling or saving. Centering for 0.8 s closes the wheel.":"A button press released outside its target does not recall or replace a group."),
                    new Command("groups.panel-save","Save from the panel","Hold a Group button (over 0.65 s)","Replace that group's members with your current selection. Tap instead to recall it."),
                    new Command("groups.lifetime","Group lifetime","Applies to all four groups","Only your units and buildings can belong to groups. Dead members drop out; a new match clears groups.")),
                new Topic("Production",
                    new Command("production.build","Build a structure","Worker > Build > choose a building","Choose a valid foundation with "+select+" / "+order+". Keep a worker on site until complete. "+back+" cancels placement."),
                    new Command("production.train","Train a unit","Production building > Train","Spend resources and reserve supply to add a unit to the building's production queue."),
                    new Command("production.research","Research an upgrade","Technology building > Research","Queue a faction-wide attack or armor upgrade. Requirements and costs appear in the action help."),
                    new Command("production.cancel-item","Cancel one queued item",click+" on its production card","The card turns red on hover. Cancel immediately and refund its cost; other items keep their order."),
                    new Command("production.cancel-last","Cancel / refund","Cancel / refund action","Remove the last queued item, or cancel unfinished construction with a partial refund."),
                    new Command("production.rally","Set a Rally Point","Select producer(s) · "+order+" on terrain","Newly trained units move to the cyan flag. Existing units keep their orders; workers move, not gather."),
                    new Command("production.clear-rally","Clear a Rally Point","Clear Rally Point action","Restore default exits. New workers auto-gather again; production stays queued."),
                    new Command("production.details","Read action details","Hover over an action for 1.5 s","See what the unit, building or upgrade does, along with its cost, timing and requirements.")),
                touch?new Topic("Board",
                    new Command("board.move-menu","Move the menu","Hold Trigger / "+bindings.MenuConfirmName+" on MOVE MENU + drag","Place the menu wherever it is comfortable. This does not move the battlefield."),
                    new Command("board.menu-distance","Adjust menu distance",bindings.HandName+" stick up / down while dragging menu","Bring the grabbed menu closer or move it farther away."),
                    new Command("board.reset-menu","Recover the menu","Reset position button","Return the menu above the board."),
                    new Command("board.reposition","Reposition the board","Settings > Reposition board","Enter placement. The confirmed battlefield stays fixed while you walk around it."),
                    new Command("board.place","Place the board","Point at a surface + "+select,"Confirm the board position. Table / Floor and Detect surfaces provide placement options."),
                    new Command("board.size-rotation","Placement size / rotation",bindings.HandName+" stick while placing","Up / down changes size; left / right rotates. These axes do not navigate the map during play."),
                    new Command("board.height","Placement height",shift+" + "+stick+" up / down","Adjust the board's height during placement. The other controller is unused."),
                    new Command("board.cancel-placement","Cancel board placement",back+" while placing","Restore the previously confirmed board position and size.")):
                new Topic("Board",
                    new Command("board.pan","Pan the map",string.Join(" / ",new[]{bindings.Name(false,"pan-up"),bindings.Name(false,"pan-left"),bindings.Name(false,"pan-down"),bindings.Name(false,"pan-right")}),"Move the desktop development view across the battlefield."),
                    new Command("board.zoom","Zoom the map","Mouse wheel","Change the desktop map scale. Quest keeps the placed board fixed."),
                    new Command("board.fit","See the full map",bindings.Name(false,"fit")+" / Full map button","Reset the desktop view to show the whole battlefield."),
                    new Command("board.reset-menu","Recover the menu",bindings.Name(false,"menu")+" / Reset position","Return the menu above the board."),
                    new Command("board.move-menu","Move the menu","Drag MOVE MENU with left mouse button","Move the console without changing the board position."),
                    new Command("board.menu-distance","Adjust menu distance","Mouse wheel while dragging menu","Bring the grabbed console closer or move it farther away."),
                    new Command("board.place","Place or reposition the board","Settings / New Match > Place board","Point and use "+select+" to confirm. Table / Floor set the manual height."),
                    new Command("board.cancel-placement","Cancel board placement",back+" / "+pause+" while placing","Restore the previous board position and size."))
            };
        }
    }
}
