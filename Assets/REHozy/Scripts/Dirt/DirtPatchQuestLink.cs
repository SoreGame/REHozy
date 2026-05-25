using UnityEngine;
using UnityEngine.Serialization;

namespace REHozy.Dirt
{
    [DisallowMultipleComponent]
    [AddComponentMenu("REHozy/Dirt/Dirt Patch Quest Link")]
    public sealed class DirtPatchQuestLink : MonoBehaviour
    {
        [SerializeField] private QuestSO quest;

        [Header("Quest Progress")]
        [Tooltip("Overrides quest points for this patch when >= 0 (added to counter when cleared, not normalized).")]
        [FormerlySerializedAs("questMassScaleOverride")]
        [SerializeField] private float questWeightOverride = -1f;

        public QuestSO Quest => quest;

        public float GetQuestWeight()
        {
            if (questWeightOverride >= 0f)
            {
                return Mathf.Max(0.01f, questWeightOverride);
            }

            var patch = GetComponent<DirtDeformPatch>();
            return patch != null ? patch.QuestWeight : 1f;
        }
    }
}
