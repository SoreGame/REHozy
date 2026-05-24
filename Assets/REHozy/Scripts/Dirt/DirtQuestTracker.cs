using System.Collections.Generic;
using UnityEngine;

namespace REHozy.Dirt
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(100)]
    [AddComponentMenu("REHozy/Dirt/Dirt Quest Tracker")]
    public sealed class DirtQuestTracker : MonoBehaviour
    {
        [Header("Quest")]
        [SerializeField] private QuestSO quest;
        [Tooltip("When remaining dirt is below this fraction (0.05 = 5%), quest completes and dirt is cleared.")]
        [SerializeField] [Range(0.01f, 0.25f)] private float completeWhenRemainingBelow = 0.05f;

        [Header("Patches")]
        [SerializeField] private bool autoDiscoverPatches = true;
        [SerializeField] private DirtDeformPatch[] patches;

        [Tooltip("Optional per-patch scale overrides (0–1). Lower = less mass counts toward the quest, progress reaches 100% sooner.")]
        [SerializeField] private PatchMassScaleEntry[] patchMassScales;

        private readonly List<DirtDeformPatch> _trackedPatches = new();
        private readonly Dictionary<DirtDeformPatch, float> _patchMassScaleByPatch = new();
        private readonly Dictionary<DirtDeformPatch, float> _patchBaselines = new();
        private float _initialMass;
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

            if (questActive && _initialMass <= 0f)
            {
                BeginTracking();
            }

            if (_progressDirty && questActive && !_clearedAfterComplete && _initialMass > 0f)
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

            if (_initialMass <= 0f)
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
            _initialMass = 0f;
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

                if (GetPatchMassScale(patch) <= 0f)
                {
                    continue;
                }

                var baseline = patch.CaptureBaselineMass();
                if (baseline <= 0f)
                {
                    continue;
                }

                _patchBaselines[patch] = baseline;
                _initialMass += GetCountedQuestMass(patch, baseline);
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

            if (_initialMass <= 0f)
            {
                BeginTracking();
            }

            if (_initialMass <= 0f)
            {
                return;
            }

            _progressDirty = true;
        }

        private void ReportProgress()
        {
            if (_initialMass <= 0f || quest.Goal <= 0)
            {
                return;
            }

            var currentMass = 0f;
            foreach (var patch in _trackedPatches)
            {
                if (patch == null || !patch.IsPlayModeReady)
                {
                    continue;
                }

                if (GetPatchMassScale(patch) <= 0f)
                {
                    continue;
                }

                currentMass += GetCountedQuestMass(patch, patch.GetQuestMass());
            }

            var remainingRatio = Mathf.Clamp01(currentMass / _initialMass);
            var cleaned01 = 1f - remainingRatio;
            var targetProgress = Mathf.Clamp(Mathf.RoundToInt(cleaned01 * quest.Goal), 0, quest.Goal);
            var delta = targetProgress - _lastSentProgress;

            if (delta > 0)
            {
                QuestBus.GetInstance().OnUpdateCounter?.Invoke(quest.QuestId, delta);
                _lastSentProgress += delta;
            }

            if (remainingRatio <= completeWhenRemainingBelow || _lastSentProgress >= quest.Goal)
            {
                ForceCompleteAndClear();
            }
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

            foreach (var patch in _trackedPatches)
            {
                patch?.ClearAllDirt();
            }

            _clearedAfterComplete = true;
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

        private float GetPatchMassScale(DirtDeformPatch patch)
        {
            if (patch != null && _patchMassScaleByPatch.TryGetValue(patch, out var scale))
            {
                return scale;
            }

            if (patch == null)
            {
                return 1f;
            }

            var link = patch.GetComponent<DirtPatchQuestLink>();
            return link != null ? link.GetQuestMassScale() : patch.QuestMassScale;
        }

        /// <summary>
        /// Mass that still blocks quest completion. With scale &lt; 1, the bottom (1-scale) of baseline is ignored
        /// (buried / invisible dirt), so progress moves faster toward 100%.
        /// </summary>
        private float GetCountedQuestMass(DirtDeformPatch patch, float rawMass)
        {
            var scale = Mathf.Clamp01(GetPatchMassScale(patch));
            if (scale <= 0f)
            {
                return 0f;
            }

            if (scale >= 1f)
            {
                return rawMass;
            }

            if (!_patchBaselines.TryGetValue(patch, out var baseline) || baseline <= 0f)
            {
                return rawMass * scale;
            }

            var exemptMass = baseline * (1f - scale);
            return Mathf.Max(0f, rawMass - exemptMass);
        }

        private void RebuildPatchMassScaleOverrides()
        {
            _patchMassScaleByPatch.Clear();

            if (patchMassScales == null)
            {
                return;
            }

            foreach (var entry in patchMassScales)
            {
                if (entry.patch == null)
                {
                    continue;
                }

                _patchMassScaleByPatch[entry.patch] = Mathf.Clamp01(entry.massScale);
            }
        }

        private void RefreshPatchList()
        {
            RebuildPatchMassScaleOverrides();
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
        private sealed class PatchMassScaleEntry
        {
            public DirtDeformPatch patch;
            [Range(0f, 1f)] public float massScale = 1f;
        }
    }
}
