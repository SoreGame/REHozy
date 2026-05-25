using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace REHozy.Dirt
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(100)]
    [AddComponentMenu("REHozy/Dirt/Dirt Quest Tracker")]
    public sealed class DirtQuestTracker : MonoBehaviour
    {
        [Header("Quest")]
        [SerializeField] private QuestSO quest;
        [Tooltip("When a patch has at most this much dirt left (0.05 = 5%), it counts as fully cleared for quest progress.")]
        [SerializeField] [Range(0f, 0.25f)] private float completeWhenRemainingBelow = 0.05f;

        [Header("Complete — hide dirt")]
        [SerializeField] private float dirtHideDuration = 0.35f;

        [Header("Patches")]
        [SerializeField] private bool autoDiscoverPatches = true;
        [SerializeField] private DirtDeformPatch[] patches;

        [Tooltip("Per-patch quest points when fully cleared (summed directly). Quest completes when total reaches Quest Goal (e.g. 100).")]
        [FormerlySerializedAs("patchMassScales")]
        [SerializeField] private PatchWeightEntry[] patchWeights;

        private readonly List<DirtDeformPatch> _trackedPatches = new();
        private readonly Dictionary<DirtDeformPatch, float> _patchWeightByPatch = new();
        private readonly Dictionary<DirtDeformPatch, float> _patchBaselines = new();
        private int _lastSentProgress;
        private bool _clearedAfterComplete;
        private bool _wasQuestActive;
        private bool _progressDirty;
        private QuestPresenter _cachedPresenter;

        private void Start()
        {
            TryBeginTrackingIfNeeded();
        }

        private void OnEnable()
        {
            DirtDeformPatch.DirtMassChanged += HandleDirtMassChanged;
            DirtDeformPatch.DirtPlayModeReady += HandlePatchPlayModeReady;
            QuestBus.GetInstance().OnStart += HandleQuestStarted;

            if (Application.isPlaying)
            {
                var existingPatches = FindObjectsByType<DirtDeformPatch>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
                foreach (var patch in existingPatches)
                {
                    if (patch != null && patch.IsPlayModeReady)
                    {
                        HandlePatchPlayModeReady(patch);
                    }
                }
            }
        }

        private void OnDisable()
        {
            DirtDeformPatch.DirtMassChanged -= HandleDirtMassChanged;
            DirtDeformPatch.DirtPlayModeReady -= HandlePatchPlayModeReady;
            QuestBus.GetInstance().OnStart -= HandleQuestStarted;
        }

        private void LateUpdate()
        {
            if (_clearedAfterComplete || quest == null)
            {
                return;
            }

            var questActive = IsQuestActive();
            if (questActive && !_wasQuestActive)
            {
                BeginTracking();
            }

            _wasQuestActive = questActive;

            if (questActive && _patchBaselines.Count == 0)
            {
                BeginTracking();
            }

            if (_progressDirty && questActive && !_clearedAfterComplete && _patchBaselines.Count > 0)
            {
                _progressDirty = false;
                ReportProgress();
            }
        }

        private void HandleQuestStarted(QuestSO startedQuest)
        {
            if (startedQuest == null || quest == null || startedQuest.QuestId != quest.QuestId)
            {
                return;
            }

            BeginTracking();
        }

        private void HandlePatchPlayModeReady(DirtDeformPatch patch)
        {
            if (_clearedAfterComplete || quest == null || !IsQuestActive())
            {
                return;
            }

            if (!_trackedPatches.Contains(patch))
            {
                RefreshPatchList();
            }

            if (_patchBaselines.Count == 0)
            {
                BeginTracking();
            }
        }

        private void TryBeginTrackingIfNeeded()
        {
            if (Application.isPlaying && quest != null && IsQuestActive())
            {
                BeginTracking();
            }
        }

        private void BeginTracking()
        {
            if (_clearedAfterComplete || quest == null)
            {
                return;
            }

            RefreshPatchList();
            _lastSentProgress = 0;
            _patchBaselines.Clear();

            foreach (var patch in _trackedPatches)
            {
                if (patch == null)
                {
                    continue;
                }

                patch.EnsurePlayModeInitialized();
                if (!patch.IsPlayModeReady)
                {
                    continue;
                }

                if (GetPatchWeight(patch) <= 0f)
                {
                    continue;
                }

                var baseline = patch.CaptureBaselineMass();
                if (baseline <= 0f)
                {
                    continue;
                }

                _patchBaselines[patch] = baseline;
            }
        }

        private void HandleDirtMassChanged(DirtDeformPatch patch)
        {
            if (!Application.isPlaying || _clearedAfterComplete || quest == null || patch == null)
            {
                return;
            }

            if (!IsQuestActive())
            {
                return;
            }

            if (!_trackedPatches.Contains(patch))
            {
                RefreshPatchList();
            }

            if (_patchBaselines.Count == 0)
            {
                BeginTracking();
            }

            if (_patchBaselines.Count == 0)
            {
                return;
            }

            _progressDirty = true;
        }

        private void ReportProgress()
        {
            if (_patchBaselines.Count == 0 || quest.Goal <= 0)
            {
                return;
            }

            var targetProgress = Mathf.Min(quest.Goal, Mathf.RoundToInt(ComputeEarnedPoints()));
            var delta = targetProgress - _lastSentProgress;

            if (delta > 0)
            {
                QuestBus.GetInstance().OnUpdateCounter?.Invoke(quest.QuestId, delta);
                _lastSentProgress += delta;
            }

            if (_lastSentProgress >= quest.Goal)
            {
                ForceCompleteAndClear();
            }
        }

        /// <summary>
        /// Raw quest points: sum of (patch weight × how much of that patch is cleared).
        /// </summary>
        private float ComputeEarnedPoints()
        {
            var earned = 0f;

            foreach (var entry in _patchBaselines)
            {
                var patch = entry.Key;
                var baseline = entry.Value;
                var weight = GetPatchWeight(patch);
                if (weight <= 0f)
                {
                    continue;
                }

                earned += weight * GetPatchCleaned01(patch, baseline);
            }

            return earned;
        }

        /// <summary>
        /// 1 = patch fully cleared for quest (removed, disabled, or dirt below reserve threshold).
        /// </summary>
        private float GetPatchCleaned01(DirtDeformPatch patch, float baseline)
        {
            if (patch == null || !patch.isActiveAndEnabled || !patch.IsPlayModeReady)
            {
                return 1f;
            }

            if (baseline <= 0f)
            {
                return 1f;
            }

            var remainingRatio = Mathf.Clamp01(patch.GetQuestMass() / baseline);
            if (remainingRatio <= completeWhenRemainingBelow)
            {
                return 1f;
            }

            return 1f - remainingRatio;
        }

        private void ForceCompleteAndClear()
        {
            if (_clearedAfterComplete)
            {
                return;
            }

            var remainingProgress = quest.Goal - _lastSentProgress;
            if (remainingProgress > 0)
            {
                QuestBus.GetInstance().OnUpdateCounter?.Invoke(quest.QuestId, remainingProgress);
                _lastSentProgress = quest.Goal;
            }

            HideAllDirtMeshesAfterQuest();

            _clearedAfterComplete = true;
        }

        private void HideAllDirtMeshesAfterQuest()
        {
            var hidden = new HashSet<DirtDeformPatch>();

            foreach (var patch in _trackedPatches)
            {
                TryHideDirtPatch(patch, hidden);
            }

            foreach (var entry in _patchBaselines)
            {
                TryHideDirtPatch(entry.Key, hidden);
            }

            if (!autoDiscoverPatches)
            {
                return;
            }

            var allPatches = FindObjectsByType<DirtDeformPatch>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (var patch in allPatches)
            {
                if (patch == null || hidden.Contains(patch))
                {
                    continue;
                }

                var link = patch.GetComponent<DirtPatchQuestLink>();
                if (link != null && link.Quest != null && link.Quest.QuestId != quest.QuestId)
                {
                    continue;
                }

                TryHideDirtPatch(patch, hidden);
            }
        }

        private void TryHideDirtPatch(DirtDeformPatch patch, HashSet<DirtDeformPatch> hidden)
        {
            if (patch == null || hidden.Contains(patch))
            {
                return;
            }

            hidden.Add(patch);
            patch.PlayQuestCompleteHide(dirtHideDuration);
        }

        private bool IsQuestActive()
        {
            if (quest == null)
            {
                return false;
            }

            if (_cachedPresenter == null)
            {
                _cachedPresenter = FindFirstObjectByType<QuestPresenter>();
            }

            if (_cachedPresenter == null || _cachedPresenter.Model == null)
            {
                return false;
            }

            return _cachedPresenter.Model.GetActiveQuest(quest.QuestId) != null;
        }

        private float GetPatchWeight(DirtDeformPatch patch)
        {
            if (patch != null && _patchWeightByPatch.TryGetValue(patch, out var weight))
            {
                return weight;
            }

            if (patch == null)
            {
                return 1f;
            }

            var link = patch.GetComponent<DirtPatchQuestLink>();
            return link != null ? link.GetQuestWeight() : patch.QuestWeight;
        }

        private void RebuildPatchWeightOverrides()
        {
            _patchWeightByPatch.Clear();

            if (patchWeights == null)
            {
                return;
            }

            foreach (var entry in patchWeights)
            {
                if (entry.patch == null)
                {
                    continue;
                }

                _patchWeightByPatch[entry.patch] = Mathf.Max(0.01f, entry.weight);
            }
        }

        private void RefreshPatchList()
        {
            RebuildPatchWeightOverrides();
            _trackedPatches.Clear();

            if (patches != null)
            {
                foreach (var patch in patches)
                {
                    if (patch != null && !_trackedPatches.Contains(patch))
                    {
                        _trackedPatches.Add(patch);
                    }
                }
            }

            if (!autoDiscoverPatches)
            {
                return;
            }

            var allPatches = FindObjectsByType<DirtDeformPatch>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (var patch in allPatches)
            {
                if (patch == null || _trackedPatches.Contains(patch))
                {
                    continue;
                }

                var link = patch.GetComponent<DirtPatchQuestLink>();
                if (link != null && link.Quest != null && link.Quest.QuestId != quest.QuestId)
                {
                    continue;
                }

                _trackedPatches.Add(patch);
            }
        }

        [System.Serializable]
        private sealed class PatchWeightEntry
        {
            public DirtDeformPatch patch;
            [FormerlySerializedAs("massScale")]
            [Tooltip("Quest points added when this patch is fully cleared (partial credit while digging).")]
            [Min(0.01f)] public float weight = 1f;
        }
    }
}
