using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Конец игры: показать кнопку перезапуска (вызвать ShowRestartButton из On Start квеста quest_FINAL),
/// по нажатию — сброс сохранения квестов и перезагрузка текущей сцены.
/// </summary>
public class GameEndRestartController : MonoBehaviour
{
    [SerializeField] private GameObject _restartButtonRoot;
    [SerializeField] private QuestPresenter _questPresenter;

    private void Awake()
    {
        if (_questPresenter == null)
            _questPresenter = FindFirstObjectByType<QuestPresenter>();

        HideRestartButton();
    }

    public void ShowRestartButton()
    {
        if (_restartButtonRoot != null)
            _restartButtonRoot.SetActive(true);
    }

    public void HideRestartButton()
    {
        if (_restartButtonRoot != null)
            _restartButtonRoot.SetActive(false);
    }

    public void RestartLevel()
    {
        if (_questPresenter != null)
            _questPresenter.CanSave = false;

        QuestPresenter.ClearSaveFileOnly();

        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}
