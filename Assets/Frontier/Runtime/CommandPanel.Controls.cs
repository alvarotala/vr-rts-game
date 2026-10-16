using UnityEngine;
using UnityEngine.UI;

namespace Frontier.Runtime
{
    public sealed partial class CommandPanel
    {
        private RectTransform controlGuide;
        private Text controlGuideDevice;
        private int controlGuideTopic;
        private readonly Target[] controlGuideTabs=new Target[6];
        private readonly Text[] controlGuideTabLabels=new Text[6];
        private readonly RectTransform[] controlGuideCards=new RectTransform[8];
        private readonly Text[] controlGuideCommands=new Text[8],controlGuideGestures=new Text[8],controlGuideDescriptions=new Text[8];

        private sealed class HelpCommand
        {
            public string Name,Gesture,Description;
            public HelpCommand(string name,string gesture,string description){Name=name;Gesture=gesture;Description=description;}
        }
        private sealed class HelpTopic
        {
            public string Name;public HelpCommand[] Commands;
            public HelpTopic(string name,params HelpCommand[] commands){Name=name;Commands=commands;}
        }

        private void CreateControlGuide()
        {
            controlGuide=Section(root,"Control guide",new Vector2(30,-390),new Vector2(1040,554),new Color(.035f,.08f,.12f));
            Label(controlGuide,"CONTROL GUIDE",new Vector2(16,-10),new Vector2(450,26),21,accent);
            controlGuideDevice=Label(controlGuide,"",new Vector2(470,-10),new Vector2(554,26),16,Color.white,TextAnchor.MiddleRight);
            for(int i=0;i<controlGuideTabs.Length;i++)
            {
                int index=i;
                var tab=Section(controlGuide,"Help category "+i,new Vector2(16+i*169,-46),new Vector2(163,42));
                controlGuideTabLabels[i]=Label(tab,"",new Vector2(4,0),new Vector2(155,42),17,Color.white,TextAnchor.MiddleCenter);
                controlGuideTabs[i]=new Target{rect=tab,image=tab.GetComponent<Image>(),normal=button,click=()=>{controlGuideTopic=index;Rebuild();UpdateMenuText();}};
            }
            for(int i=0;i<controlGuideCards.Length;i++)
            {
                var card=Section(controlGuide,"Help command "+i,new Vector2(16+(i%2)*510,-104-(i/2)*108),new Vector2(498,100),new Color(.055f,.12f,.17f));
                controlGuideCards[i]=card;
                controlGuideCommands[i]=Label(card,"",new Vector2(12,-7),new Vector2(474,23),19,Color.white);
                controlGuideGestures[i]=Label(card,"",new Vector2(12,-32),new Vector2(474,22),16,accent);
                controlGuideDescriptions[i]=Label(card,"",new Vector2(12,-57),new Vector2(474,38),16,new Color(.73f,.83f,.87f),TextAnchor.UpperLeft);
            }
        }

        private void BuildControlGuide()
        {
            bool visible=Page==MenuPage.Pause||Page==MenuPage.Controls;
            controlGuide.gameObject.SetActive(visible);if(!visible)return;
            var topics=ControlTopics(Quest.Active);controlGuideTopic=Mathf.Clamp(controlGuideTopic,0,topics.Length-1);
            controlGuideDevice.text=Quest.Active?"RIGHT TOUCH CONTROLLER · Left unassigned":"MOUSE / KEYBOARD";
            for(int i=0;i<topics.Length;i++)
            {
                var tab=controlGuideTabs[i];tab.rect.name="Help: "+topics[i].Name;
                tab.normal=i==controlGuideTopic?new Color(.12f,.36f,.39f):button;tab.image.color=tab.normal;
                controlGuideTabLabels[i].text=topics[i].Name;
                targets.Add(tab);
            }
            var commands=topics[controlGuideTopic].Commands;
            for(int i=0;i<controlGuideCards.Length;i++)
            {
                bool active=i<commands.Length;controlGuideCards[i].gameObject.SetActive(active);if(!active)continue;
                controlGuideCommands[i].text=commands[i].Name;controlGuideGestures[i].text=commands[i].Gesture;
                controlGuideDescriptions[i].text=commands[i].Description;
            }
        }

        // One guide is shared by pause and the main-menu Controls page. Every card
        // describes an input or action; opening help never submits a gameplay command.
        private static HelpTopic[] ControlTopics(bool touch)
        {
            string select=touch?"Trigger":"Left-click",order=touch?"A":"Right-click",shift=touch?"Grip":"Shift",back=touch?"B":"Escape";
            string click=touch?"Trigger / A":"Left-click";
            return new[]{
                new HelpTopic("Selection",
                    new HelpCommand("Select an item",select+" on an item","Select a unit, building or resource to see its information and available actions."),
                    new HelpCommand("Select an area",touch?"Hold Trigger + drag":"Hold left mouse button + drag","Enclose units in the flat rectangle. The drag can start outside the map."),
                    new HelpCommand("Add to selection",shift+" + "+select,"Keep your selection and add more units, including by dragging an area."),
                    new HelpCommand("Remove one unit",shift+" + hold "+(touch?"Trigger":"left click")+" still (0.5 s)","Point at a selected unit and hold still to remove it from the selection."),
                    new HelpCommand("Select the same type",touch?"Double Trigger":"Double left-click","Select nearby units of the same type. Hold "+shift+" to keep other selected units."),
                    new HelpCommand("Preview your target","Point at a unit, building or resource","The hover highlight identifies your target; the point on the map shows where you are aiming.")),
                new HelpTopic("Orders",
                    new HelpCommand("Move",order+" on terrain","Selected units move to that location using their movement and collision rules."),
                    new HelpCommand("Attack a target",order+" on a visible enemy","Focus attacks on that enemy. Double "+order+" over an enemy also attacks directly."),
                    new HelpCommand("Attack Move",touch?"Double A on terrain / Attack Move action":"Double right-click / Attack Move action","Move toward the point and attack enemies along the way. The action button needs a destination."),
                    new HelpCommand("Gather resources","Worker selected · "+order+" on minerals / gas","Assign workers automatically among nearby deposits. Gas needs a completed extractor."),
                    new HelpCommand("Repair an ally","Industrial worker · "+order+" on damaged ally","Repair a friendly unit or building, spending minerals. Biological units regenerate instead."),
                    new HelpCommand("Queue orders",shift+" + "+order+" / double "+order,"Append movement, work or attack orders. Without "+shift+", a new order replaces the plan."),
                    new HelpCommand("Stop","Stop action","Clear current and queued orders and brake. Units can still engage nearby enemies."),
                    new HelpCommand("Hold Position","Hold Position action","Clear orders and keep units in place. Attack in range without chasing; resist allied pushes.")),
                new HelpTopic("Menus",
                    new HelpCommand(touch?"Open action wheel":"Open unit actions",touch?"Press right stick / double Grip":"Select units or a building","Use the selected item's actions. Workers have Build; producers have Train and Research."),
                    new HelpCommand(touch?"Choose a wheel action":"Choose a menu action",touch?"Tilt right stick":"Point at an action button",touch?"Center the stick first, then tilt toward an action. Build opens a submenu.":"Available actions follow your selection. Hover to preview the button before clicking."),
                    new HelpCommand("Execute an action",touch?"Trigger / A in the wheel":"Left-click the action","Execute the highlighted action. Building and Attack Move then need a point on the map."),
                    new HelpCommand(touch?"Back in action wheel":"Cancel a pending action",touch?"Grip / B":"Escape",touch?"Return from Build, or close the action wheel. Group wheels use B to cancel.":"Cancel an armed build or Attack Move. Otherwise, Escape opens pause or returns from a menu."),
                    new HelpCommand("Use menu buttons",click,"Press and release on the same button. Empty menu areas never send orders through to the map."),
                    new HelpCommand("Pause / resume",touch?"B / Pause / Resume match":"Escape / Space / Pause / Resume match","During play, cancel an armed action first; otherwise open pause. Resume continues the same match."),
                    new HelpCommand("Return to main menu","Pause > Return to main menu","Confirm Leave match to end the current match. Keep playing returns to your army.")),
                new HelpTopic("Groups",
                    new HelpCommand("Recall a group",touch?"Double press right stick":"Keys 1–4",touch?"Open the four-group wheel. You do not need to have a unit selected first.":"Replace the current selection with the surviving members of the chosen control group."),
                    new HelpCommand("Choose a group",touch?"1 up · 2 right · 3 down · 4 left":"Group 1 / 2 / 3 / 4 buttons",touch?"Tilt or rotate the stick to highlight one of four groups.":"The four buttons show how many members are currently assigned."),
                    new HelpCommand("Confirm recall",touch?"Let the stick return to center":"Tap a Group button",touch?"Select the highlighted group immediately. A and Trigger are not required.":"Recall the group's surviving members. Empty groups show an empty message."),
                    new HelpCommand("Replace a group",touch?"Hold stick click + tilt; release click":"Ctrl + 1–4","Replace ALL previous members with your current selection. This does not merge selections."),
                    new HelpCommand("Cancel group choice",touch?"B":"Release mouse outside the Group button",touch?"Close without changing a group. Centering a held stick does not save until you release its click.":"A button press released outside its target does not recall or replace a group."),
                    new HelpCommand("Save from the panel","Hold a Group button","Replace that group's members with your current selection. Tap instead to recall it."),
                    new HelpCommand("Group lifetime","Applies to all four groups","Only your units and buildings can belong to groups. Dead members drop out; a new match clears groups.")),
                new HelpTopic("Production",
                    new HelpCommand("Build a structure","Worker > Build > choose a building","Point at a valid foundation, then "+(touch?"Trigger / A":"left or right click")+" to place it. "+back+" cancels placement."),
                    new HelpCommand("Train a unit","Production building > Train","Spend resources and reserve supply to add a unit to the building's production queue."),
                    new HelpCommand("Research an upgrade","Technology building > Research","Queue a faction-wide attack or armor upgrade. Requirements and costs appear in the action help."),
                    new HelpCommand("Cancel one queued item",click+" on its production card","The card turns red on hover. Cancel immediately and refund its cost; other items keep their order."),
                    new HelpCommand("Cancel / refund","Cancel / refund action","Remove the last queued item, or cancel unfinished construction with a partial refund."),
                    new HelpCommand("Set a Rally Point","Select producer(s) · "+order+" on terrain","Newly trained units move to the cyan flag. Existing units keep their orders; workers move, not gather."),
                    new HelpCommand("Clear a Rally Point","Clear Rally Point action","Restore default exits. New workers auto-gather again; production stays queued."),
                    new HelpCommand("Read action details","Hover over an action for 1.5 s","See what the unit, building or upgrade does, along with its cost, timing and requirements.")),
                touch?new HelpTopic("Board",
                    new HelpCommand("Move the menu","Hold Trigger / A on MOVE MENU + drag","Place the menu wherever it is comfortable. This does not move the battlefield."),
                    new HelpCommand("Adjust menu distance","Right stick up / down while dragging menu","Bring the grabbed menu closer or move it farther away."),
                    new HelpCommand("Recover the menu","Reset position button","Return the menu above the board."),
                    new HelpCommand("Reposition the board","Settings > Reposition board","Enter placement. The confirmed battlefield stays fixed while you walk around it."),
                    new HelpCommand("Place the board","Point at a surface + Trigger","Confirm the board position. Table / Floor and Detect surfaces provide placement options."),
                    new HelpCommand("Placement size / rotation","Right stick while placing","Up / down changes size; left / right rotates. These axes do not navigate the map during play."),
                    new HelpCommand("Placement height","Grip + right stick up / down","Adjust the board's height during placement. The left controller is unassigned."),
                    new HelpCommand("Cancel board placement","B while placing","Restore the previously confirmed board position and size.")):
                new HelpTopic("Board",
                    new HelpCommand("Pan the map","WASD","Move the desktop development view across the battlefield."),
                    new HelpCommand("Zoom the map","Mouse wheel","Change the desktop map scale. Quest keeps the placed board fixed."),
                    new HelpCommand("See the full map","F / Full map button","Reset the desktop view to show the whole battlefield."),
                    new HelpCommand("Recover the menu","M / Reset position","Return the menu above the board."),
                    new HelpCommand("Move the menu","Drag MOVE MENU with left mouse button","Move the console without changing the board position."),
                    new HelpCommand("Adjust menu distance","Mouse wheel while dragging menu","Bring the grabbed console closer or move it farther away."),
                    new HelpCommand("Place or reposition the board","Settings / New Match > Place board","Point at the position and left-click to confirm. Table / Floor set the manual height."),
                    new HelpCommand("Cancel board placement","Escape while placing","Restore the previous board position and size."))
            };
        }
    }
}
