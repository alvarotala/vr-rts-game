using System;
using System.Linq;
using Frontier.Core;
using Frontier.Runtime;
using UnityEditor;
using UnityEngine;

namespace Frontier.Editor
{
    public static class BuildPlacementValidityChecks
    {
        [MenuItem("Frontier/Validate building placement areas")]
        public static void Run()
        {
            var catalog=new Catalog(JsonUtility.FromJson<CatalogDefinition>(Resources.Load<TextAsset>("catalog").text));
            var definition=new MapDefinition
            {
                id="placement-validity-checks",title="Placement validity checks",width=32,height=24,
                spawns=new[]{new SpawnDefinition{player=0,x=1,y=1},new SpawnDefinition{player=1,x=26,y=18}},
                resources=new[]
                {
                    new ResourceDefinition{id="mineral-a",x=5,y=8},new ResourceDefinition{id="gas-a",kind="catalyst",x=9,y=8},
                    new ResourceDefinition{id="mineral-b",x=24,y=15},new ResourceDefinition{id="gas-b",kind="catalyst",x=21,y=15}
                },
                terrain=new[]
                {
                    new TerrainRegion{x=10,y=5,width=8,height=6,elevation=1},
                    new TerrainRegion{x=8,y=7,width=4,height=2,elevation=1,ramp=true},
                    new TerrainRegion{x=18,y=8,width=2,height=5,walkable=false,buildable=false},
                    new TerrainRegion{x=5,y=14,width=3,height=2,buildable=false}
                }
            };
            var terrain=new GridMap(definition);
            var view=new PlayerView{player=0,tick=1,visibility=Enumerable.Repeat((byte)2,terrain.Count).ToArray()};
            for(int x=23;x<32;x++)view.visibility[x]=0;
            for(int y=3;y<6;y++)for(int x=21;x<24;x++)view.visibility[y*32+x]=1;
            view.resources=definition.resources.Select((r,i)=>new ResourceState{id=i+1,kind=r.kind,remaining=r.amount,position=new Point(r.x+.5f,r.y+.5f)})
                .Concat(new[]
                {
                    new ResourceState{id=5,kind="catalyst",remaining=500,position=new Point(20.5f,15.5f)},
                    new ResourceState{id=6,kind="mineral",remaining=0,position=new Point(3.5f,18.5f)}
                }).ToArray();
            var hq=catalog.Role("industrial","hq");var worker=catalog.Role("industrial","worker");
            view.entities=new[]
            {
                Entity(1,0,hq.id,1,1),Entity(2,0,catalog.Role("industrial","supply").id,16,16,complete:false),
                Entity(3,1,catalog.Role("biological","barracks").id,25,15),
                Entity(4,1,catalog.Role("biological","supply").id,23,5,remembered:true),
                Entity(5,1,hq.id,19,18,health:0),Entity(6,0,worker.id,6.2f,10.2f),
                Entity(7,1,worker.id,14.2f,13.8f),Entity(8,1,worker.id,20.5f,19.5f,remembered:true),
                Entity(9,1,worker.id,6.5f,20.5f,health:0)
            };
            view.entities[1].awaitingClearance=true;
            var cache=new BuildPlacementValidity(definition);int checks=0,cells=0;
            foreach(var type in catalog.Definition.archetypes.Where(t=>t.building))Compare(type);
            int origin=18*32;
            var mask=cache.Mask(view,catalog,hq.id,hq.footprint);
            Check(mask[origin],"Open location accepts the headquarters footprint");
            Check(ReferenceEquals(mask,cache.Mask(view,catalog,hq.id,hq.footprint)),"Repeated previews reuse their storage");
            view.resources=view.resources.Concat(new[]{new ResourceState{id=10,kind="mineral",remaining=100,position=new Point(2.5f,20.5f)}}).ToArray();view.tick++;
            Check(!cache.Mask(view,catalog,hq.id,hq.footprint)[origin],"New observed minerals invalidate the same view on the next tick");
            view.resources.Last().remaining=0;view.tick++;
            Check(cache.Mask(view,catalog,hq.id,hq.footprint)[origin],"Depleted minerals release their full exclusion footprint");
            view.entities=view.entities.Concat(new[]{Entity(10,1,worker.id,3.95f,22.1f)}).ToArray();view.tick++;
            Check(!cache.Mask(view,catalog,hq.id,hq.footprint)[origin],"Enemy circle intersecting only a footprint edge blocks placement");
            view.entities.Last().position=new Point(4.5f,22.5f);view.tick++;
            Check(cache.Mask(view,catalog,hq.id,hq.footprint)[origin],"Enemy circle outside a corner does not overblock its bounding square");
            view.entities.Last().position=new Point(2,20);view.entities.Last().remembered=true;view.tick++;
            Check(cache.Mask(view,catalog,hq.id,hq.footprint)[origin],"Remembered mobile positions never block current placement");
            view.entities.Last().remembered=false;view.entities.Last().owner=0;view.tick++;
            Check(cache.Mask(view,catalog,hq.id,hq.footprint)[origin],"Friendly units can clear a construction site");
            var replacement=new PlayerView{player=0,tick=view.tick,entities=view.entities,resources=view.resources,visibility=(byte[])view.visibility.Clone()};
            replacement.visibility[origin]=1;
            Check(!cache.Mask(replacement,catalog,hq.id,hq.footprint)[origin],"A replacement filtered view invalidates the cache even at the same tick");
            Check(!cache.Mask(view,catalog,hq.id,0).Any(x=>x),"Empty footprints are invalid");
            Check(!cache.Mask(view,catalog,hq.id,40).Any(x=>x),"Oversized footprints are invalid");
            Check(!cache.Mask(null,catalog,hq.id,4).Any(x=>x),"Missing player view leaves no legal placement");
            foreach(var type in catalog.Definition.archetypes.Where(t=>t.building))Compare(type);
            Debug.Log($"FRONTIER_BUILD_PLACEMENT_VALIDITY passed: {checks} checks, {cells} origins compared; both factions, every building, terrain, vision, resources and filtered occupancy");

            void Compare(Archetype type)
            {
                var result=cache.Mask(view,catalog,type.id,type.footprint);
                for(int y=0;y<definition.height;y++)for(int x=0;x<definition.width;x++)
                {
                    bool expected=Expected(new Point(x,y),type);
                    if(result[y*definition.width+x]!=expected)throw new Exception($"Placement mismatch for {type.id} at {x},{y}: expected {expected}");
                    cells++;
                }
                checks++;
            }
            bool Expected(Point originPoint,Archetype type)
            {
                int size=type.footprint;if(!terrain.CanPlace(originPoint,size))return false;
                int x=(int)originPoint.x,y=(int)originPoint.y;
                for(int dy=0;dy<size;dy++)for(int dx=0;dx<size;dx++)if(view.visibility[(y+dy)*definition.width+x+dx]!=2)return false;
                var covered=view.resources.Where(r=>r.remaining>0&&r.position.x>=x&&r.position.x<x+size&&r.position.y>=y&&r.position.y<y+size).ToArray();
                if(type.role=="extractor"?(covered.Length!=1||covered[0].kind!="catalyst"):covered.Length>0)return false;
                foreach(var entity in view.entities)
                {
                    if(entity.health<=0)continue;var other=catalog[entity.definition];
                    if(other.building)
                    {
                        if(x<entity.position.x+other.footprint&&x+size>entity.position.x&&y<entity.position.y+other.footprint&&y+size>entity.position.y)return false;
                    }
                    else if(entity.owner!=view.player&&!entity.remembered)
                    {
                        var nearest=new Point(Math.Max(x,Math.Min(x+size,entity.position.x)),Math.Max(y,Math.Min(y+size,entity.position.y)));
                        if(entity.position.Distance(nearest)<other.collisionRadius)return false;
                    }
                }
                return true;
            }
            void Check(bool condition,string message){if(!condition)throw new Exception(message);checks++;}
        }

        private static VisibleEntity Entity(int id,int owner,string definition,float x,float y,bool complete=true,bool remembered=false,float health=100)
            =>new VisibleEntity{id=id,owner=owner,definition=definition,position=new Point(x,y),complete=complete,remembered=remembered,health=health};
    }
}
