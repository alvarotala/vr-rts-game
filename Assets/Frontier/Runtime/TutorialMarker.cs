using System;
using System.Linq;
using Frontier.Core;
using UnityEngine;

namespace Frontier.Runtime
{
    // Non-interactive teaching annotation, expressed in board coordinates. It
    // never adds a collider or changes the player's selection/visibility.
    public sealed class TutorialMarker : IDisposable
    {
        private readonly GameObject root;
        private readonly LineRenderer ring, arrow, footprint;
        private readonly Material material;
        private bool cueProvided, cueFoundation;
        private Point? cuePoint;
        private float cueRadius = .8f;
        public TutorialMarker(Transform parent)
        {
            root = new GameObject("Tutorial objective marker"); root.transform.SetParent(parent, false);
            material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            material.color = new Color(.64f, .36f, 1f);
            ring = Line("Objective ring", true, .12f);
            arrow = Line("Objective arrow", false, .14f);
            footprint = Line("Required foundation", true, .11f);
            root.SetActive(false);
        }
        private LineRenderer Line(string name, bool loop, float width)
        {
            var go = new GameObject(name); go.transform.SetParent(root.transform, false);
            var line = go.AddComponent<LineRenderer>();
            line.sharedMaterial = material; line.useWorldSpace = false; line.loop = loop;
            line.widthMultiplier = width; line.numCornerVertices = 3; line.numCapVertices = 3;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false; return line;
        }
        public void SetCue(Point? point, bool foundation, float radius = .8f)
        {
            cueProvided = true; cuePoint = point; cueFoundation = foundation; cueRadius = radius;
        }
        public void Update(BoardView board)
        {
            var tutorial = board.Session.Tutorial;
            var point = cueProvided ? cuePoint : tutorial?.FocusPoint;
            bool visible = tutorial != null && !tutorial.Complete && point.HasValue && !board.Session.HasSystemPause;
            root.SetActive(visible);
            if (!visible) return;
            // LineRenderer widths are world-space even with local positions.
            // Match the board's current size/zoom instead of covering the units.
            ring.widthMultiplier = board.CellScale * .12f;
            arrow.widthMultiplier = board.CellScale * .14f;
            footprint.widthMultiplier = board.CellScale * .11f;
            Point focus = point.Value;
            float radius = cueProvided ? cueRadius : .8f;
            bool buildStep = tutorial.Step == TutorialStep.BuildSupply || tutorial.Step == TutorialStep.BuildBarracks || tutorial.Step == TutorialStep.BuildExtractor;
            bool build = buildStep && tutorial.AcceptedOrderCount == 0 && (!cueProvided || cueFoundation);
            var entity = board.Session.View?.entities.FirstOrDefault(e => e.id == tutorial.FocusEntity && !e.remembered);
            if (!cueProvided && !build && tutorial.FocusResource == 0 && entity != null && board.Session.Catalog[entity.definition].building)
                radius = board.Session.Catalog[entity.definition].footprint * .65f;
            float height = board.HeightAt(focus) + .2f;
            const int segments = 48;
            ring.positionCount = segments;
            for (int i = 0; i < segments; i++)
            {
                float angle = i * Mathf.PI * 2 / segments;
                ring.SetPosition(i, new Vector3(focus.x + Mathf.Cos(angle) * radius, height, focus.y + Mathf.Sin(angle) * radius));
            }
            float lift = .15f * Mathf.Sin(Time.unscaledTime * 3);
            arrow.positionCount = 5;
            arrow.SetPositions(new[] {
                new Vector3(focus.x, height + 3.1f + lift, focus.y),
                new Vector3(focus.x, height + 1.1f + lift, focus.y),
                new Vector3(focus.x - .55f, height + 1.8f + lift, focus.y),
                new Vector3(focus.x, height + 1.1f + lift, focus.y),
                new Vector3(focus.x + .55f, height + 1.8f + lift, focus.y) });
            footprint.gameObject.SetActive(build);
            if (build)
            {
                int size = board.Session.Catalog[tutorial.ExpectedDefinition].footprint;
                // Foundation geometry always uses the authored placement point,
                // independently of a cue following a worker or another object.
                Point placement = tutorial.FocusPoint.Value;
                var origin = tutorial.Step == TutorialStep.BuildExtractor
                    ? new Point(Mathf.Floor(placement.x - size * .5f), Mathf.Floor(placement.y - size * .5f))
                    : new Point(Mathf.Floor(placement.x), Mathf.Floor(placement.y));
                footprint.positionCount = 4;
                footprint.SetPositions(new[] {
                    new Vector3(origin.x, height, origin.y), new Vector3(origin.x + size, height, origin.y),
                    new Vector3(origin.x + size, height, origin.y + size), new Vector3(origin.x, height, origin.y + size) });
            }
        }
        public void Dispose()
        {
            if (Application.isPlaying) { UnityEngine.Object.Destroy(root); UnityEngine.Object.Destroy(material); }
            else { UnityEngine.Object.DestroyImmediate(root); UnityEngine.Object.DestroyImmediate(material); }
        }
    }
}
