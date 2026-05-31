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
        private const int DefaultPropCountPerEntry = 1;

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
            PlaceablePropPrefabSetup.EnsurePlaceableLayer();
            PlaceablePropPrefabSetup.SetupAllPropsInFolder();
            var propPrefabs = PlaceablePropPrefabSetup.LoadAllPropPrefabs();

            if (propPrefabs.Length == 0)
            {
                Debug.LogWarning($"No prop prefabs found in {PlaceablePropPrefabSetup.PropsFolder}.");
                return;
            }

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
                soBox.FindProperty("entries").arraySize = propPrefabs.Length;
                for (var i = 0; i < propPrefabs.Length; i++)
                {
                    SetEntry(soBox, i, propPrefabs[i], DefaultPropCountPerEntry);
                }

                soBox.ApplyModifiedPropertiesWithoutUndo();
            }

            WireDecorationInput();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log(
                $"Decoration box configured with {propPrefabs.Length} prop prefab(s) from {PlaceablePropPrefabSetup.PropsFolder}. Short LMB on box spawns a random prop.");
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
            var waterableMask = LayerMask.NameToLayer("Waterable") >= 0
                ? 1 << LayerMask.NameToLayer("Waterable")
                : 0;
            var defaultMask = 1 << 0;
            var interactionMask = placeableMask | waterableMask | defaultMask;

            var so = new SerializedObject(handler);
            so.FindProperty("rayCamera").objectReferenceValue = UnityEngine.Camera.main;
            so.FindProperty("attackAction").objectReferenceValue = attackRef;
            so.FindProperty("inputActionsFallback").objectReferenceValue = actions;
            so.FindProperty("interactionMask").intValue = interactionMask;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

    }
}
#endif
