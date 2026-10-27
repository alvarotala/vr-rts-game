using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Frontier.Harness.Shim
{
    // Single-threaded frame loop standing in for the Unity player loop:
    // network delivery (EarlyUpdate), Start, Update, coroutines, Invoke, Destroy.
    public static class ShimRuntime
    {
        private sealed class Lifecycle
        {
            public MethodInfo start, update, destroy;
        }
        private static readonly Stopwatch clock = Stopwatch.StartNew();
        private static readonly Dictionary<Type, Lifecycle> lifecycles = new Dictionary<Type, Lifecycle>();
        private static readonly List<MonoBehaviour> behaviours = new List<MonoBehaviour>();
        private static readonly List<MonoBehaviour> starting = new List<MonoBehaviour>();
        private static readonly List<Coroutine> coroutines = new List<Coroutine>();
        private static readonly List<(MonoBehaviour owner, string method, double due)> invokes = new List<(MonoBehaviour, string, double)>();
        private static readonly List<Object> destroying = new List<Object>();
        private static readonly List<GameObject> objects = new List<GameObject>();
        public static event Action EarlyUpdate;
        public static double Clock { get; private set; }
        public static double Delta { get; private set; }
        public static int Frame { get; private set; }
        public static bool Quiet;
        public static int? QuitCode { get; private set; }

        public static void RequestQuit(int code) { if (QuitCode == null) QuitCode = code; }
        public static void ResetQuit() => QuitCode = null;

        internal static void Register(GameObject gameObject) => objects.Add(gameObject);

        internal static void Added(Component component)
        {
            if (!(component is MonoBehaviour behaviour)) return;
            var life = Life(component.GetType());
            behaviours.Add(behaviour); starting.Add(behaviour);
            var awake = component.GetType().GetMethod("Awake", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            awake?.Invoke(component, null);
        }

        private static Lifecycle Life(Type type)
        {
            if (lifecycles.TryGetValue(type, out var life)) return life;
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            MethodInfo Find(string name) => type.GetMethod(name, flags, null, Type.EmptyTypes, null);
            life = new Lifecycle { start = Find("Start"), update = Find("Update"), destroy = Find("OnDestroy") };
            lifecycles.Add(type, life); return life;
        }

        public static T Find<T>() where T : Object => behaviours.Where(b => !b.destroyed).OfType<T>().FirstOrDefault();

        public static Coroutine StartCoroutine(MonoBehaviour owner, IEnumerator routine)
        {
            if (routine == null) throw new ArgumentNullException(nameof(routine));
            var coroutine = new Coroutine { owner = owner };
            coroutine.stack.Push(routine);
            // Unity runs a new coroutine synchronously until its first yield.
            Advance(coroutine);
            if (!coroutine.done) coroutines.Add(coroutine);
            return coroutine;
        }

        public static void StopCoroutine(Coroutine coroutine) { if (coroutine != null) coroutine.done = true; }
        public static void StopCoroutines(MonoBehaviour owner) { foreach (var c in coroutines) if (c.owner == owner) c.done = true; }
        public static void Invoke(MonoBehaviour owner, string method, float delay) => invokes.Add((owner, method, Clock + delay));

        private static void Advance(Coroutine coroutine)
        {
            while (!coroutine.done && coroutine.stack.Count > 0)
            {
                if (coroutine.owner.destroyed) { coroutine.done = true; return; }
                var top = coroutine.stack.Peek();
                bool more;
                try { more = top.MoveNext(); }
                catch (Exception error) { UnityEngine.Debug.LogException(error); coroutine.done = true; return; }
                if (!more) { coroutine.stack.Pop(); continue; }
                object yielded = top.Current;
                if (yielded is IEnumerator nested) { coroutine.stack.Push(nested); continue; }
                if (yielded is Coroutine other) { coroutine.stack.Push(WaitFor(other)); return; }
                return; // null or unknown instruction: resume next frame
            }
            coroutine.done = true;
        }

        private static IEnumerator WaitFor(Coroutine other) { while (!other.done) yield return null; }

        public static void DestroyNow(Object target) { Destroy(target); ProcessDestroys(); }

        public static void Destroy(Object target)
        {
            if (target == null || target.destroyed || destroying.Contains(target)) return;
            destroying.Add(target);
        }

        // One frame. Returns false when the process requested to quit.
        public static bool Tick()
        {
            double now = clock.Elapsed.TotalSeconds;
            Delta = Math.Max(0, now - Clock); Clock = now; Frame++;
            EarlyUpdate?.Invoke();
            foreach (var behaviour in starting.ToArray())
            {
                starting.Remove(behaviour);
                if (behaviour.destroyed) continue;
                var start = Life(behaviour.GetType()).start;
                if (start == null) continue;
                try
                {
                    object result = start.Invoke(behaviour, null);
                    if (result is IEnumerator routine) StartCoroutine(behaviour, routine);
                }
                catch (TargetInvocationException error) { UnityEngine.Debug.LogException(error.InnerException); }
            }
            foreach (var behaviour in behaviours.ToArray())
            {
                if (behaviour.destroyed || !behaviour.enabled || starting.Contains(behaviour)) continue;
                var update = Life(behaviour.GetType()).update;
                if (update == null) continue;
                try { update.Invoke(behaviour, null); }
                catch (TargetInvocationException error) { UnityEngine.Debug.LogException(error.InnerException); }
            }
            foreach (var coroutine in coroutines.ToArray())
            {
                if (!coroutine.done) Advance(coroutine);
                if (coroutine.done) coroutines.Remove(coroutine);
            }
            foreach (var invoke in invokes.ToArray())
            {
                if (invoke.due > Clock) continue;
                invokes.Remove(invoke);
                if (invoke.owner.destroyed) continue;
                invoke.owner.GetType().GetMethod(invoke.method, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.Invoke(invoke.owner, null);
            }
            ProcessDestroys();
            return QuitCode == null;
        }

        private static void ProcessDestroys()
        {
            foreach (var target in destroying.ToArray())
            {
                destroying.Remove(target);
                var components = target is GameObject go ? go.components.ToArray() : new[] { (Component)target };
                foreach (var component in components)
                {
                    if (component.destroyed) continue;
                    component.destroyed = true;
                    if (component is MonoBehaviour behaviour)
                    {
                        behaviours.Remove(behaviour); starting.Remove(behaviour);
                        var destroy = Life(behaviour.GetType()).destroy;
                        try { destroy?.Invoke(behaviour, null); }
                        catch (TargetInvocationException error) { UnityEngine.Debug.LogException(error.InnerException); }
                    }
                }
                if (target is GameObject owner) { owner.destroyed = true; objects.Remove(owner); }
            }
        }

        // Runs frames at roughly the requested rate until the condition holds or the deadline passes.
        public static bool RunUntil(Func<bool> condition, double seconds, int frameRate = 120)
        {
            double deadline = clock.Elapsed.TotalSeconds + seconds;
            int sleep = Math.Max(1, 1000 / frameRate);
            while (clock.Elapsed.TotalSeconds < deadline)
            {
                Tick();
                if (condition()) return true;
                System.Threading.Thread.Sleep(sleep);
            }
            return condition();
        }

        public static void DestroyAll()
        {
            foreach (var gameObject in objects.ToArray()) Destroy(gameObject);
            Tick();
        }
    }
}
