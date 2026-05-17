using System.IO;
using UnityEditor;
using UnityEngine;

namespace REHozy.Editor
{
    public sealed class QuestDebugWindow : EditorWindow
    {
        const string MenuPath = "REHozy/Quest Debug %#q";

        QuestPresenter _presenter;
        Vector2 _scroll;
        int _progressDelta = 1;

        [MenuItem(MenuPath)]
        public static void Open()
        {
            var window = GetWindow<QuestDebugWindow>("Quest Debug");
            window.minSize = new Vector2(360f, 320f);
            window.Show();
        }

        void OnEnable()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            TryFindPresenter();
        }

        void OnDisable()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        }

        void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode || state == PlayModeStateChange.EnteredEditMode)
                _presenter = null;
            Repaint();
        }

        void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            DrawHeader();
            EditorGUILayout.Space(6f);
            DrawPresenterField();
            EditorGUILayout.Space(6f);
            DrawSaveSection();
            EditorGUILayout.Space(8f);
            DrawQuestList();
            EditorGUILayout.Space(8f);
            DrawHelp();

            EditorGUILayout.EndScrollView();
        }

        void DrawHeader()
        {
            EditorGUILayout.LabelField("Quest Debug", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                Application.isPlaying
                    ? "Play Mode — можно стартовать, завершать и менять прогресс."
                    : "Edit Mode — управление квестами доступно в Play Mode. JSON можно очистить сейчас.",
                EditorStyles.miniLabel);
        }

        void DrawPresenterField()
        {
            EditorGUILayout.LabelField("Сцена", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            _presenter = (QuestPresenter)EditorGUILayout.ObjectField(
                "Quest Presenter",
                _presenter,
                typeof(QuestPresenter),
                true);
            if (EditorGUI.EndChangeCheck())
                return;

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Найти в сцене"))
                    TryFindPresenter();

                GUI.enabled = _presenter != null;
                if (GUILayout.Button("Выделить"))
                    Selection.activeGameObject = _presenter.gameObject;
                GUI.enabled = true;
            }

            if (_presenter == null && Application.isPlaying)
                EditorGUILayout.HelpBox("QuestPresenter не найден в сцене.", MessageType.Warning);
        }

        void DrawSaveSection()
        {
            EditorGUILayout.LabelField("Сохранение", EditorStyles.boldLabel);

            var path = Path.Combine(Application.persistentDataPath, QuestPresenter.SaveKey);
            var exists = File.Exists(path);
            EditorGUILayout.LabelField("Файл", path, EditorStyles.wordWrappedLabel);
            EditorGUILayout.LabelField("Статус", exists ? "есть сохранение" : "файла нет");

            using (new EditorGUI.DisabledScope(!Application.isPlaying || ResolvePresenter() == null))
            {
                if (GUILayout.Button("Сохранить сейчас"))
                    _presenter.SaveNow();
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                var clearLabel = Application.isPlaying && ResolvePresenter() != null
                    ? "Очистить JSON и сбросить квесты"
                    : "Удалить JSON файл";

                if (GUILayout.Button(clearLabel))
                {
                    if (!EditorUtility.DisplayDialog(
                            "Очистить квесты",
                            Application.isPlaying && ResolvePresenter() != null
                                ? "Сбросить все квесты в игре и перезаписать сохранение?"
                                : "Удалить файл сохранения квестов?",
                            "Да",
                            "Отмена"))
                        return;

                    if (Application.isPlaying && ResolvePresenter() != null)
                        _presenter.ClearSaveAndResetRuntime();
                    else
                        QuestPresenter.ClearSaveFileOnly();

                    Debug.Log("[QuestDebug] Сохранение квестов очищено.");
                }
            }

            if (exists && GUILayout.Button("Показать в проводнике"))
                EditorUtility.RevealInFinder(path);
        }

        void DrawQuestList()
        {
            EditorGUILayout.LabelField("Квесты", EditorStyles.boldLabel);

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Запустите Play Mode для списка и кнопок управления.", MessageType.Info);
                return;
            }

            if (ResolvePresenter() == null)
                return;

            var quests = _presenter.Model._data;
            if (quests == null || quests.Count == 0)
            {
                EditorGUILayout.HelpBox("Список квестов пуст. Проверьте QuestModel → Quest List.", MessageType.Warning);
                return;
            }

            _progressDelta = EditorGUILayout.IntField("Прибавить прогресс на", Mathf.Max(1, _progressDelta));

            foreach (var quest in quests)
                DrawQuestRow(quest);
        }

        void DrawQuestRow(QuestData quest)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField($"{quest.quest_name}  (id: {quest.quest_id})", EditorStyles.boldLabel);
                EditorGUILayout.LabelField(quest.quest_description, EditorStyles.wordWrappedMiniLabel);
                EditorGUILayout.LabelField(
                    $"Прогресс: {quest.progress} / {quest.goal}   |   {StatusLabel(quest)}");

                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUI.DisabledScope(quest.active && !quest.finished))
                    {
                        if (GUILayout.Button("Старт", GUILayout.Height(22f)))
                            _presenter.DebugStartById(quest.quest_id);
                    }

                    using (new EditorGUI.DisabledScope(!quest.active || quest.finished))
                    {
                        if (GUILayout.Button("Завершить", GUILayout.Height(22f)))
                            _presenter.DebugFinishById(quest.quest_id);

                        if (GUILayout.Button($"+{_progressDelta}", GUILayout.Height(22f)))
                            _presenter.DebugAddProgress(quest.quest_id, _progressDelta);
                    }

                    if (GUILayout.Button("Сброс", GUILayout.Height(22f)))
                        _presenter.DebugResetById(quest.quest_id);
                }

                if (quest.finished && !quest.active)
                    EditorGUILayout.HelpBox("Завершён. «Старт» сбросит прогресс и запустит снова.", MessageType.None);
            }
        }

        static string StatusLabel(QuestData quest)
        {
            if (quest.finished)
                return "завершён";
            if (quest.active)
                return quest.selected ? "активен, выбран" : "активен";
            return "не начат";
        }

        void DrawHelp()
        {
            EditorGUILayout.LabelField("Подсказка", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Горячая клавиша: Ctrl+Shift+Q (Cmd+Shift+Q на Mac).\n" +
                "«+N» вызывает тот же счётчик, что и игровой QuestBus.OnUpdateCounter.\n" +
                "После очистки JSON в Play Mode UI обновится автоматически.",
                MessageType.None);
        }

        QuestPresenter ResolvePresenter()
        {
            if (_presenter != null)
                return _presenter;

            TryFindPresenter();
            return _presenter;
        }

        void TryFindPresenter()
        {
            _presenter = Object.FindFirstObjectByType<QuestPresenter>();
            Repaint();
        }
    }
}
