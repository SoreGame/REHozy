using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using REHozy.CarryableTools;
using REHozy.Rendering;
using UnityEngine;
using UnityEngine.Events;

[Serializable]
public class QuestWorldEffects
{
    public GameObject[] show = Array.Empty<GameObject>();
    public GameObject[] hide = Array.Empty<GameObject>();
    public bool switchToolMode;
    public PlayerToolMode toolMode = PlayerToolMode.None;
}

[Serializable]
public class QuestStateInfo
{
    public QuestSO Quest;

    [Header("World on start")]
    public QuestWorldEffects worldOnStart = new();

    [Header("World on finish")]
    public QuestWorldEffects worldOnFinish = new();

    public UnityEvent OnStart = new UnityEvent();
    public UnityEvent OnFinish = new UnityEvent();
}

public class QuestModel : MonoBehaviour
{
    [SerializeField] private QuestView _view;
    [SerializeField] private QuestSO[] _questList;
    [SerializeField] private List<QuestStateInfo> _questsEvents = new List<QuestStateInfo>();

    public List<QuestData> _activeQuest { get; private set; }
    public List<QuestData> _data { get; private set; }

    private Coroutine _worldRoutine;

    private void Awake()
    {
        _activeQuest = new List<QuestData>();
        _data = new List<QuestData>();
        BuildDataFromQuestList();
    }

    private void OnEnable()
    {
        var bus = QuestBus.GetInstance();
        bus.OnRuntimeLoaded += RebuildWorldState;
        bus.OnInterrupt += HandleInterrupt;
    }

    private void OnDisable()
    {
        var bus = QuestBus.GetInstance();
        bus.OnRuntimeLoaded -= RebuildWorldState;
        bus.OnInterrupt -= HandleInterrupt;
        StopWorldRoutine();
    }

    void BuildDataFromQuestList()
    {
        foreach (QuestSO sObject in _questList)
        {
            if (sObject == null)
                continue;

            if (_data.Find(q => q.quest_id == sObject.QuestId) != null)
            {
                Debug.LogWarning($"Quest with id: {sObject.QuestId} already exist");
                continue;
            }
            _data.Add(QuestData.so_to_st(sObject));
        }
    }

    public void ReloadDefinitionsFromAssets()
    {
        _data.Clear();
        BuildDataFromQuestList();
    }

    public void ClearQuestUi() => _view.ClearRuntimeUi();

    public void Load(List<QuestData> data)
    {
        foreach (var quest in data)
        {
            for (int i = 0; i < _data.Count; i++)
            {
                if (quest.quest_id == _data[i].quest_id && quest != _data[i])
                {
                    _data[i] = quest;
                    break;
                }
            }

            if (quest.active)
            {
                _activeQuest.Add(quest);
                if (quest.need_re_start_event)
                {
                    var temp = _questsEvents.FirstOrDefault(q => q.Quest.QuestId == quest.quest_id);
                    if (temp != null)
                    {
                        temp.OnStart.Invoke();
                    }
                }
            }
        }

        _view.Load(_activeQuest);
    }

    public void OnStart(QuestData data)
    {
        _view.StartQuest(data);
        var state = GetState(data);
        if (state == null)
        {
            return;
        }

        StopWorldRoutine();
        _worldRoutine = StartCoroutine(RunQuestStart(state));
    }

    public void OnFinish(QuestData data)
    {
        _view.FinishQuest(data);
        var state = GetState(data);
        if (state == null)
        {
            return;
        }

        StopWorldRoutine();
        _worldRoutine = StartCoroutine(RunQuestFinish(state));
    }

    private IEnumerator RunQuestStart(QuestStateInfo state)
    {
        yield return ApplyWorldEffects(state.worldOnStart, animated: true);
        RefreshColorSpreadExempt();
        state.OnStart.Invoke();
        _worldRoutine = null;
    }

    private IEnumerator RunQuestFinish(QuestStateInfo state)
    {
        yield return ApplyWorldEffects(state.worldOnFinish, animated: true);
        RefreshColorSpreadExempt();
        state.OnFinish.Invoke();
        _worldRoutine = null;
    }

    public void RebuildWorldState()
    {
        StopWorldRoutine();
        ApplyPhase1Hidden();

        var foundFirstUnfinished = false;
        foreach (var state in _questsEvents)
        {
            if (state?.Quest == null)
            {
                continue;
            }

            var data = GetQuest(state.Quest.QuestId);
            if (data == null)
            {
                continue;
            }

            if (data.finished)
            {
                ApplyWorldEffectsInstant(state.worldOnFinish);
            }
            else if (data.active)
            {
                ApplyWorldEffectsInstant(state.worldOnStart);
            }
            else if (!foundFirstUnfinished)
            {
                ApplyPreStartInstant(state.worldOnStart);
                foundFirstUnfinished = true;
            }
        }

        RefreshColorSpreadExempt();
    }

    private void HandleInterrupt(QuestData _) => RebuildWorldState();

    private void ApplyPhase1Hidden()
    {
        var toHide = new HashSet<GameObject>();
        foreach (var state in _questsEvents)
        {
            if (state == null)
            {
                continue;
            }

            AddObjects(toHide, state.worldOnStart.hide);
            AddObjects(toHide, state.worldOnFinish.show);
        }

        foreach (var go in toHide)
        {
            GetOrAddTransition(go)?.ApplyInstantHidden();
        }
    }

    private static void ApplyPreStartInstant(QuestWorldEffects effects)
    {
        SetObjectsVisible(effects.show, visible: true, instant: true, colorSpreadExempt: true);
        SetObjectsVisible(effects.hide, visible: false, instant: true);
        if (effects.switchToolMode)
        {
            ApplyToolMode(effects.toolMode);
        }
    }

    private static void ApplyWorldEffectsInstant(QuestWorldEffects effects)
    {
        SetObjectsVisible(effects.hide, visible: false, instant: true);
        SetObjectsVisible(effects.show, visible: true, instant: true, colorSpreadExempt: true);
        if (effects.switchToolMode)
        {
            ApplyToolMode(effects.toolMode);
        }
    }

    private void RefreshColorSpreadExempt()
    {
        ColorSpreadExemptRegistry.Clear();
        var foundFirstUnfinished = false;

        foreach (var state in _questsEvents)
        {
            if (state?.Quest == null)
            {
                continue;
            }

            var data = GetQuest(state.Quest.QuestId);
            if (data == null || data.finished)
            {
                continue;
            }

            var includeShow = data.active;
            if (!includeShow && !foundFirstUnfinished)
            {
                includeShow = true;
                foundFirstUnfinished = true;
            }

            if (!includeShow)
            {
                continue;
            }

            RegisterShowExempt(state.worldOnStart.show);
        }
    }

    private static void RegisterShowExempt(GameObject[] objects)
    {
        if (objects == null)
        {
            return;
        }

        foreach (var go in objects)
        {
            if (go != null && go.activeInHierarchy)
            {
                ColorSpreadExemptRegistry.Register(go);
            }
        }
    }

    private static void SetShowExempt(GameObject go, bool exempt)
    {
        if (go == null)
        {
            return;
        }

        if (exempt)
        {
            ColorSpreadExemptRegistry.Register(go);
        }
        else
        {
            ColorSpreadExemptRegistry.Unregister(go);
        }
    }

    private static IEnumerator ApplyWorldEffects(QuestWorldEffects effects, bool animated)
    {
        if (!animated)
        {
            ApplyWorldEffectsInstant(effects);
            yield break;
        }

        yield return SetObjectsVisibleCoroutine(effects.hide, visible: false);
        yield return SetObjectsVisibleCoroutine(effects.show, visible: true, colorSpreadExempt: true);
        if (effects.switchToolMode)
        {
            ApplyToolMode(effects.toolMode);
        }
    }

    private static void SetObjectsVisible(
        GameObject[] objects,
        bool visible,
        bool instant,
        bool colorSpreadExempt = false)
    {
        if (objects == null || !instant)
        {
            return;
        }

        foreach (var go in objects)
        {
            if (go == null)
            {
                continue;
            }

            var transition = GetOrAddTransition(go);
            if (transition == null)
            {
                continue;
            }

            if (visible)
            {
                transition.ApplyInstantShown();
            }
            else
            {
                transition.ApplyInstantHidden();
            }

            if (colorSpreadExempt)
            {
                SetShowExempt(go, visible);
            }
        }
    }

    private static IEnumerator SetObjectsVisibleCoroutine(
        GameObject[] objects,
        bool visible,
        bool colorSpreadExempt = false)
    {
        if (objects == null || objects.Length == 0)
        {
            yield break;
        }

        var pending = 0;
        foreach (var go in objects)
        {
            if (go == null)
            {
                continue;
            }

            var transition = GetOrAddTransition(go);
            if (transition == null)
            {
                continue;
            }

            pending++;
            if (visible)
            {
                transition.PlayShow(() =>
                {
                    if (colorSpreadExempt)
                    {
                        SetShowExempt(go, true);
                    }

                    pending--;
                });
            }
            else
            {
                if (colorSpreadExempt)
                {
                    SetShowExempt(go, false);
                }

                transition.PlayHide(() => pending--);
            }
        }

        if (pending == 0)
        {
            yield break;
        }

        yield return new WaitWhile(() => pending > 0);
    }

    private static void ApplyToolMode(PlayerToolMode mode)
    {
        if (mode == PlayerToolMode.None)
        {
            return;
        }

        PlayerToolModeState.Active = mode;
        var input = FindFirstObjectByType<CarryableToolInputHandler>();
        input?.RefreshToolBinding();
    }

    private static QuestWorldScaleTransition GetOrAddTransition(GameObject go)
    {
        if (go == null)
        {
            return null;
        }

        var transition = go.GetComponent<QuestWorldScaleTransition>();
        if (transition == null)
        {
            transition = go.AddComponent<QuestWorldScaleTransition>();
        }

        return transition;
    }

    private static void AddObjects(HashSet<GameObject> set, GameObject[] objects)
    {
        if (objects == null)
        {
            return;
        }

        foreach (var go in objects)
        {
            if (go != null)
            {
                set.Add(go);
            }
        }
    }

    private void StopWorldRoutine()
    {
        if (_worldRoutine != null)
        {
            StopCoroutine(_worldRoutine);
            _worldRoutine = null;
        }
    }

    private QuestStateInfo GetState(QuestData data)
    {
        return _questsEvents.FirstOrDefault(quest => quest.Quest.QuestId == data.quest_id);
    }

    public QuestData GetActiveQuest(int id)
    {
        return _activeQuest.Find(q => q.quest_id == id);
    }

    public QuestData GetQuest(int id)
    {
        return _data.Find(q => q.quest_id == id);
    }

    public QuestSO GetQuestSO(int id)
    {
        return _questList.FirstOrDefault(so => so.QuestId == id);
    }
}
