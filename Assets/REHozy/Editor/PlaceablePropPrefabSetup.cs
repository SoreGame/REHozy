#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using REHozy.CarryableTools;
using REHozy.Decoration;
using UnityEditor;
using UnityEngine;

namespace REHozy.EditorTools
{
    public static class PlaceablePropPrefabSetup
    {
        public const string PropsFolder = "Assets/REHozy/Prefabs/Props";
        private const string SetupMenuPath = "REHozy/Setup Placeable Props";

        [MenuItem(SetupMenuPath)]
        public static void SetupAllPropsMenu()
        {
            var count = SetupAllPropsInFolder();
            AssetDatabase.SaveAssets();
            Debug.Log($"Configured {count} placeable prop prefab(s) in {PropsFolder}.");
        }

        public static void ExecuteBatchSetup()
        {
            SetupAllPropsMenu();
            EditorApplication.Exit(0);
        }

        public static GameObject[] LoadAllPropPrefabs()
        {
            if (!AssetDatabase.IsValidFolder(PropsFolder))
            {
                return System.Array.Empty<GameObject>();
            }

            var guids = AssetDatabase.FindAssets("t:Prefab", new[] { PropsFolder });
            var prefabs = new List<GameObject>(guids.Length);

            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab != null)
                {
                    prefabs.Add(prefab);
                }
            }

            prefabs.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            return prefabs.ToArray();
        }

        public static int SetupAllPropsInFolder()
        {
            EnsurePlaceableLayer();

            if (!AssetDatabase.IsValidFolder(PropsFolder))
            {
                Debug.LogWarning($"Props folder not found: {PropsFolder}");
                return 0;
            }

            var count = 0;
            var guids = AssetDatabase.FindAssets("t:Prefab", new[] { PropsFolder });
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (SetupPropPrefabAtPath(path))
                {
                    count++;
                }
            }

            return count;
        }

        public static bool SetupPropPrefabAtPath(string path)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                ConfigurePlaceableProp(root);
                PrefabUtility.SaveAsPrefabAsset(root, path);
                return true;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        public static void EnsurePlaceableLayer()
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

        private static void ConfigurePlaceableProp(GameObject root)
        {
            var placeableLayer = LayerMask.NameToLayer("Placeable");
            if (placeableLayer < 0)
            {
                placeableLayer = 0;
            }

            root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            SetLayerRecursively(root, placeableLayer);

            var collider = EnsurePickupCollider(root);
            var pivot = EnsurePlacementPivot(root);

            var rb = root.GetComponent<Rigidbody>();
            if (rb == null)
            {
                rb = root.AddComponent<Rigidbody>();
            }

            rb.isKinematic = true;

            var carry = root.GetComponent<CarryableCarryDriver>();
            if (carry == null)
            {
                carry = root.AddComponent<CarryableCarryDriver>();
            }

            var soCarry = new SerializedObject(carry);
            soCarry.FindProperty("heightOffset").floatValue = ComputeCarryHeightOffset(root);
            soCarry.ApplyModifiedPropertiesWithoutUndo();

            var decoration = root.GetComponent<PlaceableDecoration>();
            if (decoration == null)
            {
                decoration = root.AddComponent<PlaceableDecoration>();
            }

            var soDecoration = new SerializedObject(decoration);
            soDecoration.FindProperty("pickupCollider").objectReferenceValue = collider;
            soDecoration.FindProperty("carryDriver").objectReferenceValue = carry;
            soDecoration.FindProperty("placementPivot").objectReferenceValue = pivot;
            soDecoration.FindProperty("groundSnapOffset").floatValue = 0f;
            soDecoration.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Transform EnsurePlacementPivot(GameObject root)
        {
            var existing = root.transform.Find("PlacementPivot");
            if (existing != null)
            {
                return existing;
            }

            var pivotGo = new GameObject("PlacementPivot");
            pivotGo.transform.SetParent(root.transform, false);
            pivotGo.transform.localPosition = Vector3.zero;
            return pivotGo.transform;
        }

        private static Collider EnsurePickupCollider(GameObject root)
        {
            var collider = root.GetComponent<Collider>();
            if (collider != null)
            {
                return collider;
            }

            collider = root.GetComponentInChildren<Collider>();
            if (collider != null)
            {
                return collider;
            }

            if (TryGetRendererBounds(root, out var bounds))
            {
                var box = root.AddComponent<BoxCollider>();
                box.center = root.transform.InverseTransformPoint(bounds.center);
                box.size = bounds.size;
                return box;
            }

            var fallback = root.AddComponent<BoxCollider>();
            fallback.size = Vector3.one;
            return fallback;
        }

        private static float ComputeCarryHeightOffset(GameObject root)
        {
            if (!TryGetRendererBounds(root, out var bounds))
            {
                return 0.5f;
            }

            return Mathf.Clamp(bounds.extents.y + 0.15f, 0.35f, 2.5f);
        }

        private static bool TryGetRendererBounds(GameObject root, out Bounds bounds)
        {
            bounds = default;
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            var hasBounds = false;

            for (var i = 0; i < renderers.Length; i++)
            {
                var renderer = renderers[i];
                if (renderer == null || renderer.gameObject.name == "PlacementPreview")
                {
                    continue;
                }

                if (!hasBounds)
                {
                    bounds = renderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            return hasBounds;
        }

        private static void SetLayerRecursively(GameObject root, int layer)
        {
            var transforms = root.GetComponentsInChildren<Transform>(true);
            for (var i = 0; i < transforms.Length; i++)
            {
                transforms[i].gameObject.layer = layer;
            }
        }
    }
}
#endif
