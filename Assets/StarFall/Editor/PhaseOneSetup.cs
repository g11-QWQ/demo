using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace StarFall.Editor
{
    public static class PhaseOneSetup
    {
        private const string Root = "Assets/StarFall/";

        [MenuItem("StarFall/Apply Phase One Assets")]
        public static void Apply()
        {
            var scene = EditorSceneManager.OpenScene(ProjectSetup.ScenePath);
            var config = AssetDatabase.LoadAssetAtPath<BattleConfig>(Root + "Config/DefaultBattle.asset");
            bool firstMigration = config.character == null;
            config.character = Asset<CharacterStats>("BaseCharacter");
            config.staff = Asset<StaffDefinition>("BasicStaff");
            config.gem = Asset<GemDefinition>("FireGem");
            if (firstMigration) config.enemyHealth = 24;
            EditorUtility.SetDirty(config);
            var wood = Material("StaffWood", new Color(0.42f, 0.22f, 0.09f));
            var fire = Material("FireOrange", new Color(1, 0.36f, 0.05f));
            var gold = Material("FireCore", new Color(1, 0.84f, 0.3f));
            var soul = Material("SoulCyan", new Color(0.1f, 0.95f, 1));
            var light = Material("SoulHighlight", new Color(0.72f, 1, 1));

            var player = PrefabUtility.LoadPrefabContents(Root + "Prefabs/Player.prefab");
            try
            {
                var facing = player.transform.Find("Facing");
                var oldMarker = facing.Find("AimMarker");
                if (oldMarker != null) UnityEngine.Object.DestroyImmediate(oldMarker.gameObject);
                var staff = facing.Find("Staff");
                if (staff == null)
                {
                    staff = new GameObject("Staff").transform;
                    staff.SetParent(facing, false);
                    Primitive("Shaft", PrimitiveType.Cylinder, staff, new Vector3(0, 0.5f, 0), new Vector3(0.14f, 0.65f, 0.14f), wood);
                    Primitive("GemSocket", PrimitiveType.Cube, staff, new Vector3(0, 1.17f, 0), Vector3.one * 0.27f, gold);
                    Primitive("FireGem", PrimitiveType.Sphere, staff, new Vector3(0, 1.4f, 0), Vector3.one * 0.43f, fire);
                }
                staff.localPosition = new Vector3(0.55f, 0, 0.1f);
                staff.localRotation = Quaternion.Euler(55, 0, 0);
                var circle = player.transform.Find("AbsorptionRange");
                var ring = circle == null ? new GameObject("AbsorptionRange", typeof(LineRenderer)).GetComponent<LineRenderer>() : circle.GetComponent<LineRenderer>();
                ring.transform.SetParent(player.transform, false);
                ConfigureLine(ring, soul, 96, 0.035f);
                ring.gameObject.SetActive(false);
                Set(player.GetComponent<PlayerController>(), "staffVisual", staff);
                Set(player.GetComponent<PlayerController>(), "collectionRing", ring);
                PrefabUtility.SaveAsPrefabAsset(player, Root + "Prefabs/Player.prefab");
            }
            finally { PrefabUtility.UnloadPrefabContents(player); }

            var projectile = PrefabUtility.LoadPrefabContents(Root + "Prefabs/Projectile.prefab");
            try
            {
                var body = projectile.transform.Find("Body");
                body.localScale = Vector3.one * config.gem.projectileRadius * 2;
                body.GetComponent<Renderer>().sharedMaterial = fire;
                if (projectile.transform.Find("Core") == null)
                {
                    Primitive("Core", PrimitiveType.Sphere, projectile.transform, new Vector3(0, 0.13f, 0.02f), Vector3.one * 0.25f, gold);
                    Primitive("FlameTail", PrimitiveType.Capsule, projectile.transform, new Vector3(0, 0, -0.27f), new Vector3(0.16f, 0.26f, 0.16f), fire)
                        .localRotation = Quaternion.Euler(90, 0, 0);
                }
                PrefabUtility.SaveAsPrefabAsset(projectile, Root + "Prefabs/Projectile.prefab");
            }
            finally { PrefabUtility.UnloadPrefabContents(projectile); }

            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(Root + "Materials/SoulTear.asset");
            if (mesh == null)
            {
                mesh = TearMesh();
                AssetDatabase.CreateAsset(mesh, Root + "Materials/SoulTear.asset");
            }
            var soulPrefab = AssetDatabase.LoadAssetAtPath<SoulPickup>(Root + "Prefabs/Soul.prefab");
            if (soulPrefab == null)
            {
                var root = new GameObject("Soul", typeof(SoulPickup));
                var shape = new GameObject("RupertTear", typeof(MeshFilter), typeof(MeshRenderer));
                shape.transform.SetParent(root.transform, false);
                shape.GetComponent<MeshFilter>().sharedMesh = mesh;
                shape.GetComponent<MeshRenderer>().sharedMaterial = soul;
                Primitive("Glint", PrimitiveType.Sphere, root.transform, new Vector3(-0.045f, 0.18f, -0.025f), new Vector3(0.13f, 0.08f, 0.17f), light);
                soulPrefab = PrefabUtility.SaveAsPrefabAsset(root, Root + "Prefabs/Soul.prefab").GetComponent<SoulPickup>();
                UnityEngine.Object.DestroyImmediate(root);
            }
            var battle = UnityEngine.Object.FindAnyObjectByType<BattleController>();
            Set(battle, "soulPrefab", soulPrefab);
            var reticleObject = Array.Find(scene.GetRootGameObjects(), root => root.name == "AimReticle");
            if (reticleObject == null) reticleObject = new GameObject("AimReticle", typeof(LineRenderer));
            var reticle = reticleObject.GetComponent<LineRenderer>();
            ConfigureLine(reticle, gold, 32, 0.04f);
            reticle.useWorldSpace = false;
            for (int i = 0; i < 32; i++)
            {
                float angle = i * Mathf.PI * 2 / 32;
                reticle.SetPosition(i, new Vector3(Mathf.Cos(angle) * 0.35f, 0, Mathf.Sin(angle) * 0.35f));
            }
            Set(battle, "aimMarker", reticle.transform);
            reticle.gameObject.SetActive(false);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("STARFALL_PHASE_ONE_SETUP_OK");
        }

        private static T Asset<T>(string name) where T : ScriptableObject
        {
            var path = Root + "Config/" + name + ".asset";
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static Material Material(string name, Color color)
        {
            var path = Root + "Materials/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            material = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { name = name };
            material.SetColor("_BaseColor", color);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static Transform Primitive(string name, PrimitiveType type, Transform parent, Vector3 position, Vector3 scale, Material material)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name; go.transform.SetParent(parent, false);
            go.transform.localPosition = position; go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = material;
            go.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            return go.transform;
        }

        private static void ConfigureLine(LineRenderer line, Material material, int count, float width)
        {
            line.sharedMaterial = material;
            line.positionCount = count; line.loop = true; line.useWorldSpace = true;
            line.widthMultiplier = width; line.alignment = LineAlignment.View;
            line.shadowCastingMode = ShadowCastingMode.Off;
        }

        private static void Set(UnityEngine.Object owner, string field, UnityEngine.Object value)
        {
            if (value == null) throw new InvalidOperationException("Missing " + field);
            var serialized = new SerializedObject(owner);
            serialized.FindProperty(field).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Mesh TearMesh()
        {
            // Rounded bulb narrowing into a long curved tail, revolved in 3D. No texture required.
            float[] z = { -0.25f, -0.21f, -0.12f, 0, 0.12f, 0.24f, 0.4f, 0.58f, 0.8f, 1.02f, 1.25f };
            float[] radius = { 0, 0.12f, 0.2f, 0.22f, 0.21f, 0.17f, 0.12f, 0.07f, 0.035f, 0.014f, 0 };
            const int sides = 24;
            var vertices = new Vector3[z.Length * sides];
            var triangles = new int[(z.Length - 1) * sides * 6];
            int index = 0;
            for (int row = 0; row < z.Length; row++)
            {
                float curve = Mathf.Max(0, z[row] - 0.2f);
                for (int side = 0; side < sides; side++)
                {
                    float angle = side * Mathf.PI * 2 / sides;
                    vertices[row * sides + side] = new Vector3(Mathf.Cos(angle) * radius[row] + curve * curve * 0.4f,
                        Mathf.Sin(angle) * radius[row], z[row]);
                    if (row == z.Length - 1) continue;
                    int a = row * sides + side, b = row * sides + (side + 1) % sides;
                    int c = a + sides, d = b + sides;
                    triangles[index++] = a; triangles[index++] = b; triangles[index++] = c;
                    triangles[index++] = b; triangles[index++] = d; triangles[index++] = c;
                }
            }
            var mesh = new Mesh { name = "RupertTear", vertices = vertices, triangles = triangles };
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            return mesh;
        }
    }
}
