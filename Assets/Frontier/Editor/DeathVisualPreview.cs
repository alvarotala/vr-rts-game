using System;
using System.Collections.Generic;
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
            var models = new UnitVisuals();
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
                var liveVisuals = (Dictionary<int, Transform>)typeof(BoardView).GetField("entities", flags).GetValue(board);
                var last = liveVisuals[living[living.Length - 1].id]; var torso = last.Find("Role silhouette");
                last.localRotation = Quaternion.Euler(0, -55, 0); last.localPosition += new Vector3(.12f, .12f, -.09f);
                torso.localPosition = new Vector3(.025f, .05f, -.015f); torso.localRotation = Quaternion.Euler(4, 37, -3);
                var lastModel = models.Get(types[types.Length - 1], remains: true);
                var expectedPose = lastModel.vertices.Select(v => last.localPosition + last.localRotation * (torso.localPosition + torso.localRotation * v)).ToArray();
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
                effects.Render(board, 101); var initialPose = mesh.vertices;
                Check(expectedPose.Select((p, i) => Vector3.Distance(p, initialPose[i])).Max() < .0001f,
                    "First death frame preserves the last visible root position, facing and torso pose after pooling");
                int objects = board.Content.GetComponentsInChildren<Transform>(true).Length;
                foreach (float age in new[] { .05f, .25f, .8f, 3f, 4.75f })
                { effects.Render(board, 101 + age * Simulation.TickRate); Capture("death-" + Mathf.RoundToInt(age * 100).ToString("000")); }
                effects.Render(board, 101 + .8f * Simulation.TickRate); var settled = mesh.vertices;
                Check(effects.ActiveCount == 12 && mesh.subMeshCount == 1 && settled.Length > 0, "All remains share one batched mesh after falling");
                foreach (var death in deaths)
                {
                    session.View.deaths = new[] { death }; effects.Render(board, 101 + .8f * Simulation.TickRate);
                    var source = models.Get(session.Catalog[death.definition], remains: true); var sourceVertices = source.vertices; var body = mesh.vertices;
                    int high = Array.IndexOf(sourceVertices, sourceVertices.OrderByDescending(v => v.y).First());
                    int low = Array.IndexOf(sourceVertices, sourceVertices.OrderBy(v => v.y).First());
                    var type = session.Catalog[death.definition]; var axis = body[high] - body[low];
                    string silhouette = " (height=" + mesh.bounds.size.y.ToString("F4") + ", live=" + source.bounds.size.y.ToString("F4")
                        + ", ratio=" + (mesh.bounds.size.y / source.bounds.size.y).ToString("F4") + ", axis=" + axis.ToString("F4") + ")";
                    if (type.faction == "biological")
                        Check(mesh.bounds.size.y < source.bounds.size.y * .98f && axis.y > 0,
                            "Biological " + type.role + " lowers its body and folds its legs without turning upside down" + silhouette);
                    else if (type.role == "worker" || type.role == "scout" || type.role == "siege")
                        Check(axis.y > source.bounds.size.y * .6f,
                            "Industrial " + type.role + " stays upright on its chassis instead of overturning" + silhouette);
                    else Check(Mathf.Abs(axis.y) < source.bounds.size.y * .5f && new Vector2(axis.x, axis.z).magnitude > source.bounds.size.y * .65f,
                        "Industrial " + type.role + " settles into a controlled prone silhouette" + silhouette);
                    CheckGround(body, "Flat-ground contact / " + type.id);
                }
                session.View.deaths = deaths; effects.Render(board, 101 + .8f * Simulation.TickRate);
                CloseView(new Point(23, 20)); Capture("industrial-settled");
                CloseView(new Point(23, 25)); Capture("biological-settled");
                preview.camera.orthographicSize = board.CellScale * 12;
                preview.camera.transform.position = center + new Vector3(.04f, .5f, -.6f); preview.camera.transform.LookAt(center);
                effects.Render(board, 101 + 3f * Simulation.TickRate);
                Check(settled.SequenceEqual(mesh.vertices) && mesh.colors.All(c => c.a == 1), "Bodies stay on the ground fully visible before the fade");
                effects.Render(board, 101 + 4.75f * Simulation.TickRate);
                Check(mesh.colors.All(c => Mathf.Abs(c.a - .5f) < .001f), "Corpses gradually dissolve through half opacity");
                var paused = mesh.vertices; session.Pause("death-preview", true); effects.Render(board, 101 + 4.75f * Simulation.TickRate);
                Check(paused.SequenceEqual(mesh.vertices), "Frozen simulation time does not advance body animation"); session.Pause("death-preview", false);
                effects.Render(board, 101 + DeathEffects.Lifetime * Simulation.TickRate);
                Capture("death-600");
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
                session.LoadMap("rift"); session.StartMatch();
                session.View.entities = Array.Empty<VisibleEntity>(); session.View.resources = Array.Empty<ResourceState>();
                session.View.attacks = Array.Empty<AttackEvent>(); session.View.abilityEvents = Array.Empty<AbilityEvent>();
                session.View.tick = 200; Array.Fill(session.View.visibility, (byte)2); Call(board, "LateUpdate");
                effects = (DeathEffects)typeof(BoardView).GetField("deathEffects", flags).GetValue(board);
                mesh = board.Content.Find("Unit remains").GetComponent<MeshFilter>().sharedMesh;
                var ramp = new Point(51.5f, 40);
                foreach (var type in types)
                {
                    session.View.deaths = new[] { new DeathEvent { unit = 9000, owner = 0, definition = type.id, position = ramp, direction = new Point(0, 1), tick = 200 } };
                    effects.Clear();
                    int bodyVertices = models.Get(type, remains: true).vertexCount;
                    foreach (float age in new[] { .1f, .25f, .8f })
                    {
                        effects.Render(board, 200 + age * Simulation.TickRate);
                        CheckGround(mesh.vertices.Take(bodyVertices).ToArray(), "Ramp contact / " + type.id + " / " + age, age >= DeathEffects.FallSeconds);
                    }
                    if (type.role == "siege" || type.role == "melee")
                    {
                        CloseView(ramp, 3); Capture("ramp-" + type.id);
                    }
                }
                Check(!ShaderUtil.ShaderHasError(Shader.Find("Frontier/Remains")), "Fog, clipping and stereo dissolve shader compiles");
                Debug.Log($"FRONTIER_DEATH_VISUALS passed: {checks} checks, {captures} views; no player build or physical Quest validation");

                void Capture(string name)
                {
                    preview.BeginStaticPreview(new Rect(0, 0, 1500, 850)); preview.Render(true);
                    var image = preview.EndStaticPreview(); File.WriteAllBytes("TestResults/Deaths/" + name + ".png", image.EncodeToPNG()); Object.DestroyImmediate(image); captures++;
                }
                void CloseView(Point point, float size = 11)
                {
                    preview.camera.orthographicSize = board.CellScale * size;
                    var focus = board.Content.TransformPoint(new Vector3(point.x, board.HeightAt(point) + .3f, point.y));
                    preview.camera.transform.position = focus + new Vector3(.04f, .24f, -.6f); preview.camera.transform.LookAt(focus);
                }
                void CheckGround(Vector3[] body, string message, bool settledBody = true)
                {
                    var gaps = body.Select(p => p.y - board.HeightAt(new Point(p.x, p.z))).ToArray();
                    Check(gaps.Min() >= -.0001f && (!settledBody || gaps.Min() <= .025f), message + " stays above terrain" + (settledBody ? " with a supported contact" : " while falling"));
                }
            }
            finally
            {
                Object.DestroyImmediate(boardObject); Object.DestroyImmediate(fixture); models.Dispose(); preview.Cleanup();
                Shader.SetGlobalVector("_FrontierBounds", oldBounds); Shader.SetGlobalMatrix("_FrontierWorldToBoard", oldMatrix);
            }
        }
    }
}
