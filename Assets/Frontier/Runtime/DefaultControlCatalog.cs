using System;
using System.Collections.Generic;

namespace Frontier.Runtime
{
    // Reference data for the CURRENT input router, not an active rebinding profile.
    // Stable IDs are independent of labels and physical buttons, so future overrides
    // can target commands without depending on their position in a menu.
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

        private static Topic[] Create(bool touch)
        {
            string select=touch?"Trigger":"Left-click",order=touch?"A":"Right-click",shift=touch?"Grip":"Shift",back=touch?"B":"Escape";
            string click=touch?"Trigger / A":"Left-click";
            return new[]{
                new Topic("Selection",
                    new Command("selection.single","Select an item",select+" on an item","Select an allied unit, building or resource to see its information and available actions."),
                    new Command("selection.area","Select an area",touch?"Hold Trigger + drag":"Hold left mouse button + drag","Enclose units in the flat rectangle. The drag can start outside the map."),
                    new Command("selection.add","Add to selection",shift+" + "+select,"Keep your selection and add more units, including by dragging an area."),
                    new Command("selection.remove","Remove one item",shift+" + hold "+(touch?"Trigger":"left click")+" still (0.5 s)","Point at a selected unit or building and hold still to remove it from the selection."),
                    new Command("selection.same-type","Select the same type",touch?"Double Trigger":"Double left-click","Select allied units of the same type within 12 map cells. Hold "+shift+" to keep other selected units."),
                    new Command("selection.toggle","Toggle one item",shift+" + short "+select+" on an ally","Add or remove one allied unit or building. Area selection with the modifier only adds units."),
                    new Command("selection.clear","Clear selection",select+" on empty terrain","Clear the current selection without the modifier. Finish or cancel an armed action first."),
                    new Command("selection.hover","Preview your target","Point at a unit, building or resource","The hover highlight identifies your target; the point on the map shows where you are aiming.")),
                new Topic("Orders",
                    new Command("orders.move","Move",order+" on terrain","Selected units move to that location using their movement and collision rules."),
                    new Command("orders.attack","Attack a target",order+" on a visible enemy","Focus attacks on that enemy. Double "+order+" over an enemy also attacks directly."),
                    new Command("orders.attack-move","Attack Move",touch?"Double A on terrain / Attack Move action":"Double right-click / Attack Move action","Move toward the point and attack enemies along the way. The action button needs a destination."),
                    new Command("orders.gather","Gather resources","Worker selected · "+order+" on minerals / gas","Assign workers automatically among nearby deposits. Gas needs a completed extractor."),
                    new Command("orders.repair","Repair an ally","Industrial worker · "+order+" on damaged ally","Repair a friendly unit or building, spending minerals. Biological units regenerate instead."),
                    new Command("orders.queue","Queue orders",shift+" + "+order+" / double "+order,"Append movement, work or attack orders. Without "+shift+", a new order replaces the plan."),
                    new Command("orders.stop","Stop","Stop action","Clear current and queued orders and brake. Units can still engage nearby enemies."),
                    new Command("orders.hold","Hold Position","Hold Position action","Clear orders and keep units in place. Attack in range without chasing; resist allied pushes.")),
                new Topic("Menus",
                    new Command("menus.actions",touch?"Open / close action wheel":"Open unit actions",touch?"Press right stick / double Grip":"Select units or a building",touch?"Stick click toggles the wheel; double Grip opens it. Actions depend on your selection.":"Use the selected item's actions. Workers have Build; producers have Train and Research."),
                    new Command("menus.choose-action",touch?"Choose a wheel action":"Choose a menu action",touch?"Tilt right stick":"Point at an action button",touch?"Center the stick first, then tilt toward an action. Build opens a submenu.":"Available actions follow your selection. Hover to preview the button before clicking."),
                    new Command("menus.execute-action","Execute an action",touch?"Trigger / A in the wheel":"Left-click the action","Execute the highlighted action. Building and Attack Move then need a point on the map."),
                    new Command("menus.back-action",touch?"Back in action wheel":"Cancel a pending action",touch?"Grip / B":"Escape",touch?"Return from Build, or close the action wheel. Group wheels use B to cancel.":"Cancel an armed build or Attack Move. Otherwise, Escape opens pause or returns from a menu."),
                    new Command("menus.activate","Use menu buttons",click,"Press and release on the same button. Empty menu areas never send orders through to the map."),
                    new Command("menus.pause","Pause / resume",touch?"B / Pause / Resume match":"Escape / Space / Pause / Resume match","During play, cancel an armed action first; otherwise open pause. Resume continues the same match."),
                    new Command("menus.leave","Return to main menu","Pause > Return to main menu","Confirm Leave match to end the current match. Keep playing returns to your army."),
                    new Command("menus.match","Start / restart a match","New Match / Rematch","Set faction, opponent, difficulty and map before Start Match. Rematch is available after a match ends.")),
                new Topic("Groups",
                    new Command("groups.recall","Recall a group",touch?"Double press right stick":"Keys 1–4",touch?"Open the four-group wheel. You do not need to have a unit selected first.":"Replace the current selection with the surviving members of the chosen control group."),
                    new Command("groups.choose","Choose a group",touch?"1 up · 2 right · 3 down · 4 left":"Group 1 / 2 / 3 / 4 buttons",touch?"Tilt or rotate the stick to highlight one of four groups.":"The four buttons show how many members are currently assigned."),
                    new Command("groups.confirm-recall","Confirm recall",touch?"Let the stick return to center":"Tap a Group button",touch?"Select the highlighted group immediately. A and Trigger are not required.":"Recall the group's surviving members. Empty groups show an empty message."),
                    new Command("groups.replace","Replace a group",touch?"Hold stick click + tilt; release click":"Ctrl + 1–4","Replace ALL previous members with your current selection. This does not merge selections."),
                    new Command("groups.cancel","Cancel group choice",touch?"B / press right stick":"Release mouse outside the Group button",touch?"Close without changing a group. Centering a held stick does not save until you release its click.":"A button press released outside its target does not recall or replace a group."),
                    new Command("groups.panel-save","Save from the panel","Hold a Group button (over 0.65 s)","Replace that group's members with your current selection. Tap instead to recall it."),
                    new Command("groups.lifetime","Group lifetime","Applies to all four groups","Only your units and buildings can belong to groups. Dead members drop out; a new match clears groups.")),
                new Topic("Production",
                    new Command("production.build","Build a structure","Worker > Build > choose a building","Point at a valid foundation, then "+(touch?"Trigger / A":"left or right click")+" to place it. "+back+" cancels placement."),
                    new Command("production.train","Train a unit","Production building > Train","Spend resources and reserve supply to add a unit to the building's production queue."),
                    new Command("production.research","Research an upgrade","Technology building > Research","Queue a faction-wide attack or armor upgrade. Requirements and costs appear in the action help."),
                    new Command("production.cancel-item","Cancel one queued item",click+" on its production card","The card turns red on hover. Cancel immediately and refund its cost; other items keep their order."),
                    new Command("production.cancel-last","Cancel / refund","Cancel / refund action","Remove the last queued item, or cancel unfinished construction with a partial refund."),
                    new Command("production.rally","Set a Rally Point","Select producer(s) · "+order+" on terrain","Newly trained units move to the cyan flag. Existing units keep their orders; workers move, not gather."),
                    new Command("production.clear-rally","Clear a Rally Point","Clear Rally Point action","Restore default exits. New workers auto-gather again; production stays queued."),
                    new Command("production.details","Read action details","Hover over an action for 1.5 s","See what the unit, building or upgrade does, along with its cost, timing and requirements.")),
                touch?new Topic("Board",
                    new Command("board.move-menu","Move the menu","Hold Trigger / A on MOVE MENU + drag","Place the menu wherever it is comfortable. This does not move the battlefield."),
                    new Command("board.menu-distance","Adjust menu distance","Right stick up / down while dragging menu","Bring the grabbed menu closer or move it farther away."),
                    new Command("board.reset-menu","Recover the menu","Reset position button","Return the menu above the board."),
                    new Command("board.reposition","Reposition the board","Settings > Reposition board","Enter placement. The confirmed battlefield stays fixed while you walk around it."),
                    new Command("board.place","Place the board","Point at a surface + Trigger","Confirm the board position. Table / Floor and Detect surfaces provide placement options."),
                    new Command("board.size-rotation","Placement size / rotation","Right stick while placing","Up / down changes size; left / right rotates. These axes do not navigate the map during play."),
                    new Command("board.height","Placement height","Grip + right stick up / down","Adjust the board's height during placement. The left controller is unassigned."),
                    new Command("board.cancel-placement","Cancel board placement","B while placing","Restore the previously confirmed board position and size.")):
                new Topic("Board",
                    new Command("board.pan","Pan the map","WASD","Move the desktop development view across the battlefield."),
                    new Command("board.zoom","Zoom the map","Mouse wheel","Change the desktop map scale. Quest keeps the placed board fixed."),
                    new Command("board.fit","See the full map","F / Full map button","Reset the desktop view to show the whole battlefield."),
                    new Command("board.reset-menu","Recover the menu","M / Reset position","Return the menu above the board."),
                    new Command("board.move-menu","Move the menu","Drag MOVE MENU with left mouse button","Move the console without changing the board position."),
                    new Command("board.menu-distance","Adjust menu distance","Mouse wheel while dragging menu","Bring the grabbed console closer or move it farther away."),
                    new Command("board.place","Place or reposition the board","Settings / New Match > Place board","Point at the position and left-click to confirm. Table / Floor set the manual height."),
                    new Command("board.cancel-placement","Cancel board placement","Escape / Space while placing","Restore the previous board position and size."))
            };
        }
    }
}
