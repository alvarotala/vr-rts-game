using System.Collections.Generic;
using Frontier.Core;
using UnityEngine;

namespace Frontier.Runtime
{
    // Organic silhouettes share the three team/structure/role materials with the
    // industrial army. Tissue and bone use vertex colors on a white material.
    public static class BiologicalVisuals
    {
        private static readonly Color Flesh = new Color(.38f, .16f, .25f), Soft = new Color(.62f, .3f, .39f);
        private static readonly Color Bone = new Color(.88f, .8f, .56f), Dark = new Color(.065f, .055f, .09f);
        private static readonly Color Acid = new Color(.59f, .86f, .25f), Shell = new Color(.7f, .76f, .67f);
        public static int LegPairs(Archetype type) => type.role == "siege" ? 4 : 3;
        public static Vector3 LegPosition(Archetype type, int pair, int side)
            => new Vector3(side * (type.role == "siege" ? .29f : .19f), .33f, (pair - (LegPairs(type) - 1) * .5f) * .24f - .07f);
        public static Quaternion LegRotation(int pair, int side) => Quaternion.Euler(0, side * (115 - pair * 28), 0);
        public static Vector3 WeaponPosition(string role) => role == "melee" ? new Vector3(.24f, .67f, .18f)
            : role == "ranged" ? new Vector3(0, .78f, .12f) : role == "siege" ? new Vector3(0, .64f, .02f) : new Vector3(0, .32f, .25f);
        public static float BadgeHeight(string role) => role == "ranged" ? 1.12f : role == "melee" ? 1.15f : role == "siege" ? 1.12f : .8f;

        public static Mesh Unit(Archetype type, bool animatedWorker, bool animatedCombat)
        {
            var b = new Builder(); string role = type.role;
            if (role == "worker")
            {
                b.Lobe(new Vector3(0, .35f, -.15f), new Vector3(.66f, .49f, .68f), 1, Flesh);
                Plates(b, new Vector3(0, .48f, -.16f), .53f, .36f, 3);
                b.Lobe(new Vector3(0, .31f, .29f), new Vector3(.39f, .31f, .29f), 0, Shell);
                Eyes(b, new Vector3(0, .43f, .39f), .11f);
                for (int side = -1; side <= 1; side += 2)
                {
                    b.Tendril(new[] { new Vector3(side * .12f, .42f, .32f), new Vector3(side * .26f, .59f, .5f), new Vector3(side * .3f, .61f, .63f) }, .026f, 1, Bone);
                    if (!animatedWorker) b.Tendril(new[] { new Vector3(side * .17f, .25f, .36f), new Vector3(side * .28f, .2f, .56f), new Vector3(side * .12f, .19f, .7f) }, .065f, 1, Bone);
                }
            }
            else if (role == "scout")
            {
                b.Lobe(new Vector3(0, .29f, -.12f), new Vector3(.31f, .3f, .76f), 1, Flesh);
                Plates(b, new Vector3(0, .38f, -.17f), .33f, .52f, 4);
                b.Lobe(new Vector3(0, .34f, .23f), new Vector3(.27f, .26f, .29f), 0, Shell);
                Eyes(b, new Vector3(0, .44f, .31f), .095f);
                for (int side = -1; side <= 1; side += 2)
                    b.Tendril(new[] { new Vector3(side * .09f, .43f, .28f), new Vector3(side * .32f, .66f, .43f), new Vector3(side * .4f, .69f, .69f) }, .027f, 1, Bone);
            }
            else if (role == "melee")
            {
                b.Lobe(new Vector3(0, .39f, -.21f), new Vector3(.66f, .54f, .65f), 1, Flesh);
                Plates(b, new Vector3(0, .62f, -.19f), .69f, .46f, 3);
                b.Lobe(new Vector3(0, .68f, .19f), new Vector3(.48f, .47f, .38f), 0, Shell);
                Eyes(b, new Vector3(0, .81f, .37f), .15f);
                b.Tendril(new[] { new Vector3(0, .8f, .2f), new Vector3(0, 1.02f, .43f), new Vector3(0, .91f, .64f) }, .09f, 1, Bone);
                // The fixed left scythe balances the articulated striking claw.
                b.Tendril(new[] { new Vector3(-.24f, .65f, .15f), new Vector3(-.54f, .7f, .43f), new Vector3(-.48f, .53f, .81f), new Vector3(-.21f, .4f, .91f) }, .105f, 1, Bone);
            }
            else if (role == "ranged")
            {
                b.Lobe(new Vector3(0, .35f, -.26f), new Vector3(.58f, .53f, .65f), 1, Flesh);
                b.Lobe(new Vector3(0, .62f, -.07f), new Vector3(.4f, .68f, .38f), 0, Shell);
                for (int side = -1; side <= 1; side += 2)
                {
                    b.Lobe(new Vector3(side * .21f, .52f, -.19f), new Vector3(.27f, .4f, .43f), 1, Acid);
                    b.Tendril(new[] { new Vector3(side * .21f, .58f, -.33f), new Vector3(side * .29f, .83f, -.35f), new Vector3(side * .35f, .98f, -.43f) }, .065f, 1, Bone);
                }
                Eyes(b, new Vector3(0, .91f, .13f), .13f);
            }
            else
            {
                b.Lobe(new Vector3(0, .5f, -.22f), new Vector3(.95f, .82f, .94f), 1, Flesh);
                for (int i = -1; i <= 1; i++)
                    b.Lobe(new Vector3(i * .24f, .65f, -.28f), new Vector3(.29f, .53f, .67f), 1, Acid);
                for (int side = -1; side <= 1; side += 2)
                {
                    b.Lobe(new Vector3(side * .4f, .51f, -.1f), new Vector3(.23f, .59f, .72f), 0, Shell);
                    for (int i = 0; i < 3; i++) b.Tendril(new[] { new Vector3(side * .38f, .65f, -.42f + i * .25f), new Vector3(side * .52f, .88f, -.51f + i * .25f), new Vector3(side * .57f, .98f, -.59f + i * .25f) }, .075f, 1, Bone);
                }
                Eyes(b, new Vector3(0, .78f, .29f), .18f);
            }
            if (!animatedWorker && !animatedCombat)
                for (int pair = 0; pair < LegPairs(type); pair++) for (int side = -1; side <= 1; side += 2)
                    Leg(b, LegPosition(type, pair, side), LegRotation(pair, side));
            if (!animatedCombat && role != "worker") Weapon(b, role, WeaponPosition(role));
            return b.Finish("Living unit / " + type.id);
        }
        public static Mesh CombatPart(Archetype type, bool leg)
        {
            var b = new Builder(); if (leg) Leg(b, Vector3.zero, Quaternion.identity); else Weapon(b, type.role, Vector3.zero);
            return b.Finish("Living limb / " + type.id, type.collisionRadius / .45f);
        }
        private static void Leg(Builder b, Vector3 origin, Quaternion rotation)
        {
            Vector3 P(float x, float y, float z) => origin + rotation * new Vector3(x, y, z);
            b.Tube(P(0, 0, 0), P(0, .06f, .28f), .065f, .045f, 0, Shell);
            b.Lobe(P(0, .06f, .28f), Vector3.one * .11f, 1, Soft);
            b.Tube(P(0, .06f, .28f), P(.025f, -.32f, .49f), .047f, .008f, 1, Bone);
        }
        private static void Weapon(Builder b, string role, Vector3 origin)
        {
            Vector3 P(float x, float y, float z) => origin + new Vector3(x, y, z);
            if (role == "melee")
            {
                b.Tendril(new[] { P(0, 0, 0), P(.17f, .04f, .27f), P(.12f, -.1f, .62f), P(-.06f, -.22f, .91f) }, .115f, 1, Bone);
                b.Tendril(new[] { P(.09f, -.06f, .3f), P(-.1f, -.15f, .59f), P(-.06f, -.22f, .76f) }, .065f, 0, Shell);
            }
            else if (role == "siege")
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    b.Tendril(new[] { P(side * .17f, -.04f, -.13f), P(side * .22f, .18f, .23f), P(side * .15f, .05f, .75f) }, .14f, 0, Shell, false);
                    b.Mouth(P(side * .15f, .05f, .79f), .12f);
                }
            }
            else
            {
                float length = role == "ranged" ? .54f : .25f;
                b.Lobe(P(0, 0, length * .35f), new Vector3(.3f, .26f, length * 1.4f), 1, Soft);
                b.Lobe(P(0, .1f, length * .2f), new Vector3(.32f, .13f, length), 0, Shell);
                b.Mouth(P(0, 0, length), role == "ranged" ? .105f : .075f);
            }
        }
        private static void Plates(Builder b, Vector3 center, float width, float length, int count)
        {
            for (int i = 0; i < count; i++)
                b.Lobe(center + new Vector3(0, Mathf.Sin((i + .5f) / count * Mathf.PI) * .035f, (i - (count - 1) * .5f) * length / count), new Vector3(width * (1 - .12f * Mathf.Abs(i - (count - 1) * .5f)), .2f, length / count * .86f), 0, Shell);
        }
        private static void Eyes(Builder b, Vector3 center, float spread)
        {
            for (int side = -1; side <= 1; side += 2) b.Lobe(center + Vector3.right * side * spread, new Vector3(.075f, .06f, .085f), 1, Acid);
        }

        public static Mesh Building(Archetype type)
        {
            var b = new Builder();
            b.Lobe(new Vector3(0, .055f, 0), new Vector3(.83f, .11f, .84f), 1, Flesh);
            for (int i = 0; i < 9; i++)
            {
                float a = i * Mathf.PI * 2 / 9; var direction = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                b.Tendril(new[] { direction * .12f + Vector3.up * .19f, direction * .3f + Vector3.up * .075f, direction * .48f + Vector3.up * .016f }, .065f, 1, Soft);
            }
            switch (type.role)
            {
                case "hq":
                    b.Lobe(new Vector3(0, .3f, .07f), new Vector3(.72f, .49f, .67f), 1, Flesh);
                    for (int i = -1; i <= 1; i++) b.Lobe(new Vector3(i * .17f, .52f, .13f), new Vector3(.22f, .42f, .45f), 1, Soft);
                    for (int side = -1; side <= 1; side += 2) for (int i = 0; i < 3; i++)
                    {
                        b.Lobe(new Vector3(side * .29f, .3f, -.17f + i * .2f), new Vector3(.23f, .37f, .21f), 0, Shell);
                        b.Tendril(new[] { new Vector3(side * .29f, .38f, -.16f + i * .2f), new Vector3(side * .35f, .63f, -.12f + i * .18f), new Vector3(side * .22f, .85f, -.05f + i * .15f) }, .06f, 1, Bone);
                    }
                    b.Lobe(new Vector3(0, .38f, -.22f), new Vector3(.27f, .3f, .14f), 1, Dark);
                    b.Lobe(new Vector3(0, .69f, .12f), new Vector3(.15f, .12f, .18f), 2, Color.white); break;
                case "barracks":
                    for (int i = -1; i <= 1; i++)
                    {
                        b.Lobe(new Vector3(i * .24f, .26f, .13f), new Vector3(.24f, .4f + (i == 0 ? .1f : 0), .37f), 1, Soft);
                        b.Lobe(new Vector3(i * .24f, .39f, .14f), new Vector3(.17f, .17f, .25f), 2, Color.white);
                        for (int side = -1; side <= 1; side += 2) b.Tendril(new[] { new Vector3(i * .24f + side * .1f, .1f, .16f), new Vector3(i * .24f + side * .12f, .32f, .16f), new Vector3(i * .24f + side * .04f, .48f, .18f) }, .027f, 0, Shell);
                    }
                    b.Lobe(new Vector3(0, .18f, -.2f), new Vector3(.5f, .26f, .23f), 0, Shell);
                    b.Lobe(new Vector3(0, .2f, -.3f), new Vector3(.32f, .18f, .07f), 1, Dark); break;
                case "extractor":
                    for (int side = -1; side <= 1; side += 2)
                    {
                        var p = new Vector3(side * .21f, .52f, .1f);
                        b.Lobe(p - Vector3.up * .2f, new Vector3(.33f, .47f, .45f), 1, Soft);
                        b.Tendril(new[] { p - Vector3.up * .23f, p + new Vector3(side * .015f, .04f, .04f), p + new Vector3(side * .05f, .22f, .02f) }, .1f, 0, Shell, false);
                        b.Lobe(p + new Vector3(side * .05f, .22f, .02f), new Vector3(.22f, .07f, .22f), 2, Color.white);
                        b.Lobe(p + new Vector3(side * .05f, .251f, .02f), new Vector3(.135f, .018f, .135f), 1, Dark);
                    } break;
                case "supply":
                    for (int i = -1; i <= 1; i++)
                    {
                        b.Lobe(new Vector3(i * .25f, .24f, .08f), new Vector3(.27f, .38f, .48f), 1, Acid);
                        b.Tendril(new[] { new Vector3(i * .25f, .12f, -.13f), new Vector3(i * .25f, .39f, .05f), new Vector3(i * .25f, .12f, .3f) }, .035f, 0, Shell, false);
                    } break;
                case "tech":
                    b.Lobe(new Vector3(0, .48f, .1f), new Vector3(.34f, .83f, .4f), 1, Soft);
                    b.Lobe(new Vector3(0, .52f, -.07f), new Vector3(.2f, .5f, .12f), 2, Color.white);
                    for (int side = -1; side <= 1; side += 2) for (int i = 0; i < 4; i++)
                        b.Tendril(new[] { new Vector3(side * .1f, .2f + i * .16f, .25f), new Vector3(side * .25f, .23f + i * .16f, .08f), new Vector3(side * .13f, .26f + i * .16f, -.09f) }, .036f, 0, Shell);
                    b.Tendril(new[] { new Vector3(0, .76f, .1f), new Vector3(.07f, .92f, .13f), new Vector3(0, 1.04f, .18f) }, .07f, 1, Bone); break;
                default:
                    b.Tendril(new[] { new Vector3(0, .12f, 0), new Vector3(0, .52f, -.08f), new Vector3(0, .91f, .04f), new Vector3(0, 1.2f, .17f) }, .16f, 0, Shell, false);
                    b.Lobe(new Vector3(0, 1.19f, .22f), new Vector3(.36f, .23f, .43f), 1, Soft);
                    b.Mouth(new Vector3(0, 1.225f, .415f), .095f);
                    for (int i = 0; i < 4; i++) b.Tendril(new[] { new Vector3(0, .45f + i * .2f, -.07f), new Vector3(0, .55f + i * .2f, -.23f), new Vector3(0, .67f + i * .2f, -.31f) }, .07f, 1, Bone); break;
            }
            return b.Finish("Living structure / " + type.id);
        }

        // Merge once while caching, keeping existing role glyphs and team identity.
        public static Mesh WithBadge(Mesh body, Mesh badge, float scale)
        {
            var vertices = new List<Vector3>(body.vertices); int offset = vertices.Count; vertices.AddRange(badge.vertices);
            for (int i = 0; i < vertices.Count; i++) vertices[i] *= scale;
            var colors = new List<Color>(body.colors); colors.AddRange(badge.colors);
            foreach (int index in badge.GetTriangles(1)) colors[offset + index] = Dark;
            var mesh = new Mesh { name = body.name, subMeshCount = 3 }; mesh.SetVertices(vertices); mesh.SetColors(colors);
            for (int sub = 0; sub < 3; sub++)
            {
                var triangles = new List<int>(body.GetTriangles(sub)); foreach (int index in badge.GetTriangles(sub)) triangles.Add(offset + index); mesh.SetTriangles(triangles, sub);
            }
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            if (Application.isPlaying) { Object.Destroy(body); Object.Destroy(badge); } else { Object.DestroyImmediate(body); Object.DestroyImmediate(badge); }
            return mesh;
        }

        private sealed class Builder
        {
            private readonly List<Vector3> vertices = new List<Vector3>();
            private readonly List<Color> colors = new List<Color>();
            private readonly List<int>[] triangles = { new List<int>(), new List<int>(), new List<int>() };
            private void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, int material, Color color)
            {
                int n = vertices.Count; vertices.AddRange(new[] { a, b, c, d }); for (int i = 0; i < 4; i++) colors.Add(color);
                triangles[material].AddRange(new[] { n, n + 1, n + 2, n, n + 2, n + 3 });
            }
            public void Lobe(Vector3 center, Vector3 size, int material, Color color)
            {
                Vector3 V(int ring, int side) { float lat = ring * Mathf.PI / 5, angle = side * Mathf.PI / 5; return center + Vector3.Scale(new Vector3(Mathf.Sin(lat) * Mathf.Cos(angle), Mathf.Cos(lat), Mathf.Sin(lat) * Mathf.Sin(angle)), size) * .5f; }
                for (int ring = 0; ring < 5; ring++) for (int side = 0; side < 10; side++) Quad(V(ring, side), V(ring, side + 1), V(ring + 1, side + 1), V(ring + 1, side), material, color);
            }
            public void Tube(Vector3 a, Vector3 b, float radiusA, float radiusB, int material, Color color)
            {
                var direction = (b - a).normalized; var right = Vector3.Cross(direction, Mathf.Abs(direction.y) > .9f ? Vector3.forward : Vector3.up).normalized;
                var up = Vector3.Cross(direction, right);
                Vector3 R(int i) => right * Mathf.Cos(i * Mathf.PI / 3) + up * Mathf.Sin(i * Mathf.PI / 3);
                for (int i = 0; i < 6; i++)
                {
                    Quad(a + R(i) * radiusA, a + R(i + 1) * radiusA, b + R(i + 1) * radiusB, b + R(i) * radiusB, material, color);
                    Quad(b, b + R(i) * radiusB, b + R(i + 1) * radiusB, b, material, color);
                }
            }
            public void Tendril(Vector3[] path, float radius, int material, Color color, bool pointed = true)
            {
                for (int i = 0; i < path.Length - 1; i++) Tube(path[i], path[i + 1], radius * (1 - .55f * i / (path.Length - 1)), pointed && i == path.Length - 2 ? .006f : radius * (1 - .55f * (i + 1) / (path.Length - 1)), material, color);
            }
            public void Mouth(Vector3 center, float radius)
            {
                Lobe(center, new Vector3(radius * 2.5f, radius * 2.3f, radius), 1, Flesh);
                Lobe(center + Vector3.forward * radius * .42f, new Vector3(radius * 1.75f, radius * 1.55f, radius * .35f), 1, Dark);
                for (int i = 0; i < 6; i++)
                {
                    float a = i * Mathf.PI / 3; var d = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0);
                    Tube(center + d * radius + Vector3.forward * radius * .35f, center + d * radius * .53f + Vector3.forward * radius * .7f, radius * .21f, .003f, 1, Bone);
                }
            }
            public Mesh Finish(string name, float scale = 1)
            {
                for (int i = 0; i < vertices.Count; i++) vertices[i] *= scale;
                var mesh = new Mesh { name = name, subMeshCount = 3 }; mesh.SetVertices(vertices); mesh.SetColors(colors);
                for (int i = 0; i < 3; i++) mesh.SetTriangles(triangles[i], i);
                mesh.RecalculateNormals(); mesh.RecalculateBounds(); return mesh;
            }
        }
    }
}
