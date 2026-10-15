using System.IO;
using System.Linq;
using System.Reflection;
using Frontier.Core;
using Frontier.Runtime;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Frontier.Editor
{
    public static class HoverPreview
    {
        [MenuItem("Frontier/Preview hover")]
        public static void Render()
        {
            var preview=new PreviewRenderUtility();
            var sessionObject=new GameObject("Hover preview session");
            var boardObject=new GameObject("Hover preview board");
            var bounds=Shader.GetGlobalVector("_FrontierBounds");var matrix=Shader.GetGlobalMatrix("_FrontierWorldToBoard");
            void Invoke(object target,string name)=>target.GetType().GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(target,null);
            try
            {
                Directory.CreateDirectory("TestResults/Hover");
                var session=sessionObject.AddComponent<GameSession>();if(session.Catalog==null)Invoke(session,"Awake");session.StartMatch();
                var board=boardObject.AddComponent<BoardView>();board.Session=session;Invoke(board,"Start");
                var worker=session.View.entities.First(e=>session.Catalog[e.definition].role=="worker");
                var building=session.View.entities.First(e=>session.Catalog[e.definition].role=="hq");
                worker.position=new Point(20.5f,14.5f);
                session.View.entities=new[]{worker,building};
                preview.AddSingleGO(boardObject);
                preview.camera.orthographic=true;preview.camera.nearClipPlane=.001f;preview.camera.farClipPlane=10;
                preview.camera.clearFlags=CameraClearFlags.SolidColor;preview.camera.backgroundColor=new Color(.025f,.04f,.06f);
                Capture("worker-idle",worker,false);Capture("worker-hover",worker,true);
                Capture("building-idle",building,false);Capture("building-hover",building,true);
                session.Selection.Add(building.id);Capture("selected-building-hover",building,true);
                Capture("selected-building-exit",building,false);
                session.Selection.Clear();worker.owner=1;worker.id=1000;Capture("enemy-hover",worker,true);
                Debug.Log("FRONTIER_HOVER_PREVIEW: 7 runtime views rendered (unit, building, selected, exit, enemy)");

                void Capture(string name,VisibleEntity target,bool hovered)
                {
                    board.Hovered=hovered?target.id:0;Invoke(board,"LateUpdate");
                    var type=session.Catalog[target.definition];float half=type.building?type.footprint*.5f:0;
                    var center=board.Content.TransformPoint(new Vector3(target.position.x+half,type.building?1.2f:.4f,target.position.y+half));
                    float size=type.building?.04f:.023f;
                    preview.camera.orthographicSize=size;preview.camera.transform.position=center+new Vector3(0,size*2,-size*2.4f);
                    preview.camera.transform.LookAt(center);
                    preview.BeginStaticPreview(new Rect(0,0,1000,800));preview.Render(true);
                    var image=preview.EndStaticPreview();File.WriteAllBytes("TestResults/Hover/"+name+".png",image.EncodeToPNG());Object.DestroyImmediate(image);
                }
            }
            finally
            {
                Object.DestroyImmediate(boardObject);Object.DestroyImmediate(sessionObject);preview.Cleanup();
                Shader.SetGlobalVector("_FrontierBounds",bounds);Shader.SetGlobalMatrix("_FrontierWorldToBoard",matrix);
            }
        }
    }
}
