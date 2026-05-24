#if UNITY_EDITOR
using System.IO;
using REHozy.CarryableTools;
using REHozy.Decoration;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;

namespace REHozy.EditorTools
{
    public static class DecorationBoxSceneSetup
    {
        private const string MenuPath = "REHozy/Setup Decoration Box Test";
        private const string PrefabFolder = "Assets/REHozy/Prefabs/Decoration";

        private const string SampleScenePath = "Assets/Scenes/SampleScene.unity";

        [MenuItem(MenuPath)]
        public static void SetupInOpenScene()
        {
            SetupInOpenSceneInternal();
        }

        public static void SetupSampleSceneBatch()
        {
            if (File.Exists(SampleScenePath))
            {
                EditorSceneManager.OpenScene(SampleScenePath);
            }

            SetupInOpenSceneInternal();
        }

        private static void SetupInOpenSceneInternal()
        {
            EnsurePlaceableLayer();
            var placeableLayer = LayerMask.NameToLayer("Placeable");

            var prefabA = EnsureDecorationPrefab("Decoration_Test_Red", new Color(0.85f, 0.25f, 0.2f), placeableLayer);
            var prefabB = EnsureDecorationPrefab("Decoration_Test_Green", new Color(0.25f, 0.75f, 0.35f), placeableLayer);
            var prefabC = EnsureDecorationPrefab("Decoration_Test_Blue", new Color(0.25f, 0.45f, 0.9f), placeableLayer);

            var homePoint = GameObject.Find("HomePoint");
            if (homePoint == null)
            {
                Debug.LogWarning("HomePoint not found. Run harpoon setup or place HomePoint first.");
                return;
            }

            PropSpawnBox box;
            if (GameObject.Find("DecorationBox") != null)
            {
                box = GameObject.Find("DecorationBox").GetComponent<PropSpawnBox>();
            }
            else
            {
                box = CreateDecorationBox(homePoint.transform);
            }

            if (box != null)
            {
                var soBox = new SerializedObject(box);
                soBox.FindProperty("entries").arraySize = 3;
                SetEntry(soBox, 0, prefabA, 2);
                SetEntry(soBox, 1, prefabB, 2);
                SetEntry(soBox, 2, prefabC, 1);
                soBox.ApplyModifiedPropertiesWithoutUndo();
            }

            WireDecorationInput();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("Decoration box test objects created. Short LMB on box spawns a random prop.");
            AssetDatabase.SaveAssets();
        }

        private static void SetEntry(SerializedObject soBox, int index, GameObject prefab, int count)
        {
            var element = soBox.FindProperty("entries").GetArrayElementAtIndex(index);
            element.FindPropertyRelative("prefab").objectReferenceValue = prefab;
            element.FindPropertyRelative("count").intValue = count;
        }

        private static PropSpawnBox CreateDecorationBox(Transform homeParent)
        {
            var root = GameObject.CreatePrimitive(PrimitiveType.Cube);
            root.name = "DecorationBox";
            root.transform.SetParent(homeParent, false);
            root.transform.localPosition = new Vector3(2f, 0.35f, 2.5f);
            root.transform.localScale = new Vector3(0.8f, 0.7f, 0.8f);

            var anchor = new GameObject("SpawnAnchor").transform;
            anchor.SetParent(root.transform, false);
            anchor.localPosition = new Vector3(0f, 0.6f, 0f);

            var box = root.AddComponent<PropSpawnBox>();
            var so = new SerializedObject(box);
            so.FindProperty("spawnAnchor").objectReferenceValue = anchor;
            so.FindProperty("interactCollider").objectReferenceValue = root.GetComponent<BoxCollider>();
            so.ApplyModifiedPropertiesWithoutUndo();

            return box;
        }

        private static GameObject EnsureDecorationPrefab(string prefabName, Color color, int placeableLayer)
        {
            if (!AssetDatabase.IsValidFolder(PrefabFolder))
            {
                Directory.CreateDirectory(PrefabFolder);
                AssetDatabase.Refresh();
            }

            var path = $"{PrefabFolder}/{prefabName}.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null)
            {
                return existing;
            }

            var root = GameObject.CreatePrimitive(PrimitiveType.Cube);
            root.name = prefabName;
            root.layer = placeableLayer;
            root.transform.localScale = Vector3.one * 0.35f;

            var renderer = root.GetComponent<Renderer>();
            if (renderer != null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit")
                    ?? Shader.Find("Standard");
                renderer.sharedMaterial = new Material(shader) { color = color };
            }

            var pivot = new GameObject("PlacementPivot").transform;
            pivot.SetParent(root.transform, false);
            pivot.localPosition = new Vector3(0f, -0.5f, 0f);

            var carry = root.AddComponent<CarryableCarryDriver>();
            var decoration = root.AddComponent<PlaceableDecoration>();
            root.AddComponent<Rigidbody>().isKinematic = true;

            var cam = UnityEngine.Camera.main;
            if (cam != null)
            {
                var soCarry = new SerializedObject(carry);
                soCarry.FindProperty("targetCamera").objectReferenceValue = cam;
                soCarry.FindProperty("heightOffset").floatValue = 0.4f;
                soCarry.ApplyModifiedPropertiesWithoutUndo();
            }

            var soDecoration = new SerializedObject(decoration);
            soDecoration.FindProperty("pickupCollider").objectReferenceValue = root.GetComponent<BoxCollider>();
            soDecoration.FindProperty("carryDriver").objectReferenceValue = carry;
            soDecoration.FindProperty("placementPivot").objectReferenceValue = pivot;
            soDecoration.FindProperty("groundSnapOffset").floatValue = 0f;
            soDecoration.ApplyModifiedPropertiesWithoutUndo();

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static void WireDecorationInput()
        {
            var gameplay = Object.FindFirstObjectByType<CarryableToolInputHandler>();
            if (gameplay == null)
            {
                Debug.LogWarning("CarryableToolInputHandler not found. Add HarpoonGameplay first.");
                return;
            }

            var handler = gameplay.GetComponent<DecorationInputHandler>();
            if (handler == null)
            {
                handler = gameplay.gameObject.AddComponent<DecorationInputHandler>();
            }

            var attackRef = AssetDatabase.LoadAssetAtPath<InputActionReference>(
                "Assets/REHozy/Settings/InputAttack.asset");
            var actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(
                "Assets/InputSystem_Actions.inputactions");

            var placeableMask = 1 << LayerMask.NameToLayer("Placeable");
            var defaultMask = 1 << 0;
            var interactionMask = placeableMask | defaultMask;

            var so = new SerializedObject(handler);
            so.FindProperty("rayCamera").objectReferenceValue = UnityEngine.Camera.main;
            so.FindProperty("attackAction").objectReferenceValue = attackRef;
            so.FindProperty("inputActionsFallback").objectReferenceValue = actions;
            so.FindProperty("interactionMask").intValue = interactionMask;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void EnsurePlaceableLayer()
        {
            if (LayerMask.NameToLayer("Placeable") >= 0)
            {
                return;
            }

            var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
            if (assets == null || assets.Length == 0)
            {
                return;
            }

            var tagManager = new SerializedObject(assets[0]);
            var layers = tagManager.FindProperty("layers");
            for (var i = 8; i < 32; i++)
            {
                var slot = layers.GetArrayElementAtIndex(i);
                if (!string.IsNullOrEmpty(slot.stringValue))
                {
                    continue;
                }

                slot.stringValue = "Placeable";
                tagManager.ApplyModifiedProperties();
                Debug.Log("Added layer 'Placeable'.");
                return;
            }

            Debug.LogWarning("Could not add Placeable layer — no free user layers.");
        }
    }
}
#endif
