#if UNITY_EDITOR
using REHozy.CarryableTools;
using REHozy.Dirt;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace REHozy.EditorTools
{
    public static class ShovelSceneSetup
    {
        private const string ShovelPrefabPath = "Assets/REHozy/Prefabs/Shovel.prefab";
        private const string DirtMaterialPath = "Assets/REHozy/Materials/DirtPatch_Default.mat";
        private const string MenuPath = "REHozy/Setup Shovel Test Objects";
        private const string WireInputPath = "REHozy/Wire Tool Input To Shovel";
        private const string WireHarpoonPath = "REHozy/Wire Tool Input To Harpoon";

        [MenuItem(MenuPath)]
        public static void SetupShovelTestObjects()
        {
            var bridgeY = -5.5f;
            var basePosition = new Vector3(-78f, bridgeY + 0.6f, -11f);

            if (GameObject.Find("Shovel") == null)
            {
                CreateShovel(basePosition);
            }

            if (GameObject.Find("DirtPatch_Test") == null)
            {
                CreateDirtPatch(new Vector3(-74f, bridgeY + 0.05f, -13f));
            }

            EnsureShovelPrefabAsset();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log(
                "Shovel test objects created. Set PlayerToolModeState.Active = Shovel and use "
                + "REHozy → Wire Tool Input To Shovel before play.");
        }

        [MenuItem(WireInputPath)]
        public static void WireToolInputToShovel()
        {
            var shovel = GameObject.Find("Shovel");
            if (shovel == null)
            {
                Debug.LogWarning("Shovel not found in scene. Run REHozy → Setup Shovel Test Objects first.");
                return;
            }

            WireToolInputTo(shovel.GetComponent<CarryableToolCore>(), PlayerToolMode.Shovel);
        }

        [MenuItem(WireHarpoonPath)]
        public static void WireToolInputToHarpoon()
        {
            var harpoon = GameObject.Find("Harpoon");
            if (harpoon == null)
            {
                Debug.LogWarning("Harpoon not found in scene.");
                return;
            }

            WireToolInputTo(harpoon.GetComponent<CarryableToolCore>(), PlayerToolMode.Harpoon);
        }

        private static void WireToolInputTo(CarryableToolCore core, PlayerToolMode mode)
        {
            if (core == null)
            {
                return;
            }

            var carry = core.CarryDriver;
            var input = Object.FindFirstObjectByType<CarryableToolInputHandler>();
            var reticle = Object.FindFirstObjectByType<CarryableAimReticleUI>();
            var returnHoldUi = Object.FindFirstObjectByType<CarryableReturnHoldUI>();

            if (input == null)
            {
                Debug.LogWarning("CarryableToolInputHandler not found. Add ToolGameplay first.");
                return;
            }

            var soInput = new SerializedObject(input);
            soInput.FindProperty("activeModeOnPlay").enumValueIndex = (int)mode;
            soInput.FindProperty("tool").objectReferenceValue = core;
            soInput.ApplyModifiedPropertiesWithoutUndo();

            if (reticle != null)
            {
                var soReticle = new SerializedObject(reticle);
                soReticle.FindProperty("tool").objectReferenceValue = core;
                soReticle.FindProperty("carryDriver").objectReferenceValue = carry;
                soReticle.ApplyModifiedPropertiesWithoutUndo();
            }

            if (returnHoldUi != null)
            {
                var soReturn = new SerializedObject(returnHoldUi);
                soReturn.FindProperty("tool").objectReferenceValue = core;
                soReturn.ApplyModifiedPropertiesWithoutUndo();
            }

            PlayerToolModeState.Active = mode;
            input.RefreshToolBinding();
            Debug.Log($"ToolGameplay wired to {mode}. Active Mode On Play set on CarryableToolInputHandler.");
        }

        private static void EnsureModeBootstrap(GameObject gameplayRoot, PlayerToolMode mode)
        {
            var bootstrap = gameplayRoot.GetComponent<CarryableToolModeBootstrap>();
            if (bootstrap == null)
            {
                bootstrap = gameplayRoot.AddComponent<CarryableToolModeBootstrap>();
            }

            var so = new SerializedObject(bootstrap);
            so.FindProperty("activeModeOnPlay").enumValueIndex = (int)mode;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void EnsureShovelPrefabAsset()
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(ShovelPrefabPath);
            if (existing != null)
            {
                return;
            }

            var shovel = GameObject.Find("Shovel");
            if (shovel == null)
            {
                return;
            }

            if (!AssetDatabase.IsValidFolder("Assets/REHozy/Prefabs"))
            {
                AssetDatabase.CreateFolder("Assets/REHozy", "Prefabs");
            }

            PrefabUtility.SaveAsPrefabAsset(shovel, ShovelPrefabPath);
            Debug.Log($"Saved {ShovelPrefabPath}");
        }

        private static GameObject CreateShovel(Vector3 position)
        {
            var root = GameObject.CreatePrimitive(PrimitiveType.Cube);
            root.name = "Shovel";
            Object.DestroyImmediate(root.GetComponent<BoxCollider>());

            root.transform.position = position;
            root.transform.localScale = new Vector3(0.15f, 0.5f, 0.25f);

            var pickup = root.AddComponent<BoxCollider>();
            pickup.size = new Vector3(3f, 3f, 3f);

            var tip = new GameObject("Tip").transform;
            tip.SetParent(root.transform, false);
            tip.localPosition = new Vector3(0f, -0.55f, 0.2f);

            var carry = root.AddComponent<CarryableCarryDriver>();
            var core = root.AddComponent<CarryableToolCore>();
            var actions = root.AddComponent<ShovelToolActions>();

            var cam = UnityEngine.Camera.main;
            if (cam != null)
            {
                var soCarry = new SerializedObject(carry);
                soCarry.FindProperty("targetCamera").objectReferenceValue = cam;
                soCarry.FindProperty("heightOffset").floatValue = 0.45f;
                soCarry.FindProperty("tipForwardAxis").vector3Value = new Vector3(0f, -1f, 0.2f).normalized;
                soCarry.ApplyModifiedPropertiesWithoutUndo();
            }

            var soCore = new SerializedObject(core);
            soCore.FindProperty("toolModeId").enumValueIndex = (int)PlayerToolMode.Shovel;
            soCore.FindProperty("tip").objectReferenceValue = tip;
            soCore.FindProperty("carryDriver").objectReferenceValue = carry;
            soCore.FindProperty("pickupCollider").objectReferenceValue = pickup;
            soCore.ApplyModifiedPropertiesWithoutUndo();

            var dirtLayer = LayerMask.NameToLayer("DirtPatch");
            if (dirtLayer >= 0)
            {
                var soActions = new SerializedObject(actions);
                soActions.FindProperty("dirtPatchMask").intValue = 1 << dirtLayer;
                soActions.ApplyModifiedPropertiesWithoutUndo();
            }

            return root;
        }

        private static void CreateDirtPatch(Vector3 position)
        {
            var patch = GameObject.CreatePrimitive(PrimitiveType.Plane);
            patch.name = "DirtPatch_Test";
            patch.transform.position = position;
            patch.transform.localScale = new Vector3(0.4f, 1f, 0.4f);

            var dirtLayer = LayerMask.NameToLayer("DirtPatch");
            if (dirtLayer >= 0)
            {
                patch.layer = dirtLayer;
            }

            var material = GetOrCreateDirtMaterial();
            patch.GetComponent<MeshRenderer>().sharedMaterial = material;
            patch.AddComponent<DirtDeformPatch>();
        }

        private static Material GetOrCreateDirtMaterial()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(DirtMaterialPath);
            if (existing != null)
            {
                return existing;
            }

            var shader = Shader.Find("LeftToMelt/SnowVertexLit");
            if (shader == null)
            {
                Debug.LogWarning("Shader LeftToMelt/SnowVertexLit not found. Assign material manually.");
                return new Material(Shader.Find("Universal Render Pipeline/Lit"));
            }

            if (!AssetDatabase.IsValidFolder("Assets/REHozy/Materials"))
            {
                AssetDatabase.CreateFolder("Assets/REHozy", "Materials");
            }

            var material = new Material(shader)
            {
                name = "DirtPatch_Default"
            };
            material.SetColor("_SnowColor", new Color(0.35f, 0.28f, 0.22f));
            material.SetColor("_DirtySnowColor", new Color(0.42f, 0.34f, 0.26f));
            material.SetColor("_GroundColor", new Color(0.22f, 0.18f, 0.14f));
            material.SetFloat("_SnowHeight", 0.2f);
            material.SetFloat("_SnowVisibilityCutoff", 0.08f);
            material.SetFloat("_DeformSmoothEnable", 1f);
            material.SetFloat("_DeformSmoothRadius", 0.06f);
            material.SetFloat("_EdgeFalloffEnable", 1f);
            material.SetFloat("_EdgeFalloffRadial", 1f);
            material.SetFloat("_EdgeFalloffUseObjectPos", 1f);
            material.SetFloat("_EdgeFalloffWidth", 0.22f);
            material.SetFloat("_HeightNoiseScale", 0.35f);
            material.SetFloat("_HeightNoiseStrength", 0.14f);

            var wavesNormal = AssetDatabase.LoadAssetAtPath<Texture2D>(
                "Assets/Bitgem/StylisedWater/URP/Textures/waves_n.png");
            if (wavesNormal != null)
            {
                material.SetTexture("_HeightNoiseTex", wavesNormal);
            }

            AssetDatabase.CreateAsset(material, DirtMaterialPath);
            AssetDatabase.SaveAssets();
            return material;
        }
    }
}
#endif
