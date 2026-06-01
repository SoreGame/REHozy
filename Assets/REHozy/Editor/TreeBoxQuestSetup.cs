#if UNITY_EDITOR
using REHozy.Decoration;
using REHozy.Watering;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace REHozy.EditorTools
{
    public static class TreeBoxQuestSetup
    {
        private const string MenuPath = "REHozy/Setup Tree Box Quest";
        private const string QuestGrassPath = "Assets/REHozy/Quests/QuestS/Demo/quest_grass.asset";

        [MenuItem(MenuPath)]
        public static void SetupTreeBoxQuest()
        {
            var quest = AssetDatabase.LoadAssetAtPath<QuestSO>(QuestGrassPath);
            if (quest == null)
            {
                Debug.LogWarning($"Quest asset not found at {QuestGrassPath}.");
                return;
            }

            var treeBox = GameObject.Find("TreeBox");
            if (treeBox == null)
            {
                Debug.LogWarning("TreeBox not found in the open scene.");
                return;
            }

            var spawnBox = treeBox.GetComponent<PropSpawnBox>();
            if (spawnBox == null)
            {
                Debug.LogWarning("TreeBox has no PropSpawnBox.");
                return;
            }

            var questBox = treeBox.GetComponent<WateringQuestSpawnBox>();
            if (questBox == null)
            {
                questBox = treeBox.AddComponent<WateringQuestSpawnBox>();
            }

            var soQuestBox = new SerializedObject(questBox);
            soQuestBox.FindProperty("spawnBox").objectReferenceValue = spawnBox;
            soQuestBox.FindProperty("quest").objectReferenceValue = quest;
            soQuestBox.FindProperty("progressPerGrow").intValue = 1;
            soQuestBox.ApplyModifiedPropertiesWithoutUndo();

            EnsureTreeQuestReporter(GameObject.Find("WaterableTree_Test"), quest);

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log(
                "TreeBox quest wired to quest_grass. Spawned bushes report progress when fully grown; " +
                "WaterableTree_Test reports +1 as well (Goal 4).");
        }

        private static void EnsureTreeQuestReporter(GameObject tree, QuestSO quest)
        {
            if (tree == null)
            {
                return;
            }

            var reporter = tree.GetComponent<WaterableQuestReporter>();
            if (reporter == null)
            {
                reporter = tree.AddComponent<WaterableQuestReporter>();
            }

            var so = new SerializedObject(reporter);
            so.FindProperty("quest").objectReferenceValue = quest;
            so.FindProperty("progressAmount").intValue = 1;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
#endif
