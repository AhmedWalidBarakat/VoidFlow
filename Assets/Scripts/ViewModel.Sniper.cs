using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace VoidFlow
{
    // The sniper: a heavy bolt-action rifle with a big scope, held in fingerless gloves. It
    // plays by the AWP's rules from CS: a 1.46 s bolt cycle between shots, a 5 round magazine
    // with a 3.67 s reload, hold right click for a single 30 degree scope, firing kicks you out of
    // the scope (press again to scope back in), and you move at 200 (100 scoped).
    // Unlike CS every shot goes exactly where the center of the screen points: no spread,
    // no movement or unscoped inaccuracy, no drop and no falloff.
    public partial class ViewModel
    {
        static readonly Vector3 SniperRest = new(0.25f, -0.1f, 0.47f);
        static readonly Quaternion SniperRestRotation = Quaternion.Euler(-3f, -15f, -4f);

        const int MagSize = 5;
        const float CycleTime = 1.463f, ReloadTime = 3.67f, Range = 5000f;
        static readonly float[] ZoomFov = { 0f, 30f }; // horizontal field of view when scoped (one zoom level)

        Transform gun, bolt, magazine, rightFist, flash;
        Vector3 boltRest, magRest, fistRest;
        Quaternion fistRestRotation;
        Material holeMat, sparkMat, tracerMat;
        Texture2D scopeTexture;

        int mag = MagSize, zoom;
        bool waitForRelease;
        float boltTime = -1f, reloadTime = -1f, sniperInspect = -1f, flashTime = 99f;
        float baseFov, baseSens;

        class Effect { public Transform t; public Vector3 velocity, scale; public float life, age; }
        readonly List<Effect> effects = new();
        readonly Queue<GameObject> holes = new();
        LineRenderer tracer;
        float tracerTime = 99f;
        bool tracerBeam;

        string SniperStatus() => "∞   ·   RMB scope"; // unlimited ammo, never reloads

        void SetupSniper()
        {
            baseSens = player ? player.sensitivity : 0f;
            ApplyFov();

            // Scope overlay: just a thin dark ring, fully see-through inside and out, so the
            // world stays exactly as bright as it is unscoped
            const int size = 512;
            scopeTexture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float r = new Vector2(x + 0.5f - size / 2f, y + 0.5f - size / 2f).magnitude / (size / 2f);
                float alpha = Mathf.Clamp01(1f - Mathf.Abs(r - 0.96f) / 0.008f) * 0.85f;
                scopeTexture.SetPixel(x, y, new Color(0f, 0f, 0f, alpha));
            }
            scopeTexture.Apply();
        }

        void RestoreView()
        {
            if (view && baseFov > 0f) view.fieldOfView = baseFov;
            if (player && baseSens > 0f) player.sensitivity = baseSens;
        }

        void SetZoom(int level, bool sound = true)
        {
            if (level == zoom || baseFov <= 0f) return;
            zoom = level;
            ApplyFov();
            overlay.cullingMask = level > 0 ? 0 : 1 << Layer;
            if (sound) Play(WeaponSounds.Zoom, 0.5f);
        }

        // The world camera's view: the chosen horizontal FOV, or the scope's when zoomed. The
        // mouse slows by the same amount the view shrinks (the tangent of half the angle), so
        // aiming feels the same at every zoom.
        float shownFov;

        void ApplyFov()
        {
            if (!view) return;
            float aspect = view.aspect > 0f ? view.aspect : 16f / 9f;
            baseFov = Camera.HorizontalToVerticalFieldOfView(horizontalFov, aspect);
            // The scope zooms in over about a tenth of a second instead of snapping (quick
            // enough to stay responsive); the mouse slows along with the view as it goes
            float target = zoom > 0 ? ZoomFov[zoom] : horizontalFov;
            if (shownFov <= 0f || !Application.isPlaying) shownFov = target;
            else shownFov = Mathf.Lerp(shownFov, target, 1f - Mathf.Exp(-30f * Time.unscaledDeltaTime));
            if (Mathf.Abs(shownFov - target) < 0.05f) shownFov = target;
            float h = shownFov;
            view.fieldOfView = Camera.HorizontalToVerticalFieldOfView(h, aspect);
            if (player && baseSens > 0f)
                player.sensitivity = baseSens * Mathf.Tan(h * 0.5f * Mathf.Deg2Rad) / Mathf.Tan(horizontalFov * 0.5f * Mathf.Deg2Rad);
        }

        void DrawScope()
        {
            if (zoom == 0 || !scopeTexture) return;
            // Thin crosshair lines across the ring, with a small gap in the middle
            float s = Screen.height, x0 = (Screen.width - s) * 0.5f, cx = Screen.width * 0.5f, cy = s * 0.5f, gap = 6f;
            var old = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.85f);
            GUI.DrawTexture(new Rect(x0, cy - 0.5f, s * 0.5f - gap, 1f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx + gap, cy - 0.5f, s * 0.5f - gap, 1f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx - 0.5f, 0f, 1f, cy - gap), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx - 0.5f, cy + gap, 1f, cy - gap), Texture2D.whiteTexture);
            GUI.color = old;
            GUI.DrawTexture(new Rect(x0, 0f, s, s), scopeTexture);
        }

        void UpdateSniper(Keyboard kb, Mouse mouse, bool locked, bool ready, float dt)
        {
            float before;
            if (reloadTime >= 0f)
            {
                before = reloadTime;
                reloadTime += dt;
                if (Crossed(before, reloadTime, 0.62f)) Play(WeaponSounds.MagOut);
                if (Crossed(before, reloadTime, 2.05f)) Play(WeaponSounds.MagIn);
                if (Crossed(before, reloadTime, 2.57f)) Play(WeaponSounds.BoltUp);
                if (Crossed(before, reloadTime, 2.68f)) Play(WeaponSounds.BoltBack);
                if (Crossed(before, reloadTime, 2.95f)) Play(WeaponSounds.BoltForward);
                if (reloadTime >= ReloadTime) { reloadTime = -1f; mag = MagSize; }
            }
            if (boltTime >= 0f)
            {
                before = boltTime;
                boltTime += dt;
                if (Crossed(before, boltTime, 0.42f)) Play(WeaponSounds.BoltUp);
                if (Crossed(before, boltTime, 0.56f)) { Play(WeaponSounds.BoltBack); EjectShell(); }
                if (Crossed(before, boltTime, 0.8f)) Play(WeaponSounds.BoltForward);
                if (boltTime >= CycleTime)
                {
                    boltTime = -1f;
                    if (mag == 0) StartReload();
                }
            }

            bool idle = boltTime < 0f && reloadTime < 0f;
            if ((!locked || mouse == null) && zoom > 0) SetZoom(0);
            if (locked && ready && mouse != null)
            {
                // Scoped only while right click is held. Firing kicks you out; hold it again
                // (release and press) to scope back in.
                if (!mouse.rightButton.isPressed) waitForRelease = false;
                bool wantScope = mouse.rightButton.isPressed && !waitForRelease && reloadTime < 0f;
                if (wantScope && zoom == 0) sniperInspect = -1f;
                SetZoom(wantScope ? 1 : 0);
                if (mouse.leftButton.wasPressedThisFrame && idle)
                {
                    if (mag > 0) Fire();
                    else { Play(WeaponSounds.Dry); StartReload(); }
                }
            }
            if (kb != null && kb.fKey.wasPressedThisFrame && idle && zoom == 0 && sniperInspect < 0f) sniperInspect = 0f;
            if (sniperInspect >= 0f)
            {
                sniperInspect += dt;
                if (sniperInspect > SniperInspectKeys[^1].t) sniperInspect = -1f;
            }
            flashTime += dt;
            PoseSniper();
            rifle.Animate(Time.time, -1f);
        }

        static bool Crossed(float before, float now, float at) => before < at && now >= at;

        void StartReload()
        {
            SetZoom(0, false);
            sniperInspect = -1f;
            reloadTime = 0f;
        }

        void Fire()
        {
            SetZoom(0, false); // firing always kicks you out of the scope
            waitForRelease = true;
            boltTime = 0f;
            sniperInspect = -1f;
            flashTime = 0f;
            lastShot = Time.time;
            inertiaVelocity += new Vector3(0f, 0.06f, -0.55f); // the rifle shoves back into the shoulder
            flash.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 90f));
            Play(sniperShotClip ? sniperShotClip : WeaponSounds.SniperShot);

            // Perfectly accurate: straight out of the center of the screen. (Sync first so
            // things moved by script this frame, like skeet discs, are where they look.)
            Physics.SyncTransforms();
            var ray = new Ray(view.transform.position, view.transform.forward);
            Vector3 end = ray.GetPoint(Range);
            var mask = player ? player.collisionMask : (LayerMask)~0;
            RaycastHit best = default;
            float bestDistance = float.MaxValue;
            foreach (var hit in Physics.RaycastAll(ray, Range, mask, QueryTriggerInteraction.Ignore))
            {
                if (player && hit.collider.transform.IsChildOf(player.transform)) continue;
                if (hit.distance < bestDistance) { best = hit; bestDistance = hit.distance; }
            }
            // Skeet discs and Void Beasts are triggers: break the nearest one in front of whatever
            // else was hit
            ISnipeTarget disc = null;
            float discDistance = bestDistance;
            foreach (var hit in Physics.RaycastAll(ray, Range, ~0, QueryTriggerInteraction.Collide))
                if (hit.distance < discDistance && hit.collider.TryGetComponent(out ISnipeTarget target)) { disc = target; discDistance = hit.distance; }
            if (disc != null)
            {
                end = ray.GetPoint(discDistance);
                disc.Break();
                hitMarkerTime = Time.time;
                hitMarkerColor = disc is VoidBeast ? new Color(1f, 0.35f, 0.35f) : Color.white;
                Play(WeaponSounds.HitTick, 0.7f);
            }
            else if (bestDistance < float.MaxValue)
            {
                end = best.point;
                Impact(best);
            }

            // Tracer from roughly the muzzle to the hit
            Transform v = view.transform;
            Vector3 muzzle = v.position + v.right * 0.1f - v.up * 0.07f + v.forward * 0.8f;
            ShowTracer(muzzle, end);
            // Void rifles fire their own kind of round: a railgun beam or a trail of fire
            var skin = Skins.Snipers[sniperSkin];
            var style = skin.model is KnifeModel.Railgun or KnifeModel.Lance ? FxLibrary.ShotStyle.Beam
                : skin.model == KnifeModel.Hellfire ? FxLibrary.ShotStyle.Fire
                : skin.rarity == SkinRarity.Void ? FxLibrary.ShotStyle.Glow : FxLibrary.ShotStyle.Normal;
            Color tint = rifle != null ? rifle.hue : Color.white;
            if (tracerMat)
            {
                Color tc = style == FxLibrary.ShotStyle.Normal ? new Color(1f, 0.85f, 0.55f) : tint;
                tracerMat.SetColor("_BaseColor", tc);
                tracerMat.SetColor("_EmissionColor", tc * (style == FxLibrary.ShotStyle.Beam ? 6f : 3f));
            }
            tracerBeam = style == FxLibrary.ShotStyle.Beam;
            FxLibrary.BulletTrail(muzzle, end, disc != null || bestDistance < float.MaxValue, style, tint);
            FxLibrary.MuzzleSmoke(v.position + v.right * 0.12f - v.up * 0.08f + v.forward * 1.1f, v.forward);
        }

        EndlessCourse courseRef;

        void Impact(RaycastHit hit)
        {
            // What it hit answers back: metal rings, stone cracks, wood knocks
            if (!courseRef) courseRef = FindAnyObjectByType<EndlessCourse>();
            AudioSource.PlayClipAtPoint(WeaponSounds.Impact(PlayerFeedback.SurfaceOf(hit.collider, courseRef)), hit.point, 1f);
            if (!holeMat)
            {
                // A scorch mark sprite if the effects are around, otherwise a plain dark square
                var scorch = fx && fx.scorch ? fx.scorch.GetTexture("_BaseMap") : null;
                holeMat = scorch ? MakeTransparent(new Material(fadeTemplate ? fadeTemplate : template)) : Make(new Color(0.03f, 0.03f, 0.03f), 0.05f, 0f);
                if (scorch)
                {
                    holeMat.SetTexture("_BaseMap", scorch);
                    holeMat.SetColor("_BaseColor", new Color(0.04f, 0.03f, 0.03f, 0.95f));
                    materials.Add(holeMat);
                }
                sparkMat = MakeGlow(new Color(1f, 0.7f, 0.3f), 4f);
            }
            // Bullet hole, stuck to whatever was hit so it moves with it
            var hole = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Destroy(hole.GetComponent<Collider>());
            hole.GetComponent<MeshRenderer>().sharedMaterial = holeMat;
            hole.transform.SetPositionAndRotation(hit.point + hit.normal * 0.01f,
                Quaternion.LookRotation(-hit.normal) * Quaternion.Euler(0f, 0f, Random.Range(0f, 360f)));
            hole.transform.localScale = Vector3.one * 0.3f;
            hole.transform.SetParent(hit.collider.transform, true);
            holes.Enqueue(hole);
            while (holes.Count > 30) Destroy(holes.Dequeue());

            // Sparks, a flash and smoke
            if (FxLibrary.Instance) { FxLibrary.Impact(hit.point, hit.normal); return; }
            for (int i = 0; i < 8; i++)
            {
                var spark = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Destroy(spark.GetComponent<Collider>());
                spark.GetComponent<MeshRenderer>().sharedMaterial = sparkMat;
                spark.transform.position = hit.point;
                Vector3 dir = (hit.normal + Random.insideUnitSphere * 0.9f).normalized;
                effects.Add(new Effect { t = spark.transform, velocity = dir * Random.Range(4f, 9f), scale = Vector3.one * 0.05f, life = Random.Range(0.2f, 0.4f) });
            }
        }

        class Brass { public Transform t; public Vector3 velocity, spin; public float age; public bool tinked; }
        readonly List<Brass> brass = new();
        readonly Stack<Transform> brassPool = new();
        Material brassMat;

        // A spent casing flips out of the ejection port as the bolt comes back, tumbles out
        // of view and, if you're on the ground, tinks as it lands. Casings are reused.
        void EjectShell()
        {
            if (rifle == null || !rifle.bolt) return;
            if (!brassMat) brassMat = Make(new Color(0.9f, 0.66f, 0.28f), 0.85f, 1f);
            Transform t;
            if (brassPool.Count > 0)
            {
                t = brassPool.Pop();
                t.gameObject.SetActive(true);
            }
            else
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                go.name = "Brass";
                Destroy(go.GetComponent<Collider>());
                go.layer = Layer;
                var r = go.GetComponent<MeshRenderer>();
                r.sharedMaterial = brassMat;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                t = go.transform;
                t.SetParent(transform, false);
                t.localScale = new Vector3(0.013f, 0.02f, 0.013f);
            }
            t.position = rifle.bolt.position;
            t.localRotation = Quaternion.Euler(0f, 0f, 90f);
            brass.Add(new Brass
            {
                t = t,
                velocity = new Vector3(Random.Range(0.5f, 0.8f), Random.Range(1.5f, 1.9f), Random.Range(-0.25f, 0f)),
                spin = new Vector3(Random.Range(-900f, 900f), Random.Range(-300f, 300f), Random.Range(600f, 1200f)),
            });
        }

        void UpdateBrass(float dt)
        {
            for (int i = brass.Count - 1; i >= 0; i--)
            {
                var b = brass[i];
                b.age += dt;
                b.velocity.y -= 7f * dt;
                b.t.localPosition += b.velocity * dt;
                b.t.localRotation *= Quaternion.Euler(b.spin * dt);
                if (!b.tinked && b.age > 0.45f)
                {
                    b.tinked = true;
                    if (player && player.Grounded) Play(WeaponSounds.Shell, 0.25f);
                }
                if (b.age > 0.8f)
                {
                    b.t.gameObject.SetActive(false);
                    brassPool.Push(b.t);
                    brass.RemoveAt(i);
                }
            }
        }

        void ShowTracer(Vector3 from, Vector3 to)
        {
            if (!tracer)
            {
                tracerMat = MakeGlow(new Color(1f, 0.85f, 0.55f), 3f);
                tracer = new GameObject("Tracer").AddComponent<LineRenderer>();
                tracer.sharedMaterial = tracerMat;
                tracer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                tracer.receiveShadows = false;
                tracer.positionCount = 2;
                tracer.widthCurve = new AnimationCurve(new Keyframe(0f, 0.012f), new Keyframe(1f, 0.03f));
            }
            tracer.SetPosition(0, from);
            tracer.SetPosition(1, to);
            tracer.enabled = true;
            tracer.widthMultiplier = 1f;
            tracerTime = 0f;
        }

        void UpdateEffects(float dt)
        {
            for (int i = effects.Count - 1; i >= 0; i--)
            {
                var e = effects[i];
                e.age += dt;
                if (e.age >= e.life || !e.t)
                {
                    if (e.t) Destroy(e.t.gameObject);
                    effects.RemoveAt(i);
                    continue;
                }
                e.velocity += Physics.gravity * dt;
                e.t.position += e.velocity * dt;
                e.t.localScale = e.scale * (1f - e.age / e.life);
            }
            tracerTime += dt;
            // The tracer burns bright, then thins away
            if (tracer)
            {
                tracer.enabled = tracerTime < 0.3f;
                tracer.widthMultiplier = Mathf.Lerp(tracerBeam ? 3.5f : 1.6f, 0f, Mathf.Clamp01(tracerTime / (tracerBeam ? 0.45f : 0.3f)));
                tracer.enabled = tracerTime < (tracerBeam ? 0.45f : 0.3f);
            }
            if (flash) flash.gameObject.SetActive(flashTime < 0.05f);
            UpdateBrass(dt);
        }

        // Keyframes for the whole rifle (offset, rotation), the bolt (slide back in pos.z,
        // lift in rot.z), the magazine (offset) and how far the right hand has moved from the
        // grip to the bolt (pos.x, 0..1)
        static readonly (float t, Vector3 pos, Vector3 rot)[] FireKeys =
        {
            (0f, Vector3.zero, Vector3.zero),
            (0.04f, new Vector3(0f, 0.015f, -0.075f), new Vector3(-13f, 0f, 2.5f)), // a heavy rifle kicks hard
            (0.2f, new Vector3(0f, 0.004f, -0.015f), new Vector3(-2f, 0f, 0f)),
            (0.35f, new Vector3(-0.01f, 0.01f, -0.02f), new Vector3(0f, 6f, 14f)),
            (1f, new Vector3(-0.01f, 0.01f, -0.02f), new Vector3(2f, 6f, 14f)),
            (1.3f, Vector3.zero, Vector3.zero),
        };

        static readonly (float t, Vector3 pos, Vector3 rot)[] FireBoltKeys =
        {
            (0.4f, Vector3.zero, Vector3.zero),
            (0.5f, Vector3.zero, new Vector3(0f, 0f, 65f)),
            (0.62f, new Vector3(0f, 0f, -0.07f), new Vector3(0f, 0f, 65f)),
            (0.75f, new Vector3(0f, 0f, -0.07f), new Vector3(0f, 0f, 65f)),
            (0.86f, Vector3.zero, new Vector3(0f, 0f, 65f)),
            (0.96f, Vector3.zero, Vector3.zero),
        };

        static readonly (float t, Vector3 pos, Vector3 rot)[] FireHandKeys =
        {
            (0.28f, Vector3.zero, Vector3.zero),
            (0.4f, Vector3.right, Vector3.zero),
            (0.96f, Vector3.right, Vector3.zero),
            (1.15f, Vector3.zero, Vector3.zero),
        };

        static readonly (float t, Vector3 pos, Vector3 rot)[] ReloadKeys =
        {
            (0f, Vector3.zero, Vector3.zero),
            (0.4f, new Vector3(-0.02f, 0.02f, -0.01f), new Vector3(-5f, 0f, -22f)),
            (2.3f, new Vector3(-0.02f, 0.02f, -0.01f), new Vector3(-5f, 0f, -22f)),
            (2.5f, new Vector3(-0.01f, 0.01f, -0.02f), new Vector3(0f, 6f, 14f)),
            (3.2f, new Vector3(-0.01f, 0.01f, -0.02f), new Vector3(0f, 6f, 14f)),
            (3.6f, Vector3.zero, Vector3.zero),
        };

        static readonly (float t, Vector3 pos, Vector3 rot)[] ReloadMagKeys =
        {
            (0.6f, Vector3.zero, Vector3.zero),
            (0.9f, new Vector3(0f, -0.25f, 0f), Vector3.zero),
            (1.5f, new Vector3(0f, -0.25f, 0f), Vector3.zero),
            (1.95f, new Vector3(0f, -0.012f, 0f), Vector3.zero),
            (2.1f, Vector3.zero, Vector3.zero),
        };

        static readonly (float t, Vector3 pos, Vector3 rot)[] ReloadBoltKeys =
        {
            (2.52f, Vector3.zero, Vector3.zero),
            (2.6f, Vector3.zero, new Vector3(0f, 0f, 65f)),
            (2.7f, new Vector3(0f, 0f, -0.07f), new Vector3(0f, 0f, 65f)),
            (2.85f, new Vector3(0f, 0f, -0.07f), new Vector3(0f, 0f, 65f)),
            (2.95f, Vector3.zero, new Vector3(0f, 0f, 65f)),
            (3.04f, Vector3.zero, Vector3.zero),
        };

        static readonly (float t, Vector3 pos, Vector3 rot)[] ReloadHandKeys =
        {
            (2.4f, Vector3.zero, Vector3.zero),
            (2.52f, Vector3.right, Vector3.zero),
            (3.04f, Vector3.right, Vector3.zero),
            (3.25f, Vector3.zero, Vector3.zero),
        };

        static readonly (float t, Vector3 pos, Vector3 rot)[] SniperInspectKeys =
        {
            (0f, Vector3.zero, Vector3.zero),
            (0.5f, new Vector3(-0.03f, 0.03f, 0.02f), new Vector3(0f, 15f, -35f)),
            (1.4f, new Vector3(-0.03f, 0.035f, 0.02f), new Vector3(-8f, 18f, -38f)),
            (1.9f, new Vector3(-0.02f, 0.05f, 0.05f), new Vector3(-18f, -10f, 20f)),
            (2.7f, new Vector3(-0.02f, 0.05f, 0.05f), new Vector3(-20f, -12f, 22f)),
            (3.2f, Vector3.zero, Vector3.zero),
        };

        void PoseSniper()
        {
            var (pos, rot) = (Vector3.zero, Vector3.zero);
            var (slide, lift) = (Vector3.zero, Vector3.zero);
            Vector3 magOffset = Vector3.zero;
            float toBolt = 0f;
            if (boltTime >= 0f)
            {
                (pos, rot) = Sample(FireKeys, boltTime);
                (slide, lift) = Sample(FireBoltKeys, boltTime);
                toBolt = Sample(FireHandKeys, boltTime).Item1.x;
            }
            else if (reloadTime >= 0f)
            {
                (pos, rot) = Sample(ReloadKeys, reloadTime);
                (slide, lift) = Sample(ReloadBoltKeys, reloadTime);
                magOffset = Sample(ReloadMagKeys, reloadTime).Item1;
                toBolt = Sample(ReloadHandKeys, reloadTime).Item1.x;
            }
            else if (sniperInspect >= 0f) (pos, rot) = Sample(SniperInspectKeys, sniperInspect);

            gun.SetLocalPositionAndRotation(pos, Quaternion.Euler(rot));
            bolt.SetLocalPositionAndRotation(boltRest + slide, Quaternion.Euler(lift));
            magazine.localPosition = magRest + magOffset;

            // The right hand leaves the grip to work the bolt
            Vector3 knob = bolt.localPosition + bolt.localRotation * new Vector3(0.05f, -0.014f, 0f);
            Quaternion atBolt = fistRestRotation * Quaternion.Euler(0f, 0f, 25f);
            Vector3 onKnob = knob - atBolt * GripFront;
            rightFist.SetLocalPositionAndRotation(Vector3.Lerp(fistRest, onKnob, toBolt), Quaternion.Slerp(fistRestRotation, atBolt, toBolt));
        }

        // Shows a moment of the bolt cycle (seconds after the shot) in edit mode, for photos
        public void PreviewSniperCycle(float time)
        {
            if (weapons == null) return;
            boltTime = time;
            PoseSniper();
            boltTime = -1f;
        }

        WeaponParts rifle;
        readonly List<Material> rifleMaterials = new();

        // Puts on a sniper skin (index into Skins.Snipers), remembers it, and pulls it out
        public void EquipSniperSkin(int index)
        {
            if (weapons == null) return;
            sniperSkin = Mathf.Clamp(index, 0, Skins.Snipers.Length - 1);
            Skins.EquippedSniper = sniperSkin;
            BuildRifleModel();
            if (current == SniperSlot) { drawTime = Application.isPlaying ? 0f : 99f; Play(WeaponSounds.Draw, 0.6f); }
            else Equip(SniperSlot);
        }

        void BuildRifleModel()
        {
            if (rifle != null) Kill(rifle.root.gameObject);
            foreach (var m in rifleMaterials) Kill(m);
            rifleMaterials.Clear();
            rifle = new WeaponBuilder(template, Layer, false, rifleMaterials).Rifle(Skins.Snipers[sniperSkin], gun);
            bolt = rifle.bolt;
            magazine = rifle.magazine;
            boltRest = rifle.boltRest;
            magRest = rifle.magRest;
        }

        Transform BuildSniper()
        {

            var root = new GameObject("Sniper Rig").transform;
            root.SetParent(anchor, false);
            gun = new GameObject("Rifle").transform;
            gun.SetParent(root, false);
            BuildRifleModel();

            // Rifle space: +Z along the barrel, +Y up, origin at the trigger
            var t = gun;

            // Muzzle flash, shown for a moment after each shot
            flash = new GameObject("Muzzle Flash").transform;
            flash.SetParent(t, false);
            flash.localPosition = new Vector3(0f, 0.018f, 1.12f);
            var muzzleTex = fx && fx.muzzle ? fx.muzzle.GetTexture("_BaseMap") : null;
            var flareTex = fx && fx.flare ? fx.flare.GetTexture("_BaseMap") : null;
            if (muzzleTex && flareTex)
            {
                // Flash sprites: a star-burst facing you, and two flame tongues along the barrel
                Material Sprite(Texture tex, float glow)
                {
                    var m = MakeTransparent(new Material(fadeTemplate ? fadeTemplate : template));
                    m.SetTexture("_BaseMap", tex);
                    m.SetColor("_BaseColor", new Color(1f, 0.75f, 0.4f, 1f));
                    m.EnableKeyword("_EMISSION");
                    m.SetTexture("_EmissionMap", tex);
                    m.SetColor("_EmissionColor", new Color(1f, 0.6f, 0.25f) * glow);
                    materials.Add(m);
                    return m;
                }
                Material burst = Sprite(flareTex, 5f), tongue = Sprite(muzzleTex, 4f);
                Part(flash, PrimitiveType.Quad, burst, Vector3.zero, Vector3.one * 0.3f, Quaternion.identity);
                Part(flash, PrimitiveType.Quad, tongue, new Vector3(0f, 0f, 0.08f), new Vector3(0.09f, 0.2f, 1f), Quaternion.LookRotation(Vector3.right, Vector3.forward));
                Part(flash, PrimitiveType.Quad, tongue, new Vector3(0f, 0f, 0.08f), new Vector3(0.09f, 0.2f, 1f), Quaternion.LookRotation(Vector3.up, Vector3.forward));
            }
            else
            {
                Material fire = MakeGlow(new Color(1f, 0.65f, 0.25f), 6f);
                Part(flash, PrimitiveType.Sphere, fire, Vector3.zero, Vector3.one * 0.05f);
            }
            flash.gameObject.SetActive(false);

            // Block arms: the right glove on the pistol grip, the left one under the forend,
            // sleeves running back to the bottom corners
            var right = BuildBlockArm(t, "Right Arm");
            rightFist = right.root;
            // Each glove sits just behind what it holds, so the weapon rests on its front edge
            fistRestRotation = FingersBack(new Vector3(-0.3f, 0.35f, 1f), new Vector3(1f, 0f, 0.3f));
            fistRest = new Vector3(0f, -0.05f, -0.06f) - fistRestRotation * GripFront;
            rightFist.SetLocalPositionAndRotation(fistRest, fistRestRotation);
            var left = BuildBlockArm(t, "Left Arm");
            Quaternion leftRotation = FingersBack(new Vector3(0.25f, 0.75f, 0.6f), new Vector3(-1f, 0f, 0.3f));
            left.root.SetLocalPositionAndRotation(new Vector3(0f, -0.028f, 0.34f) - leftRotation * GripFront, leftRotation);
            return root;
        }
    }
}
