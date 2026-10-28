#if UNITY_WEBGL && !UNITY_EDITOR
using System.Collections;
using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Frontier.Runtime
{
    // WebGL's single-threaded player needs frame-driven polling rather than
    // System.Threading.Timer/Task.Delay. Continuations stay on Unity's main thread.
    public sealed class WebGlAsync : MonoBehaviour
    {
        private static WebGlAsync instance;

        public static Task Delay(int milliseconds, CancellationToken cancellation)
        {
            cancellation.ThrowIfCancellationRequested();
            if (instance == null)
            {
                instance = new GameObject("WebGL asynchronous operations").AddComponent<WebGlAsync>();
                DontDestroyOnLoad(instance.gameObject);
            }
            var completion = new TaskCompletionSource<bool>();
            instance.StartCoroutine(Wait(milliseconds / 1000d, cancellation, completion));
            return completion.Task;
        }

        public static async Task<T> WithTimeout<T>(Task<T> operation, int milliseconds, CancellationToken cancellation)
        {
            await WithTimeout((Task)operation, milliseconds, cancellation);
            return await operation;
        }

        public static async Task WithTimeout(Task operation, int milliseconds, CancellationToken cancellation)
        {
            double end = Time.realtimeSinceStartupAsDouble + milliseconds / 1000d;
            try
            {
                while (!operation.IsCompleted)
                {
                    cancellation.ThrowIfCancellationRequested();
                    if (Time.realtimeSinceStartupAsDouble >= end) throw new TimeoutException("The matchmaking connection timed out.");
                    await Delay(1, cancellation);
                }
                cancellation.ThrowIfCancellationRequested();
                await operation;
            }
            catch
            {
                // The SDK may finish after its owner closed/cancelled the socket.
                // Observe a later fault without scheduling work on a thread pool.
                Observe(operation);
                throw;
            }
        }

        private static async void Observe(Task operation)
        {
            try { await operation; } catch (Exception) { }
        }

        private static IEnumerator Wait(double seconds, CancellationToken cancellation, TaskCompletionSource<bool> completion)
        {
            double end = Time.realtimeSinceStartupAsDouble + seconds;
            while (Time.realtimeSinceStartupAsDouble < end)
            {
                if (cancellation.IsCancellationRequested) { completion.TrySetCanceled(); yield break; }
                yield return null;
            }
            if (cancellation.IsCancellationRequested) completion.TrySetCanceled();
            else completion.TrySetResult(true);
        }
    }
}
#endif
