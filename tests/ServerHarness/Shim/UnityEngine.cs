using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Frontier.Harness.Shim;

// Minimal UnityEngine surface used by Frontier.Networking. Semantics follow Unity where
// the networking code depends on them: Awake on AddComponent, Start before the first
// Update, coroutines that run synchronously until their first yield, deferred Destroy.
namespace UnityEngine
{
    public enum LogType { Error = 0, Assert = 1, Warning = 2, Log = 3, Exception = 4 }
    public enum LogOption { None = 0, NoStacktrace = 1 }
    public enum RuntimePlatform { OSXPlayer = 1, LinuxPlayer = 13, Android = 11, LinuxServer = 43 }

    [AttributeUsage(AttributeTargets.Field)] public sealed class SerializeField : Attribute { }

    public static class Debug
    {
        public static Action<LogType, string> Sink = (type, message) =>
        {
            if (ShimRuntime.Quiet && type == LogType.Log) return;
            Console.WriteLine((type == LogType.Log ? "" : type.ToString().ToUpperInvariant() + ": ") + message);
        };
        public static void Log(object message) => Sink(LogType.Log, message?.ToString());
        public static void LogWarning(object message) => Sink(LogType.Warning, message?.ToString());
        public static void LogError(object message) => Sink(LogType.Error, message?.ToString());
        public static void LogException(Exception exception) => Sink(LogType.Exception, exception?.ToString());
        public static void LogFormat(string format, params object[] args) => Sink(LogType.Log, string.Format(format, args));
        public static void LogFormat(LogType logType, LogOption logOptions, Object context, string format, params object[] args)
            => Sink(logType, string.Format(format, args));
    }

    public class Object
    {
        internal bool destroyed;
        public string name { get; set; }
        public static void Destroy(Object obj) => ShimRuntime.Destroy(obj);
        public static void DestroyImmediate(Object obj) => ShimRuntime.DestroyNow(obj);
        public static void DontDestroyOnLoad(Object target) { }
        public static T FindFirstObjectByType<T>() where T : Object => ShimRuntime.Find<T>();
    }

    public class Component : Object
    {
        public GameObject gameObject { get; internal set; }
        public T GetComponent<T>() => gameObject.GetComponent<T>();
    }

    public class Behaviour : Component
    {
        public bool enabled { get; set; } = true;
        public bool isActiveAndEnabled => enabled && !destroyed;
    }

    public class MonoBehaviour : Behaviour
    {
        public Coroutine StartCoroutine(IEnumerator routine) => ShimRuntime.StartCoroutine(this, routine);
        public void StopCoroutine(Coroutine routine) => ShimRuntime.StopCoroutine(routine);
        public void StopAllCoroutines() => ShimRuntime.StopCoroutines(this);
        public void Invoke(string methodName, float time) => ShimRuntime.Invoke(this, methodName, time);
    }

    public sealed class GameObject : Object
    {
        internal readonly List<Component> components = new List<Component>();
        public GameObject(string name) { this.name = name; ShimRuntime.Register(this); }
        public T AddComponent<T>() where T : Component
        {
            var component = Activator.CreateInstance<T>();
            component.gameObject = this; component.name = name;
            components.Add(component);
            ShimRuntime.Added(component);
            return component;
        }
        public T GetComponent<T>() => components.Where(c => !c.destroyed).OfType<T>().FirstOrDefault();
    }

    public class YieldInstruction { }
    public sealed class Coroutine : YieldInstruction
    {
        internal MonoBehaviour owner;
        internal readonly Stack<IEnumerator> stack = new Stack<IEnumerator>();
        internal bool done;
    }
    public abstract class CustomYieldInstruction : IEnumerator
    {
        public abstract bool keepWaiting { get; }
        public object Current => null;
        public bool MoveNext() => keepWaiting;
        public void Reset() { }
    }
    public sealed class WaitForSecondsRealtime : CustomYieldInstruction
    {
        private readonly double seconds;
        private double until = -1;
        public WaitForSecondsRealtime(float time) { seconds = time; }
        public override bool keepWaiting
        {
            get
            {
                if (until < 0) until = Time.realtimeSinceStartupAsDouble + seconds;
                if (Time.realtimeSinceStartupAsDouble < until) return true;
                until = -1; return false;
            }
        }
    }
    public class AsyncOperation : YieldInstruction, IEnumerator
    {
        public bool isDone { get; internal set; }
        public object Current => null;
        public bool MoveNext() => !isDone;
        public void Reset() { }
    }

    public static class Time
    {
        public static double realtimeSinceStartupAsDouble => ShimRuntime.Clock;
        public static float realtimeSinceStartup => (float)ShimRuntime.Clock;
        public static float unscaledTime => (float)ShimRuntime.Clock;
        public static double unscaledTimeAsDouble => ShimRuntime.Clock;
        public static float time => (float)ShimRuntime.Clock;
        public static float deltaTime => (float)ShimRuntime.Delta;
        public static float unscaledDeltaTime => (float)ShimRuntime.Delta;
        public static int frameCount => ShimRuntime.Frame;
    }

    public static class Application
    {
        public static bool runInBackground { get; set; }
        public static int targetFrameRate { get; set; }
        public static bool isEditor => false;
        public static RuntimePlatform platform => RuntimePlatform.LinuxServer;
        public static void Quit() => ShimRuntime.RequestQuit(0);
        public static void Quit(int exitCode) => ShimRuntime.RequestQuit(exitCode);
    }

    public class TextAsset : Object
    {
        public string text { get; }
        public byte[] bytes { get; }
        public TextAsset(string name, byte[] content)
        {
            this.name = name; bytes = content;
            // Unity decodes UTF-8 and drops a byte order mark.
            text = new System.Text.UTF8Encoding(false).GetString(content).TrimStart('﻿');
        }
    }

    public static class Resources
    {
        // Assets/Frontier/Resources, resolved by the harness at startup.
        public static string Root;
        private static readonly string[] TextExtensions = { ".json", ".txt", ".bytes", ".csv", ".xml" };

        public static T Load<T>(string path) where T : Object
        {
            if (typeof(T) != typeof(TextAsset)) return null;
            string directory = Path.Combine(Root, Path.GetDirectoryName(path) ?? "");
            string name = Path.GetFileName(path);
            if (!Directory.Exists(directory)) return null;
            var file = Directory.GetFiles(directory).Where(f => Path.GetFileNameWithoutExtension(f) == name
                && TextExtensions.Contains(Path.GetExtension(f))).OrderBy(f => f, StringComparer.Ordinal).FirstOrDefault();
            return file == null ? null : (T)(Object)new TextAsset(name, File.ReadAllBytes(file));
        }

        public static T[] LoadAll<T>(string path) where T : Object
        {
            if (typeof(T) != typeof(TextAsset)) return Array.Empty<T>();
            string directory = Path.Combine(Root, path);
            if (!Directory.Exists(directory)) return Array.Empty<T>();
            return Directory.GetFiles(directory).Where(f => TextExtensions.Contains(Path.GetExtension(f)))
                .OrderBy(f => f, StringComparer.Ordinal)
                .Select(f => (T)(Object)new TextAsset(Path.GetFileNameWithoutExtension(f), File.ReadAllBytes(f))).ToArray();
        }
    }
}

namespace UnityEditor
{
    [System.AttributeUsage(System.AttributeTargets.Method)]
    public sealed class MenuItem : System.Attribute { public MenuItem(string itemName) { } }
}
