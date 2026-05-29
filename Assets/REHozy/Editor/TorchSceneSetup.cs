#if UNITY_EDITOR
using REHozy.CarryableTools;
using REHozy.Rendering;
using REHozy.Torch;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace REHozy.EditorTools
{
    public static class TorchSceneSetup
    {
        private const string TorchPrefabPath = "Assets/REHozy/Prefabs/Torch.prefab";
        private const string StaticTorchPrefabPath = "Assets/REHozy/Prefabs/StaticTorch.prefab";
        private const string TorchLightVfxPath = "Assets/VFXPACK_FIRE_WALLCOEUR/Prefab/VFX_TorchLight.prefab";
        private const string MenuPath = "REHozy/Setup Torch Test Objects";
        private const string BuildPrefabsPath = "REHozy/Build Torch Prefabs";
        private const string WireInputPath = "REHozy/Wire Tool Input To Torch";

        [MenuItem(BuildPrefabsPath)]
        public static void BuildTorchPrefabs()
        {
            EnsureTorchPrefabAsset();
            EnsureStaticTorchPrefabAsset();
            AssetDatabase.SaveAssets();
            Debug.Log("Torch prefabs built.");
        }

        [MenuItem(MenuPath)]
        public static void SetupTorchTestObjects()
        {
            var bridgeY = -5.5f;
            var torchPosition = new Vector3(-76f, bridgeY + 0.6f, -9f);

            EnsureTorchPrefabAsset();
            EnsureStaticTorchPrefabAsset();

            if (GameObject.Find("Torch") == null)
            {
                CreateTorchInScene(torchPosition);
            }

            EnsureCampfireIgnitionSource();
            EnsureStaticTorchesInScene(GetCampfireWorldPosition());

            WireToolInputToTorch();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("Torch test objects ready. Play mode uses Torch tool (input wired).");
        }

        [MenuItem(WireInputPath)]
        public static void WireToolInputToTorch()
        {
            var torch = GameObject.Find("Torch");
            if (torch == null)
            {
                Debug.LogWarning("Torch not found. Run REHozy → Setup Torch Test Objects first.");
                return;
            }

            WireToolInputTo(torch.GetComponent<CarryableToolCore>(), PlayerToolMode.Torch);
        }

        [MenuItem("REHozy/Ensure Torch Fuel UI")]
        public static void EnsureTorchFuelUiInScene()
        {
            var input = Object.FindFirstObjectByType<CarryableToolInputHandler>();
            if (input == null)
            {
                Debug.LogWarning("CarryableToolInputHandler not found.");
                return;
            }

            var torchCore = Object.FindFirstObjectByType<CarryableToolCore>();
            if (torchCore == null || torchCore.ToolModeId != PlayerToolMode.Torch)
            {
                foreach (var core in Object.FindObjectsByType<CarryableToolCore>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                {
                    if (core.ToolModeId == PlayerToolMode.Torch)
                    {
                        torchCore = core;
                        break;
                    }
                }
            }

            var fuelUi = input.GetComponent<TorchFuelProgressUI>();
            if (fuelUi == null)
            {
                fuelUi = input.gameObject.AddComponent<TorchFuelProgressUI>();
            }

            if (torchCore != null)
            {
                fuelUi.BindToTool(torchCore);
            }

            WireToolInputToTorch();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("Torch fuel UI ensured on ToolGameplay.");
        }

        [MenuItem("REHozy/Ensure Torch Map Outline")]
        public static void EnsureTorchMapOutlineInScene()
        {
            WireToolInputToTorch();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("Torch map outline controller ensured on ToolGameplay.");
        }

        [MenuItem("REHozy/Fix Torch Carry Hierarchy")]
        public static void FixTorchCarryHierarchyInScene()
        {
            var fixedCount = 0;
            foreach (var core in Object.FindObjectsByType<CarryableToolCore>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (core.ToolModeId != PlayerToolMode.Torch)
                {
                    continue;
                }

                if (FixTorchCarryHierarchy(core.gameObject))
                {
                    fixedCount++;
                }
            }

            if (fixedCount > 0)
            {
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            }

            Debug.Log($"Fixed torch carry hierarchy on {fixedCount} object(s).");
        }

        private static bool FixTorchCarryHierarchy(GameObject root)
        {
            var core = root.GetComponent<CarryableToolCore>();
            var aimPivot = root.GetComponentInChildren<TorchAimPivot>(true);
            if (core == null || aimPivot == null)
            {
                return false;
            }

            var visualPivot = aimPivot.transform;
            var aimGroup = EnsureAimGroup(visualPivot);
            var mesh = aimGroup.Find("Mesh") ?? visualPivot.Find("Mesh");
            var visualTip = aimGroup.Find("Tip") ?? visualPivot.Find("Tip");
            if (mesh == null || visualTip == null)
            {
                return false;
            }

            if (mesh.parent != aimGroup)
            {
                mesh.SetParent(aimGroup, false);
            }

            mesh.localPosition = new Vector3(0f, TorchLayout.MeshCenterOffset, 0f);

            if (visualTip.parent != aimGroup)
            {
                visualTip.SetParent(aimGroup, false);
            }

            visualTip.localPosition = new Vector3(0f, TorchLayout.FlameTipOffset, 0f);

            var carryTip = root.transform.Find("CarryTip");
            if (carryTip == null)
            {
                var go = new GameObject("CarryTip");
                carryTip = go.transform;
                carryTip.SetParent(root.transform, false);
            }

            carryTip.localPosition = new Vector3(0f, TorchLayout.CarryTipHeight, 0f);

            var carry = root.GetComponent<CarryableCarryDriver>();
            if (carry != null)
            {
                var soCarryFix = new SerializedObject(carry);
                soCarryFix.FindProperty("clampTipAboveWater").boolValue = true;
                soCarryFix.FindProperty("waterTipClearance").floatValue = WaterCarryClamp.DefaultTipClearance;
                soCarryFix.ApplyModifiedPropertiesWithoutUndo();
            }

            var soCore = new SerializedObject(core);
            soCore.FindProperty("tip").objectReferenceValue = carryTip;
            soCore.ApplyModifiedPropertiesWithoutUndo();

            var soAim = new SerializedObject(aimPivot);
            soAim.FindProperty("aimVisual").objectReferenceValue = aimGroup;
            soAim.FindProperty("tip").objectReferenceValue = visualTip;
            soAim.ApplyModifiedPropertiesWithoutUndo();

            return true;
        }

        private static Transform EnsureAimGroup(Transform visualPivot)
        {
            var aimGroup = visualPivot.Find("AimGroup");
            if (aimGroup == null)
            {
                var go = new GameObject("AimGroup");
                aimGroup = go.transform;
                aimGroup.SetParent(visualPivot, false);
            }

            aimGroup.localPosition = new Vector3(0f, TorchLayout.AimGroupBaseOffset, 0f);
            aimGroup.localRotation = Quaternion.identity;
            aimGroup.localScale = Vector3.one;
            return aimGroup;
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
            var torchFuelUi = Object.FindFirstObjectByType<TorchFuelProgressUI>();
            var gameplay = input != null ? input.gameObject : null;

            if (input == null)
            {
                Debug.LogWarning("CarryableToolInputHandler not found. Add ToolGameplay first.");
                return;
            }

            EnsureModeBootstrap(gameplay, mode);

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

            if (torchFuelUi == null && gameplay != null)
            {
                torchFuelUi = gameplay.AddComponent<TorchFuelProgressUI>();
            }

            if (torchFuelUi != null)
            {
                var soFuel = new SerializedObject(torchFuelUi);
                soFuel.FindProperty("torchActions").objectReferenceValue = core.GetComponent<TorchToolActions>();
                soFuel.ApplyModifiedPropertiesWithoutUndo();
                torchFuelUi.BindToTool(core);
            }

            if (mode == PlayerToolMode.Torch && gameplay != null)
            {
                var outlineController = gameplay.GetComponent<TorchMapOutlineController>();
                if (outlineController == null)
                {
                    outlineController = gameplay.AddComponent<TorchMapOutlineController>();
                }

                outlineController.BindToTool(core);
            }

            PlayerToolModeState.Active = mode;
            input.RefreshToolBinding();
            Debug.Log($"ToolGameplay wired to {mode}.");
        }

        private static void EnsureModeBootstrap(GameObject gameplayRoot, PlayerToolMode mode)
        {
            if (gameplayRoot == null)
            {
                return;
            }

            var bootstrap = gameplayRoot.GetComponent<CarryableToolModeBootstrap>();
            if (bootstrap == null)
            {
                bootstrap = gameplayRoot.AddComponent<CarryableToolModeBootstrap>();
            }

            var so = new SerializedObject(bootstrap);
            so.FindProperty("activeModeOnPlay").enumValueIndex = (int)mode;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void EnsureTorchPrefabAsset()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(TorchPrefabPath) != null)
            {
                return;
            }

            if (!AssetDatabase.IsValidFolder("Assets/REHozy/Prefabs"))
            {
                AssetDatabase.CreateFolder("Assets/REHozy", "Prefabs");
            }

            var torch = BuildTorchGameObject("Torch");
            PrefabUtility.SaveAsPrefabAsset(torch, TorchPrefabPath);
            Object.DestroyImmediate(torch);
            Debug.Log($"Saved {TorchPrefabPath}");
        }

        private static void EnsureStaticTorchPrefabAsset()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(StaticTorchPrefabPath) != null)
            {
                return;
            }

            if (!AssetDatabase.IsValidFolder("Assets/REHozy/Prefabs"))
            {
                AssetDatabase.CreateFolder("Assets/REHozy", "Prefabs");
            }

            var staticTorch = BuildStaticTorchGameObject("StaticTorch");
            PrefabUtility.SaveAsPrefabAsset(staticTorch, StaticTorchPrefabPath);
            Object.DestroyImmediate(staticTorch);
            Debug.Log($"Saved {StaticTorchPrefabPath}");
        }

        private static void CreateTorchInScene(Vector3 position)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(TorchPrefabPath);
            GameObject torch;
            if (prefab != null)
            {
                torch = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                torch.name = "Torch";
            }
            else
            {
                torch = BuildTorchGameObject("Torch");
            }

            torch.transform.position = position;
        }

        private static GameObject BuildTorchGameObject(string name)
        {
            var root = new GameObject(name);
            var pickup = root.AddComponent<CapsuleCollider>();
            pickup.direction = 1;
            pickup.height = 2f;
            pickup.radius = 0.2f;
            pickup.center = Vector3.zero;

            var carry = root.AddComponent<CarryableCarryDriver>();
            var core = root.AddComponent<CarryableToolCore>();
            root.AddComponent<TorchToolActions>();

            var visualPivotGo = new GameObject("VisualPivot");
            visualPivotGo.transform.SetParent(root.transform, false);
            var aimPivot = visualPivotGo.AddComponent<TorchAimPivot>();

            var aimGroupGo = new GameObject("AimGroup");
            aimGroupGo.transform.SetParent(visualPivotGo.transform, false);
            aimGroupGo.transform.localPosition = new Vector3(0f, TorchLayout.AimGroupBaseOffset, 0f);

            var meshGo = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            meshGo.name = "Mesh";
            Object.DestroyImmediate(meshGo.GetComponent<CapsuleCollider>());
            meshGo.transform.SetParent(aimGroupGo.transform, false);
            meshGo.transform.localPosition = new Vector3(0f, TorchLayout.MeshCenterOffset, 0f);
            meshGo.transform.localScale = new Vector3(0.18f, TorchLayout.MeshScaleY, 0.18f);

            var carryTip = new GameObject("CarryTip").transform;
            carryTip.SetParent(root.transform, false);
            carryTip.localPosition = new Vector3(0f, TorchLayout.CarryTipHeight, 0f);

            var tip = new GameObject("Tip").transform;
            tip.SetParent(aimGroupGo.transform, false);
            tip.localPosition = new Vector3(0f, TorchLayout.FlameTipOffset, 0f);

            var flameSlot = new GameObject("FlameSlot").transform;
            flameSlot.SetParent(tip, false);
            flameSlot.localPosition = Vector3.zero;
            AttachTorchLightVfx(flameSlot.gameObject);

            var cam = UnityEngine.Camera.main;
            var soCarry = new SerializedObject(carry);
            soCarry.FindProperty("heightOffset").floatValue = 0.45f;
            soCarry.FindProperty("tipForwardAxis").vector3Value = Vector3.up;
            soCarry.FindProperty("clampTipAboveWater").boolValue = true;
            soCarry.FindProperty("waterTipClearance").floatValue = WaterCarryClamp.DefaultTipClearance;
            if (cam != null)
            {
                soCarry.FindProperty("targetCamera").objectReferenceValue = cam;
            }

            soCarry.ApplyModifiedPropertiesWithoutUndo();

            var soCore = new SerializedObject(core);
            soCore.FindProperty("toolModeId").enumValueIndex = (int)PlayerToolMode.Torch;
            soCore.FindProperty("tip").objectReferenceValue = carryTip;
            soCore.FindProperty("carryDriver").objectReferenceValue = carry;
            soCore.FindProperty("pickupCollider").objectReferenceValue = pickup;
            soCore.ApplyModifiedPropertiesWithoutUndo();

            var soAim = new SerializedObject(aimPivot);
            soAim.FindProperty("aimVisual").objectReferenceValue = aimGroupGo.transform;
            soAim.FindProperty("tip").objectReferenceValue = tip;
            soAim.ApplyModifiedPropertiesWithoutUndo();

            var actions = root.GetComponent<TorchToolActions>();
            var presenter = flameSlot.GetComponent<TorchFlamePresenter>();
            if (actions != null && presenter != null)
            {
                var soActions = new SerializedObject(actions);
                soActions.FindProperty("aimPivot").objectReferenceValue = aimPivot;
                soActions.FindProperty("flamePresenter").objectReferenceValue = presenter;
                soActions.ApplyModifiedPropertiesWithoutUndo();
            }

            return root;
        }

        private static GameObject BuildStaticTorchGameObject(string name)
        {
            var root = new GameObject(name);
            var body = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            body.name = "Bracket";
            Object.DestroyImmediate(body.GetComponent<Collider>());
            body.transform.SetParent(root.transform, false);
            body.transform.localScale = new Vector3(0.12f, 0.35f, 0.12f);
            body.transform.localPosition = new Vector3(0f, 0.2f, 0f);

            var flamePoint = new GameObject("FlamePoint").transform;
            flamePoint.SetParent(root.transform, false);
            flamePoint.localPosition = new Vector3(0f, 0.42f, 0.15f);

            var flameSlot = new GameObject("FlameSlot");
            flameSlot.transform.SetParent(flamePoint, false);
            AttachTorchLightVfx(flameSlot);

            var presenter = flameSlot.AddComponent<TorchFlamePresenter>();
            var igniteTrigger = root.AddComponent<SphereCollider>();
            igniteTrigger.isTrigger = true;
            igniteTrigger.radius = 1.1f;
            igniteTrigger.center = new Vector3(0f, 0.42f, 0.15f);

            var staticTorch = root.AddComponent<StaticTorch>();
            root.AddComponent<ObjectOutlineHighlight>();

            var soStatic = new SerializedObject(staticTorch);
            soStatic.FindProperty("flamePoint").objectReferenceValue = flamePoint;
            soStatic.FindProperty("flamePresenter").objectReferenceValue = presenter;
            soStatic.FindProperty("igniteSpeedMultiplier").floatValue = 1.5f;
            soStatic.ApplyModifiedPropertiesWithoutUndo();

            return root;
        }

        private static void AttachTorchLightVfx(GameObject parent)
        {
            var vfxPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(TorchLightVfxPath);
            if (vfxPrefab == null)
            {
                Debug.LogWarning($"VFX prefab not found: {TorchLightVfxPath}");
                return;
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(vfxPrefab, parent.transform);
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.SetActive(false);

            var presenter = parent.GetComponent<TorchFlamePresenter>();
            if (presenter == null)
            {
                presenter = parent.AddComponent<TorchFlamePresenter>();
            }
        }

        private static void EnsureCampfireIgnitionSource()
        {
            EnsureIgnitionOnCampfirePrefabs();

            var linked = 0;
            foreach (var campfire in FindCampfireProps())
            {
                LinkCampfireAndVfx(campfire);
                linked++;
            }

            if (linked == 0)
            {
                EnsureIgnitionOnVfxFireOnly();
            }
        }

        private static GameObject[] FindCampfireProps()
        {
            var results = new System.Collections.Generic.List<GameObject>();
            foreach (var go in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
            {
                if (go == null)
                {
                    continue;
                }

                var name = go.name;
                if (name.Contains("Campfire") || name.Contains("campfire"))
                {
                    results.Add(go.gameObject);
                }
            }

            return results.ToArray();
        }

        private static void LinkCampfireAndVfx(GameObject campfire)
        {
            GameObjectUtility.RemoveMonoBehavioursWithMissingScript(campfire);

            var link = campfire.GetComponent<CampfireIgnitionLink>();
            if (link == null)
            {
                link = campfire.AddComponent<CampfireIgnitionLink>();
            }

            var vfx = FindNearestVfxFire(campfire.transform.position);
            if (vfx != null && vfx.transform.parent != campfire.transform)
            {
                vfx.transform.SetParent(campfire.transform, true);
                vfx.name = "VFX_Fire";
            }

            link.AutoWire(addMissingComponents: true);
            EditorUtility.SetDirty(campfire);
        }

        private static GameObject FindNearestVfxFire(Vector3 from)
        {
            GameObject best = null;
            var bestSqr = 16f;

            foreach (var ps in Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None))
            {
                if (ps == null || !ps.gameObject.name.Contains("VFX_Fire"))
                {
                    continue;
                }

                var sqr = (ps.transform.position - from).sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    best = ps.gameObject;
                }
            }

            return best;
        }

        private static void EnsureIgnitionOnVfxFireOnly()
        {
            var fire = GameObject.Find("VFX_Fire 1 (1)") ?? GameObject.Find("VFX_Fire 1");
            if (fire == null)
            {
                fire = FindNearestVfxFire(Vector3.zero);
            }

            if (fire == null)
            {
                Debug.LogWarning("Campfire prop and VFX_Fire not found in scene.");
                return;
            }

            var root = PrefabUtility.GetNearestPrefabInstanceRoot(fire) ?? fire;
            GameObjectUtility.RemoveMonoBehavioursWithMissingScript(root);

            if (root.GetComponent<TorchIgnitionSource>() == null && root.GetComponent<CampfireIgnitionLink>() == null)
            {
                AddIgnitionComponents(root);
            }
        }

        private static void EnsureIgnitionOnCampfirePrefabs()
        {
            var guids = AssetDatabase.FindAssets("VFX_Fire t:Prefab", new[] { "Assets/VFXPACK_FIRE_WALLCOEUR/Prefab" });
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var root = PrefabUtility.LoadPrefabContents(path);
                if (root == null)
                {
                    continue;
                }

                try
                {
                    GameObjectUtility.RemoveMonoBehavioursWithMissingScript(root);
                    if (root.GetComponentInChildren<TorchIgnitionSource>(true) != null)
                    {
                        continue;
                    }

                    AddIgnitionComponents(root);
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    Debug.Log($"Added TorchIgnitionSource to prefab: {path}");
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }

            AssetDatabase.SaveAssets();
        }

        private static void AddIgnitionComponents(GameObject fireRoot)
        {
            var source = fireRoot.GetComponent<TorchIgnitionSource>();
            if (source == null)
            {
                source = fireRoot.AddComponent<TorchIgnitionSource>();
            }

            source.Configure(fireRoot.transform, 1.75f, 4f);

            var trigger = fireRoot.GetComponent<SphereCollider>();
            if (trigger == null)
            {
                trigger = fireRoot.AddComponent<SphereCollider>();
            }

            trigger.isTrigger = true;
            trigger.radius = 1.75f;
            trigger.center = new Vector3(0f, 0.5f, 0f);
        }

        [MenuItem("REHozy/Link Campfire Prop And Fire VFX")]
        public static void LinkCampfirePropAndFireVfx()
        {
            EnsureCampfireIgnitionSource();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("Campfire mesh linked to VFX_Fire; TorchIgnitionSource on NF_Prop_Campfire.");
        }

        [MenuItem("REHozy/Fix Broken Campfire References")]
        public static void FixBrokenCampfireReferences()
        {
            foreach (var campfire in FindCampfireProps())
            {
                GameObjectUtility.RemoveMonoBehavioursWithMissingScript(campfire);
            }

            foreach (var fire in Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None))
            {
                if (!fire.gameObject.name.Contains("VFX_Fire"))
                {
                    continue;
                }

                var root = PrefabUtility.GetNearestPrefabInstanceRoot(fire.gameObject) ?? fire.gameObject;
                GameObjectUtility.RemoveMonoBehavioursWithMissingScript(root);
            }

            EnsureIgnitionOnCampfirePrefabs();
            EnsureCampfireIgnitionSource();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("Campfire + VFX cleaned up and ignition re-linked.");
        }

        [MenuItem("REHozy/Ensure Campfire Ignition Sources")]
        public static void EnsureCampfireIgnitionSourcesMenu()
        {
            EnsureIgnitionOnCampfirePrefabs();
            EnsureCampfireIgnitionSource();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("Campfire ignition sources ensured on prefabs and scene instances.");
        }

        private static Vector3 GetCampfireWorldPosition()
        {
            var fire = GameObject.Find("VFX_Fire 1 (1)") ?? GameObject.Find("VFX_Fire 1");
            if (fire != null)
            {
                return fire.transform.position;
            }

            return new Vector3(10.16617f, -3.19f, 6.2f);
        }

        private static void EnsureStaticTorchesInScene(Vector3 campfireApprox)
        {
            var offsets = new[]
            {
                new Vector3(2.5f, 0f, 0f),
                new Vector3(-2f, 0f, 1.5f),
                new Vector3(0.5f, 0f, -2.5f)
            };

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(StaticTorchPrefabPath);
            for (var i = 0; i < offsets.Length; i++)
            {
                var name = $"StaticTorch_{i + 1}";
                if (GameObject.Find(name) != null)
                {
                    continue;
                }

                GameObject instance;
                if (prefab != null)
                {
                    instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                    instance.name = name;
                }
                else
                {
                    instance = BuildStaticTorchGameObject(name);
                }

                instance.transform.position = campfireApprox + offsets[i];

                if (i == 0)
                {
                    var staticTorch = instance.GetComponent<StaticTorch>();
                    if (staticTorch != null)
                    {
                        var so = new SerializedObject(staticTorch);
                        so.FindProperty("startLit").boolValue = true;
                        so.ApplyModifiedPropertiesWithoutUndo();
                    }
                }
            }
        }
    }
}
#endif
