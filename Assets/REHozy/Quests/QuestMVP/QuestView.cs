using REHozy;
using REHozy.Audio;
using UnityEngine;
using TMPro;
using System.Collections.Generic;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class QuestView : MonoBehaviour
{
    [SerializeField] private Animator _animator;
    [SerializeField] private QuestPresenter _presenter;

    [Header("Info panel")]
    [SerializeField] private TextMeshProUGUI _nameText;
    [SerializeField] private TextMeshProUGUI _descriptionText;

    [Header("List panel")]
    [SerializeField] private GameObject _listPanel;
    [SerializeField] private Transform _questParent;
    [SerializeField] private GameObject _questPrefab;
    private List<QuestCell> cells = new List<QuestCell>();
    private List<GameObject> cellsObj = new List<GameObject>();
    private bool listVisible = false;

    [Header("Selected Quest")]
    [SerializeField] private GameObject _selectedPanel;
    [SerializeField] private GameObject _selectBtn;
    [SerializeField] private QuestSelectedPanel _questSelected;
    [SerializeField] private TextMeshProUGUI _btnSelectText;
    [SerializeField] private Color _selectedColor;
    [SerializeField] private Color _highlightedColor;
    [SerializeField] private Color _baseColor;
    private Image highlitedImg;
    private bool hasSelected;
    private bool highlighted;
    private QuestData highlitedData;

    private void OnEnable()
    {
        UiEventSystemUtility.EnsureAvailable();
        StaticInput.GetInstance().UI.Enable();
        StaticInput.GetInstance().UI.VisiblePanel.performed += SetVisibleList;

        QuestBus.GetInstance().OnHighlighted += Highlight;
        QuestBus.GetInstance().OnInterrupt += Interrupt;
    }

    private void Start()
    {
        SetListVisible(true);
        _selectedPanel.SetActive(hasSelected);
    }

    private void OnDisable()
    {
        StaticInput.GetInstance().UI.VisiblePanel.performed -= SetVisibleList;
        QuestBus.GetInstance().OnHighlighted -= Highlight;
        QuestBus.GetInstance().OnInterrupt -= Interrupt;

        if (listVisible)
        {
            listVisible = false;
            GameplayUiLock.SetActive(false);
        }
    }

    public void ClearRuntimeUi()
    {
        foreach (var cellObj in cellsObj)
        {
            if (cellObj != null)
                Destroy(cellObj);
        }

        cellsObj.Clear();
        cells.Clear();
        hasSelected = false;
        highlighted = false;
        highlitedData = null;
        highlitedImg = null;

        if (_selectedPanel != null)
            _selectedPanel.SetActive(false);
        if (_selectBtn != null)
            _selectBtn.SetActive(false);
    }

    public void Load(List<QuestData> data)
    {
        foreach (QuestData item in data)
        {
            CreateQuestCell(item);
            if (item.selected)
                ApplySelection(item);
        }
    }

    public void StartQuest(QuestData data)
    {
        GameAudio.Play(GameSoundId.UiQuestAppear, Vector3.zero);
        ShowPanel(data, "Получен квест");
        CreateQuestCell(data);
        ApplySelection(data);
    }

    public void FinishQuest(QuestData data)
    {
        ShowPanel(data, "Квест завершен");
        var wasShownInPanel = hasSelected && ReferenceEquals(highlitedData, data);
        RemoveQuest(data);

        if (!wasShownInPanel)
            return;

        if (!TrySelectActiveQuest())
            ClearSelection();
    }

    private void Interrupt(QuestData data)
    {
        RemoveQuest(data);
        _presenter.Interrupt(data);
    }

    private void RemoveQuest(QuestData data)
    {
        int cell_ind = cells.FindIndex(q => ReferenceEquals(q.Data, data));
        Destroy(cellsObj[cell_ind]);

        cellsObj.RemoveAt(cell_ind);
        cells.RemoveAt(cell_ind);
    }
    private void ShowPanel(QuestData data, string name)
    {
        if (!data.animation_start && data.progress < data.goal)
            return;
        if (!data.animation_finish && data.progress >= data.goal)
            return;
        _nameText.text = $"{name}: {data.quest_name}";
        _descriptionText.text = $"{data.quest_description}\n{DescriptionText(data, data.progress < data.goal)}";
        _animator.SetTrigger("Show");
    }
    private string DescriptionText(QuestData data, bool is_start)
    {
        if (is_start)
            return $"Цель: {data.goal}";
        return $"Квест завершен!";
    }

    private void Highlight(QuestData data, Image image)
    {
        UnhighlAll();

        highlitedImg = image;
        highlitedData = data;
        highlighted = true;
        highlitedData.highlighted = true;

        _selectBtn.SetActive(highlighted);
        _btnSelectText.text = data.selected ? "Убрать" : "Выбрать";
        highlitedImg.color = highlitedData.selected ? _selectedColor : _highlightedColor;
    }
    private void UnhighlAll()
    {
        _presenter.UnhighlAll();
        _selectBtn.SetActive(false);

        foreach (var cell in cellsObj)
        {
            var img = cell.GetComponent<Image>();
            img.color = img.color == _selectedColor ? _selectedColor : _baseColor;
        }

        highlitedData = null;
        highlighted = false;
    }

    public void select()
    {
        if (highlitedData == null)
            return;

        if (highlitedData.selected)
        {
            highlitedData.selected = false;
            hasSelected = false;
            _selectedPanel.SetActive(false);
            RefreshCellColors();
            if (highlitedImg != null)
                highlitedImg.color = _highlightedColor;
            _btnSelectText.text = "Выбрать";
            return;
        }

        UnselectAll();
        ApplySelection(highlitedData);
        highlighted = true;
        _selectBtn.SetActive(true);
        _btnSelectText.text = "Убрать";
    }

    private void ApplySelection(QuestData data)
    {
        _presenter.UnselAll(data);
        data.selected = true;
        hasSelected = true;
        highlitedData = data;
        highlitedImg = FindCellImage(data);

        RefreshCellColors();
        _selectedPanel.SetActive(true);
        if (_selectBtn != null)
            _selectBtn.SetActive(false);

        QuestBus.GetInstance().OnSelect?.Invoke(data);
    }

    private bool TrySelectActiveQuest()
    {
        var active = _presenter.Model._activeQuest;
        if (active == null || active.Count == 0)
            return false;

        ApplySelection(active[active.Count - 1]);
        return true;
    }

    private void ClearSelection()
    {
        hasSelected = false;
        highlitedData = null;
        highlitedImg = null;
        _selectedPanel.SetActive(false);
    }

    private Image FindCellImage(QuestData data)
    {
        int index = cells.FindIndex(c => ReferenceEquals(c.Data, data));
        return index >= 0 ? cellsObj[index].GetComponent<Image>() : null;
    }

    private void RefreshCellColors()
    {
        for (int i = 0; i < cells.Count; i++)
        {
            var img = cellsObj[i].GetComponent<Image>();
            img.color = hasSelected && ReferenceEquals(cells[i].Data, highlitedData)
                ? _selectedColor
                : _baseColor;
        }
    }

    private void UnselectAll()
    {
        _presenter.UnselAll(highlitedData);
        foreach (var cell in cellsObj)
        {
            var img = cell.GetComponent<Image>();
            img.color = _baseColor;
        }
    }
    private void SetVisibleList(InputAction.CallbackContext a)
    {
        UnhighlAll();
        SetListVisible(!listVisible);
    }

    /// <summary>UI close button — must go through here so <see cref="GameplayUiLock"/> is released.</summary>
    public void CloseQuestList()
    {
        if (!listVisible)
            return;

        UnhighlAll();
        SetListVisible(false);
    }

    private void SetListVisible(bool visible)
    {
        var wasVisible = listVisible;
        var opening = visible && !wasVisible;
        var closing = !visible && wasVisible;
        listVisible = visible;
        if (visible)
            UiEventSystemUtility.EnsureAvailable();

        _listPanel.SetActive(listVisible);
        GameplayUiLock.SetActive(listVisible);
        if (opening)
        {
            GameAudio.Play(GameSoundId.UiInventoryOpen, Vector3.zero);
        }

        if (listVisible)
        {
            QuestBus.GetInstance().OnUpdateData?.Invoke();
        }
        else if (closing)
        {
            _presenter.TryStartFirstQuest();
        }
    }
    private GameObject CreateQuestCell(QuestData data)
    {
        GameObject cell_obj = Instantiate(_questPrefab, _questParent.position, Quaternion.identity);
        cell_obj.transform.SetParent(_questParent);
        cell_obj.gameObject.transform.localScale = Vector3.one;
        cellsObj.Add(cell_obj);

        var cell = cell_obj.GetComponent<QuestCell>();
        cell.Init(data);
        cells.Add(cell);
        return cell_obj;
    }
}
