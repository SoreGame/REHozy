using UnityEngine;

namespace REHozy.Dirt
{
    [DisallowMultipleComponent]
    [AddComponentMenu("REHozy/Dirt/Dirt Patch Quest Link")]
    public sealed class DirtPatchQuestLink : MonoBehaviour
    {
        [SerializeField] private QuestSO quest;

        [Header("Quest Progress")]
        [Tooltip("Overrides Dirt Deform Patch quest mass scale when >= 0 (0–1). Lower = faster quest progress.")]
        [SerializeField] [Range(-1f, 1f)] private float questMassScaleOverride = -1f;

        public QuestSO Quest => quest;

        public float GetQuestMassScale()
        {
            if (questMassScaleOverride >= 0f)
            {
                return Mathf.Clamp01(questMassScaleOverride);
            }

            var patch = GetComponent<DirtDeformPatch>();
            return patch != null ? patch.QuestMassScale : 1f;
        }
    }
}
