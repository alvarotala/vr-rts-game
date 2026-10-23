using System.Linq;
using Frontier.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Frontier.Runtime
{
    public sealed partial class CommandPanel
    {
        private const float TutorialCardWidth=620,TutorialCardHeight=402;
        private static readonly Color TutorialViolet=ConsoleTheme.SecondaryAccent;
        private static readonly Color TutorialDark=new Color(.065f,.05f,.105f).linear;
        private static readonly Color TutorialButtonFill=ConsoleTheme.Secondary;
        private RectTransform tutorialCard,tutorialCueTargetRect;
        private Text tutorialTitle,tutorialDescription,tutorialInstruction,tutorialProgress,tutorialState,tutorialNumber;
        private Target tutorialContinue,tutorialRepeat,tutorialLeave;
        private Image tutorialProgressFill;
        private LineRenderer tutorialConnector,tutorialDestination;
        private Material tutorialLineMaterial;
        public ActionWheel TutorialWheel { get; set; }
        private bool TutorialCardVisible=>Session.IsTutorial&&Session.Running&&!Session.IsCountingDown&&!Session.HasSystemPause&&!Quest.Placing;
        private bool TutorialResumeBriefing=>Session.IsTutorial&&Session.Tutorial.Step==TutorialStep.ResumeMenu&&Session.Tutorial.Briefing;
        private string TutorialSignature=>Session.Tutorial==null?"":Session.Tutorial.Step+":"+Session.Tutorial.Briefing+":"+Session.Tutorial.AcceptedOrderCount+":"+Session.AttackMoveMode+":"+Localization.Code+":"+Bindings.ToJson();

        private void CreateTutorialCard()
        {
            // A separate world-space window. It never changes the console's layout.
            var go=new GameObject("Tutorial instructions",typeof(RectTransform),typeof(Canvas),typeof(Image));
            tutorialCard=go.GetComponent<RectTransform>();tutorialCard.SetParent(transform,false);
            tutorialCard.pivot=Vector2.one*.5f;tutorialCard.sizeDelta=new Vector2(TutorialCardWidth,TutorialCardHeight);
            var tutorialCanvas=go.GetComponent<Canvas>();tutorialCanvas.renderMode=RenderMode.WorldSpace;tutorialCanvas.sortingOrder=22;
            var fill=go.GetComponent<Image>();fill.color=TutorialDark;fill.raycastTarget=false;ConsoleTheme.StyleSurface(fill);
            var badge=Section(tutorialCard,"Lesson badge",new Vector2(24,-22),new Vector2(48,48),TutorialButtonFill);
            ConsoleTheme.StylePill(badge.GetComponent<Image>());
            tutorialNumber=Label(badge,"01",Vector2.zero,new Vector2(48,48),23,Color.white,TextAnchor.MiddleCenter);
            tutorialNumber.fontStyle=FontStyle.Bold;
            tutorialProgress=Label(tutorialCard,"",new Vector2(88,-24),new Vector2(508,36),18,Color.white);
            var track=Section(tutorialCard,"Adventure progress",new Vector2(88,-66),new Vector2(508,4),new Color(.11f,.08f,.18f));
            tutorialProgressFill=Section(track,"Completed lessons",Vector2.zero,new Vector2(0,4),TutorialViolet).GetComponent<Image>();
            tutorialTitle=Label(tutorialCard,"",new Vector2(24,-90),new Vector2(572,44),32,Color.white);
            tutorialTitle.fontStyle=FontStyle.Bold;
            tutorialDescription=Label(tutorialCard,"",new Vector2(24,-143),new Vector2(572,64),22,Color.white,TextAnchor.UpperLeft);
            var action=Section(tutorialCard,"Your next move",new Vector2(24,-216),new Vector2(572,104),new Color(.12f,.075f,.20f).linear);
            Section(action,"Violet accent",new Vector2(0,-8),new Vector2(4,88),TutorialViolet);
            tutorialInstruction=Label(action,"",new Vector2(20,-14),new Vector2(530,78),25,Color.white,TextAnchor.MiddleLeft);
            tutorialInstruction.fontStyle=FontStyle.Bold;
            tutorialState=Label(tutorialCard,"",new Vector2(24,-342),new Vector2(302,38),18,Color.white);
            tutorialContinue=TutorialButton("Continue tutorial","Continue",()=>{Session.ContinueTutorial();signature=null;Rebuild();},true);
            tutorialRepeat=TutorialButton("Repeat tutorial","Repeat tutorial",()=>Session.RestartTutorial());
            tutorialLeave=TutorialButton("Tutorial main menu","Main menu",()=>Session.LeaveMatch());
            TopLeft(tutorialContinue.rect,new Vector2(348,-336),new Vector2(248,48));
            TopLeft(tutorialRepeat.rect,new Vector2(24,-336),new Vector2(274,48));
            TopLeft(tutorialLeave.rect,new Vector2(314,-336),new Vector2(282,48));
            tutorialLineMaterial=new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            tutorialLineMaterial.name="Tutorial violet guide";tutorialLineMaterial.color=TutorialViolet;
            // Teaching annotations stay legible across world-space UI surfaces.
            tutorialLineMaterial.renderQueue=4000;tutorialLineMaterial.SetFloat("_ZWrite",0);
            tutorialConnector=TutorialLine("Tutorial connector",false);
            tutorialDestination=TutorialLine("Tutorial target highlight",true);
            HideTutorialPresentation();
        }

        private Target TutorialButton(string name,string title,System.Action click,bool primary=false)
        {
            var target=HudButton(tutorialCard,title,"",Vector2.zero,new Vector2(274,48),click,null,out var detail);
            target.rect.name=name;detail.gameObject.SetActive(false);
            var text=target.rect.GetComponentInChildren<Text>();text.fontSize=21;text.fontStyle=FontStyle.Bold;text.color=Color.white;
            text.resizeTextForBestFit=false;
            var rect=text.rectTransform;rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;
            target.normal=primary?ConsoleTheme.Primary:TutorialButtonFill;target.image.color=target.normal;
            return target;
        }

        private LineRenderer TutorialLine(string name,bool loop)
        {
            var go=new GameObject(name);go.transform.SetParent(transform,false);
            var line=go.AddComponent<LineRenderer>();line.sharedMaterial=tutorialLineMaterial;line.useWorldSpace=true;line.loop=loop;
            line.widthMultiplier=.002f;line.numCornerVertices=4;line.numCapVertices=4;
            line.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;line.receiveShadows=false;
            return line;
        }

        private void LayoutTutorialCard(bool hud)
        {
            if(tutorialCard==null)return;
            tutorialCard.gameObject.SetActive(TutorialCardVisible);
        }

        private void BuildTutorialCard()
        {
            UpdateTutorialCard();
            if(!TutorialCardVisible)return;
            if(tutorialContinue.rect.gameObject.activeSelf)targets.Add(tutorialContinue);
            if(tutorialRepeat.rect.gameObject.activeSelf)targets.Add(tutorialRepeat);
            if(tutorialLeave.rect.gameObject.activeSelf)targets.Add(tutorialLeave);
        }

        private void UpdateTutorialCard()
        {
            if(tutorialCard==null)return;
            if(!TutorialCardVisible){HideTutorialPresentation();return;}
            var tutorial=Session.Tutorial;
            tutorialContinue.rect.gameObject.SetActive(tutorial.Briefing&&!tutorial.Complete);
            tutorialRepeat.rect.gameObject.SetActive(tutorial.Complete);tutorialLeave.rect.gameObject.SetActive(tutorial.Complete);
            tutorialState.gameObject.SetActive(!tutorial.Complete);
            tutorialNumber.text=tutorial.Complete?"!":tutorial.StepNumber.ToString("00");
            tutorialProgressFill.rectTransform.sizeDelta=new Vector2(508*tutorial.Progress,4);
            UpdateTutorialContent();
            UpdateTutorialPresentation();
        }

        private void HideTutorialPresentation()
        {
            Board?.ShowTutorialCue(null,false);
            if(tutorialCard!=null)tutorialCard.gameObject.SetActive(false);
            if(tutorialConnector!=null)tutorialConnector.gameObject.SetActive(false);
            if(tutorialDestination!=null)tutorialDestination.gameObject.SetActive(false);
        }

        private void UpdateTutorialPresentation()
        {
            if(tutorialCard==null)return;
            if(!TutorialCardVisible||!root.gameObject.activeInHierarchy){HideTutorialPresentation();return;}
            tutorialCard.gameObject.SetActive(true);
            // Beside the console with a visible air gap, facing the same viewer.
            // Moving/resizing the console carries its companion without growing the HUD.
            tutorialCard.rotation=root.rotation;tutorialCard.localScale=root.localScale;
            tutorialCard.position=root.TransformPoint(new Vector3(root.rect.xMax+40+TutorialCardWidth*.5f,0,-12));
            Vector3 destination=Vector3.zero;
            bool connected=!Session.Tutorial.Complete&&TryTutorialCue(out destination);
            tutorialConnector.gameObject.SetActive(connected);tutorialDestination.gameObject.SetActive(connected);
            if(!connected)return;
            var destinationInConsole=root.InverseTransformPoint(destination);
            bool aroundConsole=tutorialCueTargetRect==null&&!tutorialCueWheel&&string.IsNullOrEmpty(tutorialCueAction)&&destinationInConsole.y<root.rect.yMin;
            var bend=aroundConsole?root.TransformPoint(new Vector3(root.rect.xMax+24,root.rect.yMin-28,-16)):destination;
            var local=tutorialCard.InverseTransformPoint(bend);
            var direction=new Vector2(local.x,local.y);
            float edge=Mathf.Max(Mathf.Abs(direction.x)/(TutorialCardWidth*.5f),Mathf.Abs(direction.y)/(TutorialCardHeight*.5f));
            if(edge<.001f){direction=Vector2.left;edge=1/(TutorialCardWidth*.5f);}
            var start=tutorialCard.TransformPoint(new Vector3(direction.x/edge,direction.y/edge,-3));
            tutorialConnector.positionCount=3;
            tutorialConnector.SetPositions(new[]{start,aroundConsole?bend:start+(destination-start).normalized*.045f,destination});
            if(tutorialCueTargetRect!=null)
            {
                var corners=new Vector3[4];tutorialCueTargetRect.GetWorldCorners(corners);
                var center=tutorialCueTargetRect.TransformPoint(tutorialCueTargetRect.rect.center);
                for(int i=0;i<4;i++)corners[i]=center+(corners[i]-center)*1.035f-tutorialCueTargetRect.forward*.003f;
                tutorialDestination.positionCount=4;tutorialDestination.SetPositions(corners);
            }
            else
            {
                const int points=24;tutorialDestination.positionCount=points;
                float radius=.007f+.001f*Mathf.Sin(Time.unscaledTime*3);
                for(int i=0;i<points;i++)
                {
                    float angle=i*Mathf.PI*2/points;
                    tutorialDestination.SetPosition(i,destination+(Quest.Camera.transform.right*Mathf.Cos(angle)+Quest.Camera.transform.up*Mathf.Sin(angle))*radius);
                }
            }
        }

        private bool TryTutorialCue(out Vector3 position)
        {
            position=Vector3.zero;tutorialCueTargetRect=null;
            Board.ShowTutorialCue(null,false);
            if(!TutorialCardVisible||Session.Tutorial.Complete)return false;
            if(tutorialCueAction=="cancel")
            {
                if(!Quest.Active||Quest.TrackingSpace==null||!Quest.PointerTracked)return false;
                position=Quest.PointerRay().origin+Quest.PointerRotation()*ControllerVisuals.ButtonPosition(Bindings.LeftHand,Bindings.Get(true,"back"));return true;
            }
            if(tutorialCueWheel&&Quest.Active)
            {
                string wheelAction=tutorialCueAction=="group:0"?"group-1":tutorialCueAction;
                if(TutorialWheel!=null&&TutorialWheel.TryTutorialTarget(wheelAction,out position))return true;
                if(Quest.TrackingSpace!=null&&Quest.PointerTracked)
                {
                    if(tutorialCueAction=="back")
                    {
                        position=Quest.PointerRay().origin+Quest.PointerRotation()*ControllerVisuals.ButtonPosition(Bindings.LeftHand,"touch:b");return true;
                    }
                    var surface=ControllerVisuals.StickSurface(Quest.ActiveController==OVRInput.Controller.LTouch);
                    position=Quest.PointerRay().origin+Quest.PointerRotation()*surface.position;return true;
                }
            }
            if(!string.IsNullOrEmpty(tutorialCueAction))
            {
                var target=tutorialCueAction=="pause"?matchTargets[1]:tutorialCueAction=="group:0"?groupTargets[0]:
                    targets.FirstOrDefault(t=>tutorialCueAction=="resume"?t.rect.name=="Resume match":tutorialCueAction=="back"?t.rect.name=="Back":t.actionId==tutorialCueAction);
                if(target!=null&&target.rect.gameObject.activeInHierarchy)
                {
                    tutorialCueTargetRect=target.rect;
                    // Attach at the edge nearest the instructions, leaving the label clear.
                    position=target.rect.TransformPoint(new Vector3(target.rect.rect.xMax,target.rect.rect.center.y,-4));return true;
                }
                return false;
            }
            Point? point=tutorialCuePoint;
            float lift=.35f,radius=.8f;
            if(tutorialCueEntity>0)
            {
                var entity=Session.View.entities.FirstOrDefault(e=>e.id==tutorialCueEntity&&!e.remembered);
                if(entity!=null)
                {
                    var type=Session.Catalog[entity.definition];float half=type.building?type.footprint*.5f:0;
                    point=new Point(entity.position.x+half,entity.position.y+half);lift=type.building?1.7f:1.2f;
                    if(type.building)radius=type.footprint*.65f;
                }
            }
            if(tutorialCueResource>0)point=Session.View.resources.FirstOrDefault(r=>r.id==tutorialCueResource)?.position;
            // Briefings can identify the subject while its action buttons are locked.
            if(!point.HasValue&&Session.Tutorial.Briefing)point=Session.Tutorial.FocusPoint;
            if(!point.HasValue||Board.Content==null||!Board.Content.gameObject.activeInHierarchy)return false;
            bool foundation=Session.BuildDefinition!=null&&Session.BuildDefinition==Session.Tutorial.ExpectedDefinition&&!Session.Tutorial.Briefing&&Session.Tutorial.AcceptedOrderCount==0;
            Board.ShowTutorialCue(point,foundation,radius);
            position=Board.Content.TransformPoint(new Vector3(point.Value.x,Board.HeightAt(point.Value)+lift,point.Value.y));return true;
        }

        private void DisposeTutorialPresentation()
        {
            if(tutorialLineMaterial==null)return;
            if(Application.isPlaying)Destroy(tutorialLineMaterial);else DestroyImmediate(tutorialLineMaterial);
        }
    }
}
