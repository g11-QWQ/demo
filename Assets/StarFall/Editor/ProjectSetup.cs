using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace StarFall.Editor
{
    public static class ProjectSetup
    {
        public const string ScenePath = "Assets/StarFall/Scenes/StarFall.unity";
        private const string Root = "Assets/StarFall/";

        // Initial creation only; never regenerate an existing scene or overwrite designer changes.
        [MenuItem("StarFall/Create Initial Scene")]
        public static void Create()
        {
            if (File.Exists(ScenePath)) throw new InvalidOperationException("StarFall scene already exists; creation skipped.");
            foreach (string directory in new[] { "Scenes", "Prefabs", "Materials", "Config" })
                Directory.CreateDirectory(Root + directory);
            AssetDatabase.Refresh();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            PlayerSettings.companyName = "SpaceHook";
            PlayerSettings.productName = "StarFall";
            PlayerSettings.defaultScreenWidth = 1280;
            PlayerSettings.defaultScreenHeight = 720;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.runInBackground = true;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.colorSpace = ColorSpace.Linear;
            var playerSettings = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")[0]);
            var input = playerSettings.FindProperty("activeInputHandler");
            if (input != null) { input.intValue = 1; playerSettings.ApplyModifiedPropertiesWithoutUndo(); }

            var cyan = Material("PlayerCyan", new Color(0.12f, 0.88f, 0.92f));
            var coral = Material("EnemyCoral", new Color(1f, 0.24f, 0.32f));
            var yellow = Material("BulletGold", new Color(1f, 0.83f, 0.18f));
            var white = Material("Highlight", new Color(0.9f, 0.98f, 1));
            var floor = Material("GroundNavy", new Color(0.055f, 0.085f, 0.13f));
            var grid = Material("Grid", new Color(0.085f, 0.135f, 0.19f));
            var boundary = Material("Boundary", new Color(0.18f, 0.4f, 0.48f));
            var config = AssetDatabase.LoadAssetAtPath<BattleConfig>(Root + "Config/DefaultBattle.asset");
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<BattleConfig>();
                AssetDatabase.CreateAsset(config, Root + "Config/DefaultBattle.asset");
            }

            var playerObject = new GameObject("Player");
            var player = playerObject.AddComponent<PlayerController>();
            Primitive("Body", PrimitiveType.Sphere, playerObject.transform, Vector3.zero, Vector3.one * 1.1f, cyan);
            var facing = new GameObject("Facing").transform;
            facing.SetParent(playerObject.transform, false);
            Primitive("AimMarker", PrimitiveType.Cube, facing, new Vector3(0, 0.15f, 0.57f), new Vector3(0.23f, 0.22f, 0.6f), white);
            SetReference(player, "facing", facing);
            var playerPrefab = PrefabUtility.SaveAsPrefabAsset(playerObject, Root + "Prefabs/Player.prefab").GetComponent<PlayerController>();
            UnityEngine.Object.DestroyImmediate(playerObject);

            var enemyObject = new GameObject("Enemy");
            enemyObject.AddComponent<EnemyController>();
            Primitive("Body", PrimitiveType.Cube, enemyObject.transform, Vector3.zero, new Vector3(1.05f, 1, 1.05f), coral);
            Primitive("FrontStripe", PrimitiveType.Cube, enemyObject.transform, new Vector3(0, 0.52f, 0.25f), new Vector3(0.6f, 0.04f, 0.12f), white);
            var enemyPrefab = PrefabUtility.SaveAsPrefabAsset(enemyObject, Root + "Prefabs/Enemy.prefab").GetComponent<EnemyController>();
            UnityEngine.Object.DestroyImmediate(enemyObject);

            var bulletObject = new GameObject("Projectile");
            bulletObject.AddComponent<Projectile>();
            Primitive("Body", PrimitiveType.Sphere, bulletObject.transform, Vector3.zero, Vector3.one * 0.32f, yellow);
            var bulletPrefab = PrefabUtility.SaveAsPrefabAsset(bulletObject, Root + "Prefabs/Projectile.prefab").GetComponent<Projectile>();
            UnityEngine.Object.DestroyImmediate(bulletObject);

            var cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.GetComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 16;
            camera.backgroundColor = new Color(0.025f, 0.04f, 0.065f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 100;
            camera.transform.SetPositionAndRotation(new Vector3(0, 30, 0), Quaternion.Euler(90, 0, 0));
            var arena = new GameObject("Arena").transform;
            Primitive("Ground", PrimitiveType.Cube, arena, new Vector3(0, -0.15f, 0), new Vector3(42, 0.2f, 26), floor);
            for (int x = -20; x <= 20; x += 2)
                Primitive("GridX_" + x, PrimitiveType.Cube, arena, new Vector3(x, -0.04f, 0), new Vector3(0.025f, 0.015f, 26), grid);
            for (int z = -12; z <= 12; z += 2)
                Primitive("GridZ_" + z, PrimitiveType.Cube, arena, new Vector3(0, -0.04f, z), new Vector3(42, 0.015f, 0.025f), grid);
            foreach (int sign in new[] { -1, 1 })
            {
                Primitive("BoundaryX", PrimitiveType.Cube, arena, new Vector3(sign * 21, 0, 0), new Vector3(0.15f, 0.15f, 26.15f), boundary);
                Primitive("BoundaryZ", PrimitiveType.Cube, arena, new Vector3(0, 0, sign * 13), new Vector3(42.15f, 0.15f, 0.15f), boundary);
            }
            var application = new GameObject("StarFall").AddComponent<GameApplication>();
            SetReference(application, "worldCamera", camera);
            var battle = application.GetComponent<BattleController>();
            SetReference(battle, "config", config);
            SetReference(battle, "playerPrefab", playerPrefab);
            SetReference(battle, "enemyPrefab", enemyPrefab);
            SetReference(battle, "projectilePrefab", bulletPrefab);
            SetReference(application.GetComponent<GameUI>(), "font", AssetDatabase.LoadAssetAtPath<Font>(Root + "Fonts/SarasaUiSC-Regular.ttf"));
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
            // Only an editor-playable prototype is requested: no player build pipeline or profiles.
            EditorBuildSettings.scenes = Array.Empty<EditorBuildSettingsScene>();
            EditorBuildSettings.RemoveConfigObject("com.unity.input.settings.actions");
            AssetDatabase.SaveAssets();
            PhaseOneSetup.Apply();
            Debug.Log("STARFALL_SETUP_OK: scene, config and prefabs created.");
        }

        private static Material Material(string name, Color color)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(Root + "Materials/" + name + ".mat");
            if (existing != null) return existing;
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) throw new InvalidOperationException("URP Unlit shader missing.");
            var material = new Material(shader) { name = name };
            material.SetColor("_BaseColor", color);
            AssetDatabase.CreateAsset(material, Root + "Materials/" + name + ".mat");
            return material;
        }

        private static void Primitive(string name, PrimitiveType type, Transform parent, Vector3 position, Vector3 scale, Material material)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = material;
            go.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
        }

        private static void SetReference(UnityEngine.Object owner, string name, UnityEngine.Object value)
        {
            if (value == null) throw new InvalidOperationException("Missing reference: " + name);
            var serialized = new SerializedObject(owner);
            serialized.FindProperty(name).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
