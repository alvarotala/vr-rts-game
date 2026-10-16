using System;
using System.Collections.Generic;
using System.Linq;
using Frontier.Core;
using UnityEngine;

namespace Frontier.Runtime
{
    public sealed class GameSession : MonoBehaviour
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
        public event Action Changed;
        public event Action NewMatch;
        public event Action MatchClosed;
        public event Action<bool> Feedback;
        public event Action<int, Point> UnitMoveOrdered;
        public string BuildDefinition { get; private set; }
        public bool AttackMoveMode;
        private Strategist bot;
        private float accumulator;
        private int sequence;
        private readonly HashSet<string> pauseReasons = new HashSet<string>();
        private readonly List<int>[] groups = { new List<int>(), new List<int>(), new List<int>() };
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
        public void StartMatch()
        {
            Match = new Simulation(Map, Catalog, Faction, EnemyFaction);
            bot = new Strategist(1, Difficulty == "very-easy" ? AiDifficulty.VeryEasy : Difficulty == "easy" ? AiDifficulty.Easy : AiDifficulty.Normal, Catalog, Map);
            Match.Notice += (player, message) => { if (player == 0) Announce(message); };
            Selection.Clear(); SelectedResource = 0; foreach (var group in groups) group.Clear();
            BuildDefinition = null; AttackMoveMode = false; sequence = 0; accumulator = 0;
            // A rematch must not override tracking, headset or application pauses.
            pauseReasons.Remove("menu"); Match.Paused=IsPaused; Running = true; View = Match.View(0);
            Announce(MapId == "training" ? $"1 · Select a worker. Build: {Catalog.Role(Faction, "supply").name} + {Catalog.Role(Faction, "barracks").name}." : "Expand your base. Scout before attacking.");
            NewMatch?.Invoke(); Changed?.Invoke();
        }
        public void LeaveMatch()
        {
            if (Match != null) Match.Paused = true;
            Running = false; Match = null; View = null; bot = null;
            Selection.Clear(); SelectedResource = 0; foreach (var group in groups) group.Clear();
            BuildDefinition = null; AttackMoveMode = false; sequence = 0; accumulator = 0;
            // Closing a match must not dismiss headset, focus or tracking interruptions.
            pauseReasons.Remove("menu");
            Announce("Ready for a new match.");
            MatchClosed?.Invoke(); Changed?.Invoke();
        }
        private void Update()
        {
            if (!Running || Match == null) return;
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
                RefreshView(); Selection.RemoveWhere(id => !View.entities.Any(e => e.id == id && e.owner == 0));
                if (Match.Winner != -1) Announce(Match.Winner == 0 ? "VICTORY · The territory is yours." : Match.Winner == 2 ? "DRAW" : "DEFEAT · Your colony has fallen.");
                else if (MapId == "training" && Time.unscaledTime > messageUntil)
                    Message = !Match.HasRole(0, "barracks") ? $"1 · Use a worker to build: {Catalog.Role(Faction, "barracks").name}." : Match.Units(0) < 10 ? $"2 · Select {Catalog.Role(Faction, "barracks").name} and train an army." : "3 · Use Attack Move to explore and destroy enemy buildings.";
                Changed?.Invoke();
            }
        }
        public bool IsPaused => pauseReasons.Count > 0;
        public void Pause(string reason, bool paused)
        { if (paused) pauseReasons.Add(reason); else pauseReasons.Remove(reason); if (Match != null) Match.Paused = IsPaused; Changed?.Invoke(); }
        public void TogglePause() => Pause("menu", !pauseReasons.Contains("menu"));
        public void Announce(string text) { Message = text; messageUntil = Time.unscaledTime + 6; }
        public void Select(int id, bool additive, int resourceId = 0)
        {
            if (!additive) Selection.Clear();
            SelectedResource = 0;
            if (View != null && View.entities.Any(e => e.id == id && e.owner == 0)) { if (!Selection.Add(id) && additive) Selection.Remove(id); }
            else if (!additive && View != null && View.resources.Any(r => r.id == resourceId)) SelectedResource = resourceId;
            Feedback?.Invoke(true); Changed?.Invoke();
        }
        public void SelectRectangle(Point a, Point b, bool additive)
        {
            SelectedResource = 0;
            if (!additive) Selection.Clear();
            if (View == null) return;
            foreach (var e in View.entities)
                if (e.owner == 0 && !Catalog[e.definition].building && e.position.x >= Math.Min(a.x,b.x) && e.position.x <= Math.Max(a.x,b.x) && e.position.y >= Math.Min(a.y,b.y) && e.position.y <= Math.Max(a.y,b.y)) Selection.Add(e.id);
            Feedback?.Invoke(true); Changed?.Invoke();
        }
        public void Deselect(int id)
        {
            bool removed=Selection.Remove(id);
            if(removed)SelectedResource=0;
            Feedback?.Invoke(removed);Changed?.Invoke();
        }
        public void SelectNearbySameType(int id,bool additive)
        {
            var source=View?.entities.FirstOrDefault(e=>e.id==id&&e.owner==0&&!e.remembered);
            if(source==null||Catalog[source.definition].building)return;
            if(!additive)Selection.Clear();
            SelectedResource=0;
            foreach(var entity in View.entities)
                if(entity.owner==0&&!entity.remembered&&entity.definition==source.definition
                    &&entity.position.Distance(source.position)<=12)Selection.Add(entity.id);
            Feedback?.Invoke(true);Changed?.Invoke();
        }
        public void Group(int index, bool save)
        {
            if(View==null||index<0||index>=groups.Length)return;
            if (save) { groups[index].Clear(); groups[index].AddRange(Selection); Announce("Group " + (index + 1) + " saved."); }
            else { Selection.Clear(); SelectedResource = 0; foreach (int id in groups[index]) if (View.entities.Any(e=>e.id==id && e.owner==0)) Selection.Add(id); }
            Changed?.Invoke();
        }
        public int GroupSize(int index) => groups[index].Count(id=>View != null && View.entities.Any(e=>e.id==id));
        public void ChooseBuild(string id) { BuildDefinition = id; AttackMoveMode = false; Announce("Point at the terrain and confirm construction."); Changed?.Invoke(); }
        public void CancelMode() { BuildDefinition = null; AttackMoveMode = false; Changed?.Invoke(); }
        private void RefreshView()
        {
            var previous=View;View=Match.View(0);
            foreach(var entity in View.entities)
                if(entity.owner==View.player&&entity.activeOrderSequence>0&&
                    (entity.order==CommandKind.Move||entity.order==CommandKind.AttackMove)&&
                    (previous==null||!previous.entities.Any(old=>old.id==entity.id&&old.activeOrderSequence==entity.activeOrderSequence)))
                    // Enqueuing leaves the active sequence unchanged. A new active waypoint
                    // turns the model now, including when simulation advances the queue.
                    UnitMoveOrdered?.Invoke(entity.id,entity.destination);
        }
        public int Context(Point point,int entityId,int resourceId,bool append=false,bool attack=false,int replaceSequence=0)
        {
            if (View == null) return 0;
            if (BuildDefinition != null)
            {
                if(!Send(CommandKind.Build,definition:BuildDefinition,position:point,append:append))return 0;
                BuildDefinition=null;return sequence;
            }
            if (AttackMoveMode)
            {
                bool accepted=Send(CommandKind.AttackMove,position:point,append:append,replaceSequence:replaceSequence);
                AttackMoveMode=false;return accepted?sequence:0;
            }
            var entity = View.entities.FirstOrDefault(e=>e.id==entityId && !e.remembered);
            var selected = View.entities.Where(e=>Selection.Contains(e.id)).ToArray();
            if(attack)
            {
                bool accepted=entity!=null&&entity.owner!=0
                    ?Send(CommandKind.Attack,target:entity.id,append:append,replaceSequence:replaceSequence)
                    :Send(CommandKind.AttackMove,position:point,append:append,replaceSequence:replaceSequence);
                return accepted?sequence:0;
            }
            // The whole extractor footprint is a gas-order target, not only the deposit's center.
            if (entity != null && entity.owner == 0 && Catalog[entity.definition].role == "extractor" && entity.complete)
                resourceId = View.resources.FirstOrDefault(r => r.extractorId == entity.id)?.id ?? resourceId;
            bool sent;
            if (entity != null && entity.owner != 0) sent=Send(CommandKind.Attack,target:entity.id,append:append,replaceSequence:replaceSequence);
            else if (resourceId > 0 && selected.All(e=>Catalog[e.definition].role=="worker")) sent=Send(CommandKind.Gather,target:resourceId,append:append,replaceSequence:replaceSequence);
            else if (entity != null && entity.owner==0 && entity.health < Catalog[entity.definition].health && Faction=="industrial" && selected.All(e=>Catalog[e.definition].role=="worker")) sent=Send(CommandKind.Repair,target:entity.id,append:append,replaceSequence:replaceSequence);
            else sent=Send(CommandKind.Move,position:point,append:append,replaceSequence:replaceSequence);
            return sent?sequence:0;
        }
        public bool Send(CommandKind kind,string definition=null,int target=0,Point position=default,int queueOrderId=0,bool append=false,int replaceSequence=0)
        {
            if (Match == null) return false;
            var selected = Selection.OrderBy(id=>id).ToArray();
            var result = Match.Submit(new GameCommand { player=0, sequence=++sequence, kind=kind, definition=definition, target=target, position=position, queueOrderId=queueOrderId, entities=selected,append=append,replacePendingSequence=replaceSequence });
            if (!result.accepted) Announce(result.message);
            Feedback?.Invoke(result.accepted); RefreshView();
            Changed?.Invoke(); return result.accepted;
        }
        private void OnApplicationFocus(bool focused) { if (Application.platform != RuntimePlatform.Android || Application.isEditor) return; Pause("focus", !focused); }
        private void OnApplicationPause(bool paused) { if (Application.platform == RuntimePlatform.Android && !Application.isEditor) Pause("application", paused); }
    }
}
