using System.Collections.Generic;
using UnityEngine;

public class QuestPresenter : MonoBehaviour
{
    public const string SaveKey = "quest_data";

    [SerializeField] private QuestModel _model;
    private QuestJsonSaver jsonSaver;
    private string key = SaveKey;

    [HideInInspector] public bool CanSave = true; // Используется в ResetJson
    private void OnEnable()
    {
        QuestBus.GetInstance().OnStart += StartQuest;
        QuestBus.GetInstance().OnUpdateCounter += update;
        QuestBus.GetInstance().OnInterrupt += Interrupt;    
    }

    private void Awake()
    {
        jsonSaver = new QuestJsonSaver();
        LoadFromJson();
    }
    private void LoadFromJson() 
    {
        try
        {
            var temp = jsonSaver.Load(key);
            _model.Load(temp);
        }
        catch 
        { 
            Debug.LogWarning("Json file empty or not exist");
        }

        QuestBus.GetInstance().OnRuntimeLoaded?.Invoke();
    }

    private void OnDisable()
    {
        QuestBus.GetInstance().OnStart -= StartQuest;
        QuestBus.GetInstance().OnUpdateCounter -= update;
        QuestBus.GetInstance().OnInterrupt -= Interrupt;
        if (CanSave) jsonSaver.Save(key, _model._data);
    }

    public void StartQuest(QuestSO questGet)
    {
        QuestData quest = _model.GetQuest(questGet.QuestId);
        if (quest == null)
        {
            Debug.LogWarning($"Cant start, quest not exit id: {quest.quest_id}");
            return;
        }
        if (quest.active)
        {
            Debug.LogWarning($"Quest already started id: {quest.quest_id}");
            return;
        }
        if (quest.finished)
        {
            Debug.LogWarning($"Quest already finished id: {quest.quest_id}");
            return;
        }

        _model._activeQuest.Add(quest);
        quest.active = true;
        _model.OnStart(quest);
    }

    public void update(int id, int count)
    {
        QuestData quest = _model.GetActiveQuest(id);
        if (quest == null)
        {
            Debug.LogWarning($"Cant update, quest not exit id: {id}");
            return;
        }
        quest.progress += count;
        if (quest.progress >= quest.goal)
        {
            FinishQuest(id);
            return;
        }
        QuestBus.GetInstance().OnUpdateData?.Invoke();
    }

    public void select(QuestData data)
    {
        data.selected = !data.selected;
    }

    public void UnhighlAll()
    {
        foreach(var qd in _model._activeQuest)
            qd.highlighted = false;
    }

    public void UnselAll(QuestData data)
    {
        foreach (var qd in _model._activeQuest)
            if(qd != data)
                qd.selected = false;
    }
    public void FinishQuest(int id)
    {
        QuestData quest = _model.GetActiveQuest(id);
        if (quest == null)
        {
            Debug.LogWarning($"Cant finish, quest not exit id: {id}");
            return;
        }
        _model._activeQuest.Remove(quest);
        quest.active = false;
        quest.finished = true;
        quest.selected = false;
        QuestBus.GetInstance().OnFinish?.Invoke(quest);
        _model.OnFinish(quest);
    }

    public void select(int id)
    {
        QuestData quest = _model._activeQuest.Find(q => q.selected);
        if (quest != null)
        {
            if (quest.quest_id != id)
                quest.selected = false;
        }
        quest = _model.GetActiveQuest(id);
        quest.selected = true;
    }
    public void Interrupt(QuestData data)
    {
        data.progress = 0;
        data.active = false;
        data.finished = false;
        data.selected = false;
        data.highlighted = false;
    }

    public QuestModel Model => _model;
    public string SaveFilePath => jsonSaver.GetPath(key);

    public void SaveNow()
    {
        jsonSaver.Save(key, _model._data);
    }

    public void DebugStartById(int id)
    {
        var quest = _model.GetQuest(id);
        if (quest == null)
        {
            Debug.LogWarning($"[QuestDebug] Quest not found: id {id}");
            return;
        }

        if (quest.finished)
        {
            quest.finished = false;
            quest.progress = 0;
        }

        var so = _model.GetQuestSO(id);
        if (so == null)
        {
            Debug.LogWarning($"[QuestDebug] QuestSO not found for id {id}");
            return;
        }

        StartQuest(so);
    }

    public void DebugFinishById(int id)
    {
        var quest = _model.GetActiveQuest(id);
        if (quest == null)
        {
            Debug.LogWarning($"[QuestDebug] Active quest not found: id {id}");
            return;
        }

        quest.progress = quest.goal;
        FinishQuest(id);
        QuestBus.GetInstance().OnUpdateData?.Invoke();
    }

    public void DebugAddProgress(int id, int delta)
    {
        if (delta == 0)
            return;

        update(id, delta);
        QuestBus.GetInstance().OnUpdateData?.Invoke();
    }

    public void DebugResetById(int id)
    {
        var quest = _model.GetQuest(id);
        if (quest == null)
            return;

        if (quest.active)
        {
            _model._activeQuest.Remove(quest);
            QuestBus.GetInstance().OnInterrupt?.Invoke(quest);
        }

        Interrupt(quest);
        QuestBus.GetInstance().OnUpdateData?.Invoke();
    }

    public void ClearSaveAndResetRuntime()
    {
        CanSave = false;
        jsonSaver.Delete(key);

        InterruptAllActiveQuests();
        ResetQuestStates(_model._data);

        _model._activeQuest.Clear();
        CanSave = true;
        jsonSaver.Save(key, _model._data);
        QuestBus.GetInstance().OnUpdateData?.Invoke();
        QuestBus.GetInstance().OnRuntimeLoaded?.Invoke();
    }

    public void ReloadFromScriptableObjects(bool writeSave = true)
    {
        CanSave = false;

        InterruptAllActiveQuests();
        _model._activeQuest.Clear();
        _model.ClearQuestUi();
        _model.ReloadDefinitionsFromAssets();

        CanSave = true;

        if (writeSave)
            jsonSaver.Save(key, _model._data);
        else
            jsonSaver.Delete(key);

        QuestBus.GetInstance().OnUpdateData?.Invoke();
        Debug.Log("[QuestDebug] Квесты перезагружены из Quest List (ScriptableObject).");
    }

    void InterruptAllActiveQuests()
    {
        var active = new List<QuestData>(_model._activeQuest);
        foreach (var quest in active)
            QuestBus.GetInstance().OnInterrupt?.Invoke(quest);
    }

    static void ResetQuestStates(IEnumerable<QuestData> quests)
    {
        foreach (var quest in quests)
        {
            quest.progress = 0;
            quest.active = false;
            quest.selected = false;
            quest.finished = false;
            quest.highlighted = false;
        }
    }

    public static void ClearSaveFileOnly()
    {
        new QuestJsonSaver().Delete(SaveKey);
    }
}
