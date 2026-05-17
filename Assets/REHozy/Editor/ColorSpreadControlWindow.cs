using REHozy.Rendering;
using UnityEditor;
using UnityEngine;

namespace REHozy.Editor
{
    public sealed class ColorSpreadControlWindow : EditorWindow
    {
        const string SettingsPath = "Assets/REHozy/Settings/ColorSpreadSettings.asset";

        ColorSpreadController _controller;
        ColorSpreadSettings _settings;
        SerializedObject _settingsSerialized;
        SerializedObject _controllerSerialized;

        Transform _waveOrigin;
        Vector3 _manualOrigin;
        bool _useManualOrigin;
        bool _autoApplySettings = true;
        bool _showGizmo = true;
        float _previewRadius = 20f;

        Vector2 _scroll;

        [MenuItem("REHozy/Color Spread Control %#c")]
        public static void Open()
        {
            var window = GetWindow<ColorSpreadControlWindow>("Color Spread");
            window.minSize = new Vector2(320f, 480f);
            window.Show();
        }

        void OnEnable()
        {
            EditorApplication.update += OnEditorUpdate;
            SceneView.duringSceneGui += OnSceneGUI;
            TryAutoAssign();
        }

        void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
            SceneView.duringSceneGui -= OnSceneGUI;
            SceneView.RepaintAll();
        }

        void OnEditorUpdate()
        {
            Repaint();
            if (_showGizmo)
                SceneView.RepaintAll();
        }

        void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            DrawHeader();
            EditorGUILayout.Space(6f);
            DrawEffectToggle();
            EditorGUILayout.Space(6f);
            DrawSceneReferences();
            EditorGUILayout.Space(8f);
            DrawColorModes();
            EditorGUILayout.Space(8f);
            DrawWaveOrigin();
            EditorGUILayout.Space(8f);
            DrawSettings();
            EditorGUILayout.Space(8f);
            DrawStatus();
            EditorGUILayout.Space(8f);
            DrawUtilities();

            EditorGUILayout.EndScrollView();
        }

        void DrawHeader()
        {
            EditorGUILayout.LabelField("Color Spread", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                Application.isPlaying
                    ? "Play Mode — режимы применяются сразу."
                    : "Edit Mode — переключатель работает в Scene View и в Play.",
                EditorStyles.miniLabel);
        }

        void DrawEffectToggle()
        {
            EditorGUILayout.LabelField("Эффект", EditorStyles.boldLabel);

            if (ResolveController() == null)
            {
                EditorGUILayout.HelpBox("Назначьте ColorSpreadController из сцены.", MessageType.Warning);
                return;
            }

            EditorGUI.BeginChangeCheck();
            var enabled = EditorGUILayout.ToggleLeft("Включён (сцена и игра)", _controller.EffectEnabled);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(_controller, "Toggle Color Spread Effect");
                _controller.SetEffectEnabled(enabled);
                EditorUtility.SetDirty(_controller);
                SceneView.RepaintAll();
                UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
            }

            if (!_controller.EffectEnabled)
                EditorGUILayout.HelpBox("Выключено — обычные цвета без пост-эффекта.", MessageType.Info);
        }

        void DrawSceneReferences()
        {
            EditorGUILayout.LabelField("Сцена", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            _controller = (ColorSpreadController)EditorGUILayout.ObjectField(
                "Controller",
                _controller,
                typeof(ColorSpreadController),
                true);

            if (_controller == null && Application.isPlaying)
                _controller = ColorSpreadController.Instance;

            _settings = (ColorSpreadSettings)EditorGUILayout.ObjectField(
                "Settings",
                _settings,
                typeof(ColorSpreadSettings),
                false);

            if (EditorGUI.EndChangeCheck())
            {
                BindSerializedObjects();
                if (_controller != null && _settings != null && _controller.Settings != _settings)
                {
                    Undo.RecordObject(_controller, "Assign Color Spread Settings");
                    _controllerSerialized ??= new SerializedObject(_controller);
                    _controllerSerialized.FindProperty("settings").objectReferenceValue = _settings;
                    _controllerSerialized.ApplyModifiedProperties();
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Найти в сцене"))
                    TryAutoAssign();

                GUI.enabled = _controller != null;
                if (GUILayout.Button("Выделить"))
                    Selection.activeGameObject = _controller.gameObject;
                GUI.enabled = true;
            }
        }

        void DrawColorModes()
        {
            EditorGUILayout.LabelField("Режим цвета", EditorStyles.boldLabel);
            ResolveController();

            var current = Application.isPlaying && _controller != null
                ? _controller.CurrentStep
                : (ColorSpreadStep?)null;

            using (new EditorGUI.DisabledScope(_controller != null && !_controller.EffectEnabled))
            {
                DrawColorModeButtons(current);
            }

            if (_controller != null && !_controller.EffectEnabled)
                EditorGUILayout.HelpBox("Включите эффект выше, чтобы применять режимы.", MessageType.None);
        }

        void DrawColorModeButtons(ColorSpreadStep? current)
        {
            DrawStepButton(ColorSpreadStep.Grayscale, "0 — Серый", current);
            DrawStepButton(ColorSpreadStep.RedTones, "1 — Красные", current);
            DrawStepButton(ColorSpreadStep.BlueTones, "2 — Синие", current);
            DrawStepButton(ColorSpreadStep.GreenTones, "3 — Зелёные", current);
            DrawStepButton(ColorSpreadStep.FullColor, "4 — Все цвета", current);

            if (!Application.isPlaying)
                EditorGUILayout.HelpBox("Запустите Play Mode для переключения режимов.", MessageType.Info);

            using (new EditorGUI.DisabledScope(!Application.isPlaying))
            {
                if (GUILayout.Button("Перезапустить волну (текущий режим)"))
                {
                    if (ResolveController() != null)
                        _controller.SetStep(_controller.CurrentStep, ResolveOrigin());
                }
            }
        }

        void DrawStepButton(ColorSpreadStep step, string label, ColorSpreadStep? current)
        {
            var isCurrent = current.HasValue && current.Value == step;
            var style = isCurrent ? CreateActiveButtonStyle() : GUI.skin.button;

            using (new EditorGUI.DisabledScope(!Application.isPlaying))
            {
                if (!GUILayout.Button(label, style, GUILayout.Height(28f)))
                    return;
            }

            ApplyStep(step);
        }

        static GUIStyle CreateActiveButtonStyle()
        {
            var style = new GUIStyle(GUI.skin.button)
            {
                fontStyle = FontStyle.Bold
            };
            style.normal.textColor = new Color(1f, 0.55f, 0.2f);
            return style;
        }

        void DrawWaveOrigin()
        {
            EditorGUILayout.LabelField("Центр волны", EditorStyles.boldLabel);

            _waveOrigin = (Transform)EditorGUILayout.ObjectField("Transform", _waveOrigin, typeof(Transform), true);
            if (_waveOrigin == null && _controller != null && GUILayout.Button("Использовать позицию Controller"))
                _waveOrigin = _controller.transform;
            _useManualOrigin = EditorGUILayout.Toggle("Ручная позиция", _useManualOrigin);

            using (new EditorGUI.DisabledScope(!_useManualOrigin))
            {
                _manualOrigin = EditorGUILayout.Vector3Field("Position", _manualOrigin);
            }

            _showGizmo = EditorGUILayout.Toggle("Gizmo в Scene View", _showGizmo);
            _previewRadius = EditorGUILayout.Slider("Preview radius (edit)", _previewRadius, 0f, 200f);
        }

        void DrawSettings()
        {
            EditorGUILayout.LabelField("Настройки эффекта", EditorStyles.boldLabel);

            if (_settings == null)
            {
                EditorGUILayout.HelpBox("Назначьте ColorSpreadSettings или создайте через «Создать Settings».", MessageType.Warning);
                if (GUILayout.Button("Создать Settings"))
                {
                    ColorSpreadSetup.Setup();
                    _settings = AssetDatabase.LoadAssetAtPath<ColorSpreadSettings>(SettingsPath);
                    BindSerializedObjects();
                }
                return;
            }

            _settingsSerialized ??= new SerializedObject(_settings);
            _settingsSerialized.Update();

            EditorGUILayout.PropertyField(_settingsSerialized.FindProperty("growthSpeed"));
            EditorGUILayout.PropertyField(_settingsSerialized.FindProperty("maxRadius"));
            EditorGUILayout.PropertyField(_settingsSerialized.FindProperty("edgeSoftness"));
            EditorGUILayout.PropertyField(_settingsSerialized.FindProperty("noiseScale"));
            EditorGUILayout.PropertyField(_settingsSerialized.FindProperty("noiseStrength"));
            EditorGUILayout.PropertyField(_settingsSerialized.FindProperty("noiseTexture"));

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Полоса волны", EditorStyles.miniBoldLabel);
            EditorGUILayout.PropertyField(_settingsSerialized.FindProperty("waveEdgeIntensity"));
            EditorGUILayout.PropertyField(_settingsSerialized.FindProperty("waveEdgeWidth"));
            EditorGUILayout.PropertyField(_settingsSerialized.FindProperty("waveEdgeColor"));

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Красные (hue 0–1)", EditorStyles.miniBoldLabel);
            EditorGUILayout.PropertyField(_settingsSerialized.FindProperty("redHueRangeA"));
            EditorGUILayout.PropertyField(_settingsSerialized.FindProperty("redHueRangeB"));

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Синие", EditorStyles.miniBoldLabel);
            EditorGUILayout.PropertyField(_settingsSerialized.FindProperty("blueHueRangeA"));
            EditorGUILayout.PropertyField(_settingsSerialized.FindProperty("blueHueRangeB"));

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Зелёные", EditorStyles.miniBoldLabel);
            EditorGUILayout.PropertyField(_settingsSerialized.FindProperty("greenHueRangeA"));
            EditorGUILayout.PropertyField(_settingsSerialized.FindProperty("greenHueRangeB"));

            if (_settingsSerialized.ApplyModifiedProperties())
            {
                EditorUtility.SetDirty(_settings);
                if (Application.isPlaying && _autoApplySettings && ResolveController() != null)
                    _controller.RefreshFromSettings();
            }

            _autoApplySettings = EditorGUILayout.Toggle("Auto-apply в Play Mode", _autoApplySettings);

            using (new EditorGUI.DisabledScope(!Application.isPlaying || _controller == null))
            {
                if (GUILayout.Button("Применить настройки сейчас"))
                    _controller.RefreshFromSettings();
            }
        }

        void DrawStatus()
        {
            EditorGUILayout.LabelField("Статус", EditorStyles.boldLabel);

            if (ResolveController() == null)
            {
                EditorGUILayout.HelpBox("ColorSpreadController не найден.", MessageType.Warning);
                return;
            }

            EditorGUILayout.LabelField("Эффект", _controller.EffectEnabled ? "включён" : "выключен");

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Режимы и волна — в Play Mode. Переключатель эффекта работает всегда.", MessageType.Info);
                return;
            }

            EditorGUILayout.LabelField("Текущий режим", _controller.CurrentStep.ToString());
            EditorGUILayout.LabelField("Runtime step", _controller.RuntimeData.step.ToString());
            EditorGUILayout.LabelField("Палитра (биты)", FormatPaletteMask(_controller.RuntimeData.unlockedMask));
            EditorGUILayout.LabelField("Волна добавляет", FormatPaletteMask(_controller.RuntimeData.waveAddMask));
            EditorGUILayout.LabelField("Радиус волны", $"{_controller.GetCurrentEffectRadius():F1} / {_controller.RuntimeData.maxRadius:F0}");
            EditorGUILayout.LabelField("Центр", ResolveOrigin().ToString("F1"));

            var passOk = ColorSpreadRendererFeature.LastPassEnqueued;
            EditorGUILayout.LabelField("Render pass", passOk ? "активен" : "не вызывается");
            if (passOk)
                EditorGUILayout.LabelField("Shader step", ColorSpreadRendererFeature.LastStepApplied.ToString());

            EditorGUILayout.LabelField("Причина", ColorSpreadRendererFeature.LastSkipReason);

            if (!passOk)
                EditorGUILayout.HelpBox(
                    "Pass не вызывается — см. «Причина» выше. Post Processing выкл. или нет shader в PC_Renderer.",
                    MessageType.Warning);

            if (_controller.CurrentStep == ColorSpreadStep.Grayscale)
                EditorGUILayout.HelpBox("Режим 0: весь экран должен быть серым. Волны нет.", MessageType.Info);
            else if (_controller.CurrentStep > ColorSpreadStep.Grayscale
                     && _controller.CurrentStep < ColorSpreadStep.FullColor)
                EditorGUILayout.HelpBox(
                    "С серого — в волне только выбранный цвет. С палитрой — к ней добавляется новый (красный + зелёный и т.д.).",
                    MessageType.Info);
            else if (Vector3.Distance(ResolveOrigin(), Vector3.zero) < 1f)
                EditorGUILayout.HelpBox("Центр волны в (0,0,0). Укажите Transform маяка в «Transform».", MessageType.Warning);
        }

        void DrawUtilities()
        {
            EditorGUILayout.LabelField("Утилиты", EditorStyles.boldLabel);

            if (GUILayout.Button("Setup Color Spread (проект)"))
                ColorSpreadSetup.Setup();

            EditorGUILayout.HelpBox(
                "Горячая клавиша окна: Ctrl+Shift+C (Cmd+Shift+C на Mac).\n" +
                "В Play Mode: 0–4 на клавиатуре (ColorSpreadDebug на объекте).",
                MessageType.None);
        }

        static string FormatPaletteMask(int mask)
        {
            if (mask == 0)
                return "—";

            var parts = new System.Collections.Generic.List<string>(3);
            if ((mask & ColorSpreadPaletteMask.Red) != 0)
                parts.Add("красный");
            if ((mask & ColorSpreadPaletteMask.Blue) != 0)
                parts.Add("синий");
            if ((mask & ColorSpreadPaletteMask.Green) != 0)
                parts.Add("зелёный");
            if ((mask & ColorSpreadPaletteMask.All) != 0)
                parts.Add("все");
            return string.Join(" + ", parts);
        }

        void ApplyStep(ColorSpreadStep step)
        {
            if (ResolveController() == null)
            {
                Debug.LogWarning("ColorSpreadController не найден в сцене.");
                return;
            }

            _controller.SetStep(step, ResolveOrigin());
            Repaint();
            SceneView.RepaintAll();
        }

        ColorSpreadController ResolveController()
        {
            if (_controller != null)
                return _controller;

            _controller = ColorSpreadController.Instance
                ?? Object.FindFirstObjectByType<ColorSpreadController>();

            if (_controller != null && _settings == null)
                _settings = _controller.Settings;

            BindSerializedObjects();
            return _controller;
        }

        void TryAutoAssign()
        {
            _controller ??= Object.FindFirstObjectByType<ColorSpreadController>();
            _settings ??= _controller != null ? _controller.Settings : null;
            _settings ??= AssetDatabase.LoadAssetAtPath<ColorSpreadSettings>(SettingsPath);
            BindSerializedObjects();
        }

        void BindSerializedObjects()
        {
            _settingsSerialized = _settings != null ? new SerializedObject(_settings) : null;
            _controllerSerialized = _controller != null ? new SerializedObject(_controller) : null;
        }

        Vector3 ResolveOrigin()
        {
            if (_useManualOrigin)
                return _manualOrigin;

            if (_waveOrigin != null)
                return _waveOrigin.position;

            if (_controller != null)
                return _controller.transform.position;

            return Vector3.zero;
        }

        void OnSceneGUI(SceneView sceneView)
        {
            if (!_showGizmo)
                return;

            var origin = ResolveOrigin();
            var radius = Application.isPlaying && _controller != null
                ? _controller.GetCurrentEffectRadius()
                : _previewRadius;

            Handles.color = new Color(1f, 0.45f, 0.15f, 0.9f);
            Handles.DrawWireDisc(origin, Vector3.up, radius);
            Handles.Label(origin + Vector3.up * 2f, $"Color Spread r={radius:F1}");
        }
    }
}
