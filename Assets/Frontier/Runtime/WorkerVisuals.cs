using System.Collections.Generic;
using Frontier.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace Frontier.Runtime
{
    public enum WorkerTask { Idle, Moving, Minerals, Gas, Building, Repairing }

    public struct WorkerActivity
    {
        public WorkerTask task;
        public bool active, hasTarget;
        public Point target;
        public float targetRadius;
    }

    // All tools, cargo, job badges and work connections share one dynamic mesh.
    // Uses only the player's filtered view; no particles, colliders or per-worker materials.
    public sealed class WorkerVisuals : System.IDisposable
    {
        public static readonly Color MineralColor = new Color(.25f, .8f, 1);
        public static readonly Color GasColor = new Color(.45f, 1, .35f);
        public static readonly Color BuildColor = new Color(1, .63f, .18f);
        public static readonly Color RepairColor = new Color(.84f, .58f, 1);
        private static readonly Color Dark = new Color(.035f, .065f, .09f);
        private readonly Transform root;
        private readonly Mesh mesh;
        private readonly Material material;
        private readonly List<Vector3> vertices = new List<Vector3>(16000);
        private readonly List<Vector3> normals = new List<Vector3>(16000);
        private readonly List<Color> colors = new List<Color>(16000);
        private readonly List<int> triangles = new List<int>(24000);
        private Vector3 right, up, normal;
        private BoardView board;
        private struct Motion { public Point position; public int tick; public float amount; }
        private readonly Dictionary<int,Motion> motions=new Dictionary<int,Motion>();
        private readonly HashSet<int> currentWorkers=new HashSet<int>();
        private readonly List<int> departed=new List<int>();
        public float AnimationTime { get; private set; }

        public WorkerVisuals(Transform parent)
        {
            root = new GameObject("Worker activity", typeof(MeshFilter), typeof(MeshRenderer)).transform;
            root.SetParent(parent, false);
            mesh = new Mesh { name = "Worker tools, cargo and activity", indexFormat = IndexFormat.UInt32 }; mesh.MarkDynamic();
            material = new Material(Shader.Find("Frontier/Board")); material.SetColor("_BaseColor", Color.white);
            root.GetComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = root.GetComponent<MeshRenderer>(); renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
        }

        public static WorkerActivity Describe(VisibleEntity worker, PlayerView view, Catalog catalog)
        {
            var result = new WorkerActivity { task = WorkerTask.Moving };
            if (worker.order == CommandKind.Stop || worker.order == CommandKind.Hold) result.task = WorkerTask.Idle;
            else if (worker.order == CommandKind.Gather)
            {
                foreach (var resource in view.resources)
                    if (resource.id == worker.target)
                    {
                        result.task = resource.kind == "catalyst" ? WorkerTask.Gas : WorkerTask.Minerals;
                        result.target = resource.position; result.hasTarget = resource.remaining != 0;
                        result.targetRadius = resource.kind == "catalyst" ? 1.35f : .8f;
                        break;
                    }
            }
            else if (worker.order == CommandKind.Build || worker.order == CommandKind.Repair)
            {
                result.task = worker.order == CommandKind.Build ? WorkerTask.Building : WorkerTask.Repairing;
                foreach (var target in view.entities)
                    if (target.id == worker.target && !target.remembered)
                    {
                        var type = catalog[target.definition]; float half = type.building ? type.footprint * .5f : 0;
                        result.target = new Point(target.position.x + half, target.position.y + half);
                        result.targetRadius = half * .85f; result.hasTarget = true; break;
                    }
            }
            result.active = worker.working && result.hasTarget && !worker.depositing;
            return result;
        }

        public void Begin(BoardView value, float delta)
        {
            board = value; vertices.Clear(); normals.Clear(); colors.Clear(); triangles.Clear();
            currentWorkers.Clear();
            float tickTime = board.Session.View.tick / (float)Simulation.TickRate;
            // Interpolate only as far as the next authoritative tick. Pauses and a
            // stalled player loop cannot leave a worker endlessly extracting.
            if ((!board.Session.IsPaused || board.Session.IsOnline) && board.Session.View.winner == -1)
                AnimationTime = Mathf.Clamp(AnimationTime + Mathf.Max(0, delta), tickTime, tickTime + 1f / Simulation.TickRate);
            var camera = board.Viewer != null ? board.Viewer : Camera.main;
            right = camera != null ? board.Content.InverseTransformDirection(camera.transform.right).normalized : Vector3.right;
            up = camera != null ? board.Content.InverseTransformDirection(camera.transform.up).normalized : Vector3.forward;
            normal = -Vector3.Cross(right, up).normalized;
        }

        public static Color Tint(WorkerTask task)
        {
            switch (task)
            {
                case WorkerTask.Minerals: return MineralColor;
                case WorkerTask.Gas: return GasColor;
                case WorkerTask.Building: return BuildColor;
                case WorkerTask.Repairing: return RepairColor;
                case WorkerTask.Idle: return new Color(1, .84f, .4f);
                default: return new Color(.7f, .8f, .86f);
            }
        }

        public void Add(VisibleEntity worker, WorkerActivity activity, Transform visual, Transform model, Archetype type)
        {
            float size = type.collisionRadius / .45f;
            float phase = AnimationTime * Mathf.PI * 2 + worker.id * 2.39996f;
            float cycle = Mathf.Sin(phase * (activity.task == WorkerTask.Building ? 1.8f : 2.7f));
            float bob = activity.active ? .025f * cycle : .012f * Mathf.Sin(phase * .35f);
            model.localPosition = Vector3.up * bob * size;
            model.localRotation = Quaternion.Euler(activity.active ? cycle * 4 : 0, 0, 0);
            var origin = visual.localPosition;
            Vector3 Local(float x, float y, float z) => origin + visual.localRotation * (new Vector3(x, y + bob, z) * size);
            var tint = Tint(activity.task);
            bool biological=type.faction=="biological";
            if(biological)
            {
                currentWorkers.Add(worker.id);
                if(!motions.TryGetValue(worker.id,out var motion))motion=new Motion{position=worker.position,tick=board.Session.View.tick};
                if(motion.tick!=board.Session.View.tick)
                {
                    float seconds=Mathf.Max(1,board.Session.View.tick-motion.tick)/(float)Simulation.TickRate;
                    motion.amount=Mathf.Clamp01(worker.position.Distance(motion.position)/seconds/Mathf.Max(.1f,type.speed));
                    motion.position=worker.position;motion.tick=board.Session.View.tick;
                }
                motions[worker.id]=motion;
                for(int pair=0;pair<3;pair++)for(int side=-1;side<=1;side+=2)
                {
                    float step=Mathf.Sin(AnimationTime*12+worker.id*.37f+(pair+(side==1?1:0))*Mathf.PI)*motion.amount;
                    var hip=BiologicalVisuals.LegPosition(type,pair,side);
                    var rotation=BiologicalVisuals.LegRotation(pair,side)*Quaternion.Euler(-Mathf.Max(0,step)*28,step*22,0);
                    var knee=hip+rotation*new Vector3(0,.06f,.28f);var foot=hip+rotation*new Vector3(.025f,-.32f,.49f);
                    Stroke(Local(hip.x,hip.y,hip.z),Local(knee.x,knee.y,knee.z),.09f*size,new Color(.45f,.24f,.32f));
                    Stroke(Local(knee.x,knee.y,knee.z),Local(foot.x,foot.y,foot.z),.055f*size,new Color(.88f,.8f,.56f));
                }
            }
            float stroke = activity.active ? (cycle + 1) * .5f : 0;
            // Retracted claws while travelling; alternating drills, pumping arms,
            // hammer blows and a steady welding tool give each job a distinct pose.
            for (int side = -1; side <= 1; side += 2)
            {
                float extension = activity.active ? .16f + .13f * (side == 1 ? stroke : 1 - stroke) : 0;
                float lift = activity.active && activity.task == WorkerTask.Building ? .32f * stroke : .04f;
                var shoulder = Local(side * .24f, .26f, .15f);
                var elbow = Local(side * .4f, .28f + lift, .37f);
                var tip = Local(side * .3f, .18f + lift, .65f + extension);
                Stroke(shoulder, elbow, .085f * size, biological?new Color(.6f,.31f,.39f):new Color(.24f, .32f, .37f));
                Stroke(elbow, tip, .07f * size, tint);
                if(biological)
                {
                    var end=tip+visual.localRotation*new Vector3(-side*.1f,-.03f,.14f)*size;
                    Stroke(tip,end,.055f*size,new Color(.88f,.8f,.56f));
                    Stroke(tip,tip+visual.localRotation*new Vector3(side*.055f,0,.1f)*size,.035f*size,tint);
                }
                else if (activity.task == WorkerTask.Building)
                {
                    var head = visual.localRotation * Vector3.right * .13f * size;
                    Stroke(tip - head, tip + head, .14f * size, tint);
                }
                else if (activity.task == WorkerTask.Repairing)
                {
                    var fork = visual.localRotation * new Vector3(0, 0, .14f) * size;
                    Stroke(tip, tip + fork + right * .08f * size, .055f * size, tint);
                    Stroke(tip, tip + fork - right * .08f * size, .055f * size, tint);
                }
                else Crystal(tip, .095f * size, tint);
            }
            // Cargo keeps its own type when a worker is reassigned to another resource.
            if (worker.cargo > 0)
            {
                var cargoTint = worker.cargoKind == "catalyst" ? GasColor : MineralColor;
                int pieces = Mathf.Clamp(Mathf.CeilToInt(worker.cargo / 3f), 1, 3);
                for (int i = 0; i < pieces; i++)
                {
                    var p = Local((i - (pieces - 1) * .5f) * .23f, .6f, -.48f);
                    if (worker.cargoKind == "catalyst")
                    {
                        if(biological)Disc(p,.17f*size,cargoTint);
                        else Stroke(p - Vector3.up * .13f * size, p + Vector3.up * .13f * size, .15f * size, cargoTint);
                        Crystal(p + Vector3.up * .16f * size, .055f * size, Color.white);
                    }
                    else Crystal(p, .16f * size, cargoTint);
                }
            }
            var badge = origin + Vector3.up * (1.35f * size);
            float radius = Mathf.Clamp(.0038f / board.CellScale, .38f * size, .57f * size);
            Disc(badge, radius, activity.task == WorkerTask.Idle ? Color.Lerp(Dark, tint * .35f, .25f + .15f * Mathf.Sin(phase * .45f)) : Dark);
            Glyph(activity.task, badge + normal * .015f, radius, tint);
            if (worker.depositing)
            {
                var p = badge + up * radius * 1.35f;
                Stroke(p + right * radius * .42f, p - right * radius * .42f, radius * .13f, Color.white);
                Stroke(p - right * radius * .42f, p + (-right + up) * radius * .15f, radius * .13f, Color.white);
                Stroke(p - right * radius * .42f, p + (-right - up) * radius * .15f, radius * .13f, Color.white);
            }
            if (!activity.hasTarget) return;
            bool inspected = board.Session.Selection.Contains(worker.id) || board.Hovered == worker.id;
            if (inspected)
            {
                // Mark the assigned patch itself, even when its crystals hide the
                // contact sparks or the worker is on the return leg of a trip.
                var center = new Vector3(activity.target.x, board.HeightAt(activity.target) + .08f, activity.target.y);
                float ringRadius = activity.task == WorkerTask.Minerals ? 1.25f : activity.targetRadius + .35f;
                for (int i = 0; i < 24; i++)
                {
                    float a = i * Mathf.PI / 12, b = (i + .7f) * Mathf.PI / 12;
                    Stroke(center + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * ringRadius,
                        center + new Vector3(Mathf.Cos(b), 0, Mathf.Sin(b)) * ringRadius, .055f * size, tint);
                }
            }
            var target = new Vector3(activity.target.x, board.HeightAt(activity.target) + .45f, activity.target.y);
            var direction = origin - target; direction.y = 0;
            if (direction.sqrMagnitude > .001f)
                target += direction.normalized * Mathf.Min(activity.targetRadius, direction.magnitude * .65f);
            if (activity.active)
            {
                var tool = Local(0, .25f, .65f);
                bool gas = activity.task == WorkerTask.Gas;
                Stroke(tool, target, (gas ? .055f : .035f) * size, tint * .6f);
                for (int i = 0; i < 3; i++)
                {
                    float t = Mathf.Repeat(AnimationTime * (gas ? 1.2f : 2.2f) + worker.id * .17f + i / 3f, 1);
                    var p = Vector3.Lerp(gas || activity.task == WorkerTask.Minerals ? target : tool,
                        gas || activity.task == WorkerTask.Minerals ? tool : target, t);
                    Crystal(p, (gas ? .095f : .06f) * size, tint);
                }
                if (!gas)
                    for (int i = 0; i < 3; i++)
                    {
                        float a = phase * 2 + i * Mathf.PI * 2 / 3;
                        var offset = (right * Mathf.Cos(a) + up * Mathf.Sin(a)) * (.12f + stroke * .13f) * size;
                        Stroke(target + offset * .4f, target + offset, .035f * size, Color.Lerp(tint, Color.white, .6f));
                    }
            }
            else if (inspected && worker.pendingOrders.Length == 0)
            {
                // Show the exact assignment even while returning cargo. Bound the
                // number of dashes on long routes and let the board shader clip them.
                int count = Mathf.Clamp(Mathf.CeilToInt(Vector3.Distance(origin, target) / .8f), 1, 48);
                for (int i = 0; i < count; i++)
                {
                    float t = i / (float)count;
                    var a = Vector3.Lerp(origin + Vector3.up * .22f, target, t);
                    var b = Vector3.Lerp(origin + Vector3.up * .22f, target, t + .4f / count);
                    Stroke(a, b, .035f * size, tint);
                }
            }
        }

        private void Glyph(WorkerTask task, Vector3 center, float radius, Color tint)
        {
            Vector3 P(float x, float y) => center + (right * x + up * y) * radius;
            void Line(float x, float y, float a, float b) => Stroke(P(x, y), P(a, b), radius * .16f, tint);
            switch (task)
            {
                case WorkerTask.Minerals:
                    Line(0, .65f, .46f, .05f); Line(.46f, .05f, 0, -.55f); Line(0, -.55f, -.46f, .05f); Line(-.46f, .05f, 0, .65f);
                    Line(0, .65f, 0, -.55f); break;
                case WorkerTask.Gas:
                    Disc(P(-.22f, -.25f), radius * .23f, tint); Disc(P(.27f, -.05f), radius * .25f, tint); Disc(P(-.07f, .43f), radius * .21f, tint); break;
                case WorkerTask.Building:
                    Line(-.34f, -.5f, .21f, .3f); Stroke(P(-.25f, .56f), P(.5f, .04f), radius * .32f, tint); break;
                case WorkerTask.Repairing:
                    Line(-.36f, -.5f, .2f, .22f); Line(.2f, .22f, -.12f, .43f); Line(-.12f, .43f, -.1f, .66f);
                    Line(.2f, .22f, .48f, .34f); Line(.48f, .34f, .54f, .62f); break;
                case WorkerTask.Idle:
                    Line(-.23f, -.42f, -.23f, .48f); Line(.23f, -.42f, .23f, .48f); break;
                default:
                    Line(-.4f, -.35f, 0, .05f); Line(0, .05f, .4f, -.35f); Line(-.4f, .08f, 0, .48f); Line(0, .48f, .4f, .08f); break;
            }
        }

        public void End()
        {
            departed.Clear();foreach(int id in motions.Keys)if(!currentWorkers.Contains(id))departed.Add(id);
            foreach(int id in departed)motions.Remove(id);
            mesh.Clear(); mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0); mesh.SetNormals(normals); mesh.SetColors(colors); mesh.RecalculateBounds();
            root.gameObject.SetActive(vertices.Count > 0 && board.Content.gameObject.activeInHierarchy);
        }
        public void Hide() => root.gameObject.SetActive(false);
        private void Triangle(Vector3 a, Vector3 b, Vector3 c, Color color)
        {
            var n = Vector3.Cross(b - a, c - a).normalized;
            int first = vertices.Count; vertices.Add(a); vertices.Add(b); vertices.Add(c);
            for (int i = 0; i < 3; i++) { normals.Add(n); colors.Add(color); triangles.Add(first + i); }
        }
        private void Stroke(Vector3 a, Vector3 b, float width, Color color)
        {
            var side = Vector3.Cross(normal, b - a).normalized * width * .5f;
            Triangle(a - side, b - side, b + side, color); Triangle(a - side, b + side, a + side, color);
        }
        private void Disc(Vector3 center, float radius, Color color)
        {
            for (int i = 0; i < 12; i++)
            {
                float a = i * Mathf.PI / 6, b = (i + 1) * Mathf.PI / 6;
                Triangle(center, center + (right * Mathf.Cos(b) + up * Mathf.Sin(b)) * radius,
                    center + (right * Mathf.Cos(a) + up * Mathf.Sin(a)) * radius, color);
            }
        }
        private void Crystal(Vector3 center, float radius, Color color)
        {
            for (int i = 0; i < 4; i++)
            {
                float a = i * Mathf.PI / 2, b = (i + 1) * Mathf.PI / 2;
                var p = center + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * radius;
                var q = center + new Vector3(Mathf.Cos(b), 0, Mathf.Sin(b)) * radius;
                Triangle(center + Vector3.up * radius * 1.5f, q, p, color);
                Triangle(center - Vector3.up * radius, p, q, color);
            }
        }
        public void Dispose() { Release(root.gameObject); Release(mesh); Release(material); }
        private static void Release(Object value) { if (value == null) return; if (Application.isPlaying) Object.Destroy(value); else Object.DestroyImmediate(value); }
    }
}
