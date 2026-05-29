#if UNITY_EDITOR
using REHozy.CarryableTools;
using REHozy.Watering;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;

namespace REHozy.EditorTools
{
    public static class WateringSceneSetup
    {
        private const string MenuPath = "REHozy/Setup Watering Test Objects";
        private const string WireInputPath = "REHozy/Wire Tool Input To Watering Can";
        private const string FixPourVisualPath = "REHozy/Fix Watering Can Pour Visual";
        private const string FoliageMaterialPath = "Assets/REHozy/Materials/FoliageReveal_Default.mat";
        private const string GrassBladePath = "Assets/REHozy/Prefabs/Watering/GrassBlade.prefab";

        [MenuItem(MenuPath)]
        public static void SetupWateringTestObjects()
        {
            var bridgeY = -5.5f;
            var basePos = new Vector3(-70f, bridgeY + 0.6f, -11f);

            Collider homeCollider = null;
            if (GameObject.Find("HomePoint") == null)
            {
                homeCollider = CreateHomeZone(new Vector3(-72f, bridgeY, -12f));
            }
            else
            {
                var home = GameObject.Find("HomePoint");
                homeCollider = home != null ? home.GetComponent<Collider>() : null;
            }

            GameObject wateringCan = null;
            if (GameObject.Find("WateringCan") == null)
            {
                wateringCan = CreateWateringCan(basePos);
            }
            else
            {
                wateringCan = GameObject.Find("WateringCan");
            }

            if (GameObject.Find("WaterableBush_Test") == null)
            {
                CreateBushTest(new Vector3(-68f, bridgeY + 0.2f, -9f));
            }

            if (GameObject.Find("WaterableTree_Test") == null)
            {
                CreateTreeTest(new Vector3(-66f, bridgeY + 0.2f, -12f));
            }

            if (GameObject.Find("WaterableGrassPatch_Test") == null)
            {
                CreateGrassPatchTest(new Vector3(-64f, bridgeY + 0.05f, -10f));
            }

            if (Object.FindFirstObjectByType<CarryableToolInputHandler>() == null && wateringCan != null)
            {
                CreateGameplay(wateringCan, homeCollider);
            }

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("Watering test objects created. Use REHozy → Wire Tool Input To Watering Can before play.");
        }

        [MenuItem(FixPourVisualPath)]
        public static void FixWateringCanPourVisual()
        {
            var can = GameObject.Find("WateringCan");
            if (can == null)
            {
                Debug.LogWarning("WateringCan not found in the open scene.");
                return;
            }

            EnsurePourVisualHierarchy(can.transform);
            EditorSceneManager.MarkSceneDirty(can.scene);
            Debug.Log("WateringCan pour visual hierarchy updated.");
        }

        [MenuItem(WireInputPath)]
        public static void WireToolInputToWateringCan()
        {
            var can = GameObject.Find("WateringCan");
            if (can == null)
            {
                Debug.LogWarning("WateringCan not found. Run REHozy → Setup Watering Test Objects first.");
                return;
            }

            WireToolInputTo(can.GetComponent<CarryableToolCore>(), PlayerToolMode.Water);
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

            EnsureModeBootstrap(input.gameObject, mode);
            PlayerToolModeState.Active = mode;
            input.RefreshToolBinding();
            Debug.Log($"ToolGameplay wired to {mode}.");
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

        private static Collider CreateHomeZone(Vector3 position)
        {
            var home = new GameObject("HomePoint");
            home.transform.position = position;
            var col = home.AddComponent<BoxCollider>();
            col.isTrigger = true;
            col.size = new Vector3(4f, 2f, 4f);
            col.center = new Vector3(0f, 1f, 0f);
            var registry = home.AddComponent<HomeZoneRegistry>();
            var so = new SerializedObject(registry);
            so.FindProperty("homeZone").objectReferenceValue = col;
            so.ApplyModifiedPropertiesWithoutUndo();
            return col;
        }

        private static void CreateGameplay(GameObject wateringCan, Collider homeCollider)
        {
            var go = new GameObject("ToolGameplay");
            var input = go.AddComponent<CarryableToolInputHandler>();
            go.AddComponent<CarryableAimReticleUI>();
            go.AddComponent<CarryableReturnHoldUI>();

            var attackRef = AssetDatabase.LoadAssetAtPath<InputActionReference>(
                "Assets/REHozy/Settings/InputAttack.asset");
            var actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(
                "Assets/InputSystem_Actions.inputactions");

            var core = wateringCan.GetComponent<CarryableToolCore>();
            var carry = wateringCan.GetComponent<CarryableCarryDriver>();

            if (homeCollider != null)
            {
                var soCore = new SerializedObject(core);
                soCore.FindProperty("homeZone").objectReferenceValue = homeCollider;
                soCore.ApplyModifiedPropertiesWithoutUndo();
            }

            var so = new SerializedObject(input);
            so.FindProperty("activeModeOnPlay").enumValueIndex = (int)PlayerToolMode.Water;
            so.FindProperty("tool").objectReferenceValue = core;
            so.FindProperty("rayCamera").objectReferenceValue = UnityEngine.Camera.main;
            so.FindProperty("attackAction").objectReferenceValue = attackRef;
            so.FindProperty("inputActionsFallback").objectReferenceValue = actions;
            so.ApplyModifiedPropertiesWithoutUndo();

            EnsureModeBootstrap(go, PlayerToolMode.Water);
            PlayerToolModeState.Active = PlayerToolMode.Water;
        }

        private static GameObject CreateWateringCan(Vector3 position)
        {
            var modelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Pandazole_Ultimate_Pack/Pandazole Farm Ranch Pack/Prefabs/Prop_WateringCan_02.prefab");

            GameObject root;
            if (modelPrefab != null)
            {
                root = (GameObject)PrefabUtility.InstantiatePrefab(modelPrefab);
                root.name = "WateringCan";
            }
            else
            {
                root = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                root.name = "WateringCan";
                Object.DestroyImmediate(root.GetComponent<CapsuleCollider>());
            }

            root.transform.position = position;
            root.transform.localScale = Vector3.one * 1.2f;

            var pickup = root.GetComponent<Collider>();
            if (pickup == null)
            {
                pickup = root.AddComponent<BoxCollider>();
            }

            pickup.isTrigger = false;

            var pivotGo = new GameObject("PourPivot");
            pivotGo.transform.SetParent(root.transform, false);
            pivotGo.transform.localPosition = new Vector3(0f, -0.08f, -0.05f);
            var aimPivot = pivotGo.GetComponent<WateringCanAimPivot>();
            if (aimPivot == null)
            {
                aimPivot = pivotGo.AddComponent<WateringCanAimPivot>();
            }

            var pourVisual = EnsurePourVisualHierarchy(root.transform);

            var tipGo = pourVisual.Find("Tip");
            if (tipGo == null)
            {
                tipGo = new GameObject("Tip").transform;
                tipGo.SetParent(pourVisual, false);
                tipGo.localPosition = new Vector3(0f, 0.15f, 0.35f);
            }

            var soPivot = new SerializedObject(aimPivot);
            soPivot.FindProperty("pourVisual").objectReferenceValue = pourVisual;
            soPivot.FindProperty("tip").objectReferenceValue = tipGo;
            soPivot.ApplyModifiedPropertiesWithoutUndo();

            var spout = new GameObject("WaterParticles");
            spout.transform.SetParent(tipGo, false);
            spout.transform.localPosition = Vector3.zero;
            var particles = spout.AddComponent<ParticleSystem>();
            ConfigureWaterParticles(particles);

            var carry = root.AddComponent<CarryableCarryDriver>();
            var core = root.AddComponent<CarryableToolCore>();
            var actions = root.AddComponent<WateringCanToolActions>();

            var cam = UnityEngine.Camera.main;
            if (cam != null)
            {
                var soCarry = new SerializedObject(carry);
                soCarry.FindProperty("targetCamera").objectReferenceValue = cam;
                soCarry.FindProperty("heightOffset").floatValue = 0.5f;
                soCarry.FindProperty("enableWorkPose").boolValue = true;
                soCarry.FindProperty("workHeightOffsetDelta").floatValue = -0.1f;
                soCarry.ApplyModifiedPropertiesWithoutUndo();
            }

            var soCore = new SerializedObject(core);
            soCore.FindProperty("toolModeId").enumValueIndex = (int)PlayerToolMode.Water;
            soCore.FindProperty("tip").objectReferenceValue = tipGo;
            soCore.FindProperty("carryDriver").objectReferenceValue = carry;
            soCore.FindProperty("pickupCollider").objectReferenceValue = pickup;
            soCore.ApplyModifiedPropertiesWithoutUndo();

            var waterableLayer = LayerMask.NameToLayer("Waterable");
            var waterableMask = waterableLayer >= 0 ? 1 << waterableLayer : ~0;

            var soActions = new SerializedObject(actions);
            soActions.FindProperty("aimPivot").objectReferenceValue = aimPivot;
            soActions.FindProperty("waterParticles").objectReferenceValue = particles;
            soActions.FindProperty("waterableMask").intValue = waterableMask;
            soActions.ApplyModifiedPropertiesWithoutUndo();

            return root;
        }

        private static Transform EnsurePourVisualHierarchy(Transform root)
        {
            var pourPivot = root.Find("PourPivot");
            if (pourPivot == null)
            {
                var pivotGo = new GameObject("PourPivot");
                pourPivot = pivotGo.transform;
                pourPivot.SetParent(root, false);
                pourPivot.localPosition = new Vector3(0f, -0.08f, -0.05f);
                pivotGo.AddComponent<WateringCanAimPivot>();
            }

            var pourVisual = pourPivot.Find("PourVisual");
            if (pourVisual == null)
            {
                var pourVisualGo = new GameObject("PourVisual");
                pourVisual = pourVisualGo.transform;
                pourVisual.SetParent(pourPivot, false);
                pourVisual.localPosition = Vector3.zero;
                pourVisual.localRotation = Quaternion.identity;
            }

            var meshFilter = root.GetComponent<MeshFilter>();
            var meshRenderer = root.GetComponent<MeshRenderer>();
            var meshChild = pourVisual.Find("Mesh");
            if (meshFilter != null && meshFilter.sharedMesh != null && meshChild == null)
            {
                var meshGo = new GameObject("Mesh");
                meshGo.transform.SetParent(pourVisual, false);
                meshGo.transform.localPosition = Vector3.zero;
                meshGo.transform.localRotation = Quaternion.identity;
                meshGo.transform.localScale = Vector3.one;
                meshGo.AddComponent<MeshFilter>().sharedMesh = meshFilter.sharedMesh;

                if (meshRenderer != null)
                {
                    var migratedRenderer = meshGo.AddComponent<MeshRenderer>();
                    migratedRenderer.sharedMaterials = meshRenderer.sharedMaterials;
                    Object.DestroyImmediate(meshRenderer);
                }

                Object.DestroyImmediate(meshFilter);
            }

            var legacyTip = root.Find("Tip");
            if (legacyTip != null && legacyTip.parent != pourVisual)
            {
                legacyTip.SetParent(pourVisual, true);
            }

            var aimPivot = pourPivot.GetComponent<WateringCanAimPivot>();
            if (aimPivot != null)
            {
                var soPivot = new SerializedObject(aimPivot);
                soPivot.FindProperty("pourVisual").objectReferenceValue = pourVisual;
                var tip = pourVisual.Find("Tip");
                if (tip != null)
                {
                    soPivot.FindProperty("tip").objectReferenceValue = tip;
                }

                soPivot.ApplyModifiedPropertiesWithoutUndo();
            }

            return pourVisual;
        }

        private static void ConfigureWaterParticles(ParticleSystem particles)
        {
            var main = particles.main;
            main.startLifetime = 0.4f;
            main.startSpeed = 2.5f;
            main.startSize = 0.06f;
            main.maxParticles = 64;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startColor = new Color(0.4f, 0.7f, 1f, 0.85f);

            var emission = particles.emission;
            emission.rateOverTime = 40f;

            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 12f;
            shape.radius = 0.02f;

            var renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
        }

        private static void CreateBushTest(Vector3 position)
        {
            var bush = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            bush.name = "WaterableBush_Test";
            bush.transform.position = position;
            bush.transform.localScale = Vector3.one * 0.25f;
            SetWaterableLayer(bush);
            bush.AddComponent<WaterableBushGrow>();
        }

        private static void CreateTreeTest(Vector3 position)
        {
            var tree = new GameObject("WaterableTree_Test");
            tree.transform.position = position;
            SetWaterableLayer(tree);

            var trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            trunk.name = "Trunk";
            trunk.transform.SetParent(tree.transform, false);
            trunk.transform.localScale = new Vector3(0.2f, 0.6f, 0.2f);
            trunk.transform.localPosition = new Vector3(0f, 0.6f, 0f);
            SetWaterableLayer(trunk);

            var foliage = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            foliage.name = "Foliage";
            foliage.transform.SetParent(tree.transform, false);
            foliage.transform.localScale = Vector3.one * 1.2f;
            foliage.transform.localPosition = new Vector3(0f, 1.4f, 0f);

            var foliageMat = AssetDatabase.LoadAssetAtPath<Material>(FoliageMaterialPath);
            if (foliageMat != null)
            {
                foliage.GetComponent<MeshRenderer>().sharedMaterial = foliageMat;
            }

            var reveal = tree.AddComponent<WaterableTreeFoliageReveal>();
            var so = new SerializedObject(reveal);
            so.FindProperty("foliageRenderer").objectReferenceValue = foliage.GetComponent<MeshRenderer>();
            so.ApplyModifiedPropertiesWithoutUndo();

            var trunkCol = trunk.GetComponent<Collider>();
            if (trunkCol != null)
            {
                trunkCol.isTrigger = false;
            }
        }

        private static void CreateGrassPatchTest(Vector3 position)
        {
            var patch = GameObject.CreatePrimitive(PrimitiveType.Plane);
            patch.name = "WaterableGrassPatch_Test";
            patch.transform.position = position;
            patch.transform.localScale = new Vector3(0.15f, 1f, 0.15f);
            SetWaterableLayer(patch);

            var col = patch.GetComponent<Collider>();
            if (col != null)
            {
                col.isTrigger = true;
            }

            var spawner = patch.AddComponent<WaterableGrassSprouter>();
            var grassPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(GrassBladePath);
            if (grassPrefab != null)
            {
                var so = new SerializedObject(spawner);
                so.FindProperty("grassPrefabs").arraySize = 1;
                so.FindProperty("grassPrefabs").GetArrayElementAtIndex(0).objectReferenceValue = grassPrefab;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static void SetWaterableLayer(GameObject go)
        {
            var layer = LayerMask.NameToLayer("Waterable");
            if (layer < 0)
            {
                return;
            }

            go.layer = layer;
        }
    }
}
#endif
