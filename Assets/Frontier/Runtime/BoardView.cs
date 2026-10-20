using System.Collections.Generic;
using System.Linq;
using Frontier.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace Frontier.Runtime
{
    public sealed class BoardView : MonoBehaviour
    {
        public GameSession Session;
        public Camera Viewer;
        public Transform Content { get; private set; }
        public float Width = 1.2f, Zoom = 1;
        public Vector2 Pan;
        public bool Placed;
        public const string BaseTargetingKey="frontier.input.base-targeting.v1";
        public bool BaseTargeting { get; private set; }
        public int Hovered;
        public int HoveredResource;
        public static readonly Color NormalPointerColor = new Color(.3f,.95f,.94f);
        public static readonly Color AttackPointerColor = new Color(1f,.16f,.12f);
        public static readonly Color DamageAbilityColor = new Color(.85f,.4f,1f), HealingAbilityColor = new Color(.3f,1f,.55f);
        public Color PointerColor => Session != null && Session.AbilityPointerActive
            ? (Session.PendingAbility.effect=="heal"?HealingAbilityColor:DamageAbilityColor)
            : Session != null && Session.AttackPointerActive ? AttackPointerColor : NormalPointerColor;
        private Transform terrainRoot;
        private MeshCollider terrainCollider;
        private Matrix4x4 raycastTransform;
        private readonly Dictionary<int, Transform> entities = new Dictionary<int, Transform>();
        private readonly Dictionary<int, Transform> resources = new Dictionary<int, Transform>();
        private readonly Dictionary<int, Renderer> resourceRenderers = new Dictionary<int, Renderer>();
        private readonly Dictionary<string, Stack<Transform>> pools = new Dictionary<string, Stack<Transform>>();
        private readonly Dictionary<int,string> entityTypes = new Dictionary<int,string>();
        private readonly Dictionary<int,Point> pendingMoveFacing = new Dictionary<int,Point>();
        private readonly Dictionary<Transform, VisualParts> parts = new Dictionary<Transform, VisualParts>();
        private readonly HashSet<int> alive = new HashSet<int>();
        private readonly List<int> removed = new List<int>();
        private sealed class VisualParts
        {
            public Transform selection, health, impact, model;
            public Renderer healthRenderer, impactRenderer;
            public Renderer[] body;
            public Material[] visibleMaterials;
            public bool remembered, hovered, receivingDamage, complete;
            public float damageAmount;
            public readonly UnitFacing facing = new UnitFacing();
            public SoldierAnimation soldier;
        }
        private Material ground, blue, red, neutral, resourceMaterial, highlight;
        private Material unitStructure, organicTissue, impactMaterial;
        private MaterialPropertyBlock hoverProperties, impactProperties;
        private static readonly Color AllyHover=new Color(1,.92f,.32f),EnemyHover=new Color(1,.3f,.18f);
        private readonly UnitVisuals unitVisuals=new UnitVisuals();
        private readonly BuildingVisuals buildingVisuals=new BuildingVisuals();
        private readonly ResourceVisuals resourceVisuals=new ResourceVisuals();
        private readonly Dictionary<string,Material> buildingMaterials=new Dictionary<string,Material>();
        private Material[] memoryMaterials, constructionMaterials;
        private WeaponEffects weapons;
        private AbilityEffects abilityEffects;
        private HarvestingMarkers harvestingMarkers;
        private WorkerVisuals workerVisuals;
        private CreepVisuals creepVisuals;
        private OrderQueueVisuals orderQueueVisuals;
        private readonly Dictionary<int,AttackEvent> firing=new Dictionary<int,AttackEvent>();
        private readonly Dictionary<int,VisibleEntity> observedEntities=new Dictionary<int,VisibleEntity>();
        private readonly Dictionary<string,Material> roleMaterials=new Dictionary<string,Material>();
        private Texture2D fog;
        private Color32[] fogColors;
        private int fogTick = -1;
        private Transform preview;
        private Point? pointer;
        public Point? Pointer
        {
            get=>pointer;
            set{pointer=value;if(!value.HasValue)HoveredResource=0;UpdatePointer();}
        }
        public float CellScale => Width / Session.Map.width * Zoom;
        public float Depth => Width * Session.Map.height / Session.Map.width;
        private void Start()
        {
            BaseTargeting=PlayerPrefs.GetInt(BaseTargetingKey,0)!=0;
            hoverProperties=new MaterialPropertyBlock();
            impactProperties=new MaterialPropertyBlock();
            impactProperties.SetFloat("_Ring",1);
            impactMaterial=new Material(Shader.Find("Frontier/Impact"));
            Content = new GameObject("Map coordinates").transform; Content.SetParent(transform,false);
            ground=Material(new Color(.16f,.31f,.31f));ground.SetFloat("_UseFog",1);ground.SetFloat("_Grid",1);
            blue=Material(new Color(.2f,.8f,1));red=Material(new Color(1,.36f,.23f));
            neutral=Material(new Color(.2f,.28f,.35f));resourceMaterial=Material(Color.white);highlight=Material(new Color(.9f,1,.6f));
            unitStructure=Material(new Color(.12f,.17f,.22f));
            organicTissue=Material(Color.white);
            foreach(string role in new[]{"worker","scout","melee","ranged","siege","caster"})roleMaterials[role]=Material(UnitVisuals.Accent(role));
            foreach(string role in new[]{"hq","barracks","extractor","supply","tech","academy","defense"})buildingMaterials[role]=Material(BuildingVisuals.Accent(role));
            var memoryStructure=Material(new Color(.12f,.17f,.22f));memoryStructure.SetFloat("_Desaturate",1);
            memoryMaterials=new[]{neutral,memoryStructure,neutral};
            constructionMaterials=new[]{Material(new Color(.52f,.52f,.52f)),Material(new Color(.19f,.19f,.19f)),Material(new Color(.78f,.78f,.78f))};
            foreach(var material in constructionMaterials)material.SetFloat("_Desaturate",1);
            neutral.SetFloat("_Desaturate",1);
            Session.NewMatch += Rebuild; Session.MatchClosed += Rebuild; Session.UnitMoveOrdered += FaceMoveOrder; Rebuild();
        }
        private void OnDestroy()
        {
            foreach(var visual in parts.Values)visual.soldier?.Dispose();
            if(Session!=null){Session.NewMatch-=Rebuild;Session.MatchClosed-=Rebuild;Session.UnitMoveOrdered-=FaceMoveOrder;}
            unitVisuals.Dispose();
            buildingVisuals.Dispose();
            resourceVisuals.Dispose();
            weapons?.Dispose();
            abilityEffects?.Dispose();
            harvestingMarkers?.Dispose();
            workerVisuals?.Dispose();
            creepVisuals?.Dispose();
            orderQueueVisuals?.Dispose();
            foreach(var material in roleMaterials.Values)Release(material);
            foreach(var material in buildingMaterials.Values)Release(material);
            if(constructionMaterials!=null)foreach(var material in constructionMaterials)Release(material);
            if(memoryMaterials!=null)Release(memoryMaterials[1]);
            foreach(var material in new[]{ground,blue,red,neutral,resourceMaterial,highlight,unitStructure,organicTissue,impactMaterial})Release(material);
            Release(fog);
            if(terrainCollider!=null)Release(terrainCollider.sharedMesh);
        }
        private static void Release(Object resource)
        {if(resource!=null){if(Application.isPlaying)Destroy(resource);else DestroyImmediate(resource);}}
        private Material Material(Color color) { var m=new Material(Shader.Find("Frontier/Board")){enableInstancing=true};m.SetColor("_BaseColor",color);return m; }
        public void Rebuild()
        {
            if(Content==null)return;
            foreach(var visual in parts.Values)visual.soldier?.Dispose();
            weapons?.Dispose();weapons=null;firing.Clear();
            abilityEffects?.Dispose();abilityEffects=null;
            harvestingMarkers?.Dispose();harvestingMarkers=null;
            workerVisuals?.Dispose();workerVisuals=null;
            creepVisuals?.Dispose();creepVisuals=null;
            orderQueueVisuals?.Dispose();orderQueueVisuals=null;
            resourceVisuals.ClearVapor();
            if(terrainCollider!=null)Release(terrainCollider.sharedMesh);
            for(int i=Content.childCount-1;i>=0;i--)Release(Content.GetChild(i).gameObject);
            entities.Clear();entityTypes.Clear();pendingMoveFacing.Clear();resources.Clear();resourceRenderers.Clear();pools.Clear();parts.Clear();preview=null;
            Hovered=0;Pointer=null;ground.SetFloat("_HoverWidth",0);
            DrawSelection(default,default,false);
            Release(fog);
            fog=new Texture2D(Session.Map.width,Session.Map.height,TextureFormat.RGBA32,false){filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp};
            fogColors=new Color32[Session.Map.width*Session.Map.height];ground.SetTexture("_FogTex",fog);fogTick=-1;
            weapons=new WeaponEffects(Content,fog,Session.Map.width,Session.Map.height);
            abilityEffects=new AbilityEffects(Content,fog,Session.Map.width,Session.Map.height);
            harvestingMarkers=new HarvestingMarkers(Content);
            workerVisuals=new WorkerVisuals(Content);
            creepVisuals=new CreepVisuals(Session.Map);ground.SetTexture("_CreepTex",creepVisuals.Texture);
            orderQueueVisuals=new OrderQueueVisuals(Content);
            resourceVisuals.BindVapor(Content,fog,Session.Map.width,Session.Map.height);
            var grid=new GridMap(Session.Map);
            terrainRoot=new GameObject("Terrain").transform;terrainRoot.SetParent(Content,false);
            var vertices=new List<Vector3>();var triangles=new List<int>();var colors=new List<Color>();var uv=new List<Vector2>();
            for(int y=0;y<grid.Height;y++)for(int x=0;x<grid.Width;x++)
            {
                int i=grid.Index(x,y);float h=Height(grid,i);
                Color c=!grid.Walkable[i]?new Color(.35f,.39f,.45f):grid.Ramp[i]?new Color(.75f,.72f,.45f):grid.Elevation[i]>0?new Color(.8f,.88f,.77f):Color.white;
                float VertexHeight(float vx,float vy)
                {
                    if(!grid.Ramp[i])return h;
                    var ramp=Session.Map.terrain.Last(r=>r.ramp&&x>=r.x&&x<r.x+r.width&&y>=r.y&&y<r.y+r.height);
                    float t=ramp.rampAxis=="x"?(vx-ramp.x)/ramp.width:(vy-ramp.y)/ramp.height;
                    return (ramp.rampDirection>0?t:1-t)*1.5f;
                }
                AddQuad(new Vector3(x,VertexHeight(x,y),y),new Vector3(x,VertexHeight(x,y+1),y+1),new Vector3(x+1,VertexHeight(x+1,y+1),y+1),new Vector3(x+1,VertexHeight(x+1,y),y),c);
                if(h>0)
                {
                    AddQuad(new Vector3(x,0,y),new Vector3(x,h,y),new Vector3(x+1,h,y),new Vector3(x+1,0,y),c*.7f);
                    AddQuad(new Vector3(x+1,0,y+1),new Vector3(x+1,h,y+1),new Vector3(x,h,y+1),new Vector3(x,0,y+1),c*.7f);
                    AddQuad(new Vector3(x,0,y+1),new Vector3(x,h,y+1),new Vector3(x,h,y),new Vector3(x,0,y),c*.7f);
                    AddQuad(new Vector3(x+1,0,y),new Vector3(x+1,h,y),new Vector3(x+1,h,y+1),new Vector3(x+1,0,y+1),c*.7f);
                }
            }
            void AddQuad(Vector3 a,Vector3 b,Vector3 c,Vector3 d,Color tint)
            {int n=vertices.Count;vertices.AddRange(new[]{a,b,c,d});triangles.AddRange(new[]{n,n+1,n+2,n,n+2,n+3});foreach(var v in new[]{a,b,c,d}){uv.Add(new Vector2(v.x/grid.Width,v.z/grid.Height));colors.Add(tint);}}
            var mesh=new Mesh{indexFormat=IndexFormat.UInt32};mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.SetUVs(0,uv);mesh.SetColors(colors);mesh.RecalculateNormals();mesh.RecalculateBounds();
            terrainRoot.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;terrainRoot.gameObject.AddComponent<MeshRenderer>().sharedMaterial=ground;
            terrainCollider=terrainRoot.gameObject.AddComponent<MeshCollider>();terrainCollider.sharedMesh=mesh;
            raycastTransform=Matrix4x4.zero;
            foreach(var d in Session.Map.decoration) { var rock=Primitive(PrimitiveType.Sphere,Content,neutral);rock.localPosition=new Vector3(d.x,1.2f,d.y);rock.localScale=new Vector3(4,2.4f,4); }
            Zoom=1;Pan=Vector2.zero;UpdateTransform();UpdateFog();
        }
        public void Fit() { Zoom=1;Pan=Vector2.zero; }
        private void FaceMoveOrder(int id,Point destination) => pendingMoveFacing[id]=destination;
        public void SetVisible(bool visible)
        {
            if(Content==null)return;
            Content.gameObject.SetActive(visible);
            if(!visible)
            {
                weapons?.Clear();
                abilityEffects?.Clear();
                harvestingMarkers?.Hide();
                workerVisuals?.Hide();
                orderQueueVisuals?.Hide();
                Hovered=0;Pointer=null;DrawSelection(default,default,false);
                if(ground!=null)ground.SetFloat("_HoverWidth",0);
                if(preview!=null)preview.gameObject.SetActive(false);
            }
        }
        public void Navigate(Vector2 move,float zoom,float delta)
        {
            Zoom=Mathf.Clamp(Zoom*Mathf.Exp(zoom*delta),1,4);
            Pan+=move*(Session.Map.width*.35f*delta/Zoom);
            float limitX=Session.Map.width*.5f*(1-1/Zoom),limitY=Session.Map.height*.5f*(1-1/Zoom);
            Pan=new Vector2(Mathf.Clamp(Pan.x,-limitX,limitX),Mathf.Clamp(Pan.y,-limitY,limitY));
        }
        private void UpdateTransform()
        {
            if(Content==null)return;
            Content.localScale=Vector3.one*CellScale;
            Content.localPosition=new Vector3(-(Session.Map.width*.5f+Pan.x)*CellScale,0,-(Session.Map.height*.5f+Pan.y)*CellScale);
            Shader.SetGlobalMatrix("_FrontierWorldToBoard",transform.worldToLocalMatrix);
            Shader.SetGlobalVector("_FrontierBounds",new Vector4(Width*.5f,Depth*.5f,Session.Map.width,Session.Map.height));
        }
        private void LateUpdate()
        {
            UpdateTransform(); UpdateFog(); UpdatePointer();
            if(ground!=null)ground.SetFloat("_HoverWidth",0);
            if(Session.View==null){weapons?.Clear();abilityEffects?.Clear();harvestingMarkers?.Hide();workerVisuals?.Hide();orderQueueVisuals?.Hide();return;}
            workerVisuals.Begin(this,Time.unscaledDeltaTime);
            float combatTick=weapons.AdvanceClock(this);
            creepVisuals.Update(Session.View,Session.Catalog);ground.SetFloat("_CreepTime",combatTick/Simulation.TickRate);
            observedEntities.Clear();foreach(var entity in Session.View.entities)observedEntities[entity.id]=entity;
            firing.Clear();
            foreach(var shot in Session.View.attacks)
                if(Session.View.tick>=shot.tick&&Session.View.tick-shot.tick<Simulation.TickRate)firing[shot.attacker]=shot;
            alive.Clear();
            foreach(var e in Session.View.entities)
            {
                alive.Add(e.id);var type=Session.Catalog[e.definition];
                bool newlyVisible=!entities.TryGetValue(e.id,out var visual);
                if(newlyVisible)
                {
                    string key=e.definition+"/"+e.owner;
                    if(!pools.TryGetValue(key,out var pool))pools[key]=pool=new Stack<Transform>();
                    visual=pool.Count>0?pool.Pop():MakeEntity(type,e.owner);visual.gameObject.SetActive(true);entities[e.id]=visual;entityTypes[e.id]=key;
                }
                var point=e.position;float half=type.building?type.footprint*.5f:0;
                var target=new Vector3(point.x+half,HeightAt(point),point.y+half);
                var cached=parts[visual];
                bool ownWorker=type.role=="worker"&&e.owner==Session.View.player&&!e.remembered;
                var work=ownWorker?WorkerVisuals.Describe(e,Session.View,Session.Catalog):default;
                AttackEvent? attack=null;
                Vector3? aim=null;
                if((!type.building||type.role=="defense")&&!e.remembered&&firing.TryGetValue(e.id,out var shot))
                {
                    attack=shot;
                    var direction=new Vector3(shot.destination.x-shot.origin.x,0,shot.destination.y-shot.origin.y);
                    if(observedEntities.TryGetValue(shot.target,out var victim)&&!victim.remembered)
                    {
                        var victimType=Session.Catalog[victim.definition];float offset=victimType.building?victimType.footprint*.5f:0;
                        direction=new Vector3(victim.position.x+offset-target.x,0,victim.position.y+offset-target.z);
                    }
                    if(direction.sqrMagnitude>.001f)aim=direction;
                }
                if(!type.building)
                {
                    if(ownWorker&&work.active)
                        aim=new Vector3(work.target.x-point.x,0,work.target.y-point.y);
                    // A previous shot can remain in the effects history after a new MOVE.
                    if(e.owner==Session.View.player&&e.order==CommandKind.Move){aim=null;attack=null;}
                    if(newlyVisible)
                    {
                        cached.facing.Reset(target,aim.HasValue?Quaternion.LookRotation(aim.Value):Quaternion.identity,Session.View.tick);
                        cached.soldier?.Reset(e.position,Session.View.tick);
                    }
                    if(pendingMoveFacing.TryGetValue(e.id,out var moveTarget))
                    {
                        if(e.order==CommandKind.Move||e.order==CommandKind.AttackMove)
                            cached.facing.FaceDestination(target,new Vector3(moveTarget.x,target.y,moveTarget.y));
                        pendingMoveFacing.Remove(e.id);
                    }
                    visual.localRotation=cached.facing.Step(target,Session.View.tick,Time.unscaledDeltaTime,Session.IsPaused,aim);
                }
                else if(aim.HasValue)visual.localRotation=Quaternion.LookRotation(aim.Value);
                visual.localPosition=type.building||newlyVisible?target:Vector3.Lerp(visual.localPosition,target,1-Mathf.Exp(-Time.unscaledDeltaTime*22));
                visual.localScale=Vector3.one*(e.complete?1:Mathf.Lerp(.25f,1,e.progress));
                if(ownWorker)workerVisuals.Add(e,work,visual,cached.model,type);
                cached.soldier?.Apply(e,Session.View.tick,combatTick,attack,aim);
                if(type.building&&type.faction=="biological")
                    cached.model.localScale=new Vector3(1,e.complete&&!e.remembered?1+.018f*Mathf.Sin(combatTick/Simulation.TickRate*2+e.id):1,1);
                bool hovered=Hovered==e.id&&!e.remembered;
                cached.selection.gameObject.SetActive(Session.Selection.Contains(e.id));
                int damageAge=Session.View.tick-e.damageTick;
                bool receivingDamage=!e.remembered&&e.damageTick>=0&&damageAge>=0&&damageAge<12;
                float damageAmount=receivingDamage?Mathf.Clamp01(1-damageAge/4f)*.9f:0;
                Color hoverColor=e.owner==Session.View.player?AllyHover:EnemyHover;
                if(newlyVisible||cached.hovered!=hovered||cached.damageAmount!=damageAmount)
                {
                    hoverProperties.SetFloat("_HoverAmount",hovered?1:0);hoverProperties.SetColor("_HoverColor",hoverColor);
                    hoverProperties.SetFloat("_DamageAmount",damageAmount);
                    foreach(var renderer in cached.body)renderer.SetPropertyBlock(hoverProperties);
                    cached.hovered=hovered;cached.damageAmount=damageAmount;
                }
                if(hovered)
                {
                    // Paint the hint on the terrain itself so it follows ramps and never forms walls.
                    float radius=type.building?type.footprint*.5f+.45f:Mathf.Max(type.collisionRadius+.35f,.008f/CellScale);
                    ground.SetVector("_HoverArea",new Vector4(visual.localPosition.x,visual.localPosition.z,radius,type.building?1:0));
                    ground.SetFloat("_HoverWidth",Mathf.Clamp(.0018f/CellScale,.035f,.2f));
                    ground.SetColor("_HoverColor",hoverColor);
                }
                cached.health.gameObject.SetActive(e.health<type.health-.5f||Session.Selection.Contains(e.id)||hovered||receivingDamage);
                cached.health.localScale=new Vector3(Mathf.Max(.03f,e.health/type.health)*(type.building?type.footprint:1.4f),.1f,.12f);
                if(newlyVisible||cached.receivingDamage!=receivingDamage)
                {cached.healthRenderer.sharedMaterial=receivingDamage?impactMaterial:highlight;cached.receivingDamage=receivingDamage;}
                cached.impact.gameObject.SetActive(receivingDamage);
                if(receivingDamage)
                {
                    // One reusable marker per entity; simultaneous hits refresh it instead of stacking effects.
                    float diameter=type.building?type.footprint*1.25f:Mathf.Max(1.6f,.014f/CellScale);
                    cached.impact.localScale=Vector3.one*diameter;
                    impactProperties.SetFloat("_Progress",damageAge/12f);cached.impactRenderer.SetPropertyBlock(impactProperties);
                }
                // Mobile units retain their role accents; only buildings become fog memories.
                if(type.building&&(newlyVisible||cached.remembered!=e.remembered||cached.complete!=e.complete))
                {
                    foreach(var renderer in cached.body)renderer.sharedMaterials=e.remembered?memoryMaterials:e.complete?cached.visibleMaterials:constructionMaterials;
                    cached.remembered=e.remembered;cached.complete=e.complete;
                }
            }
            workerVisuals.End();
            removed.Clear();foreach(int id in entities.Keys)if(!alive.Contains(id))removed.Add(id);
            foreach(int id in removed) { var v=entities[id];v.gameObject.SetActive(false);pools[entityTypes[id]].Push(v);entities.Remove(id);entityTypes.Remove(id);pendingMoveFacing.Remove(id); }
            foreach(var resource in Session.View.resources)
            {
                if(!resources.TryGetValue(resource.id,out var visual))
                {
                    var model=new GameObject(resource.kind=="mineral"?"Mineral crystals":"Gas vent",typeof(MeshFilter),typeof(MeshRenderer));
                    visual=model.transform;visual.SetParent(Content,false);model.GetComponent<MeshFilter>().sharedMesh=resourceVisuals.Get(resource.kind,resource.id);
                    var renderer=model.GetComponent<Renderer>();renderer.sharedMaterial=resourceMaterial;
                    resources[resource.id]=visual;resourceRenderers[resource.id]=renderer;visual.localRotation=Quaternion.Euler(0,resource.id*137.5f%360,0);
                }
                visual.localPosition=new Vector3(resource.position.x,HeightAt(resource.position),resource.position.y);visual.gameObject.SetActive(resource.remaining!=0);
                bool hovered=Hovered==0&&HoveredResource==resource.id&&Pointer.HasValue&&!Session.IsPaused&&ResourceVisible(resource,2);
                hoverProperties.SetFloat("_HoverAmount",hovered?1:0);hoverProperties.SetFloat("_DamageAmount",0);hoverProperties.SetColor("_HoverColor",AllyHover);
                resourceRenderers[resource.id].SetPropertyBlock(hoverProperties);
                if(hovered)
                {
                    ground.SetVector("_HoverArea",new Vector4(resource.position.x,resource.position.y,Mathf.Clamp(.012f/CellScale,1.2f,1.5f),0));
                    ground.SetFloat("_HoverWidth",Mathf.Clamp(.0018f/CellScale,.035f,.2f));ground.SetColor("_HoverColor",AllyHover);
                }
            }
            if(Session.BuildDefinition!=null&&Pointer.HasValue)
            {
                if(preview==null)preview=Primitive(PrimitiveType.Cube,Content,highlight);
                int size=Session.Catalog[Session.BuildDefinition].footprint;Point p=Pointer.Value;
                preview.gameObject.SetActive(true);preview.localPosition=new Vector3(Mathf.Floor(p.x)+size*.5f,HeightAt(p)+.15f,Mathf.Floor(p.y)+size*.5f);preview.localScale=new Vector3(size,.3f,size);
                bool valid=Session.Match.Map.CanPlace(new Point(Mathf.Floor(p.x),Mathf.Floor(p.y)),size)&&Session.Match.Visible(0,p);
                preview.GetComponent<Renderer>().sharedMaterial=valid?highlight:red;
            }
            else if(preview!=null)preview.gameObject.SetActive(false);
            weapons?.Render(this,combatTick,Viewer!=null?Viewer:Camera.main);
            abilityEffects?.Render(this,combatTick,Viewer!=null?Viewer:Camera.main);
            var viewer=Viewer!=null?Viewer:Camera.main;
            resourceVisuals.UpdateVapor(this,viewer);
            harvestingMarkers?.Update(this,viewer);
            orderQueueVisuals?.Update(this);
        }
        public bool TryEntityTop(int id,out Vector3 top)
        {
            top=default;if(!entities.TryGetValue(id,out var visual)||!visual.gameObject.activeInHierarchy)return false;
            var body=parts[visual].body;if(body.Length==0)return false;
            var bounds=body[0].bounds;for(int i=1;i<body.Length;i++)bounds.Encapsulate(body[i].bounds);
            top=new Vector3(bounds.center.x,bounds.max.y,bounds.center.z);return true;
        }
        public bool TryCombatCenter(int id,out Vector3 center)
        {
            center=default;
            if(!entities.TryGetValue(id,out var visual)||!visual.gameObject.activeInHierarchy||!observedEntities.TryGetValue(id,out var entity)||entity.remembered)return false;
            center=visual.localPosition;return true;
        }
        public bool TryWeaponMuzzle(int id,int shotId,out Vector3 muzzle)
        {
            muzzle=default;
            if(!entities.TryGetValue(id,out var visual)||!visual.gameObject.activeInHierarchy||parts[visual].soldier==null)return false;
            muzzle=Content.InverseTransformPoint(parts[visual].soldier.Muzzle(shotId));return true;
        }
        private Transform MakeEntity(Archetype type,int owner)
        {
            var root=new GameObject(type.name).transform;root.SetParent(Content,false);
            Material color=owner==0?blue:red;
            float size=type.building?type.footprint*.85f:type.collisionRadius*2;
            var model=new GameObject(type.building?"Building silhouette":"Role silhouette",typeof(MeshFilter),typeof(MeshRenderer));model.transform.SetParent(root,false);
            bool soldier=!type.building&&type.role!="worker";
            var mesh=type.building?buildingVisuals.Get(type):unitVisuals.Get(type,type.role=="worker"&&owner==(Session.View?.player??0),soldier);model.GetComponent<MeshFilter>().sharedMesh=mesh;
            var visibleMaterials=new[]{color,type.faction=="biological"?organicTissue:unitStructure,type.building?buildingMaterials[type.role]:roleMaterials[type.role]};
            model.GetComponent<MeshRenderer>().sharedMaterials=visibleMaterials;
            float height=mesh.bounds.max.y;
            var ring=Primitive(PrimitiveType.Cylinder,root,highlight);ring.name="Selection";ring.localPosition=Vector3.up*.04f;ring.localScale=new Vector3(size*1.3f,.035f,size*1.3f);ring.gameObject.SetActive(false);
            var hp=Primitive(PrimitiveType.Cube,root,highlight);hp.name="Health";hp.localPosition=new Vector3(0,height+1,0);
            var impact=Primitive(PrimitiveType.Quad,root,impactMaterial);impact.name="Damage impact";impact.localPosition=Vector3.up*(height+1.15f);impact.localRotation=Quaternion.Euler(90,0,0);impact.gameObject.SetActive(false);
            var animation=soldier?new SoldierAnimation(root,model.transform,type,unitVisuals,visibleMaterials):null;
            parts[root]=new VisualParts{soldier=animation,model=model.transform,selection=ring,health=hp,impact=impact,healthRenderer=hp.GetComponent<Renderer>(),impactRenderer=impact.GetComponent<Renderer>(),body=animation?.Renderers??new[]{model.GetComponent<Renderer>()},visibleMaterials=visibleMaterials};
            return root;
        }
        private static Transform Primitive(PrimitiveType type,Transform parent,Material material)
        {var go=GameObject.CreatePrimitive(type);Release(go.GetComponent<Collider>());go.transform.SetParent(parent,false);go.GetComponent<Renderer>().sharedMaterial=material;return go.transform;}
        private void UpdateFog()
        {
            if(fog==null||Session.Match!=null&&fogTick==Session.View.tick/4)return;
            fogTick=Session.View==null?-1:Session.View.tick/4;
            for(int i=0;i<fogColors.Length;i++){byte v=Session.View==null?(byte)255:Session.View.visibility[i]==2?(byte)255:Session.View.visibility[i]==1?(byte)75:(byte)0;fogColors[i]=new Color32(v,v,v,255);}
            fog.SetPixels32(fogColors);fog.Apply(false);
        }
        private static float Height(GridMap map,int cell) => map.SurfaceLevel[cell]*1.5f;
        public float HeightAt(Point point)
        {if(Session.Match==null)return 0;int c=Session.Match.Map.Cell(point);return c<0?0:Height(Session.Match.Map,c);}
        private void UpdatePointer()
        {
            if(ground==null)return;
            bool visible=pointer.HasValue&&Content!=null&&Content.gameObject.activeInHierarchy&&Session.Running&&!Session.IsPaused;
            ground.SetColor("_PointerColor",PointerColor);
            ground.SetColor("_PointerDotColor",Session.AbilityPointerActive?PointerColor:Session.AttackPointerActive?new Color(1f,.3f,.24f):Color.white);
            ground.SetVector("_CommandPulse",Vector4.zero);
            if(Content!=null&&Content.gameObject.activeInHierarchy&&Session.CommandPulseActive)
            {
                float age=Mathf.Clamp01((Time.unscaledTime-Session.LastCommandTime)/GameSession.CommandPulseDuration);
                float radius=Mathf.Clamp(.012f/CellScale,.65f,1.6f)*Mathf.Lerp(.35f,1.2f,age);
                var point=Session.LastCommandPoint;
                ground.SetVector("_CommandPulse",new Vector4(point.x,point.y,radius,1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.35f,1,age))));
                ground.SetFloat("_CommandWidth",Mathf.Clamp(.0014f/CellScale,.045f,.16f));
                ground.SetColor("_CommandColor",Session.LastCommandKind==CommandKind.Move?NormalPointerColor:AttackPointerColor);
            }
            ground.SetVector("_AbilityArea",Vector4.zero);ground.SetVector("_AbilityRange",Vector4.zero);
            if(visible&&Session.AbilityPointerActive)
            {
                var ability=Session.PendingAbility;Point point=pointer.Value;
                int x=(int)point.x,y=(int)point.y,index=y*Session.Map.width+x;
                bool seen=point.x>=0&&point.y>=0&&x<Session.Map.width&&y<Session.Map.height&&Session.View.visibility[index]==2;
                ground.SetColor("_AbilityColor",seen?PointerColor:AttackPointerColor);
                ground.SetVector("_AbilityArea",new Vector4(point.x,point.y,ability.radius,.0015f/CellScale));
                var caster=Session.AbilityCasters(ability.id).Where(e=>Session.AbilityReady(e,ability))
                    .OrderBy(e=>e.order==CommandKind.Cast?1:0)
                    .ThenBy(e=>e.position.Distance(point)<=ability.range&&Session.Match.Map.HasLineOfSight(e.position,point)?0:1)
                    .ThenBy(e=>e.position.Distance(point)).ThenBy(e=>e.id).FirstOrDefault();
                if(caster!=null)ground.SetVector("_AbilityRange",new Vector4(caster.position.x,caster.position.y,ability.range,.001f/CellScale));
            }
            // Keep the dot and guide lines the same physical size when zooming the map.
            ground.SetVector("_PointerArea",visible?new Vector4(pointer.Value.x,pointer.Value.y,.0018f/CellScale,.01f/CellScale):Vector4.zero);
        }
        public bool RayToMap(Ray ray,out Point point)=>RayToMap(ray,out point,out _);
        public bool RayToMap(Ray ray,out Point point,out Vector3 worldHit)
        {
            point=default;worldHit=default;
            // Input can change pan/zoom before LateUpdate; raycast the transform drawn this frame.
            UpdateTransform();
            if(terrainCollider==null||!terrainCollider.gameObject.activeInHierarchy)return false;
            var currentTransform=terrainRoot.localToWorldMatrix;
            if(raycastTransform!=currentTransform){Physics.SyncTransforms();raycastTransform=currentTransform;}
            if(!terrainCollider.Raycast(ray,out var hit,20))return false;
            if(Vector3.Dot(hit.normal,transform.up)<.5f)return false;
            var world=hit.point;var local=transform.InverseTransformPoint(world);
            if(Mathf.Abs(local.x)>Width*.5f||Mathf.Abs(local.z)>Depth*.5f)return false;
            var map=Content.InverseTransformPoint(world);point=new Point(map.x,map.z);
            if(point.x<0||point.y<0||point.x>=Session.Map.width||point.y>=Session.Map.height)return false;
            worldHit=world;return true;
        }
        public bool RayToSelectionPlane(Ray ray,out Point point)
        {
            point=default;
            if(Content==null||!Content.gameObject.activeInHierarchy)return false;
            UpdateTransform();
            if(Vector3.Dot(ray.direction,transform.up)>=-.0001f||!new Plane(transform.up,transform.position).Raycast(ray,out float distance)||distance>20)return false;
            var local=Content.InverseTransformPoint(ray.GetPoint(distance));point=new Point(local.x,local.z);return true;
        }
        public bool TrySelectionBounds(Point a,Point b,out Point min,out Point max)
        {
            // Intersect with the visible map window, not just the full map, so zoom cannot select clipped units.
            float centerX=Session.Map.width*.5f+Pan.x,centerY=Session.Map.height*.5f+Pan.y;
            float halfX=Session.Map.width*.5f/Zoom,halfY=Session.Map.height*.5f/Zoom;
            min=new Point(Mathf.Max(Mathf.Min(a.x,b.x),Mathf.Max(0,centerX-halfX)),Mathf.Max(Mathf.Min(a.y,b.y),Mathf.Max(0,centerY-halfY)));
            max=new Point(Mathf.Min(Mathf.Max(a.x,b.x),Mathf.Min(Session.Map.width,centerX+halfX)),Mathf.Min(Mathf.Max(a.y,b.y),Mathf.Min(Session.Map.height,centerY+halfY)));
            return max.x-min.x>.001f&&max.y-min.y>.001f;
        }
        public int EntityAt(Point point)
        {
            if(Session.View==null)return 0;
            var found=Session.View.entities.Where(e=>!e.remembered).Select(e=>new{e,t=Session.Catalog[e.definition]})
                .Where(a=>new Point(a.e.position.x+(a.t.building?a.t.footprint*.5f:0),a.e.position.y+(a.t.building?a.t.footprint*.5f:0)).Distance(point)<(a.t.building?a.t.footprint*.8f:1.6f))
                .OrderBy(a=>a.e.position.Distance(point)).FirstOrDefault();return found?.e.id??0;
        }
        // Mineral patches remain usable on a small board. Overlapping assistance zones
        // choose the nearest deposit rather than treating the whole mineral line as one.
        public int ResourceAt(Point point) => Session.View?.resources.Where(r=>ResourceVisible(r)&&r.position.Distance(point)<(r.kind=="mineral"?Mathf.Clamp(.03f/CellScale,2.5f,4):2))
            .OrderBy(r=>r.position.Distance(point)).ThenBy(r=>r.id).FirstOrDefault()?.id??0;
        private bool ResourceVisible(ResourceState resource,byte minimumVision=1)
        {
            if(resource.remaining==0||Session.View==null)return false;
            int x=Mathf.FloorToInt(resource.position.x),y=Mathf.FloorToInt(resource.position.y);
            if(x<0||y<0||x>=Session.Map.width||y>=Session.Map.height||Session.View.visibility[y*Session.Map.width+x]<minimumVision)return false;
            float halfX=Session.Map.width*.5f/Zoom,halfY=Session.Map.height*.5f/Zoom;
            return Mathf.Abs(resource.position.x-Session.Map.width*.5f-Pan.x)<=halfX&&Mathf.Abs(resource.position.y-Session.Map.height*.5f-Pan.y)<=halfY;
        }
        public void ResolvePointerTarget(Ray ray,Point point,Vector3 terrainHit,out int entityId,out int resourceId)
        {
            if(BaseTargeting){ResolveBaseTarget(point,out entityId,out resourceId);return;}
            entityId=EntityAt(point);resourceId=ResourceAt(point);
            if(Session.View==null)return;
            // The terrain intersection can be well behind a crystal when viewed from
            // the side. Test the visible model too, stopping at the terrain in front.
            float closest=Vector3.Distance(ray.origin,terrainHit)+.001f;
            bool hitResource=false;
            foreach(var resource in Session.View.resources)
            {
                if(!ResourceVisible(resource)||!resourceRenderers.TryGetValue(resource.id,out var renderer))continue;
                if(!HitBody(renderer,Mathf.Min(.004f,CellScale*.4f),ray,closest,out float distance))continue;
                closest=distance;resourceId=resource.id;hitResource=true;
            }
            if(resourceId==0)return;
            int directEntity=0;
            foreach(var entity in Session.View.entities)
            {
                if(entity.remembered||!entities.TryGetValue(entity.id,out var visual))continue;
                foreach(var renderer in parts[visual].body)
                {
                    if(!HitBody(renderer,0,ray,closest,out float distance))continue;
                    closest=distance;directEntity=entity.id;
                }
            }
            // A direct hit on a unit/building retains priority over nearby resource
            // assistance. Its oversized ground selection halo does not hide minerals.
            if(directEntity!=0){entityId=directEntity;resourceId=0;return;}
            if(hitResource){entityId=0;return;}
            int groundEntity=entityId;var candidate=Session.View.entities.FirstOrDefault(e=>e.id==groundEntity);
            if(candidate==null)return;
            var type=Session.Catalog[candidate.definition];
            bool inside=type.building?point.x>=candidate.position.x&&point.y>=candidate.position.y&&point.x<=candidate.position.x+type.footprint&&point.y<=candidate.position.y+type.footprint
                :candidate.position.Distance(point)<=type.collisionRadius+.15f;
            if(inside)resourceId=0;else entityId=0;
        }
        public void SetBaseTargeting(bool enabled)
        {
            BaseTargeting=enabled;
            PlayerPrefs.SetInt(BaseTargetingKey,enabled?1:0);PlayerPrefs.Save();
            Hovered=0;Pointer=null;
        }
        private void ResolveBaseTarget(Point point,out int entityId,out int resourceId)
        {
            entityId=0;resourceId=0;
            if(Session.View==null||!VisibleBase(point))return;
            int directUnit=0,nearUnit=0,building=0;
            float directDistance=float.MaxValue,nearDistance=float.MaxValue,buildingDistance=float.MaxValue;
            foreach(var entity in Session.View.entities)
            {
                if(entity.remembered)continue;
                var type=Session.Catalog[entity.definition];
                if(type.building)
                {
                    // Use the footprint, not the oversized circular selection halo.
                    if(point.x<entity.position.x||point.y<entity.position.y||point.x>entity.position.x+type.footprint||point.y>entity.position.y+type.footprint)continue;
                    float distance=new Point(entity.position.x+type.footprint*.5f,entity.position.y+type.footprint*.5f).Distance(point);
                    if(distance<buildingDistance){building=entity.id;buildingDistance=distance;}
                }
                else
                {
                    if(!VisibleBase(entity.position))continue;
                    float distance=entity.position.Distance(point);
                    if(distance<=type.collisionRadius+.15f&&distance<directDistance){directUnit=entity.id;directDistance=distance;}
                    if(distance<1.6f&&distance<nearDistance){nearUnit=entity.id;nearDistance=distance;}
                }
            }
            // A unit's base wins over nearby assistance. A building only wins inside
            // its footprint; foreground geometry never participates in this mode.
            entityId=directUnit!=0?directUnit:building;
            if(entityId!=0)return;
            resourceId=ResourceAt(point);
            if(resourceId==0)entityId=nearUnit;
        }
        private bool VisibleBase(Point point)
        {
            if(point.x<0||point.y<0||point.x>=Session.Map.width||point.y>=Session.Map.height)return false;
            return Mathf.Abs(point.x-Session.Map.width*.5f-Pan.x)<=Session.Map.width*.5f/Zoom
                &&Mathf.Abs(point.y-Session.Map.height*.5f-Pan.y)<=Session.Map.height*.5f/Zoom;
        }
        private bool HitBody(Renderer renderer,float padding,Ray ray,float maximum,out float distance)
        {
            distance=0;if(!renderer.gameObject.activeInHierarchy)return false;
            // Test in model space so rotating the board does not enlarge its hit box.
            var model=renderer.transform;var scale=model.lossyScale;
            var bounds=renderer.localBounds;
            bounds.Expand(new Vector3(padding/Mathf.Max(.000001f,Mathf.Abs(scale.x)),padding/Mathf.Max(.000001f,Mathf.Abs(scale.y)),padding/Mathf.Max(.000001f,Mathf.Abs(scale.z)))*2);
            var localRay=new Ray(model.InverseTransformPoint(ray.origin),model.InverseTransformVector(ray.direction).normalized);
            if(!bounds.IntersectRay(localRay,out float localDistance))return false;
            var hit=model.TransformPoint(localRay.GetPoint(localDistance));distance=Vector3.Distance(ray.origin,hit);
            if(distance>maximum)return false;
            var local=transform.InverseTransformPoint(hit);
            return Mathf.Abs(local.x)<=Width*.5f&&Mathf.Abs(local.z)<=Depth*.5f;
        }
        public void DrawSelection(Point a,Point b,bool visible)
        {
            if(ground==null)return;
            if(!visible||!TrySelectionBounds(a,b,out var min,out var max)){ground.SetFloat("_SelectionWidth",0);return;}
            // Paint the outline in the terrain material: exactly on the surface, including ramps.
            // There is no elevated mesh, depth offset or vertical wall geometry.
            float x0=min.x,x1=max.x,z0=min.y,z1=max.y;
            float width=Mathf.Min(.002f/CellScale,Mathf.Min(x1-x0,z1-z0)*.5f);
            ground.SetVector("_SelectionBounds",new Vector4(x0,z0,x1,z1));
            ground.SetFloat("_SelectionWidth",width);
        }
    }
}
