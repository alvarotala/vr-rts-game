using System;
using System.Collections.Generic;
using System.Linq;
using Frontier.Core;
using UnityEngine;

namespace Frontier.Runtime
{
    public sealed partial class GameSession : MonoBehaviour
    {
        public Simulation Match { get; private set; }
        public Catalog Catalog { get; private set; }
        public PlayerView View { get; private set; }
        public MapDefinition Map { get; private set; }
        public readonly HashSet<int> Selection = new HashSet<int>();
        public int SelectedResource { get; private set; }
        public bool Running { get; private set; }
        public string Faction = "industrial", EnemyFaction = "biological", Difficulty = "normal", MapId = "rift";
        public string Message { get; private set; } = "Choose New Match to begin.";
        public uint MessageVersion { get; private set; }
        public event Action Changed;
        public event Action NewMatch;
        public event Action MatchClosed;
        public event Action<bool> Feedback;
        public event Action<int, Point> UnitMoveOrdered;
        public string BuildDefinition { get; private set; }
        public bool AttackMoveMode;
        public string AbilityDefinitionId { get; private set; }
        public AbilityDefinition PendingAbility => Catalog?.Ability(AbilityDefinitionId);
        public bool HasTargetingMode => BuildDefinition != null || AttackMoveMode || AbilityDefinitionId != null;
        public bool AbilityPointerActive => CanIssueOrders && PendingAbility != null;
        public bool AttackPointerActive => CanIssueOrders
            && AttackMoveMode;
        public const float CommandPulseDuration = 1.2f;
        public CommandKind LastCommandKind { get; private set; }
        public Point LastCommandPoint { get; private set; }
        public int LastCommandSequence { get; private set; }
        public float LastCommandTime { get; private set; }
        public bool CommandPulseActive => CanIssueOrders
            && LastCommandSequence > 0 && Time.unscaledTime-LastCommandTime < CommandPulseDuration;
        private Strategist bot;
        private float accumulator;
        private int sequence;
        private readonly HashSet<string> pauseReasons = new HashSet<string>();
        public const int ControlGroupCount = 4;
        private readonly List<int>[] groups = Enumerable.Range(0,ControlGroupCount).Select(_=>new List<int>()).ToArray();
        private float messageUntil;

        private void Awake()
        {
            Catalog = new Catalog(JsonUtility.FromJson<CatalogDefinition>(Resources.Load<TextAsset>("catalog").text));
            LoadMap("rift");
        }
        public void LoadMap(string id)
        {
            var asset = Resources.Load<TextAsset>("Maps/" + id);
            if (asset == null) throw new ArgumentException("Map not found: " + id);
            Map = JsonUtility.FromJson<MapDefinition>(asset.text); _ = new GridMap(Map); MapId = id;
        }
        public void CycleMap()=>LoadMap(MapId=="rift"?"training":MapId=="training"?"desert":"rift");
        public void StartMatch()
        {
            if(IsReplay)StopReplay();
            DisconnectOnline();
            LastClosedWasReplay=false;
            CloseArchive();
            Match = new Simulation(Map, Catalog, Faction, EnemyFaction);
            StartArchive();
            bot = new Strategist(1, Difficulty == "hard" ? AiDifficulty.Hard : Difficulty == "easy" ? AiDifficulty.Easy : AiDifficulty.Normal, Catalog, Map);
            Match.Notice += (player, message) => { if (player == 0) Announce(message); };
            Selection.Clear(); SelectedResource = 0; foreach (var group in groups) group.Clear();
            BuildDefinition = null; AttackMoveMode = false; AbilityDefinitionId = null; LastCommandSequence = 0; sequence = 0; accumulator = 0;
            // A rematch must not override tracking, headset or application pauses.
            pauseReasons.Remove("menu"); Match.Paused=IsPaused; Running = true; View = Match.View(0);
            Announce(MapId == "training" ? $"1 · Select a worker. Build: {Catalog.Role(Faction, "supply").name} + {Catalog.Role(Faction, "barracks").name}." : "Expand your base. Scout before attacking.");
            NewMatch?.Invoke(); Changed?.Invoke();
        }
        public void LeaveMatch()
        {
            if(IsReplay){StopReplay();return;}
            DisconnectOnline();
            LastClosedWasReplay=false;
            CloseArchive();
            if (Match != null) Match.Paused = true;
            Running = false; Match = null; View = null; bot = null;
            Selection.Clear(); SelectedResource = 0; foreach (var group in groups) group.Clear();
            BuildDefinition = null; AttackMoveMode = false; AbilityDefinitionId = null; LastCommandSequence = 0; sequence = 0; accumulator = 0;
            // Closing a match must not dismiss headset, focus or tracking interruptions.
            pauseReasons.Remove("menu");
            Announce("Ready for a new match.");
            MatchClosed?.Invoke(); Changed?.Invoke();
        }
        private void Update()
        {
            if(IsReplay){UpdateReplay();return;}
            if (!Running || Match == null) return;
            UpdateArchive();
            Match.Paused = pauseReasons.Count > 0;
            if (Match.Paused || Match.Winner != -1) { accumulator = 0; return; }
            accumulator += Math.Min(Time.unscaledDeltaTime, .25f);
            int steps = 0;
            while (accumulator >= 1f / Simulation.TickRate && steps++ < 5)
            {
                if (Match.Tick % 20 == 0) foreach (var command in bot.Decide(Match.View(1))) Match.Submit(command);
                Match.Step(); accumulator -= 1f / Simulation.TickRate;
            }
            if (steps > 0)
            {
                RefreshView(); Selection.RemoveWhere(id => !View.entities.Any(e => e.id == id && e.owner == LocalPlayer));
                if (Match.Winner != -1) Announce(Match.Winner == 0 ? "VICTORY · The territory is yours." : Match.Winner == 2 ? "DRAW" : "DEFEAT · Your colony has fallen.");
                else if (MapId == "training" && Time.unscaledTime > messageUntil)
                    Message = !Match.HasRole(0, "barracks") ? $"1 · Use a worker to build: {Catalog.Role(Faction, "barracks").name}." : Match.Units(0) < 10 ? $"2 · Select {Catalog.Role(Faction, "barracks").name} and train an army." : "3 · Use Attack Move to explore and destroy enemy buildings.";
                Changed?.Invoke();
            }
        }
        public bool IsPaused => pauseReasons.Count > 0 || IsReplay && (Replay.Paused || Replay.Seeking || Replay.Ended || Replay.Error != null);
        // Menus and board placement still accept controller input. System interruptions
        // must hide hand-attached UI even when one of those local menus is also open.
        public bool HasSystemPause => pauseReasons.Any(reason => reason != "menu" && reason != "placement");
        public void Pause(string reason, bool paused)
        { if (paused) pauseReasons.Add(reason); else pauseReasons.Remove(reason); if (Match != null && !IsReplay) Match.Paused = IsPaused; Changed?.Invoke(); }
        public void TogglePause() => Pause("menu", !pauseReasons.Contains("menu"));
        public void Announce(string text) { Message = text; MessageVersion=unchecked(MessageVersion+1); messageUntil = Time.unscaledTime + 6; }
        public void Select(int id, bool additive, int resourceId = 0)
        {
            if (!additive) Selection.Clear();
            SelectedResource = 0;
            if (View != null && View.entities.Any(e => e.id == id && e.owner == LocalPlayer)) { if (!Selection.Add(id) && additive) Selection.Remove(id); }
            else if (!additive && View != null && View.resources.Any(r => r.id == resourceId)) SelectedResource = resourceId;
            ValidateAbilitySelection();Feedback?.Invoke(true); Changed?.Invoke();
        }
        public void SelectRectangle(Point a, Point b, bool additive)
        {
            SelectedResource = 0;
            if (!additive) Selection.Clear();
            if (View == null) return;
            foreach (var e in View.entities)
                if (e.owner == LocalPlayer && !Catalog[e.definition].building && e.position.x >= Math.Min(a.x,b.x) && e.position.x <= Math.Max(a.x,b.x) && e.position.y >= Math.Min(a.y,b.y) && e.position.y <= Math.Max(a.y,b.y)) Selection.Add(e.id);
            ValidateAbilitySelection();Feedback?.Invoke(true); Changed?.Invoke();
        }
        public void Deselect(int id)
        {
            bool removed=Selection.Remove(id);
            if(removed)SelectedResource=0;
            ValidateAbilitySelection();Feedback?.Invoke(removed);Changed?.Invoke();
        }
        public void SelectSameTypeOnMap(int id,bool additive)
        {
            var source=View?.entities.FirstOrDefault(e=>e.id==id&&e.owner==View.player&&!e.remembered&&e.health>0);
            if(source==null)return;
            if(!additive)Selection.Clear();
            SelectedResource=0;
            foreach(var entity in View.entities)
                if(entity.owner==View.player&&!entity.remembered&&entity.health>0&&entity.definition==source.definition
                    &&entity.position.Distance(source.position)<=12)Selection.Add(entity.id);
            ValidateAbilitySelection();Feedback?.Invoke(true);Changed?.Invoke();
        }
        public void SelectTypeInSelection(string definition,bool remove)
        {
            if(View==null)return;
            var members=new HashSet<int>(View.entities.Where(e=>Selection.Contains(e.id)&&e.owner==View.player&&!e.remembered&&e.definition==definition).Select(e=>e.id));
            if(members.Count==0)return;
            if(remove)Selection.ExceptWith(members);else Selection.IntersectWith(members);
            SelectedResource=0;ValidateAbilitySelection();Feedback?.Invoke(true);Changed?.Invoke();
        }
        private IEnumerable<VisibleEntity> SelectableUnits => View==null?Enumerable.Empty<VisibleEntity>():View.entities.Where(e=>
            e.owner==View.player&&!e.remembered&&e.health>0&&e.complete&&!Catalog[e.definition].building);
        public IEnumerable<VisibleEntity> IdleWorkers => SelectableUnits.Where(e=>Catalog[e.definition].role=="worker"&&
            (e.order==CommandKind.Stop||e.order==CommandKind.Hold)&&e.pendingOrders.Length==0&&!e.working&&!e.depositing);
        public IEnumerable<VisibleEntity> Army => SelectableUnits.Where(e=>Catalog[e.definition].role!="worker");
        public bool SelectIdleWorkers()=>SelectUnits(IdleWorkers,"idle workers","No idle workers available.");
        public bool SelectArmy()=>SelectUnits(Army,"army units","No army units available.");
        private bool SelectUnits(IEnumerable<VisibleEntity> units,string label,string emptyMessage)
        {
            if(!Running||IsPaused||IsReplay)return false;
            var ids=units.Select(e=>e.id).ToArray();
            if(ids.Length==0){Announce(emptyMessage);Feedback?.Invoke(false);Changed?.Invoke();return false;}
            CancelMode();SelectedResource=0;Selection.Clear();Selection.UnionWith(ids);
            Announce(ids.Length+" "+label+" selected.");Feedback?.Invoke(true);Changed?.Invoke();return true;
        }
        public void Group(int index, bool save)
        {
            if(View==null||index<0||index>=groups.Length)return;
            if (save) { groups[index].Clear(); groups[index].AddRange(Selection.Where(OwnGroupMember)); Announce("Group " + (index + 1) + " saved."); }
            else { Selection.Clear(); SelectedResource = 0; foreach (int id in groups[index]) if (OwnGroupMember(id)) Selection.Add(id);
                Announce("Group " + (index + 1) + (Selection.Count>0?" selected.":" is empty.")); }
            ValidateAbilitySelection();Feedback?.Invoke(save||Selection.Count>0);Changed?.Invoke();
        }
        private bool OwnGroupMember(int id)=>View!=null&&View.entities.Any(e=>e.id==id&&e.owner==View.player&&!e.remembered);
        public int GroupSize(int index) => index>=0&&index<groups.Length?groups[index].Count(OwnGroupMember):0;
        public void ChooseBuild(string id) { if(IsReplay)return; BuildDefinition = id; AttackMoveMode = false; AbilityDefinitionId = null; Announce("Place " + Catalog[id].name + " · Point at the terrain and confirm construction."); Changed?.Invoke(); }
        public void ChooseAttackMove()
        {
            if(IsReplay)return;
            BuildDefinition = null; AttackMoveMode = true; AbilityDefinitionId = null;
            Announce("Attack Move · Choose a point on the battlefield."); Changed?.Invoke();
        }
        public IEnumerable<VisibleEntity> AbilityCasters(string id) => View == null ? Enumerable.Empty<VisibleEntity>() : View.entities.Where(e =>
            e.owner == View.player && e.health > 0 && e.complete && Selection.Contains(e.id) && Catalog[e.definition].abilities.Contains(id));
        public bool AbilityReady(VisibleEntity caster, AbilityDefinition ability) => ability != null && caster.energy >= ability.energyCost
            && !caster.abilityCooldowns.Any(c => c.ability == ability.id && c.readyTick > View.tick);
        public string AbilityStatus(string id)
        {
            var ability=Catalog.Ability(id);var casters=AbilityCasters(id).ToArray();
            if(ability==null||casters.Length==0)return "Select a caster";
            int ready=casters.Count(e=>AbilityReady(e,ability));
            if(ready>0)return ability.energyCost+" energy · "+ready+" ready";
            int wait=casters.Min(e=>Math.Max(0,e.abilityCooldowns.Where(c=>c.ability==id).Select(c=>c.readyTick-View.tick).DefaultIfEmpty(0).Max()));
            return wait>0?ability.energyCost+" energy · "+((wait+Simulation.TickRate-1)/Simulation.TickRate)+" s cooldown"
                :"Needs "+ability.energyCost+" energy";
        }
        public bool ChooseAbility(string id)
        {
            if(IsReplay)return false;
            var ability=Catalog.Ability(id);
            if(ability==null||!AbilityCasters(id).Any(e=>AbilityReady(e,ability)))
            {Announce(AbilityStatus(id));Feedback?.Invoke(false);Changed?.Invoke();return false;}
            BuildDefinition=null;AttackMoveMode=false;AbilityDefinitionId=id;
            Announce(ability.name+" · Choose a visible point. One caster will move into range and cast.");Changed?.Invoke();return true;
        }
        private void ValidateAbilitySelection()
        { if(AbilityDefinitionId!=null&&!AbilityCasters(AbilityDefinitionId).Any())AbilityDefinitionId=null; }
        public void CancelMode()
        {
            if (BuildDefinition != null) Announce("Construction placement cancelled.");
            else if (AttackMoveMode) Announce("Attack destination cancelled.");
            else if (PendingAbility != null) Announce(PendingAbility.name+" targeting cancelled.");
            BuildDefinition = null; AttackMoveMode = false; AbilityDefinitionId = null; Changed?.Invoke();
        }
        private void RefreshView()
        {
            if(IsReplay){if(!Replay.Seeking)RefreshReplayView();return;}
            UpdateArchive();
            ApplyView(Match.View(0));
        }
        private void ApplyView(PlayerView view)
        {
            var previous=View;View=view;ValidateAbilitySelection();
            foreach(var entity in View.entities)
                if(entity.owner==View.player&&(entity.order==CommandKind.Move||entity.order==CommandKind.AttackMove))
                {
                    var old=previous?.entities.FirstOrDefault(e=>e.id==entity.id);
                    // Enqueuing leaves the active sequence unchanged. A new active waypoint
                    // or a freshly deployed unit with an automatic rally turns now.
                    if(old==null||(entity.activeOrderSequence>0&&old.activeOrderSequence!=entity.activeOrderSequence))
                        UnitMoveOrdered?.Invoke(entity.id,entity.destination);
                }
        }
        public bool CanSetRallyPoint => View!=null&&Selection.Count>0&&Selection.All(id=>View.entities.Any(e=>
            e.id==id&&e.owner==View.player&&!e.remembered&&e.complete&&Catalog.CanProduceUnits(e.definition)));
        public bool ClearRallyPoint()
        {
            if(!Send(CommandKind.ClearRallyPoint))return false;
            CancelMode();Announce(IsOnline?"Clear rally point requested.":"Rally point cleared. New units use their default behavior.");return true;
        }
        public int Context(Point point,int entityId,int resourceId,bool append=false,bool attack=false,int replaceSequence=0)
        {
            if (View == null) return 0;
            if (PendingAbility != null)
            {
                var ability=PendingAbility;
                if(!Send(CommandKind.Cast,definition:ability.id,position:point))return 0;
                AbilityDefinitionId=null;Announce(ability.name+(IsOnline?" requested.":" ordered."));Changed?.Invoke();return sequence;
            }
            if(CanSetRallyPoint)
            {
                if(!Send(CommandKind.SetRallyPoint,position:point))return 0;
                CancelMode();Announce(IsOnline?"Rally point requested.":"Rally point set. Newly trained units will move there.");return sequence;
            }
            if (BuildDefinition != null)
            {
                if(!Send(CommandKind.Build,definition:BuildDefinition,position:point,append:append))return 0;
                Announce(Catalog[BuildDefinition].name + (IsOnline ? " construction requested." : append ? " construction queued." : " construction ordered."));
                BuildDefinition=null;return sequence;
            }
            if (AttackMoveMode)
            {
                bool accepted=Send(CommandKind.AttackMove,position:point,append:append,replaceSequence:replaceSequence);
                if(accepted)AttackMoveMode=false;return accepted?sequence:0;
            }
            var entity = View.entities.FirstOrDefault(e=>e.id==entityId && !e.remembered);
            var selected = View.entities.Where(e=>Selection.Contains(e.id)).ToArray();
            if(attack)
            {
                bool accepted=entity!=null&&entity.owner!=LocalPlayer
                    ?Send(CommandKind.Attack,target:entity.id,append:append,replaceSequence:replaceSequence)
                    :Send(CommandKind.AttackMove,position:point,append:append,replaceSequence:replaceSequence);
                return accepted?sequence:0;
            }
            // The whole extractor footprint is a gas-order target, not only the deposit's center.
            if (entity != null && entity.owner == LocalPlayer && Catalog[entity.definition].role == "extractor" && entity.complete)
                resourceId = View.resources.FirstOrDefault(r => r.extractorId == entity.id)?.id ?? resourceId;
            bool sent;
            if (entity != null && entity.owner != LocalPlayer) sent=Send(CommandKind.Attack,target:entity.id,append:append,replaceSequence:replaceSequence);
            else if (resourceId > 0 && selected.All(e=>Catalog[e.definition].role=="worker")) sent=Send(CommandKind.Gather,target:resourceId,append:append,replaceSequence:replaceSequence);
            else if (entity != null && entity.owner==LocalPlayer && (!entity.complete || entity.health < Catalog[entity.definition].health) && selected.All(e=>Catalog[e.definition].role=="worker")) sent=Send(CommandKind.Repair,target:entity.id,append:append,replaceSequence:replaceSequence);
            else sent=Send(CommandKind.Move,position:point,append:append,replaceSequence:replaceSequence);
            return sent?sequence:0;
        }
        public bool Send(CommandKind kind,string definition=null,int target=0,Point position=default,int queueOrderId=0,bool append=false,int replaceSequence=0)
        {
            if (IsReplay || !IsOnline && Match == null || IsOnline && !CanIssueOrders) return false;
            var selected = Selection.OrderBy(id=>id).ToArray();
            var command = new GameCommand { player=LocalPlayer, sequence=++sequence, kind=kind, definition=definition, target=target, position=position, queueOrderId=queueOrderId, entities=selected,append=append,replacePendingSequence=replaceSequence };
            if(IsOnline)return SendOnline(command);
            var result = Match.Submit(command);
            if (!result.accepted) Announce(result.message);
            else
            {
                bool attacking = kind == CommandKind.Attack || kind == CommandKind.AttackMove;
                if (attacking || kind == CommandKind.Move)
                {
                    // Accepted commands mark their destination, independently of where
                    // the cursor moves next. Never turn confirmation into targeting mode.
                    var focus = kind == CommandKind.Attack ? View.entities.FirstOrDefault(e=>e.id==target&&!e.remembered) : null;
                    float half = focus != null && Catalog[focus.definition].building ? Catalog[focus.definition].footprint*.5f : 0;
                    LastCommandPoint = focus == null ? position : new Point(focus.position.x+half,focus.position.y+half);
                    LastCommandKind = kind; LastCommandSequence = sequence; LastCommandTime = Time.unscaledTime;
                }
                else LastCommandSequence = 0;
                if (attacking) Announce((kind == CommandKind.AttackMove ? "Attack Move" : "Attack") + (append ? " queued." : " ordered."));
            }
            Feedback?.Invoke(result.accepted); RefreshView();
            Changed?.Invoke(); return result.accepted;
        }
        private void OnApplicationFocus(bool focused) { if(!focused)UpdateArchive(true); if (Application.platform != RuntimePlatform.Android || Application.isEditor) return; Pause("focus", !focused); }
        private void OnApplicationPause(bool paused) { if(paused)UpdateArchive(true); if (Application.platform == RuntimePlatform.Android && !Application.isEditor) Pause("application", paused); }
    }
}
