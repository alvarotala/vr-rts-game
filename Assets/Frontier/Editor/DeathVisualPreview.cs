using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Frontier.Core;
using Frontier.Runtime;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Frontier.Editor
{
    public static class DeathVisualPreview
    {
        [MenuItem("Frontier/Preview and check unit deaths")]
        public static void Run()
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var fixture = new GameObject("Death preview session");
            var boardObject = new GameObject("Death preview board");
            var preview = new PreviewRenderUtility(); int checks = 0, captures = 0;
            var oldBounds = Shader.GetGlobalVector("_FrontierBounds"); var oldMatrix = Shader.GetGlobalMatrix("_FrontierWorldToBoard");
            void Call(object target, string method) => target.GetType().GetMethod(method, flags).Invoke(target, null);
            void Check(bool condition, string message) { if (!condition) throw new Exception(message); checks++; Debug.Log("DEATH_PASS: " + message); }
            try
            {
                var session = fixture.AddComponent<GameSession>(); if (session.Catalog == null) Call(session, "Awake");
                session.LoadMap("training"); session.StartMatch();
                var board = boardObject.AddComponent<BoardView>(); board.Session = session; Call(board, "Start");
                board.Viewer = preview.camera; preview.AddSingleGO(boardObject);
                var types = new[] { "industrial", "biological" }.SelectMany(f => new[] { "worker", "scout", "melee", "ranged", "siege", "caster" }.Select(r => session.Catalog.Role(f, r))).ToArray();
                var living = types.Select((type, i) => new VisibleEntity { id = 1000 + i, owner = i / 6, definition = type.id,
                    position = new Point(14 + i % 6 * 3.5f, 20 + i / 6 * 5), health = type.health, complete = true }).ToArray();
                session.View.entities = living; session.View.resources = Array.Empty<ResourceState>(); session.View.attacks = Array.Empty<AttackEvent>();
                session.View.abilityEvents = Array.Empty<AbilityEvent>(); session.View.tick = 100;
                Array.Fill(session.View.visibility, (byte)2); Call(board, "LateUpdate");
                var effects = (DeathEffects)typeof(BoardView).GetField("deathEffects", flags).GetValue(board);
                var mesh = board.Content.Find("Unit remains").GetComponent<MeshFilter>().sharedMesh;
                Check(effects.ActiveCount == 0, "Living units have no corpse effects");
                preview.camera.orthographic = true; preview.camera.orthographicSize = board.CellScale * 12;
                preview.camera.nearClipPlane = .001f; preview.camera.farClipPlane = 10;
                preview.camera.clearFlags = CameraClearFlags.SolidColor; preview.camera.backgroundColor = new Color(.025f, .04f, .06f);
                var center = board.Content.TransformPoint(new Vector3(23, .5f, 22.5f));
                preview.camera.transform.position = center + new Vector3(.04f, .5f, -.6f); preview.camera.transform.LookAt(center);
                Directory.CreateDirectory("TestResults/Deaths"); Capture("00-alive");
                var deaths = living.Select(e => new DeathEvent { unit = e.id, owner = e.owner, definition = e.definition, position = e.position, tick = 101 }).ToArray();
                session.View.entities = Array.Empty<VisibleEntity>(); session.View.deaths = deaths; session.View.tick = 101;
                Call(board, "LateUpdate");
                Check(effects.ActiveCount == 12, "Both factions and all six unit roles leave bodies after live visuals are removed");
                Check(board.Content.Find("Unit remains").GetComponentsInChildren<Collider>().Length == 0, "Bodies have no colliders");
                int objects = board.Content.GetComponentsInChildren<Transform>(true).Length;
                foreach (float age in new[] { .05f, .25f, .8f, 3f, 4.75f, 6f })
                { effects.Render(board, 101 + age * Simulation.TickRate); Capture("death-" + Mathf.RoundToInt(age * 100).ToString("000")); }
                effects.Render(board, 101 + .8f * Simulation.TickRate); var settled = mesh.vertices;
                Check(effects.ActiveCount == 12 && mesh.subMeshCount == 1 && settled.Length > 0, "All remains share one batched mesh after falling");
                effects.Render(board, 101 + 3f * Simulation.TickRate);
                Check(settled.SequenceEqual(mesh.vertices) && mesh.colors.All(c => c.a == 1), "Bodies stay on the ground fully visible before the fade");
                effects.Render(board, 101 + 4.75f * Simulation.TickRate);
                Check(mesh.colors.All(c => Mathf.Abs(c.a - .5f) < .001f), "Corpses gradually dissolve through half opacity");
                var paused = mesh.vertices; session.Pause("death-preview", true); effects.Render(board, 101 + 4.75f * Simulation.TickRate);
                Check(paused.SequenceEqual(mesh.vertices), "Frozen simulation time does not advance body animation"); session.Pause("death-preview", false);
                effects.Render(board, 101 + DeathEffects.Lifetime * Simulation.TickRate);
                Check(effects.ActiveCount == 0 && mesh.vertexCount == 0, "Bodies are removed after six seconds");
                effects.Render(board, 100); Check(effects.ActiveCount == 0, "Future deaths cannot appear early");
                Array.Fill(session.View.visibility, (byte)1); effects.Render(board, 102);
                Check(effects.ActiveCount == 0, "Unseen terrain hides remains");
                Array.Fill(session.View.visibility, (byte)2); session.View.deaths = Array.Empty<DeathEvent>(); effects.Render(board, 102);
                Check(effects.ActiveCount == 0, "A disappearing unit without a death event never creates a corpse");
                session.View.deaths = Enumerable.Range(0, 200).Select(i => { var death = deaths[i % deaths.Length]; death.unit = 2000 + i; return death; }).ToArray();
                effects.Render(board, 104);
                Check(effects.ActiveCount > 0 && effects.ActiveCount <= DeathEffects.MaxBodies && mesh.vertexCount <= DeathEffects.MaxVertices, "Mass casualties stay within both body and vertex budgets");
                Check(board.Content.GetComponentsInChildren<Transform>(true).Length == objects, "Deaths do not create per-body GameObjects or particle systems");
                session.View.deaths = deaths; effects.Render(board, 120);
                var point = deaths[0].position; var ground = board.Content.TransformPoint(new Vector3(point.x, board.HeightAt(point), point.y));
                board.ResolvePointerTarget(new Ray(ground + Vector3.up, Vector3.down), point, ground, out int target, out _);
                Check(target == 0, "Pointer passes over a corpse without selecting or targeting it");
                board.Content.gameObject.SetActive(false); effects.Render(board, 102);
                Check(effects.ActiveCount == 0 && mesh.vertexCount == 0, "Hidden board clears corpse rendering");
                board.Content.gameObject.SetActive(true); effects.Render(board, 102); effects.Clear();
                Check(effects.ActiveCount == 0 && mesh.vertexCount == 0, "Clear drops old remains");
                session.StartMatch(); Call(board, "LateUpdate");
                effects = (DeathEffects)typeof(BoardView).GetField("deathEffects", flags).GetValue(board);
                Check(effects.ActiveCount == 0 && session.View.deaths.Length == 0, "Rematch starts without old corpses or poses");
                Check(!ShaderUtil.ShaderHasError(Shader.Find("Frontier/Remains")), "Fog, clipping and stereo dissolve shader compiles");
                Debug.Log($"FRONTIER_DEATH_VISUALS passed: {checks} checks, {captures} views; no player build or physical Quest validation");

                void Capture(string name)
                {
                    preview.BeginStaticPreview(new Rect(0, 0, 1500, 850)); preview.Render(true);
                    var image = preview.EndStaticPreview(); File.WriteAllBytes("TestResults/Deaths/" + name + ".png", image.EncodeToPNG()); Object.DestroyImmediate(image); captures++;
                }
            }
            finally
            {
                Object.DestroyImmediate(boardObject); Object.DestroyImmediate(fixture); preview.Cleanup();
                Shader.SetGlobalVector("_FrontierBounds", oldBounds); Shader.SetGlobalMatrix("_FrontierWorldToBoard", oldMatrix);
            }
        }
    }
}
