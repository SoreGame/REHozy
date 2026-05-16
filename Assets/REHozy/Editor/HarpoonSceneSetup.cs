#if UNITY_EDITOR
using REHozy.Harpoon;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;

namespace REHozy.EditorTools
{
    public static class HarpoonSceneSetup
    {
        private const string MenuPath = "REHozy/Setup Harpoon Test Objects";

        [MenuItem(MenuPath)]
        public static void SetupInOpenScene()
        {
            if (Object.FindFirstObjectByType<HarpoonController>() != null)
            {
                Debug.Log("Harpoon test objects already exist in the open scene.");
                return;
            }

            var bridgeY = -5.5f;
            var harpoonRoot = CreateHarpoon(new Vector3(-76f, bridgeY + 0.6f, -11f));
            CreateMountable(new Vector3(-74f, bridgeY + 0.35f, -9.5f));
            CreateTrashBin(new Vector3(-72f, bridgeY, -12f));
            CreateGameplay(harpoonRoot);

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("Harpoon test objects created.");
        }

        private static GameObject CreateHarpoon(Vector3 position)
        {
            var root = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            root.name = "Harpoon";
            Object.DestroyImmediate(root.GetComponent<CapsuleCollider>());

            root.transform.position = position;
            root.transform.localScale = new Vector3(0.2f, 0.6f, 0.2f);

            var pickup = root.AddComponent<CapsuleCollider>();
            pickup.direction = 1;
            pickup.height = 2f;
            pickup.radius = 0.5f;

            var tip = new GameObject("Tip").transform;
            tip.SetParent(root.transform, false);
            tip.localPosition = new Vector3(0f, -1f, 0f);

            var socket = new GameObject("MountSocket").transform;
            socket.SetParent(root.transform, false);
            socket.localPosition = new Vector3(0f, -0.85f, 0f);

            var carry = root.AddComponent<HarpoonCarryDriver>();
            var controller = root.AddComponent<HarpoonController>();

            var cam = UnityEngine.Camera.main;
            if (cam != null)
            {
                var soCarry = new SerializedObject(carry);
                soCarry.FindProperty("targetCamera").objectReferenceValue = cam;
                soCarry.FindProperty("heightOffset").floatValue = 0.45f;
                soCarry.FindProperty("tipForwardAxis").vector3Value = Vector3.down;
                soCarry.ApplyModifiedPropertiesWithoutUndo();
            }

            var so = new SerializedObject(controller);
            so.FindProperty("tip").objectReferenceValue = tip;
            so.FindProperty("mountSocket").objectReferenceValue = socket;
            so.FindProperty("carryDriver").objectReferenceValue = carry;
            so.FindProperty("pickupCollider").objectReferenceValue = pickup;
            so.ApplyModifiedPropertiesWithoutUndo();

            return root;
        }

        private static void CreateMountable(Vector3 position)
        {
            var item = GameObject.CreatePrimitive(PrimitiveType.Cube);
            item.name = "MountableItem_Test";
            item.transform.position = position;
            item.transform.localScale = Vector3.one * 0.35f;
            item.AddComponent<HarpoonMountableItem>();
        }

        private static void CreateTrashBin(Vector3 position)
        {
            var bin = new GameObject("TrashBin");
            bin.transform.position = position;
            var col = bin.AddComponent<BoxCollider>();
            col.isTrigger = true;
            col.size = new Vector3(1.2f, 1f, 1.2f);
            col.center = new Vector3(0f, 0.5f, 0f);
            bin.AddComponent<HarpoonTrashBin>();
        }

        private static void CreateGameplay(GameObject harpoon)
        {
            var go = new GameObject("HarpoonGameplay");
            var input = go.AddComponent<HarpoonInputHandler>();
            var reticle = go.AddComponent<HarpoonAimReticleUI>();

            var attackRef = AssetDatabase.LoadAssetAtPath<InputActionReference>(
                "Assets/REHozy/Settings/InputAttack.asset");
            var actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(
                "Assets/InputSystem_Actions.inputactions");

            var so = new SerializedObject(input);
            so.FindProperty("harpoon").objectReferenceValue = harpoon.GetComponent<HarpoonController>();
            so.FindProperty("rayCamera").objectReferenceValue = UnityEngine.Camera.main;
            so.FindProperty("attackAction").objectReferenceValue = attackRef;
            so.FindProperty("inputActionsFallback").objectReferenceValue = actions;
            so.ApplyModifiedPropertiesWithoutUndo();

            var soReticle = new SerializedObject(reticle);
            soReticle.FindProperty("harpoon").objectReferenceValue = harpoon.GetComponent<HarpoonController>();
            soReticle.FindProperty("carryDriver").objectReferenceValue = harpoon.GetComponent<HarpoonCarryDriver>();
            soReticle.FindProperty("worldCamera").objectReferenceValue = UnityEngine.Camera.main;
            soReticle.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
#endif
