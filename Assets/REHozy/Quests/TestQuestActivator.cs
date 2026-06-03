using UnityEngine;

/// <summary>
/// Устарел: сброс сохранения выполняет <see cref="QuestPresenter.BeginFreshGame"/> при запуске сцены;
/// первый квест стартует при закрытии списка заданий (<see cref="QuestView.CloseQuestList"/>).
/// Оставлен для ручного вызова из инспектора / UnityEvent.
/// </summary>
public class TestQuestActivator : MonoBehaviour
{
    public QuestSO quest;

    public void Activate()
    {
        if (quest == null)
            return;

        QuestBus.GetInstance().OnStart?.Invoke(quest);
    }
}
