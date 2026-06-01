using UnityEngine;

namespace REHozy.Watering
{
    [DisallowMultipleComponent]
    [AddComponentMenu("REHozy/Watering/Waterable Quest Reporter")]
    public sealed class WaterableQuestReporter : MonoBehaviour
    {
        [SerializeField] private QuestSO quest;
        [SerializeField] private int progressAmount = 1;

        private IWaterable _waterable;
        private bool _reported;

        public void Configure(QuestSO questSo, int amount)
        {
            quest = questSo;
            progressAmount = Mathf.Max(1, amount);
            _reported = false;
        }

        private void Awake()
        {
            _waterable = GetComponent<IWaterable>();
        }

        private void Update()
        {
            if (_reported || quest == null || _waterable == null)
            {
                return;
            }

            if (!_waterable.IsWateringComplete)
            {
                return;
            }

            _reported = true;
            QuestBus.GetInstance().OnUpdateCounter?.Invoke(quest.QuestId, progressAmount);
        }
    }
}
