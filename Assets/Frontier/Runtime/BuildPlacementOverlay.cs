using Frontier.Core;
using UnityEngine;

namespace Frontier.Runtime
{
    // One texel per pointer-origin cell. The selected footprint's legality comes
    // from the same observed-state mask as the ghost. Refineries retain their
    // existing gas target mapping; all other buildings follow the pointer freely.
    public sealed class BuildPlacementOverlay : System.IDisposable
    {
        private readonly Color32[] pixels;
        private readonly int width,height;
        private PlayerView previousView;
        private int previousTick=-1;
        private string previousDefinition;
        public Texture2D Texture { get; }

        public BuildPlacementOverlay(MapDefinition map)
        {
            width=map.width;height=map.height;pixels=new Color32[width*height];
            Texture=new Texture2D(map.width,map.height,TextureFormat.RGBA32,false,true)
                {name="Construction placement zones",filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp};
            Texture.SetPixels32(pixels);Texture.Apply(false);
        }

        public bool Update(GameSession session)
        {
            var view=session.View;
            if(view==null||session.BuildDefinition==null)return false;
            if(ReferenceEquals(previousView,view)&&previousTick==view.tick&&previousDefinition==session.BuildDefinition)return false;
            previousView=view;previousTick=view.tick;previousDefinition=session.BuildDefinition;
            int size=session.Catalog[session.BuildDefinition].footprint;
            var allowed=session.BuildPlacementMask(size);
            for(int i=0;i<pixels.Length;i++)
            {
                // Unknown terrain stays unmarked; a visible origin can be blocked
                // by insufficient vision elsewhere in the proposed footprint.
                pixels[i]=view.visibility[i]!=2?new Color32(0,0,0,0)
                    :allowed[i]?new Color32(255,0,0,255):new Color32(0,255,0,255);
            }
            if(session.Catalog[session.BuildDefinition].role=="extractor")
            {
                // Only cells near gas can differ from ordinary grid placement.
                // Reuse the actual resolver, including nearest-deposit tie breaks.
                foreach(var deposit in view.resources)
                {
                    if(deposit.kind!="catalyst"||deposit.remaining<=0)continue;
                    int x0=Mathf.Max(0,Mathf.FloorToInt(deposit.position.x-2.5f)),x1=Mathf.Min(width-1,Mathf.CeilToInt(deposit.position.x+2.5f));
                    int y0=Mathf.Max(0,Mathf.FloorToInt(deposit.position.y-2.5f)),y1=Mathf.Min(height-1,Mathf.CeilToInt(deposit.position.y+2.5f));
                    for(int y=y0;y<=y1;y++)for(int x=x0;x<=x1;x++)
                    {
                        int i=y*width+x;if(view.visibility[i]!=2)continue;
                        var origin=session.BuildPlacement(new Point(x+.5f,y+.5f));
                        int ox=(int)origin.x,oy=(int)origin.y;
                        bool valid=ox>=0&&oy>=0&&ox<width&&oy<height&&allowed[oy*width+ox];
                        pixels[i]=valid?new Color32(255,0,0,255):new Color32(0,255,0,255);
                    }
                }
            }
            Texture.SetPixels32(pixels);Texture.Apply(false);return true;
        }

        public void Dispose()
        {if(Application.isPlaying)Object.Destroy(Texture);else Object.DestroyImmediate(Texture);}
    }
}
