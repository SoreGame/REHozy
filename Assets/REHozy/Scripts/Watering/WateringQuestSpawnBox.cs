using REHozy.Decoration;
using UnityEngine;

namespace REHozy.Watering
{
    /// <summary>
    /// Attach to a <see cref="PropSpawnBox"/> (e.g. TreeBox) to grant quest progress when spawned waterables finish growing.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("REHozy/Watering/Watering Quest Spawn Box")]
    public sealed class WateringQuestSpawnBox : MonoBehaviour
    {
        [SerializeField] private PropSpawnBox spawnBox;
        [SerializeField] private QuestSO quest;
        [SerializeField] private int progressPerGrow = 1;

        public PropSpawnBox SpawnBox => spawnBox != null ? spawnBox : GetComponent<PropSpawnBox>();

        public void OnSpawned(GameObject instance)
        {
            if (instance == null || quest == null)
            {
                return;
            }

            var waterable = instance.GetComponentInChildren<IWaterable>();
            if (waterable == null)
            {
                return;
            }

            var reporter = instance.GetComponent<WaterableQuestReporter>();
            if (reporter == null)
            {
                reporter = instance.AddComponent<WaterableQuestReporter>();
            }

            reporter.Configure(quest, progressPerGrow);
        }

        private void Reset()
        {
            spawnBox = GetComponent<PropSpawnBox>();
        }
    }
}
