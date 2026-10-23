using System;
using Frontier.Core;

namespace Frontier.Runtime
{
    // Spatial preview only. Uses the same public terrain and filtered observations
    // as the board, never the authoritative simulation or hidden enemy state.
    public sealed class BuildPlacementValidity
    {
        public readonly MapDefinition Definition;
        private readonly GridMap terrain;
        private readonly int width,height,stride;
        private readonly int[] blocked,elevation,resources,gas;
        private readonly bool[] valid;
        private PlayerView cachedView;
        private Catalog cachedCatalog;
        private string cachedDefinition;
        private int cachedTick,cachedSize;

        public BuildPlacementValidity(MapDefinition definition)
        {
            Definition=definition;terrain=new GridMap(definition);
            width=definition.width;height=definition.height;stride=width+1;
            int count=stride*(height+1);
            blocked=new int[count];elevation=new int[count];resources=new int[count];gas=new int[count];
            valid=new bool[width*height];
            for(int y=0;y<height;y++)for(int x=0;x<width;x++)
                elevation[(y+1)*stride+x+1]=terrain.Elevation[y*width+x];
            Prefix(elevation);
        }

        // Returned storage belongs to this cache. One boolean describes a full
        // footprint whose lower-left origin is that cell, not just that tile.
        public bool[] Mask(PlayerView view,Catalog catalog,string definition,int size)
        {
            if(ReferenceEquals(view,cachedView)&&ReferenceEquals(catalog,cachedCatalog)
                &&view!=null&&view.tick==cachedTick&&definition==cachedDefinition&&size==cachedSize)return valid;
            cachedView=view;cachedCatalog=catalog;cachedTick=view?.tick??-1;
            cachedDefinition=definition;cachedSize=size;
            Array.Clear(valid,0,valid.Length);
            if(view==null||catalog==null||size<1||size>width||size>height||view.visibility.Length!=valid.Length)return valid;
            Array.Clear(blocked,0,blocked.Length);Array.Clear(resources,0,resources.Length);Array.Clear(gas,0,gas.Length);
            for(int y=0;y<height;y++)for(int x=0;x<width;x++)
            {
                int i=y*width+x;
                if(view.visibility[i]!=2||!terrain.Buildable[i]||!terrain.Walkable[i]||terrain.Ramp[i])blocked[(y+1)*stride+x+1]=1;
            }
            foreach(var entity in view.entities)
            {
                if(entity.health<=0||!catalog.Has(entity.definition))continue;
                var type=catalog[entity.definition];
                if(!type.building)continue;
                int x0=Math.Max(0,(int)Math.Floor(entity.position.x)),y0=Math.Max(0,(int)Math.Floor(entity.position.y));
                int x1=Math.Min(width,(int)Math.Ceiling(entity.position.x+type.footprint)),y1=Math.Min(height,(int)Math.Ceiling(entity.position.y+type.footprint));
                // Unfinished foundations reserve the same space as completed buildings.
                for(int y=y0;y<y1;y++)for(int x=x0;x<x1;x++)blocked[(y+1)*stride+x+1]=1;
            }
            foreach(var resource in view.resources)
            {
                if(resource.remaining<=0)continue;
                int cell=terrain.Cell(resource.position);if(cell<0)continue;
                int index=(cell/width+1)*stride+cell%width+1;
                resources[index]++;if(resource.kind=="catalyst")gas[index]++;
            }
            Prefix(blocked);Prefix(resources);Prefix(gas);
            bool extractor=catalog.Has(definition)&&catalog[definition].role=="extractor";
            int area=size*size;
            for(int y=0;y<=height-size;y++)for(int x=0;x<=width-size;x++)
            {
                if(Sum(blocked,x,y,size)!=0)continue;
                int level=Sum(elevation,x,y,size);if(level!=0&&level!=area)continue;
                int deposits=Sum(resources,x,y,size);
                valid[y*width+x]=extractor?deposits==1&&Sum(gas,x,y,size)==1:deposits==0;
            }
            // Units move continuously: block origins by the exact circle/footprint
            // test, rather than rasterizing a whole cell and overstating their size.
            foreach(var entity in view.entities)
            {
                if(entity.owner==view.player||entity.remembered||entity.health<=0||!catalog.Has(entity.definition))continue;
                var type=catalog[entity.definition];if(type.building)continue;
                float radius=type.collisionRadius,radiusSquared=radius*radius;
                int x0=Math.Max(0,(int)Math.Floor(entity.position.x-radius)-size),y0=Math.Max(0,(int)Math.Floor(entity.position.y-radius)-size);
                int x1=Math.Min(width-size,(int)Math.Ceiling(entity.position.x+radius)),y1=Math.Min(height-size,(int)Math.Ceiling(entity.position.y+radius));
                for(int y=y0;y<=y1;y++)for(int x=x0;x<=x1;x++)
                {
                    float dx=entity.position.x-Math.Max(x,Math.Min(x+size,entity.position.x));
                    float dy=entity.position.y-Math.Max(y,Math.Min(y+size,entity.position.y));
                    if(dx*dx+dy*dy<radiusSquared)valid[y*width+x]=false;
                }
            }
            return valid;
        }

        private void Prefix(int[] values)
        {
            for(int y=1;y<=height;y++)
            {
                int row=0;
                for(int x=1;x<=width;x++){int i=y*stride+x;row+=values[i];values[i]=values[i-stride]+row;}
            }
        }

        private int Sum(int[] values,int x,int y,int size)
            =>values[(y+size)*stride+x+size]-values[y*stride+x+size]-values[(y+size)*stride+x]+values[y*stride+x];
    }
}
