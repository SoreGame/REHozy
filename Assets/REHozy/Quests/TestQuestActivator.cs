using UnityEngine;

public class TestQuestActivator : MonoBehaviour
{

    public QuestSO quest;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        QuestBus.GetInstance().OnStart?.Invoke(quest);
    }

}
