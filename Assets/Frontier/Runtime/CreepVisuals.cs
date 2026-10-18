using System.Collections.Generic;
using Frontier.Core;
using UnityEngine;

namespace Frontier.Runtime
{
    // Presentation only: sources come from PlayerView, never the hidden simulation.
    // One small texture follows terrain UVs, including ramps, with no extra draws.
    public sealed class CreepVisuals : System.IDisposable
    {
        private sealed class Source
        {
            public Vector2 center;
            public float radius, maximum;
            public bool seen;
        }
        private readonly Dictionary<int, Source> sources = new Dictionary<int, Source>();
        private readonly List<int> expired = new List<int>();
        private readonly GridMap grid;
        private readonly Color32[] pixels;
        private readonly float[] field;
        private readonly int resolutionX, resolutionY;
        private int previousTick = -1;
        public Texture2D Texture { get; }
        public CreepVisuals(MapDefinition map)
        {
            grid = new GridMap(map); resolutionX = grid.Width * 2; resolutionY = grid.Height * 2;
            Texture = new Texture2D(resolutionX, resolutionY, TextureFormat.RGBA32, false) { name = "Observed creep", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            pixels = new Color32[resolutionX * resolutionY]; field = new float[pixels.Length];
            Texture.SetPixels32(pixels); Texture.Apply(false);
        }
        public void Update(PlayerView view, Catalog catalog)
        {
            if(view == null || view.tick == previousTick || previousTick >= 0 && view.tick > previousTick && view.tick - previousTick < 4)return;
            if(view.tick < previousTick){sources.Clear();System.Array.Clear(pixels,0,pixels.Length);previousTick=-1;}
            float elapsed = previousTick < 0 ? 0 : (view.tick - previousTick) / (float)Simulation.TickRate;
            previousTick = view.tick;
            foreach(var source in sources.Values)source.seen=false;
            foreach(var entity in view.entities)
            {
                var type=catalog[entity.definition];if(!type.building || type.faction!="biological")continue;
                if(!sources.TryGetValue(entity.id,out var source))
                {
                    // A remembered structure cannot invent an unseen infestation.
                    if(entity.remembered)continue;
                    source=new Source{center=new Vector2(entity.position.x+type.footprint*.5f,entity.position.y+type.footprint*.5f),radius=type.footprint*.45f};sources.Add(entity.id,source);
                }
                source.seen=true;
                if(!entity.remembered)
                {
                    source.maximum=type.footprint*.5f+(entity.complete?(type.role=="hq"?6:4):Mathf.Clamp01(entity.progress));
                    source.radius=Mathf.MoveTowards(source.radius,source.maximum,elapsed*.65f);
                }
            }
            expired.Clear();System.Array.Clear(field,0,field.Length);
            foreach(var pair in sources)
            {
                var source=pair.Value;
                if(!source.seen)
                {
                    int cx=Mathf.Clamp((int)source.center.x,0,grid.Width-1),cy=Mathf.Clamp((int)source.center.y,0,grid.Height-1);
                    if(view.visibility[grid.Index(cx,cy)]==2)source.radius=Mathf.Max(0,source.radius-elapsed*1.2f);
                    if(source.radius<=0){expired.Add(pair.Key);continue;}
                }
                int minX=Mathf.Max(0,Mathf.FloorToInt((source.center.x-source.radius-1)*2)),maxX=Mathf.Min(resolutionX-1,Mathf.CeilToInt((source.center.x+source.radius+1)*2));
                int minY=Mathf.Max(0,Mathf.FloorToInt((source.center.y-source.radius-1)*2)),maxY=Mathf.Min(resolutionY-1,Mathf.CeilToInt((source.center.y+source.radius+1)*2));
                for(int y=minY;y<=maxY;y++)for(int x=minX;x<=maxX;x++)
                {
                    int cell=grid.Index(x/2,y/2);if(!grid.Walkable[cell])continue;
                    var offset=new Vector2((x+.5f)*.5f,(y+.5f)*.5f)-source.center;
                    float angle=Mathf.Atan2(offset.y,offset.x),seed=pair.Key*2.39996f;
                    float radius=source.radius*(.94f+.06f*Mathf.Sin(angle*3+seed)+.045f*Mathf.Sin(angle*5-seed));
                    int i=y*resolutionX+x;field[i]=Mathf.Max(field[i],Mathf.Clamp01((radius-offset.magnitude)/1.2f+.5f));
                }
            }
            foreach(int id in expired)sources.Remove(id);
            for(int y=0;y<resolutionY;y++)for(int x=0;x<resolutionX;x++)
            {
                int i=y*resolutionX+x;byte visibility=view.visibility[grid.Index(x/2,y/2)];
                // Explored fog keeps its last observed coverage; unexplored terrain
                // cannot receive knowledge from the edge of an adjacent source.
                if(visibility==2)pixels[i]=new Color32((byte)Mathf.RoundToInt(field[i]*255),0,0,255);
                else if(visibility==0)pixels[i]=new Color32(0,0,0,255);
            }
            Texture.SetPixels32(pixels);Texture.Apply(false);
        }
        public void Dispose(){if(Application.isPlaying)Object.Destroy(Texture);else Object.DestroyImmediate(Texture);}
    }
}
