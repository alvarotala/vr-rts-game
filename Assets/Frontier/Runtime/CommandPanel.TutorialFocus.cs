using UnityEngine;
using UnityEngine.UI;

namespace Frontier.Runtime
{
    public sealed partial class CommandPanel
    {
        private enum TutorialFocus { Briefing, Map, Menu, Wheel }
        private TutorialFocus tutorialFocus;
        private Image tutorialMenuDim;
        private string tutorialAttentionState;
        private float tutorialAttentionTime;
        private int tutorialAttentionChanges;
        private bool tutorialPositionReady;
        public CommanderInput TutorialInput { private get; set; }

        private void CreateTutorialFocus()
        {
            var cover=Section(root,"Tutorial menu dim",Vector2.zero,root.sizeDelta,Color.clear);
            cover.anchorMin=Vector2.zero;cover.anchorMax=Vector2.one;
            cover.offsetMin=cover.offsetMax=Vector2.zero;
            tutorialMenuDim=cover.GetComponent<Image>();tutorialMenuDim.raycastTarget=false;
            tutorialMenuDim.gameObject.SetActive(false);
        }

        private void ResetTutorialFocus()
        {
            if(tutorialMenuDim!=null)tutorialMenuDim.gameObject.SetActive(false);
#if FRONTIER_BROWSER
            foreach(var edge in browserTutorialEdges)if(edge!=null)edge.gameObject.SetActive(false);
#endif
            tutorialPositionReady=false;
            if(!Session.IsTutorial)tutorialAttentionState=null;
        }

        private void PositionTutorialCard(Vector3 destination,bool connected)
        {
#if FRONTIER_BROWSER
            PositionBrowserTutorial();
#else
            var lesson=Session.Tutorial;var camera=Quest.Camera;
            tutorialFocus=lesson.Briefing||lesson.Complete?TutorialFocus.Briefing:
                tutorialCueTargetRect!=null?TutorialFocus.Menu:tutorialCueWheel?TutorialFocus.Wheel:
                connected&&string.IsNullOrEmpty(tutorialCueAction)?TutorialFocus.Map:TutorialFocus.Menu;
            bool map=tutorialFocus==TutorialFocus.Map;
            var desiredScale=root.localScale*(map?1.28f:1f);
            var desiredRotation=root.rotation;
            // Explanations sit over the upper half of the console, in the same
            // field of view as its controls, instead of extending it sideways.
            var desiredPosition=root.TransformPoint(new Vector3(0,root.rect.height*.22f,-28));
            if(tutorialFocus==TutorialFocus.Menu&&tutorialCueTargetRect!=null)
            {
                var corners=new Vector3[4];tutorialCueTargetRect.GetWorldCorners(corners);
                var min=root.InverseTransformPoint(corners[0]);var max=min;
                foreach(var corner in corners){var p=root.InverseTransformPoint(corner);min=Vector3.Min(min,p);max=Vector3.Max(max,p);}
                float halfWidth=tutorialCard.rect.width*.5f,halfHeight=tutorialCard.rect.height*.5f;
                // Centered, full-width buttons always use the same side; tiny
                // transform rounding must not flip the guide across the menu.
                float x=(min.x+max.x)>=-1?min.x-28-halfWidth:max.x+28+halfWidth;
                float y=(min.y+max.y)*.5f;
                x=Mathf.Clamp(x,root.rect.xMin+halfWidth+24,root.rect.xMax-halfWidth-24);
                y=Mathf.Clamp(y,root.rect.yMin+halfHeight+24,root.rect.yMax-halfHeight-24);
                // A full-width action leaves no side gap: place the guide above
                // it, or below. Allow it to extend past the console rather than
                // clamping it back over the very button being taught.
                if(x+halfWidth>min.x-16&&x-halfWidth<max.x+16)
                    y=max.y+28+halfHeight<=root.rect.yMax-24?max.y+28+halfHeight:min.y-28-halfHeight;
                desiredPosition=root.TransformPoint(new Vector3(x,y,-28));
            }
            else if(map&&camera!=null)
            {
                // Compensate for distance: near the board the guide must also
                // look larger, rather than shrinking as it moves farther away.
                float depth=Mathf.Clamp(camera.transform.InverseTransformPoint(destination).z-.12f,.65f,2.5f);
                float consoleDepth=Mathf.Max(.65f,camera.transform.InverseTransformPoint(root.position).z);
                desiredScale*=depth/consoleDepth;
                float height=tutorialCard.rect.height*desiredScale.y*tutorialCard.parent.lossyScale.y;
                desiredPosition=destination+camera.transform.up*(height*.5f+.065f)-camera.transform.forward*.12f;
                desiredRotation=camera.transform.rotation;
            }
            if(camera!=null)KeepTutorialInView(camera,ref desiredPosition,ref desiredScale);

            string state=lesson.Step+":"+lesson.Briefing+":"+lesson.AcceptedOrderCount+":"+lesson.WheelExplored+":"+tutorialFocus
                +":"+tutorialCueAction+":"+tutorialCueEntity+":"+tutorialCueResource+":"+Session.BuildDefinition+":"+Session.AttackMoveMode;
            if(state!=tutorialAttentionState)
            {
                tutorialAttentionState=state;tutorialAttentionTime=Time.unscaledTime;tutorialAttentionChanges++;
                if(Application.isPlaying&&!Session.HasSystemPause)
                {
                    Session.GetComponent<AudioFeedback>()?.TutorialTransition();
                    TutorialInput?.TutorialTransitionHint();
                }
            }
            float pulse=Application.isPlaying?Mathf.Sin(Mathf.PI*Mathf.Clamp01((Time.unscaledTime-tutorialAttentionTime)/.4f)):0;
            desiredScale*=1+pulse*.035f;
            float blend=tutorialPositionReady&&Application.isPlaying?1-Mathf.Exp(-Time.unscaledDeltaTime*14):1;
            tutorialCard.SetPositionAndRotation(Vector3.Lerp(tutorialCard.position,desiredPosition,blend),Quaternion.Slerp(tutorialCard.rotation,desiredRotation,blend));
            tutorialCard.localScale=Vector3.Lerp(tutorialCard.localScale,desiredScale,blend);
            tutorialPositionReady=true;

            float alpha=map&&Page==MenuPage.Actions?.48f:0;
            if(Application.isPlaying)alpha=Mathf.MoveTowards(tutorialMenuDim.color.a,alpha,Time.unscaledDeltaTime*2.4f);
            tutorialMenuDim.color=new Color(0,0,0,alpha);
            tutorialMenuDim.gameObject.SetActive(alpha>.001f);
            tutorialMenuDim.transform.SetAsLastSibling();
#endif
        }

        private void KeepTutorialInView(Camera camera,ref Vector3 position,ref Vector3 scale)
        {
            var local=camera.transform.InverseTransformPoint(position);
            local.z=Mathf.Clamp(local.z,.65f,2.5f);
            float vertical=camera.orthographic?camera.orthographicSize:local.z*Mathf.Tan(camera.fieldOfView*Mathf.Deg2Rad*.5f);
            float horizontal=vertical*camera.aspect;
            var parentScale=tutorialCard.parent.lossyScale;
            float halfWidth=tutorialCard.rect.width*scale.x*parentScale.x*.5f;
            float halfHeight=tutorialCard.rect.height*scale.y*parentScale.y*.5f;
            float fit=Mathf.Min(1,horizontal*.84f/Mathf.Max(.001f,halfWidth),vertical*.84f/Mathf.Max(.001f,halfHeight));
            scale*=fit;halfWidth*=fit;halfHeight*=fit;
            local.x=Mathf.Clamp(local.x,-horizontal*.92f+halfWidth,horizontal*.92f-halfWidth);
            local.y=Mathf.Clamp(local.y,-vertical*.92f+halfHeight,vertical*.92f-halfHeight);
            position=camera.transform.TransformPoint(local);
        }
    }
}
