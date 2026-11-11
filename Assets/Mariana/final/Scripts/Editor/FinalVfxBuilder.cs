using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace FinalVfx.EditorTools
{
    /// <summary>
    /// Builds (or rebuilds) materials and the test scene of the final VFX demo.
    /// Every step is idempotent: running it again updates the existing assets and objects.
    /// </summary>
    public static class FinalVfxBuilder
    {
        private const string Root = "Assets/Mariana/final";
        private const string TexDir = Root + "/Textures";
        private const string MatDir = Root + "/Materials";
        private const string ScenePath = Root + "/Scenes/FinalVfx_Test.unity";
        private const string ControllerName = "VFX Test Controller";

        // ------------------------------------------------------------------ menu
        [MenuItem("Tools/Final VFX/Build All", priority = 0)]
        public static void BuildAll()
        {
            BuildLaser();
            BuildOrb();
            BuildTrail();
        }

        [MenuItem("Tools/Final VFX/1 Build Laser", priority = 20)]
        public static void BuildLaser()
        {
            ConfigureTexture("T_LaserMask_RGB.png", srgb: false, TextureWrapMode.Repeat);
            Material mat = GetOrCreateMaterial("M_LaserBeam", "Final VFX/SG_LaserBeam");
            mat.SetTexture("_BeamMask", LoadTexture("T_LaserMask_RGB.png"));
            EditorUtility.SetDirty(mat);

            Scene scene = OpenOrCreateTestScene();
            GameObject go = ReplaceObject("Laser", new Vector3(-5.5f, 3.1f, 0f),
                Quaternion.LookRotation(Vector3.right));

            var line = go.AddComponent<LineRenderer>();
            line.sharedMaterial = mat;
            line.positionCount = 2;
            line.SetPositions(new[] { go.transform.position, go.transform.position + Vector3.right * 11f });
            line.useWorldSpace = true;
            line.alignment = LineAlignment.View;
            line.textureMode = LineTextureMode.Tile;
            line.textureScale = new Vector2(0.45f, 1f);
            line.numCapVertices = 0;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.widthCurve = new AnimationCurve(
                new Keyframe(0f, 0.65f), new Keyframe(0.06f, 1f), new Keyframe(0.88f, 0.85f), new Keyframe(1f, 0.3f));
            line.colorGradient = MakeGradient(
                new[] { (new Color(1f, 0.85f, 1f), 0f), (new Color(0.85f, 0.55f, 1f), 0.6f), (new Color(0.55f, 0.45f, 1f), 1f) },
                new[] { (1f, 0f), (1f, 0.82f), (0f, 1f) });

            var laser = go.AddComponent<LaserEffect>();
            SetSerialized(laser, "maxLength", 11f);
            SetLoop(laser, hold: 1.0f, rest: 1.1f, delay: 0f);

            WireController("laser", laser);
            SaveScene(scene);
            Debug.Log("[Final VFX] Laser built.");
        }

        [MenuItem("Tools/Final VFX/2 Build Orb", priority = 21)]
        public static void BuildOrb()
        {
            const string shader = "Final VFX/SG_OrbAdditive";
            Material circleMat = OrbMaterial("M_Orb_Circle", shader, "T_OrbCircle.png", TextureWrapMode.Clamp, new Color(1.3f, 2.4f, 0.45f));
            Material sparkMat = OrbMaterial("M_Orb_Spark", shader, "T_OrbSpark.png", TextureWrapMode.Clamp, new Color(1.6f, 2.6f, 0.9f));
            Material sparkTrailMat = OrbMaterial("M_Orb_SparkTrail", shader, "T_OrbSparkTrail.png", TextureWrapMode.Clamp, new Color(0.9f, 2.0f, 0.3f));
            Material boltMat = OrbMaterial("M_Orb_Bolt", shader, "T_OrbBolt.png", TextureWrapMode.Clamp, new Color(2.6f, 3.0f, 1.6f));
            Material smokeMat = OrbMaterial("M_Orb_Smoke", shader, "T_OrbSmoke.png", TextureWrapMode.Clamp, new Color(0.25f, 0.55f, 0.12f));

            Scene scene = OpenOrCreateTestScene();
            GameObject root = ReplaceObject("Orb", new Vector3(-3.2f, 0.9f, 0f), Quaternion.identity);

            // 1. circle core: a few overlapping rings that flicker in place
            ParticleSystem circle = NewSystem(root, "Circle", circleMat);
            {
                var main = circle.main;
                main.startLifetime = 0.32f;
                main.startSpeed = 0f;
                main.startSize = 1.25f;
                main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
                main.startColor = new Color(0.8f, 1f, 0.55f, 0.8f);
                main.maxParticles = 30;
                var em = circle.emission;
                em.rateOverTime = 24f;
                var shape = circle.shape;
                shape.enabled = false;
                var col = circle.colorOverLifetime;
                col.enabled = true;
                col.color = MakeGradient(new[] { (Color.white, 0f), (Color.white, 1f) },
                    new[] { (0f, 0f), (1f, 0.2f), (0f, 1f) });
                var sol = circle.sizeOverLifetime;
                sol.enabled = true;
                sol.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.8f), new Keyframe(1f, 1.12f)));
            }

            // 2. sparks: noisy orbiting dots with trails
            ParticleSystem sparks = NewSystem(root, "Sparks", sparkMat);
            {
                var main = sparks.main;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.95f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 1.5f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.13f);
                main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.85f, 1f, 0.5f), new Color(1f, 1f, 0.85f));
                main.maxParticles = 120;
                var em = sparks.emission;
                em.rateOverTime = 45f;
                var shape = sparks.shape;
                shape.enabled = true;
                shape.shapeType = ParticleSystemShapeType.Sphere;
                shape.radius = 0.42f;
                shape.radiusThickness = 0.2f;
                var noise = sparks.noise;
                noise.enabled = true;
                noise.strength = 1.3f;
                noise.frequency = 1.7f;
                noise.scrollSpeed = 0.9f;
                noise.damping = true;
                var trails = sparks.trails;
                trails.enabled = true;
                trails.ratio = 1f;
                trails.lifetime = 0.32f;
                trails.dieWithParticles = true;
                trails.widthOverTrail = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0f)));
                trails.inheritParticleColor = true;
                var r = sparks.GetComponent<ParticleSystemRenderer>();
                r.trailMaterial = sparkTrailMat;
                var col = sparks.colorOverLifetime;
                col.enabled = true;
                col.color = MakeGradient(new[] { (Color.white, 0f), (Color.white, 1f) },
                    new[] { (0f, 0f), (1f, 0.1f), (1f, 0.6f), (0f, 1f) });
            }

            // 3. central bolts: short-lived flipbook lightning at random angles
            ParticleSystem bolts = NewSystem(root, "Bolts", boltMat);
            {
                var main = bolts.main;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.07f, 0.15f);
                main.startSpeed = 0f;
                main.startSize = new ParticleSystem.MinMaxCurve(0.8f, 1.35f);
                main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
                main.startColor = new Color(1f, 1f, 0.8f, 1f);
                main.maxParticles = 12;
                var em = bolts.emission;
                em.rateOverTime = 20f;
                var shape = bolts.shape;
                shape.enabled = true;
                shape.shapeType = ParticleSystemShapeType.Sphere;
                shape.radius = 0.05f;
                var tsa = bolts.textureSheetAnimation;
                tsa.enabled = true;
                tsa.numTilesX = 2;
                tsa.numTilesY = 2;
                tsa.frameOverTime = new ParticleSystem.MinMaxCurve(0f, 0.999f);
                tsa.cycleCount = 1;
            }

            // 4. smoke: slow green haze around the orb
            ParticleSystem smoke = NewSystem(root, "Smoke", smokeMat);
            {
                var main = smoke.main;
                main.startLifetime = new ParticleSystem.MinMaxCurve(1.1f, 1.7f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.1f, 0.3f);
                main.startSize = new ParticleSystem.MinMaxCurve(1.0f, 1.7f);
                main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
                main.startColor = new Color(0.6f, 0.9f, 0.4f, 0.55f);
                main.maxParticles = 24;
                var em = smoke.emission;
                em.rateOverTime = 9f;
                var shape = smoke.shape;
                shape.enabled = true;
                shape.shapeType = ParticleSystemShapeType.Sphere;
                shape.radius = 0.25f;
                var rol = smoke.rotationOverLifetime;
                rol.enabled = true;
                rol.z = new ParticleSystem.MinMaxCurve(-0.8f, 0.8f);
                var col = smoke.colorOverLifetime;
                col.enabled = true;
                col.color = MakeGradient(new[] { (Color.white, 0f), (Color.white, 1f) },
                    new[] { (0f, 0f), (0.6f, 0.3f), (0f, 1f) });
                smoke.GetComponent<ParticleSystemRenderer>().sortingFudge = 10f;
            }

            // 5. point light
            var lightGo = new GameObject("Point Light");
            lightGo.transform.SetParent(root.transform, false);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(0.65f, 1f, 0.25f);
            light.range = 4.5f;
            light.intensity = 0f;
            light.shadows = LightShadows.None;

            var orb = root.AddComponent<OrbEffect>();
            var so = new SerializedObject(orb);
            SerializedProperty arr = so.FindProperty("systems");
            ParticleSystem[] list = { smoke, circle, sparks, bolts };
            arr.arraySize = list.Length;
            for (int i = 0; i < list.Length; i++) arr.GetArrayElementAtIndex(i).objectReferenceValue = list[i];
            so.FindProperty("pointLight").objectReferenceValue = light;
            so.FindProperty("burstSystem").objectReferenceValue = sparks;
            SetFloats(so.FindProperty("baseStartSizes"), System.Array.ConvertAll(list, p => p.main.startSizeMultiplier));
            SetFloats(so.FindProperty("baseEmissionRates"), System.Array.ConvertAll(list, p => p.emission.rateOverTimeMultiplier));
            so.ApplyModifiedPropertiesWithoutUndo();
            SetLoop(orb, hold: 1.6f, rest: 1.0f, delay: 0.4f);

            WireController("orb", orb);
            SaveScene(scene);
            Debug.Log("[Final VFX] Orb built.");
        }

        [MenuItem("Tools/Final VFX/3 Build Trail", priority = 22)]
        public static void BuildTrail()
        {
            const string shader = "Final VFX/SG_StylizedTrail";
            ConfigureTexture("T_TrailStreak.png", srgb: false, TextureWrapMode.Repeat);
            Material bright = GetOrCreateMaterial("M_StylizedTrail", shader);
            bright.SetTexture("_StreakTex", LoadTexture("T_TrailStreak.png"));
            EditorUtility.SetDirty(bright);

            // second, darker material for the trail behind (same shader, lower values)
            Material dark = GetOrCreateMaterial("M_StylizedTrail_Dark", shader);
            dark.SetTexture("_StreakTex", LoadTexture("T_TrailStreak.png"));
            dark.SetColor("_HeadColor", new Color(1.1f, 0.12f, 0.3f));
            dark.SetColor("_TailColor", new Color(0.02f, 0.22f, 0.3f));
            dark.SetVector("_StreakScroll", new Vector4(-0.45f, 0f, 0f, 0f));
            EditorUtility.SetDirty(dark);

            const string particleShader = "Final VFX/SG_OrbAdditive";
            Material glowMat = OrbMaterial("M_Trail_HeadGlow", particleShader, "T_OrbCircle.png", TextureWrapMode.Clamp, new Color(3.2f, 1.1f, 0.6f));
            Material sparkMat = OrbMaterial("M_Trail_Sparks", particleShader, "T_OrbSpark.png", TextureWrapMode.Clamp, new Color(2.6f, 0.5f, 0.35f));

            Scene scene = OpenOrCreateTestScene();
            GameObject oldTest = GameObject.Find("Trail Test Object");
            if (oldTest != null) Object.DestroyImmediate(oldTest);

            GameObject root = ReplaceObject("Trail", new Vector3(3.2f, 1.0f, 0f), Quaternion.identity);
            var head = new GameObject("Head");
            head.transform.SetParent(root.transform, false);
            head.AddComponent<TrailTestMover>();

            TrailRenderer brightTrail = NewTrail(head, "Bright Trail", bright, time: 0.85f, sortingOrder: 1);
            TrailRenderer darkTrail = NewTrail(head, "Dark Trail", dark, time: 1.3f, sortingOrder: 0);
            const float width = 0.75f;
            const float darkWidth = width * 1.35f;

            // head glow: few big soft particles that follow the head (local space)
            ParticleSystem glow = NewSystem(head, "Head Glow", glowMat);
            {
                var main = glow.main;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.45f);
                main.startSpeed = 0f;
                main.startSize = 0.9f;
                main.maxParticles = 10;
                var em = glow.emission;
                em.rateOverTime = 10f;
                var shape = glow.shape;
                shape.enabled = false;
                var sol = glow.sizeOverLifetime;
                sol.enabled = true;
                sol.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                    new Keyframe(0f, 0.3f), new Keyframe(0.3f, 1f), new Keyframe(1f, 0f)));
                var col = glow.colorOverLifetime;
                col.enabled = true;
                col.color = MakeGradient(new[] { (Color.white, 0f), (Color.white, 1f) },
                    new[] { (0f, 0f), (1f, 0.25f), (0f, 1f) });
            }

            // sparks left behind in world space, pushed around by noise
            ParticleSystem sparks = NewSystem(head, "Head Sparks", sparkMat);
            {
                var main = sparks.main;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.8f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.1f, 0.5f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.18f);
                main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.75f, 0.6f), new Color(1f, 0.4f, 0.5f));
                main.maxParticles = 200;
                var em = sparks.emission;
                em.rateOverTime = 36f;
                var shape = sparks.shape;
                shape.enabled = true;
                shape.shapeType = ParticleSystemShapeType.Sphere;
                shape.radius = 0.12f;
                var noise = sparks.noise;
                noise.enabled = true;
                noise.strength = 1.6f;
                noise.frequency = 2.2f;
                noise.damping = true;
                var col = sparks.colorOverLifetime;
                col.enabled = true;
                col.color = MakeGradient(new[] { (Color.white, 0f), (Color.white, 1f) },
                    new[] { (1f, 0f), (1f, 0.5f), (0f, 1f) });
            }

            var lightGo = new GameObject("Head Light");
            lightGo.transform.SetParent(head.transform, false);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.55f, 0.4f);
            light.range = 3f;
            light.intensity = 0f;
            light.shadows = LightShadows.None;

            var effect = head.AddComponent<TrailEffect>();
            var so = new SerializedObject(effect);
            SetObjects(so.FindProperty("trails"), new Object[] { brightTrail, darkTrail });
            SetFloats(so.FindProperty("trailWidths"), new[] { width, darkWidth });
            SetObjects(so.FindProperty("headParticles"), new Object[] { glow, sparks });
            SetFloats(so.FindProperty("headEmissionRates"), new[] { 10f, 36f });
            so.FindProperty("headLight").objectReferenceValue = light;
            so.ApplyModifiedPropertiesWithoutUndo();
            SetLoop(effect, hold: 2.2f, rest: 0.9f, delay: 0.8f);

            WireController("trail", effect);
            SaveScene(scene);
            Debug.Log("[Final VFX] Trail built.");
        }

        private static TrailRenderer NewTrail(GameObject parent, string name, Material mat, float time, int sortingOrder)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            var trail = go.AddComponent<TrailRenderer>();
            trail.sharedMaterial = mat;
            trail.time = time;
            trail.minVertexDistance = 0.03f;
            trail.alignment = LineAlignment.View;
            trail.textureMode = LineTextureMode.Stretch;
            trail.numCapVertices = 4;
            trail.numCornerVertices = 2;
            trail.emitting = false;
            trail.sortingOrder = sortingOrder;
            trail.shadowCastingMode = ShadowCastingMode.Off;
            trail.receiveShadows = false;
            trail.widthCurve = new AnimationCurve(
                new Keyframe(0f, 0.7f), new Keyframe(0.12f, 1f), new Keyframe(1f, 0f, -1.4f, 0f));
            trail.colorGradient = MakeGradient(
                new[] { (Color.white, 0f), (Color.white, 1f) },
                new[] { (1f, 0f), (1f, 1f) });
            return trail;
        }

        private static void SetLoop(Object effect, float hold, float rest, float delay)
        {
            var so = new SerializedObject(effect);
            so.FindProperty("loopAttack").boolValue = true;
            so.FindProperty("previewInEditMode").boolValue = true;
            so.FindProperty("holdTime").floatValue = hold;
            so.FindProperty("restTime").floatValue = rest;
            so.FindProperty("firstDelay").floatValue = delay;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetFloats(SerializedProperty arr, float[] values)
        {
            arr.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) arr.GetArrayElementAtIndex(i).floatValue = values[i];
        }

        private static void SetObjects(SerializedProperty arr, Object[] values)
        {
            arr.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) arr.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }

        private static Material OrbMaterial(string name, string shader, string texture, TextureWrapMode wrap, Color tint)
        {
            ConfigureTexture(texture, srgb: true, wrap);
            Material mat = GetOrCreateMaterial(name, shader);
            mat.SetTexture("_ParticleTex", LoadTexture(texture));
            mat.SetColor("_TintColor", tint);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static ParticleSystem NewSystem(GameObject parent, string name, Material material)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = true;
            main.duration = 1f;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = material;
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            return ps;
        }

        // ------------------------------------------------------------------ assets
        private static Texture2D LoadTexture(string file)
        {
            return AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexDir}/{file}");
        }

        private static void ConfigureTexture(string file, bool srgb, TextureWrapMode wrap)
        {
            string path = $"{TexDir}/{file}";
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                Debug.LogError($"[Final VFX] Missing texture {path}. Run Tools~/generate_textures.py first.");
                return;
            }

            bool changed = importer.sRGBTexture != srgb || importer.wrapMode != wrap ||
                           importer.alphaSource != TextureImporterAlphaSource.FromInput ||
                           !importer.alphaIsTransparency;
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = srgb;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.wrapMode = wrap;
            importer.mipmapEnabled = true;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            if (changed)
            {
                importer.SaveAndReimport();
            }
        }

        private static Material GetOrCreateMaterial(string name, string shaderName)
        {
            Directory.CreateDirectory(MatDir);
            string path = $"{MatDir}/{name}.mat";
            Shader shader = Shader.Find(shaderName);
            if (shader == null)
            {
                throw new System.InvalidOperationException($"[Final VFX] Shader '{shaderName}' not found.");
            }

            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(mat, path);
            }
            else if (mat.shader != shader)
            {
                mat.shader = shader;
            }
            return mat;
        }

        // ------------------------------------------------------------------ scene
        private static Scene OpenOrCreateTestScene()
        {
            Scene active = SceneManager.GetActiveScene();
            if (active.path == ScenePath)
            {
                EnsureUrpCameraData();
                return active;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                throw new System.OperationCanceledException("[Final VFX] Build cancelled by user.");
            }

            if (File.Exists(ScenePath))
            {
                Scene opened = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                EnsureUrpCameraData();
                return opened;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            camGo.transform.SetPositionAndRotation(new Vector3(0f, 2f, -10.5f),
                Quaternion.LookRotation(new Vector3(0f, 1.7f, 0f) - new Vector3(0f, 2f, -10.5f)));
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.025f, 0.025f, 0.045f);
            cam.fieldOfView = 50f;
            cam.allowHDR = true;
            camGo.AddComponent<AudioListener>();
            EnsureUrpCameraData();

            var lightGo = new GameObject("Directional Light");
            lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 0.35f;
            light.color = new Color(0.75f, 0.8f, 1f);

            var controller = new GameObject(ControllerName);
            controller.AddComponent<EffectTestHotkeys>();

            EditorSceneManager.SaveScene(scene, ScenePath);
            return scene;
        }

        private static void EnsureUrpCameraData()
        {
            foreach (Camera cam in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
            {
                if (cam.GetComponent<UniversalAdditionalCameraData>() == null)
                {
                    var data = cam.gameObject.AddComponent<UniversalAdditionalCameraData>();
                    data.renderPostProcessing = false;
                }
            }
        }

        private static GameObject ReplaceObject(string name, Vector3 position, Quaternion rotation)
        {
            GameObject old = GameObject.Find(name);
            if (old != null)
            {
                Object.DestroyImmediate(old);
            }

            var go = new GameObject(name);
            go.transform.SetPositionAndRotation(position, rotation);
            return go;
        }

        private static void WireController(string field, Object effect)
        {
            GameObject controller = GameObject.Find(ControllerName);
            if (controller == null)
            {
                controller = new GameObject(ControllerName);
            }

            var hotkeys = controller.GetComponent<EffectTestHotkeys>();
            if (hotkeys == null)
            {
                hotkeys = controller.AddComponent<EffectTestHotkeys>();
            }
            SetSerialized(hotkeys, field, effect);
        }

        private static void SaveScene(Scene scene)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
        }

        // ------------------------------------------------------------------ helpers
        private static void SetSerialized(Object target, string field, float value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(field).floatValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetSerializedBool(Object target, string field, bool value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(field).boolValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetSerialized(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(field).objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Gradient MakeGradient((Color color, float time)[] colors, (float alpha, float time)[] alphas)
        {
            var gradient = new Gradient();
            var ck = new GradientColorKey[colors.Length];
            for (int i = 0; i < colors.Length; i++) ck[i] = new GradientColorKey(colors[i].color, colors[i].time);
            var ak = new GradientAlphaKey[alphas.Length];
            for (int i = 0; i < alphas.Length; i++) ak[i] = new GradientAlphaKey(alphas[i].alpha, alphas[i].time);
            gradient.SetKeys(ck, ak);
            return gradient;
        }
    }
}
