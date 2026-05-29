#if UNITY_EDITOR
using REHozy.CarryableTools;
using REHozy.Harpoon;
using REHozy.Torch;
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
            if (Object.FindFirstObjectByType<CarryableToolCore>() != null)
            {
                Debug.Log("Carryable tool test objects already exist in the open scene.");
                return;
            }

            var bridgeY = -5.5f;
            var homeCollider = CreateHomeZone(new Vector3(-72f, bridgeY, -12f));
            var harpoonRoot = CreateHarpoon(new Vector3(-76f, bridgeY + 0.6f, -11f));
            CreateMountable(new Vector3(-74f, bridgeY + 0.35f, -9.5f));
            CreateGameplay(harpoonRoot, homeCollider);

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("Harpoon / carryable tool test objects created.");
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

            var carry = root.AddComponent<CarryableCarryDriver>();
            var core = root.AddComponent<CarryableToolCore>();
            root.AddComponent<HarpoonToolActions>();

            var cam = UnityEngine.Camera.main;
            if (cam != null)
            {
                var soCarry = new SerializedObject(carry);
                soCarry.FindProperty("targetCamera").objectReferenceValue = cam;
                soCarry.FindProperty("heightOffset").floatValue = 0.45f;
                soCarry.FindProperty("tipForwardAxis").vector3Value = Vector3.down;
                soCarry.ApplyModifiedPropertiesWithoutUndo();
            }

            var so = new SerializedObject(core);
            so.FindProperty("toolModeId").enumValueIndex = (int)PlayerToolMode.Harpoon;
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

        private static void CreateGameplay(GameObject harpoon, Collider homeCollider)
        {
            var go = new GameObject("ToolGameplay");
            var input = go.AddComponent<CarryableToolInputHandler>();
            var reticle = go.AddComponent<CarryableAimReticleUI>();
            var returnHoldUi = go.AddComponent<CarryableReturnHoldUI>();
            go.AddComponent<TorchFuelProgressUI>();

            var attackRef = AssetDatabase.LoadAssetAtPath<InputActionReference>(
                "Assets/REHozy/Settings/InputAttack.asset");
            var actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(
                "Assets/InputSystem_Actions.inputactions");

            var core = harpoon.GetComponent<CarryableToolCore>();
            var carry = harpoon.GetComponent<CarryableCarryDriver>();

            var soCore = new SerializedObject(core);
            soCore.FindProperty("homeZone").objectReferenceValue = homeCollider;
            soCore.ApplyModifiedPropertiesWithoutUndo();

            var so = new SerializedObject(input);
            so.FindProperty("tool").objectReferenceValue = core;
            so.FindProperty("rayCamera").objectReferenceValue = UnityEngine.Camera.main;
            so.FindProperty("attackAction").objectReferenceValue = attackRef;
            so.FindProperty("inputActionsFallback").objectReferenceValue = actions;
            so.ApplyModifiedPropertiesWithoutUndo();

            var soReticle = new SerializedObject(reticle);
            soReticle.FindProperty("tool").objectReferenceValue = core;
            soReticle.FindProperty("carryDriver").objectReferenceValue = carry;
            soReticle.FindProperty("worldCamera").objectReferenceValue = UnityEngine.Camera.main;
            soReticle.ApplyModifiedPropertiesWithoutUndo();

            var soReturnHold = new SerializedObject(returnHoldUi);
            soReturnHold.FindProperty("tool").objectReferenceValue = core;
            soReturnHold.FindProperty("inputHandler").objectReferenceValue = input;
            soReturnHold.FindProperty("worldCamera").objectReferenceValue = UnityEngine.Camera.main;
            soReturnHold.ApplyModifiedPropertiesWithoutUndo();

            PlayerToolModeState.Active = PlayerToolMode.Harpoon;
        }
    }
}
#endif
