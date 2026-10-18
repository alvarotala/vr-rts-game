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
    public static class SoldierVisualPreview
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static void Invoke(object target, string method) => target.GetType().GetMethod(method, Private).Invoke(target, null);
        [MenuItem("Frontier/Preview soldier combat")]
        public static void Render()
        {
            const string output = "TestResults/Soldiers"; Directory.CreateDirectory(output);
            var preview = new PreviewRenderUtility(); var fixture = new GameObject("Soldier session"); var boardObject = new GameObject("Soldier board");
            var bounds = Shader.GetGlobalVector("_FrontierBounds"); var matrix = Shader.GetGlobalMatrix("_FrontierWorldToBoard");
            int checks = 0, captures = 0;
            try
            {
                var session = fixture.AddComponent<GameSession>(); if (session.Catalog == null) Invoke(session, "Awake");
                var board = boardObject.AddComponent<BoardView>(); board.Session = session; board.Viewer = preview.camera; Invoke(board, "Start"); preview.AddSingleGO(boardObject);
                var visuals = (Dictionary<int, Transform>)typeof(BoardView).GetField("entities", Private).GetValue(board);
                foreach (string faction in new[] { "industrial", "biological" })
                {
                    session.Faction = session.EnemyFaction = faction; session.StartMatch();
                    var actors = new List<VisibleEntity>(); var shots = new List<AttackEvent>();
                    string[] roles = { "melee", "ranged", "siege", "scout" };
                    for (int i = 0; i < roles.Length; i++)
                    {
                        var type = session.Catalog.Role(faction, roles[i]); var targetType = session.Catalog.Role(faction, "melee");
                        var from = new Point(17, 19 + i * 5); var to = new Point(17 + (i == 0 ? 1.55f : i == 1 ? 5.5f : i == 2 ? 9 : 2.8f), from.y);
                        actors.Add(new VisibleEntity { id = 100 + i, owner = 0, definition = type.id, position = from, health = type.health, complete = true, order = CommandKind.Attack, target = 200 + i });
                        actors.Add(new VisibleEntity { id = 200 + i, owner = 1, definition = targetType.id, position = to, health = targetType.health - 10, complete = true, damageTick = 100 });
                        shots.Add(new AttackEvent { id = i + 1, tick = 100, attacker = 100 + i, target = 200 + i, owner = 0, definition = type.id, targetDefinition = targetType.id, origin = from, destination = to });
                    }
                    session.View.entities = actors.ToArray(); session.View.attacks = Array.Empty<AttackEvent>(); session.View.resources = Array.Empty<ResourceState>();
                    Array.Fill(session.View.visibility, (byte)2); session.View.tick = 100;
                    Refresh(); CameraAt(new Point(21, 26), 10); Refresh();
                    var melee = visuals[100]; var rifle = visuals[101]; var siege = visuals[102];
                    var meleeBody = melee.Find("Role silhouette"); var rifleWeapon = rifle.Find("Role silhouette/Animated weapon");
                    var siegeWeapon = siege.Find("Role silhouette/Animated weapon");
                    Vector3 rifleRest = rifleWeapon.localPosition, siegeRest = siegeWeapon.localPosition;
                    session.View.attacks = shots.ToArray();
                    session.View.tick = 102; Refresh();
                    Check(meleeBody.localPosition.magnitude > .15f && Quaternion.Angle(meleeBody.Find("Animated weapon").localRotation, Quaternion.identity) > 20,
                        "Melee lunges and swings its actual weapon / " + faction);
                    Check(rifleWeapon.localPosition.z < rifleRest.z - .02f, "Rifle recoils along its barrel / " + faction);
                    Check(siegeWeapon.localPosition.z < siegeRest.z - .03f, "Artillery barrels recoil / " + faction);
                    Check(Vector3.Dot(rifle.Find("Role silhouette").forward, board.Content.right) > .97f, "Upper body aims at its actual victim");
                    Check(board.TryWeaponMuzzle(101, 2, out var muzzle) && Vector3.Distance(muzzle, board.Content.InverseTransformPoint(rifleWeapon.position)) > .4f,
                        "Tracer starts at the animated barrel tip");
                    Capture(faction + "-strikes");
                    session.Selection.Add(101); Refresh(); Capture(faction + "-selected-target"); session.Selection.Clear();
                    session.Pause("soldier-preview", true); Refresh();
                    var frozen = Poses(); var effectMesh = board.Content.Find("Weapon effects").GetComponent<MeshFilter>().sharedMesh; var frozenEffects = effectMesh.vertices;
                    Refresh(); Check(frozen.SequenceEqual(Poses()) && frozenEffects.SequenceEqual(effectMesh.vertices), "Pause freezes rigs and projectiles together");
                    session.Pause("soldier-preview", false);
                    for (int frame = 0; frame < 24; frame++)
                    {
                        session.View.tick = 100 + frame; Refresh(); Capture(faction + "-pose-" + frame.ToString("00"));
                    }
                    Check(meleeBody.localPosition.sqrMagnitude < .00001f && rifleWeapon.localPosition == rifleRest && siegeWeapon.localPosition == siegeRest,
                        "Attack recovery returns every weapon and torso to rest");
                    session.View.tick = 130; actors[0].position = new Point(17.35f, 19); Refresh();
                    Check(Quaternion.Angle(melee.Find("Left stride").localRotation, melee.Find("Right stride").localRotation) > 5,
                        "Walking alternates the two legs from actual movement");
                    session.View.tick = 102; actors[2].order = CommandKind.Move; Refresh();
                    Check(rifleWeapon.localPosition == rifleRest, "New Move cancels an old firing pose"); actors[2].order = CommandKind.Attack;
                    int objects = board.Content.GetComponentsInChildren<Transform>(true).Length;
                    session.View.entities = actors.Where(e => e.id != 101 && e.id != 201).ToArray(); Refresh();
                    var replacement = new VisibleEntity { id = 999, owner = 0, definition = actors[2].definition, position = actors[2].position, complete = true, health = actors[2].health, order = CommandKind.Stop };
                    session.View.entities = session.View.entities.Append(replacement).ToArray(); session.View.attacks = Array.Empty<AttackEvent>(); Refresh();
                    Check(visuals[999] == rifle && rifle.Find("Role silhouette").localPosition == Vector3.zero && rifleWeapon.localPosition == rifleRest,
                        "Pooled soldier cannot inherit the previous attack pose");
                    Check(board.Content.GetComponentsInChildren<Transform>(true).Length == objects, "Rigs are reused without per-attack objects");
                    session.LeaveMatch();

                    // Actual simulation: melee must run into range before the first
                    // attack; ranged and siege fire from their respective distances.
                    session.StartMatch();
                    foreach (var entity in session.Match.Entities.Values.Where(e => !session.Catalog[e.definition].building).ToArray()) session.Match.Entities.Remove(entity.id);
                    var fighters = new List<EntityState>();
                    for (int i = 0; i < 3; i++)
                    {
                        var type = session.Catalog.Role(faction, roles[i]); var victimType = session.Catalog.Role(faction, "melee");
                        var attacker = new EntityState { id = 1000 + i, owner = 0, definition = type.id, position = new Point(22, 20 + i * 7), health = type.health, order = CommandKind.Hold, attackReadyTick = int.MaxValue };
                        var victim = new EntityState { id = 2000 + i, owner = 1, definition = victimType.id, position = new Point(i == 2 ? 31 : i == 0 ? 29 : 27.5f, 20 + i * 7), health = victimType.health, order = CommandKind.Hold, attackReadyTick = int.MaxValue };
                        session.Match.Entities.Add(attacker.id, attacker); session.Match.Entities.Add(victim.id, victim); fighters.Add(attacker);
                    }
                    for (int i = 0; i < 4; i++) session.Match.Step();
                    foreach (var fighter in fighters)
                    {
                        fighter.attackReadyTick = 0;
                        Check(session.Match.Submit(new GameCommand { kind = CommandKind.Attack, player = 0, entities = new[] { fighter.id }, target = 2000 + fighter.id - 1000 }).accepted,
                            "Real attack order accepted for " + fighter.definition);
                    }
                    SetView(); CameraAt(new Point(26, 27), 10); Refresh();
                    bool approached = false, struck = false;
                    for (int frame = 0; frame < 48; frame++)
                    {
                        session.Match.Step(); session.Match.Step(); SetView();
                        // Settle only the renderer's frame interpolation between the
                        // sampled simulation ticks; this never advances the match.
                        for (int render = 0; render < 4; render++) Refresh();
                        var actual = session.View.attacks.Where(a => a.attacker == 1000).ToArray();
                        if (fighters[0].position.x > 22.5f && actual.Length == 0 && !struck) approached = true;
                        if (actual.Length > 0) struck = true;
                        Capture(faction + "-battle-" + frame.ToString("00"));
                    }
                    Check(approached && struck, "Real melee runs into contact before showing its first swing");
                    var center = board.Content.TransformPoint(new Vector3(26, 1, 27));
                    preview.camera.transform.position = center + Vector3.up * .6f; preview.camera.transform.LookAt(center, board.transform.forward); Refresh(); Capture(faction + "-top");
                    board.Width = .6f; CameraAt(new Point(26, 27), 10); Refresh(); Capture(faction + "-compact"); board.Width = 1.2f;
                    session.LeaveMatch(); Check(visuals.Count == 0, "Closing a match removes soldier rigs");

                    Vector3[] Poses() => board.Content.GetComponentsInChildren<Transform>().Where(t => t.name == "Animated weapon" || t.name == "Role silhouette" || t.name.Contains("stride"))
                        .SelectMany(t => new[] { t.localPosition, t.localEulerAngles }).ToArray();
                    void SetView() { typeof(GameSession).GetProperty("View").SetValue(session, session.Match.View(0)); Refresh(); }
                }
                Debug.Log($"FRONTIER_SOLDIER_VISUALS passed: {checks} checks, {captures} views; actual melee pursuit, animated limbs/weapons, recoil, target identity, pause, pool reuse, both factions; no player build");
                void Refresh() => Invoke(board, "LateUpdate");
                void Check(bool condition, string message) { if (!condition) throw new Exception(message); checks++; Debug.Log("SOLDIER_PASS: " + message); }
                void CameraAt(Point point, float radius)
                {
                    Refresh(); var center = board.Content.TransformPoint(new Vector3(point.x, .8f, point.y));
                    preview.camera.orthographic = true; preview.camera.orthographicSize = board.CellScale * radius;
                    preview.camera.nearClipPlane = .001f; preview.camera.farClipPlane = 10;
                    preview.camera.transform.position = center + new Vector3(.08f, .45f, -.5f); preview.camera.transform.LookAt(center);
                    preview.camera.clearFlags = CameraClearFlags.SolidColor; preview.camera.backgroundColor = new Color(.025f, .04f, .06f);
                }
                void Capture(string name)
                {
                    preview.BeginStaticPreview(new Rect(0, 0, 1400, 1000)); preview.Render(true);
                    var image = preview.EndStaticPreview(); File.WriteAllBytes(output + "/" + name + ".png", image.EncodeToPNG()); Object.DestroyImmediate(image); captures++;
                }
            }
            finally
            {
                Object.DestroyImmediate(boardObject); Object.DestroyImmediate(fixture); preview.Cleanup();
                Shader.SetGlobalVector("_FrontierBounds", bounds); Shader.SetGlobalMatrix("_FrontierWorldToBoard", matrix);
            }
            WeaponPreview.Render(); HitFeedbackPreview.Render(); UnitFacingPreview.Render();
        }
    }
}
