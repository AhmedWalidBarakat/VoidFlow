using System.Collections.Generic;
using UnityEngine;

namespace VoidFlow
{
    // The Void snipers that get their own build on top of the base rifle, each one a different
    // idea (all original designs): Crescent, Leviathan, Stormcaller, Clockwork, Nebula Core,
    // Viper, Grim Harvest, Event Horizon and Glitch. Moving parts (gears, orbits, crackling
    // lightning, glitching blocks) are animated by WeaponParts.Animate.
    //
    // Rifle space: +Z along the barrel, +Y up, origin at the trigger. The bore runs at y 0.018,
    // the muzzle ends near z 1.09, the scope sits at y 0.098 from z -0.17 to 0.35.
    public partial class WeaponBuilder
    {
        const float Bore = 0.018f;

        void BuildVoidRifle(Skins.Skin skin, WeaponParts parts, Transform t, Material chassis, Material metal, List<Vector3> burn)
        {
            switch (skin.model)
            {
                case KnifeModel.Crescent: Crescent(parts, t, chassis, metal, burn); break;
                case KnifeModel.Leviathan: Leviathan(parts, t, chassis, burn); break;
                case KnifeModel.Storm: Storm(parts, t, metal, burn); break;
                case KnifeModel.Clockwork: Clockwork(parts, t, metal, burn); break;
                case KnifeModel.Orbit: Orbit(parts, t, chassis, metal, burn); break;
                case KnifeModel.Serpent: Serpent(parts, t, chassis, burn); break;
                case KnifeModel.ScytheRifle: ScytheRifle(parts, t, chassis, metal, burn); break;
                case KnifeModel.BlackHole: BlackHole(parts, t, metal, burn); break;
                case KnifeModel.Glitch: Glitch(parts, t, chassis, burn); break;
            }
        }

        // A flat arc of blade in the rifle's side plane: centered at (z, y), radius r, from angle
        // a0 to a1 (degrees), widest in the middle
        Transform Arc(Transform t, Material mat, float z, float y, float r, float a0, float a1, float width)
        {
            const int n = 20;
            var spine = new Vector2[n + 1];
            var edge = new Vector2[n + 1];
            for (int i = 0; i <= n; i++)
            {
                float s = (float)i / n, a = Mathf.Lerp(a0, a1, s) * Mathf.Deg2Rad;
                var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                float w = width * Mathf.Sin(s * Mathf.PI) + 0.001f;
                spine[i] = dir * r;
                edge[i] = dir * (r - w);
            }
            // The blade is built in XY; turning it about Y lays its X along the barrel
            return MeshPart(t, mat, RailBlade(spine, edge, 0.003f, 0.0005f), new Vector3(0f, y, z), Quaternion.Euler(0f, -90f, 0f));
        }

        Transform Pivot(Transform t, string name, Vector3 at, Quaternion rotation)
        {
            var p = new GameObject(name).transform;
            p.SetParent(t, false);
            p.SetLocalPositionAndRotation(at, rotation);
            return p;
        }

        // Crescent: blood-red moon blades, one hanging under the forend like a bayonet and a
        // pair of horns over the stock, and a crescent moon glowing at the muzzle
        void Crescent(WeaponParts parts, Transform t, Material chassis, Material metal, List<Vector3> burn)
        {
            Material moon = Glow(parts.hue, 3.2f, parts);
            Arc(t, chassis, 0.74f, 0.04f, 0.14f, 205f, 335f, 0.05f);
            Arc(t, moon, 0.74f, 0.04f, 0.146f, 215f, 325f, 0.008f);
            Arc(t, chassis, -0.28f, 0.0f, 0.075f, 30f, 150f, 0.022f);
            Part(t, PrimitiveType.Sphere, moon, new Vector3(0f, Bore, 1.13f), new Vector3(0.09f, 0.09f, 0.012f));
            Part(t, PrimitiveType.Sphere, metal, new Vector3(0.024f, Bore + 0.018f, 1.133f), new Vector3(0.08f, 0.08f, 0.016f));
            MeshPart(t, moon, Torus(0.03f, 0.003f, 28, 6), new Vector3(0f, Bore, 1.095f), Quaternion.identity);
            for (int k = 0; k < 4; k++) burn.Add(new Vector3(0f, -0.07f, 0.66f + k * 0.05f));
        }

        // Leviathan: a sea serpent. Fins along the barrel, scale plates on the forend, glowing
        // gills on the stock, and a fanged jaw at the muzzle with burning eyes
        void Leviathan(WeaponParts parts, Transform t, Material chassis, List<Vector3> burn)
        {
            Material glow = Glow(parts.hue, 2.8f, parts);
            Material bone = Mat(new Color(0.9f, 0.9f, 0.85f), 0.7f, 0.1f); keep.Add(bone);
            for (int k = 0; k < 5; k++)
            {
                float z = 0.45f + k * 0.12f, h = 0.05f - k * 0.006f;
                var spine = new[] { new Vector2(0f, 0f), new Vector2(0.02f, h), new Vector2(0.06f, 0f) };
                var edge = new[] { new Vector2(0f, 0f), new Vector2(0.02f, 0.004f), new Vector2(0.06f, 0f) };
                MeshPart(t, chassis, RailBlade(spine, edge, 0.0025f, 0.0008f), new Vector3(0f, Bore + 0.008f, z - 0.03f), Quaternion.Euler(0f, -90f, 0f));
            }
            foreach (float x in new[] { -1f, 1f })
            {
                for (int k = 0; k < 6; k++)
                    Part(t, PrimitiveType.Cube, chassis, new Vector3(x * 0.025f, -0.004f, 0.24f + k * 0.04f), new Vector3(0.004f, 0.036f, 0.045f), Quaternion.Euler(0f, x * 12f, 0f));
                for (int k = 0; k < 3; k++)
                    Part(t, PrimitiveType.Cube, glow, new Vector3(x * 0.0205f, 0.005f - k * 0.02f, -0.2f), new Vector3(0.002f, 0.004f, 0.07f), Quaternion.Euler(-10f, 0f, 0f));
                // Pectoral fins off the receiver, swept back
                var s2 = new[] { new Vector2(0f, 0f), new Vector2(0.03f, 0.03f), new Vector2(0.08f, 0.01f) };
                var e2 = new[] { new Vector2(0f, 0f), new Vector2(0.04f, 0.005f), new Vector2(0.08f, 0.01f) };
                MeshPart(t, chassis, RailBlade(s2, e2, 0.002f, 0.0006f), new Vector3(x * 0.026f, -0.01f, 0.2f), Quaternion.Euler(0f, 90f, x * 150f));
            }
            // Jaws, teeth and eyes
            Part(t, PrimitiveType.Cube, chassis, new Vector3(0f, Bore + 0.016f, 1.12f), new Vector3(0.042f, 0.012f, 0.09f), Quaternion.Euler(-14f, 0f, 0f));
            Part(t, PrimitiveType.Cube, chassis, new Vector3(0f, Bore - 0.018f, 1.11f), new Vector3(0.036f, 0.01f, 0.08f), Quaternion.Euler(14f, 0f, 0f));
            for (int k = 0; k < 4; k++)
            {
                float z = 1.09f + k * 0.022f;
                foreach (float x in new[] { -0.012f, 0.012f })
                {
                    MeshPart(t, bone, CrystalMesh(0.018f, 0.003f, 4, 0f), new Vector3(x, Bore + 0.012f, z), Quaternion.Euler(180f, 0f, 0f));
                    MeshPart(t, bone, CrystalMesh(0.014f, 0.003f, 4, 0f), new Vector3(x, Bore - 0.013f, z), Quaternion.identity);
                }
            }
            foreach (float x in new[] { -1f, 1f })
                Part(t, PrimitiveType.Sphere, glow, new Vector3(x * 0.022f, Bore + 0.028f, 1.1f), Vector3.one * 0.011f);
            for (int k = 0; k < 4; k++) burn.Add(new Vector3(0f, 0.07f, 0.45f + k * 0.12f));
        }

        // Stormcaller: three tesla coils on the barrel with lightning crackling between them, and
        // a storm orb at the muzzle throwing sparks
        void Storm(WeaponParts parts, Transform t, Material metal, List<Vector3> burn)
        {
            Material glow = Glow(parts.hue, 3.4f, parts);
            Material bolt = Glow(Color.Lerp(parts.hue, Color.white, 0.6f), 5f, parts);
            float[] zs = { 0.5f, 0.72f, 0.94f };
            foreach (float z in zs)
            {
                Rod(t, metal, new Vector3(0f, Bore, z), new Vector3(0f, 0.06f, z), 0.008f);
                for (int k = 0; k < 3; k++)
                    MeshPart(t, k == 1 ? glow : metal, Torus(0.012f - k * 0.002f, 0.003f, 20, 6), new Vector3(0f, 0.035f + k * 0.009f, z), Quaternion.Euler(90f, 0f, 0f));
                Part(t, PrimitiveType.Sphere, glow, new Vector3(0f, 0.066f, z), Vector3.one * 0.018f);
            }
            var random = new System.Random(5);
            // Two sets of bolts between each pair of coils, crackling on and off
            for (int set = 0; set < 3; set++)
            for (int pair = 0; pair < 2; pair++)
            {
                var holder = Pivot(t, "Lightning", Vector3.zero, Quaternion.identity);
                Vector3 a = new(0f, 0.066f, zs[pair]), b = new(0f, 0.066f, zs[pair + 1]);
                Vector3 last = a;
                for (int s = 1; s <= 4; s++)
                {
                    Vector3 next = Vector3.Lerp(a, b, s / 4f);
                    if (s < 4) next += new Vector3(((float)random.NextDouble() - 0.5f) * 0.03f, ((float)random.NextDouble() - 0.3f) * 0.03f, 0f);
                    Rod(holder, bolt, last, next, 0.0025f);
                    last = next;
                }
                parts.flickers.Add((holder, (float)random.NextDouble() * 10f, 9f + set * 3f));
            }
            // Storm orb at the muzzle
            Vector3 orb = new(0f, Bore, 1.13f);
            Part(t, PrimitiveType.Sphere, SeeThroughGlow(parts.hue, 0.3f, 2.5f), orb, Vector3.one * 0.06f);
            Part(t, PrimitiveType.Sphere, bolt, orb, Vector3.one * 0.022f);
            for (int k = 0; k < 4; k++)
            {
                var holder = Pivot(t, "Spark", orb, Quaternion.Euler(0f, 0f, k * 90f + 20f));
                Rod(holder, bolt, Vector3.zero, new Vector3(0.015f, 0.02f, 0.005f), 0.002f);
                Rod(holder, bolt, new Vector3(0.015f, 0.02f, 0.005f), new Vector3(0.008f, 0.038f, 0f), 0.002f);
                parts.flickers.Add((holder, k * 2.7f, 13f));
            }
            foreach (float z in zs) burn.Add(new Vector3(0f, 0.075f, z));
        }

        // Clockwork: brass gears turning on both sides of the receiver and the stock, copper pipes
        // under the barrel, a pressure gauge by the scope and a flared bell at the muzzle
        void Clockwork(WeaponParts parts, Transform t, Material metal, List<Vector3> burn)
        {
            Material brass = Mat(new Color(0.95f, 0.7f, 0.3f), 0.9f, 1f); keep.Add(brass);
            Material copper = Mat(new Color(0.85f, 0.45f, 0.25f), 0.85f, 1f); keep.Add(copper);
            Material glow = Glow(parts.hue, 2.6f, parts);
            void Gear(Vector3 at, float radius, int teeth, float rate, Material mat)
            {
                // Its axis runs across the rifle (X); it turns about that
                var g = Pivot(t, "Gear", at, Quaternion.Euler(0f, 90f, 0f));
                MeshPart(g, mat, Torus(radius, radius * 0.2f, 32, 6), Vector3.zero, Quaternion.identity);
                Part(g, PrimitiveType.Cylinder, mat, Vector3.zero, new Vector3(radius * 0.5f, 0.002f, radius * 0.5f), Quaternion.Euler(90f, 0f, 0f));
                for (int k = 0; k < teeth; k++)
                {
                    float a = k * 360f / teeth;
                    var dir = Quaternion.Euler(0f, 0f, a) * Vector3.up;
                    Part(g, PrimitiveType.Cube, mat, dir * radius * 1.18f, new Vector3(radius * 0.22f, radius * 0.3f, 0.004f), Quaternion.Euler(0f, 0f, a));
                }
                for (int k = 0; k < 3; k++)
                    Part(g, PrimitiveType.Cube, mat, Quaternion.Euler(0f, 0f, k * 120f) * Vector3.up * radius * 0.5f, new Vector3(radius * 0.15f, radius, 0.003f), Quaternion.Euler(0f, 0f, k * 120f));
                parts.spinners.Add((g, g.localRotation, new Vector3(0f, 0f, rate)));
            }
            foreach (float x in new[] { -1f, 1f })
            {
                Gear(new Vector3(x * 0.03f, 0.012f, 0.04f), 0.036f, 12, 60f * x, brass);
                Gear(new Vector3(x * 0.03f, 0.024f, 0.12f), 0.022f, 8, -100f * x, copper);
                Gear(new Vector3(x * 0.022f, -0.02f, -0.26f), 0.022f, 10, 40f * x, brass);
            }
            // Pipes with valves
            foreach (float x in new[] { -0.012f, 0.012f })
            {
                Rod(t, copper, new Vector3(x, -0.018f, 0.48f), new Vector3(x, -0.018f, 0.98f), 0.007f);
                Rod(t, copper, new Vector3(x, -0.018f, 0.98f), new Vector3(x, Bore - 0.01f, 1.02f), 0.007f);
            }
            foreach (float z in new[] { 0.6f, 0.8f })
                Part(t, PrimitiveType.Sphere, brass, new Vector3(0f, -0.018f, z), new Vector3(0.036f, 0.014f, 0.014f));
            // Gauge with a needle
            var gauge = Pivot(t, "Gauge", new Vector3(-0.04f, 0.098f, 0.12f), Quaternion.Euler(0f, 90f, 0f));
            Part(gauge, PrimitiveType.Cylinder, brass, Vector3.zero, new Vector3(0.034f, 0.004f, 0.034f), Quaternion.Euler(90f, 0f, 0f));
            Part(gauge, PrimitiveType.Cylinder, glow, new Vector3(0f, 0f, -0.0045f), new Vector3(0.027f, 0.001f, 0.027f), Quaternion.Euler(90f, 0f, 0f));
            var needle = Pivot(gauge, "Needle", new Vector3(0f, 0f, -0.0055f), Quaternion.identity);
            Part(needle, PrimitiveType.Cube, metal, new Vector3(0f, 0.006f, 0f), new Vector3(0.002f, 0.012f, 0.001f));
            parts.spinners.Add((needle, needle.localRotation, new Vector3(0f, 0f, 150f)));
            // Bell muzzle
            for (int k = 0; k < 4; k++)
                Rod(t, brass, new Vector3(0f, Bore, 1.06f + k * 0.015f), new Vector3(0f, Bore, 1.075f + k * 0.015f), 0.03f + k * 0.012f);
            MeshPart(t, glow, Torus(0.03f, 0.003f, 28, 6), new Vector3(0f, Bore, 1.12f), Quaternion.identity);
            burn.Add(new Vector3(0f, Bore + 0.03f, 1.12f));
            burn.Add(new Vector3(0.03f, 0.04f, 0.04f));
            burn.Add(new Vector3(-0.03f, 0.04f, 0.04f));
        }

        // Nebula Core: a glowing core in the middle of the barrel with three rings orbiting it
        // on different tilts, and little planets circling it
        void Orbit(WeaponParts parts, Transform t, Material chassis, Material metal, List<Vector3> burn)
        {
            Material glow = Glow(parts.hue, 3.4f, parts);
            Material star = Glow(Color.Lerp(parts.hue, Color.white, 0.7f), 5f, parts);
            Vector3 core = new(0f, Bore, 0.72f);
            Part(t, PrimitiveType.Sphere, star, core, Vector3.one * 0.03f);
            Part(t, PrimitiveType.Sphere, SeeThroughGlow(parts.hue, 0.25f, 2.5f), core, Vector3.one * 0.06f);
            foreach (float z in new[] { 0.64f, 0.8f })
                MeshPart(t, metal, Torus(0.016f, 0.005f, 20, 6), new Vector3(0f, Bore, z), Quaternion.identity);
            var tilts = new[] { new Vector3(20f, 0f, 0f), new Vector3(70f, 30f, 0f), new Vector3(-50f, -40f, 0f) };
            for (int k = 0; k < 3; k++)
            {
                var ring = Pivot(t, "Ring", core, Quaternion.Euler(tilts[k]));
                MeshPart(ring, k == 1 ? glow : chassis, Torus(0.05f + k * 0.012f, 0.0025f, 40, 6), Vector3.zero, Quaternion.identity);
                parts.spinners.Add((ring, ring.localRotation, new Vector3(0f, 0f, 70f - k * 55f)));
                var orbit = Pivot(t, "Orbit", core, Quaternion.Euler(tilts[k]));
                Part(orbit, PrimitiveType.Sphere, k == 0 ? chassis : k == 1 ? glow : metal, new Vector3(0.05f + k * 0.012f, 0f, 0f), Vector3.one * (0.012f - k * 0.002f));
                parts.spinners.Add((orbit, orbit.localRotation, new Vector3(0f, 0f, 150f + k * 60f)));
            }
            burn.Add(core + Vector3.up * 0.03f);
            for (int k = 0; k < 3; k++) burn.Add(new Vector3(0f, 0.03f, 0.35f + k * 0.12f));
        }

        // Viper: a snake coiled round the barrel, tail wound on the stock, its head at the muzzle
        // with fangs, burning eyes and a flicking forked tongue
        void Serpent(WeaponParts parts, Transform t, Material chassis, List<Vector3> burn)
        {
            Material glow = Glow(parts.hue, 3f, parts);
            Material bone = Mat(new Color(0.95f, 0.95f, 0.9f), 0.8f, 0.1f); keep.Add(bone);
            const int n = 40;
            for (int k = 0; k < n; k++)
            {
                float s = (float)k / (n - 1), z = Mathf.Lerp(0.36f, 1.04f, s), a = s * Mathf.PI * 2f * 3.5f;
                float r = 0.024f, size = Mathf.Lerp(0.016f, 0.026f, s);
                Part(t, PrimitiveType.Sphere, k % 6 == 3 ? glow : chassis, new Vector3(Mathf.Cos(a) * r, Bore + Mathf.Sin(a) * r, z), Vector3.one * size);
            }
            for (int k = 0; k < 14; k++)
            {
                float s = k / 13f, z = Mathf.Lerp(-0.34f, -0.12f, s), a = s * Mathf.PI * 3f;
                Part(t, PrimitiveType.Sphere, chassis, new Vector3(Mathf.Cos(a) * 0.024f, 0.035f + Mathf.Sin(a) * 0.012f, z), Vector3.one * Mathf.Lerp(0.006f, 0.012f, s));
            }
            Vector3 head = new(0f, Bore + 0.012f, 1.1f);
            Part(t, PrimitiveType.Sphere, chassis, head, new Vector3(0.048f, 0.03f, 0.075f));
            Part(t, PrimitiveType.Sphere, chassis, head + new Vector3(0f, -0.004f, 0.028f), new Vector3(0.036f, 0.02f, 0.05f));
            foreach (float x in new[] { -1f, 1f })
            {
                Part(t, PrimitiveType.Sphere, glow, head + new Vector3(x * 0.018f, 0.011f, 0.012f), new Vector3(0.008f, 0.006f, 0.01f));
                MeshPart(t, bone, CrystalMesh(0.022f, 0.003f, 4, 0f), head + new Vector3(x * 0.01f, -0.012f, 0.035f), Quaternion.Euler(180f, 0f, 0f));
            }
            var tongue = Pivot(t, "Tongue", head + new Vector3(0f, -0.006f, 0.052f), Quaternion.identity);
            Rod(tongue, glow, Vector3.zero, new Vector3(0f, 0f, 0.025f), 0.002f);
            foreach (float x in new[] { -1f, 1f })
                Rod(tongue, glow, new Vector3(0f, 0f, 0.025f), new Vector3(x * 0.006f, 0f, 0.035f), 0.0015f);
            parts.flickers.Add((tongue, 0f, 4f));
            for (int k = 0; k < 4; k++) burn.Add(new Vector3(0f, 0.045f, 0.45f + k * 0.17f));
        }

        // Grim Harvest: a great scythe blade curving back under the barrel from the muzzle,
        // chains hanging from the forend to the stock, and spikes down the stock
        void ScytheRifle(WeaponParts parts, Transform t, Material chassis, Material metal, List<Vector3> burn)
        {
            Material glow = Glow(parts.hue, 3f, parts);
            Arc(t, chassis, 0.84f, 0.14f, 0.23f, 275f, 205f, 0.065f);
            Arc(t, glow, 0.84f, 0.14f, 0.236f, 272f, 210f, 0.006f);
            Part(t, PrimitiveType.Cube, metal, new Vector3(0f, -0.03f, 1.02f), new Vector3(0.03f, 0.06f, 0.03f));
            // A chain sagging from the forend to the stock
            const int links = 16;
            for (int k = 0; k < links; k++)
            {
                float s = (float)k / (links - 1);
                float z = Mathf.Lerp(0.42f, -0.18f, s), y = -0.03f - Mathf.Sin(s * Mathf.PI) * 0.06f;
                MeshPart(t, metal, Torus(0.007f, 0.0018f, 12, 4), new Vector3(0.026f, y, z), Quaternion.Euler(0f, k % 2 == 0 ? 90f : 0f, 0f));
            }
            for (int k = 0; k < 5; k++)
                MeshPart(t, chassis, CrystalMesh(0.035f, 0.006f, 4, 0.4f), new Vector3(0f, -0.09f + k * 0.004f, -0.34f + k * 0.05f), Quaternion.Euler(150f, 0f, 0f));
            for (int k = 0; k < 4; k++) burn.Add(new Vector3(0f, -0.08f - k * 0.02f, 0.9f - k * 0.05f));
        }

        // Event Horizon: a black hole at the muzzle, its accretion disk spinning round it,
        // debris spiralling in along the barrel, rails converging on it
        void BlackHole(WeaponParts parts, Transform t, Material metal, List<Vector3> burn)
        {
            Material glow = Glow(parts.hue, 3.5f, parts);
            Material hot = Glow(Color.Lerp(parts.hue, Color.white, 0.6f), 5f, parts);
            Material voidBlack = Mat(new Color(0.005f, 0.005f, 0.008f), 1f, 0f); keep.Add(voidBlack);
            Vector3 hole = new(0f, Bore, 1.15f);
            Part(t, PrimitiveType.Sphere, voidBlack, hole, Vector3.one * 0.05f);
            Part(t, PrimitiveType.Sphere, SeeThroughGlow(parts.hue, 0.18f, 2f), hole, Vector3.one * 0.085f);
            var disk = Pivot(t, "Accretion Disk", hole, Quaternion.Euler(70f, 0f, 15f));
            for (int k = 0; k < 3; k++)
                MeshPart(disk, k == 0 ? hot : glow, Torus(0.038f + k * 0.012f, 0.004f - k * 0.0008f, 40, 4), Vector3.zero, Quaternion.identity);
            parts.spinners.Add((disk, disk.localRotation, new Vector3(0f, 0f, 160f)));
            for (int k = 0; k < 4; k++)
            {
                var dir = Quaternion.Euler(0f, 0f, 45f + k * 90f) * Vector3.up;
                Rod(t, metal, new Vector3(0f, Bore, 0.92f) + dir * 0.03f, hole + dir * 0.03f + Vector3.back * 0.02f, 0.005f);
            }
            for (int k = 0; k < 6; k++)
            {
                var spiral = Pivot(t, "Debris", new Vector3(0f, Bore, 0.7f + k * 0.06f), Quaternion.identity);
                Part(spiral, PrimitiveType.Cube, k % 2 == 0 ? glow : hot, new Vector3(0.03f - k * 0.003f, 0f, 0f), Vector3.one * 0.006f, Quaternion.Euler(45f, 45f, 0f));
                parts.spinners.Add((spiral, Quaternion.Euler(0f, 0f, k * 60f), new Vector3(0f, 0f, 120f + k * 40f)));
            }
            burn.Add(hole + Vector3.up * 0.045f);
            for (int k = 0; k < 3; k++) burn.Add(new Vector3(0f, 0.03f, 0.4f + k * 0.15f));
        }

        // Glitch: the rifle looks corrupted. Blocks of it slip out of place and snap back,
        // pixels float around it and a hologram panel flickers over the scope
        void Glitch(WeaponParts parts, Transform t, Material chassis, List<Vector3> burn)
        {
            Material glow = Glow(parts.hue, 3.2f, parts);
            Material cyan = Glow(new Color(0.2f, 1f, 0.9f), 3f, parts);
            var random = new System.Random(13);
            float R() => (float)random.NextDouble();
            // Slipping blocks along the barrel and body
            for (int k = 0; k < 10; k++)
            {
                var at = new Vector3((R() - 0.5f) * 0.04f, Bore + (R() - 0.5f) * 0.05f, -0.3f + R() * 1.3f);
                var block = Part(t, PrimitiveType.Cube, k % 3 == 0 ? cyan : k % 3 == 1 ? glow : chassis, at, new Vector3(0.012f + R() * 0.03f, 0.006f + R() * 0.014f, 0.02f + R() * 0.06f));
                parts.jitters.Add((block, at, 0.02f));
            }
            // Pixels floating round it
            for (int k = 0; k < 16; k++)
            {
                var at = new Vector3((R() - 0.5f) * 0.14f, Bore + (R() - 0.3f) * 0.14f, -0.2f + R() * 1.2f);
                var pixel = Part(t, PrimitiveType.Cube, k % 2 == 0 ? glow : cyan, at, Vector3.one * (0.005f + R() * 0.006f));
                parts.jitters.Add((pixel, at, 0.03f));
            }
            // Hologram panel over the scope: a see-through screen with scanlines
            var holo = Pivot(t, "Hologram", new Vector3(0f, 0.15f, 0.08f), Quaternion.identity);
            Part(holo, PrimitiveType.Cube, SeeThroughGlow(parts.hue, 0.22f, 2f), Vector3.zero, new Vector3(0.07f, 0.04f, 0.001f));
            for (int k = 0; k < 4; k++)
                Part(holo, PrimitiveType.Cube, cyan, new Vector3(0f, -0.015f + k * 0.01f, -0.001f), new Vector3(0.06f * (0.5f + R() * 0.5f), 0.0015f, 0.001f));
            parts.flickers.Add((holo, 3f, 7f));
            for (int k = 0; k < 4; k++) burn.Add(new Vector3(0f, 0.035f, 0.3f + k * 0.2f));
        }
    }
}
