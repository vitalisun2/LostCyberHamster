using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Assets.Scripts.Common.Models;
using Assets.Scripts.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LostCyberHamster.Editor.Backgrounds
{
    /// <summary>Выбор четырёх слоёв Procreate и подготовка готового окружения.</summary>
    public sealed class BackgroundProcreateWindow : EditorWindow
    {
        private const string DefaultFolder = @"G:\My Drive\LostCyberHamster\Backgrounds";
        private const string FolderPreference = "LostCyberHamster.Backgrounds.SourceFolder";
        private static readonly string[] RoleNames = { "Дорога", "Первый фон", "Второй фон", "Небо" };
        private static readonly string[] PreviewRoleNames = { "Дорога", "Фон 1", "Фон 2", "Небо" };
        private static readonly string[] DaypartNames = Enum.GetNames(typeof(PartOfDayEnum));

        [SerializeField] private string _folder;
        [SerializeField] private int _locationIndex;
        [SerializeField] private int _daypartIndex;
        private readonly Dictionary<string, ProcreateDocument> _documents = new Dictionary<string, ProcreateDocument>();
        private readonly HashSet<string> _openFiles = new HashSet<string>();
        private readonly HashSet<string> _openGroups = new HashSet<string>();
        private readonly HashSet<string> _openEnvironments = new HashSet<string>();
        private readonly Dictionary<EnvironmentLayerRole, ProcreateNode> _layers = new Dictionary<EnvironmentLayerRole, ProcreateNode>();
        private string[] _files = Array.Empty<string>();
        private string[] _locationIds = Array.Empty<string>();
        private IReadOnlyList<BackgroundEnvironmentCatalogEntry> _environments = Array.Empty<BackgroundEnvironmentCatalogEntry>();
        private ProcreateDocument _document;
        private ProcreateDocument _previewDocument;
        private ProcreateNode _previewNode;
        private Texture2D _previewTexture;
        private Rect _previewUv;
        private Vector2Int _previewSize;
        private BackgroundAuthoringSession _session;
        private Vector2 _scroll;
        private string _message;
        private bool _quitting;
        private bool _windowEnabled;
        private bool _runningSceneAction;
        private Action _pendingSceneAction;

        /// <summary>Показывает, ожидает ли окно завершения операции со сценой.</summary>
        public bool IsSceneActionPending => _pendingSceneAction != null || _runningSceneAction;

        /// <summary>Открывает окно подготовки четырёх фонов.</summary>
        [MenuItem("Tools/Backgrounds/Procreate", priority = 701)]
        public static void Open() => GetWindow<BackgroundProcreateWindow>("Procreate Backgrounds");

        /// <summary>Подключает окно к каталогу и событиям редакторской сессии.</summary>
        private void OnEnable()
        {
            // Восстанавливаем папку и берём локации из существующей структуры контента.
            _windowEnabled = true;
            minSize = new Vector2(600f, 520f);
            if (string.IsNullOrEmpty(_folder))
                _folder = EditorPrefs.GetString(FolderPreference, DefaultFolder);
            _locationIds = Directory.GetDirectories("Assets/Content/locations")
                .Where(path => Directory.Exists(Path.Combine(path, "levels")) && char.IsDigit(Path.GetFileName(path)[0]))
                .Select(Path.GetFileName).OrderBy(id => id, StringComparer.Ordinal).ToArray();
            _locationIndex = Mathf.Clamp(_locationIndex, 0, Math.Max(0, _locationIds.Length - 1));
            _daypartIndex = Mathf.Clamp(_daypartIndex, 0, DaypartNames.Length - 1);
            RefreshFiles();

            // Черновик принадлежит открытому окну редактора.
            EditorApplication.update += OnEditorUpdate;
            EditorApplication.projectChanged += RefreshEnvironmentCatalog;
            EditorApplication.quitting += OnQuitting;
            AssemblyReloadEvents.beforeAssemblyReload += OnBeforeReload;
            EditorSceneManager.activeSceneChangedInEditMode += OnSceneChanged;
            Undo.undoRedoPerformed += OnUndoRedo;
        }

        /// <summary>Освобождает превью, черновик и подписки окна.</summary>
        private void OnDisable()
        {
            // Отключаем события перед закрытием временной сцены.
            _windowEnabled = false;
            CancelPendingSceneAction();
            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.projectChanged -= RefreshEnvironmentCatalog;
            EditorApplication.quitting -= OnQuitting;
            AssemblyReloadEvents.beforeAssemblyReload -= OnBeforeReload;
            EditorSceneManager.activeSceneChangedInEditMode -= OnSceneChanged;
            Undo.undoRedoPerformed -= OnUndoRedo;
            // Превью хранится только в памяти открытого окна.
            ClearLayerPreview();
            // Закрытие окна отбрасывает черновик.
            CloseSession(!_quitting);
        }

        /// <summary>Показывает исходники, превью и готовые окружения либо текущую композицию.</summary>
        private void OnGUI()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorGUILayout.HelpBox("Подготовка фонов доступна в Edit Mode.", MessageType.Info);
                return;
            }
            // Отложенная операция сохраняет текущий выбор и исключает повторный запуск.
            using (new EditorGUI.DisabledScope(IsSceneActionPending))
            {
                if (_session != null && _session.IsActive)
                {
                    DrawComposition();
                    return;
                }

                // Общая прокрутка сохраняет превью сразу под деревом любой высоты.
                DrawFolder();
                _scroll = EditorGUILayout.BeginScrollView(_scroll);
                foreach (var path in _files)
                    DrawFile(path);
                DrawLayerPreview();
                DrawEnvironmentCatalog();
                DrawAssignments();
                DrawMessage();
                EditorGUILayout.EndScrollView();
            }
        }

        /// <summary>Выбирает и обновляет папку исходных Procreate-файлов.</summary>
        private void DrawFolder()
        {
            EditorGUILayout.BeginHorizontal();
            _folder = EditorGUILayout.TextField("Папка Procreate", _folder);
            if (GUILayout.Button("Выбрать…", GUILayout.Width(85)))
            {
                var folder = EditorUtility.OpenFolderPanel("Исходники Procreate", _folder, string.Empty);
                if (!string.IsNullOrEmpty(folder))
                {
                    _folder = folder;
                    RefreshFiles();
                }
            }
            if (GUILayout.Button("Обновить", GUILayout.Width(80)))
                RefreshFiles();
            EditorGUILayout.EndHorizontal();
        }

        /// <summary>Раскрывает структуру выбранного Procreate-файла.</summary>
        private void DrawFile(string path)
        {
            // Открытие файла читает только структуру архива.
            var expanded = _openFiles.Contains(path);
            var requested = EditorGUILayout.Foldout(expanded, Path.GetFileName(path), true);
            if (requested != expanded)
            {
                if (requested)
                    TryAction(() => SelectDocument(path));
                else
                    _openFiles.Remove(path);
            }
            if (!_openFiles.Contains(path) || !_documents.TryGetValue(path, out var document))
                return;

            // Группы раскрывают дерево; роли назначаются отдельным растровым слоям.
            foreach (var node in document.Roots)
                DrawNode(document, node, 1);
        }

        /// <summary>Показывает группу либо выбор превью и роли растрового слоя.</summary>
        private void DrawNode(ProcreateDocument document, ProcreateNode node, int depth)
        {
            // Группы служат навигацией по исходному дереву.
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(depth * 16);
            var label = node.Name + (node.IsHidden ? " (скрыт)" : string.Empty);
            if (node.IsGroup)
            {
                var key = document.FilePath + ":" + node.Id;
                var expanded = _openGroups.Contains(key);
                if (EditorGUILayout.Foldout(expanded, label, true) != expanded)
                {
                    if (expanded)
                        _openGroups.Remove(key);
                    else
                        _openGroups.Add(key);
                }
                EditorGUILayout.EndHorizontal();
                if (_openGroups.Contains(key))
                    foreach (var child in node.Children)
                        DrawNode(document, child, depth + 1);
                return;
            }
            // Клик по имени показывает рисунок независимо от назначений ролей.
            var previewSelected = ReferenceEquals(document, _previewDocument) && ReferenceEquals(node, _previewNode);
            if (GUILayout.Toggle(previewSelected, label, EditorStyles.miniButton, GUILayout.MinWidth(110)) && !previewSelected)
                TryAction(() => ShowLayerPreview(document, node));

            // Назначение роли снимает предыдущую роль этого же рисунка.
            for (var i = 0; i < BackgroundAuthoringSession.Roles.Length; i++)
            {
                var role = BackgroundAuthoringSession.Roles[i];
                var selected = ReferenceEquals(document, _document) && _layers.TryGetValue(role, out var assigned) && ReferenceEquals(assigned, node);
                if (GUILayout.Toggle(selected, RoleNames[i], EditorStyles.miniButton, GUILayout.Width(78)) != selected)
                {
                    if (!ReferenceEquals(document, _document))
                        SelectDocument(document.FilePath);
                    foreach (var existing in _layers.Where(pair => ReferenceEquals(pair.Value, node)).Select(pair => pair.Key).ToArray())
                        _layers.Remove(existing);
                    if (!selected)
                        _layers[role] = node;
                }
            }
            EditorGUILayout.EndHorizontal();
        }

        /// <summary>Выбирает вариант окружения и запускает подготовку четырёх слоёв.</summary>
        private void DrawAssignments()
        {
            // Названия экспорта следуют роли, локации и времени суток.
            _locationIndex = EditorGUILayout.Popup("Локация", _locationIndex, _locationIds);
            _daypartIndex = EditorGUILayout.Popup("Время суток", _daypartIndex, DaypartNames);
            foreach (var role in BackgroundAuthoringSession.Roles)
                EditorGUILayout.LabelField(RoleNames[(int)role], _layers.TryGetValue(role, out var layer) ? layer.Name : "—");

            // Сцена создаётся после четырёх уникальных назначений.
            using (new EditorGUI.DisabledScope(_document == null || _layers.Count != 4 || _locationIds.Length == 0))
                if (GUILayout.Button("Создать композицию"))
                    TryAction(CreateComposition);
        }

        /// <summary>Откладывает создание композиции до завершения текущего OnGUI.</summary>
        public void CreateComposition()
        {
            // Фиксируем четыре назначения на момент нажатия кнопки.
            if (_document == null || _layers.Count != 4 || _locationIds.Length == 0)
                throw new InvalidOperationException("Выберите исходник и назначьте четыре роли.");
            var document = _document;
            var layers = new Dictionary<EnvironmentLayerRole, ProcreateNode>(_layers);
            var locationId = _locationIds[_locationIndex];
            var daypart = DaypartNames[_daypartIndex];

            // Native progress bar, новая сцена и Simulator открываются вне GUI-стека.
            QueueSceneAction(() => OpenComposition(() =>
                BackgroundAuthoringSession.Create(document, layers, locationId, daypart)));
        }

        /// <summary>Откладывает открытие готовой композиции до завершения текущего OnGUI.</summary>
        public void OpenSavedComposition(string locationId, string daypart) =>
            QueueSceneAction(() => OpenComposition(() => BackgroundAuthoringSession.OpenSaved(locationId, daypart)));

        /// <summary>Создаёт сессию и показывает Simulator только для ещё открытого окна.</summary>
        private void OpenComposition(Func<BackgroundAuthoringSession> create)
        {
            // Native операции могут обработать закрытие окна во время создания сессии.
            var session = create();
            if (this == null || !_windowEnabled || _quitting)
            {
                session.Dispose(!_quitting);
                return;
            }

            // Готовая сессия использует общий режим вертикальной настройки.
            _session = session;
            EditorApplication.ExecuteMenuItem("Window/General/Device Simulator");
            Repaint();
        }

        /// <summary>Редактирует высоты и скорости, управляет просмотром и завершает экспорт.</summary>
        private void DrawComposition()
        {
            EditorGUILayout.LabelField($"{_session.LocationId} / {_session.Daypart}", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("В Device Simulator выберите Apple iPhone 11 и landscape. Высота слоёв задаётся относительно нижнего края дороги.", MessageType.Info);

            // Дорога задаёт неподвижное начало композиции.
            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.FloatField("Нижний край дороги", 0f);
            foreach (var role in BackgroundAuthoringSession.Roles)
            {
                if (role == EnvironmentLayerRole.Road)
                    continue;
                var y = _session.Environment.GetLayer(role).transform.localPosition.y;
                EditorGUI.BeginChangeCheck();
                var nextY = EditorGUILayout.FloatField(RoleNames[(int)role] + " Y", y);
                if (EditorGUI.EndChangeCheck())
                    TryAction(() => _session.SetLayerY(role, nextY));
            }

            // Скорости и воспроизведение проверяют стыки четырёх слоёв в симуляторе.
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(_session.IsPreviewPlaying ? "Stop" : "Play", GUILayout.Width(55f)))
                TryAction(() =>
                {
                    if (_session.IsPreviewPlaying)
                        _session.StopPreviewPlayback();
                    else
                        _session.StartPreviewPlayback();
                });
            foreach (var role in BackgroundAuthoringSession.Roles)
            {
                GUILayout.Label(PreviewRoleNames[(int)role], GUILayout.Width(45f));
                EditorGUI.BeginChangeCheck();
                var speed = EditorGUILayout.FloatField(_session.GetPreviewSpeed(role), GUILayout.Width(60f));
                if (EditorGUI.EndChangeCheck())
                    TryAction(() => _session.SetPreviewSpeed(role, speed));
            }
            EditorGUILayout.EndHorizontal();

            // Ползунок сдвигает только временную камеру симулятора.
            EditorGUILayout.BeginHorizontal();
            EditorGUI.BeginChangeCheck();
            var previewX = EditorGUILayout.Slider(new GUIContent("Просмотр по X",
                "Сдвиг камеры для просмотра. В префаб не сохраняется."),
                _session.PreviewOffsetX, -_session.PreviewScrollRange, _session.PreviewScrollRange);
            if (EditorGUI.EndChangeCheck())
                TryAction(() => _session.SetPreviewOffsetX(previewX));
            if (GUILayout.Button("Сброс", GUILayout.Width(60f)))
                TryAction(() => _session.SetPreviewOffsetX(0f));
            EditorGUILayout.EndHorizontal();

            // Save завершает сессию и возвращает редактор в Bootstrap.
            if (GUILayout.Button("Save"))
                QueueSceneAction(() =>
                {
                    var path = _session.Save();
                    _session = null;
                    _message = $"Сохранено: {path}";
                    RefreshEnvironmentCatalog();
                });
            if (GUILayout.Button("Отбросить композицию"))
                QueueSceneAction(() => CloseSession(true));
            DrawMessage();
        }

        /// <summary>Выбирает исходник и подставляет вариант окружения по имени файла.</summary>
        private void SelectDocument(string path)
        {
            // Другой документ начинает отдельный набор назначений.
            if (!_documents.TryGetValue(path, out var document))
            {
                document = ProcreateDocumentReader.Read(path);
                _documents.Add(path, document);
            }
            _openFiles.Add(path);
            if (ReferenceEquals(document, _document))
                return;
            ClearLayerPreview();
            _document = document;
            _layers.Clear();
            _message = null;

            // Имя исходника помогает выбрать канонический вариант окружения.
            var sourceName = Path.GetFileNameWithoutExtension(path).ToLowerInvariant().Replace("barselona", "barcelona");
            for (var i = 0; i < _locationIds.Length; i++)
                if (sourceName.StartsWith(BackgroundAuthoringSession.GetLocationSlug(_locationIds[i]) + "_", StringComparison.Ordinal))
                    _locationIndex = i;
            for (var i = 0; i < DaypartNames.Length; i++)
                if (sourceName.EndsWith("_" + DaypartNames[i].ToLowerInvariant(), StringComparison.Ordinal))
                    _daypartIndex = i;
        }

        /// <summary>Перечитывает исходники, готовые окружения и сбрасывает назначения.</summary>
        private void RefreshFiles()
        {
            // Сбрасываем структуру предыдущего документа.
            ClearLayerPreview();
            _documents.Clear();
            _openFiles.Clear();
            _openGroups.Clear();
            _layers.Clear();
            _document = null;
            // Читаем только файлы выбранной папки.
            _files = Directory.Exists(_folder)
                ? Directory.EnumerateFiles(_folder).Where(path => path.EndsWith(".procreate", StringComparison.OrdinalIgnoreCase))
                    .OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToArray()
                : Array.Empty<string>();
            _message = _files.Length == 0 ? "В папке нет файлов .procreate." : null;
            EditorPrefs.SetString(FolderPreference, _folder);
            // Статусы готового контента обновляются вместе с ручным обновлением источников.
            RefreshEnvironmentCatalog();
        }

        /// <summary>Перечитывает статусы готовых окружений после изменений проекта.</summary>
        private void RefreshEnvironmentCatalog()
        {
            _environments = BackgroundEnvironmentCatalog.Read(_locationIds, DaypartNames);
            Repaint();
        }

        /// <summary>Показывает кешированный каталог готовых вариантов окружения.</summary>
        private void DrawEnvironmentCatalog()
        {
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Готовые окружения", EditorStyles.boldLabel);
            foreach (var entry in _environments)
            {
                // Цвет и подпись показывают результат последней проверки ассетов.
                var previousColor = GUI.contentColor;
                GUI.contentColor = entry.IsReady ? new Color(.3f, .8f, .4f)
                    : string.IsNullOrEmpty(entry.Error) ? Color.gray : new Color(1f, .7f, .25f);
                var status = entry.IsReady ? "Готово" : string.IsNullOrEmpty(entry.Error) ? "Не подготовлено" : "Ошибка";
                var expanded = _openEnvironments.Contains(entry.Address);
                var requested = EditorGUILayout.Foldout(expanded,
                    $"{entry.LocationId} / {entry.Daypart} — {status}", true);
                GUI.contentColor = previousColor;
                if (requested != expanded)
                {
                    if (requested)
                        _openEnvironments.Add(entry.Address);
                    else
                        _openEnvironments.Remove(entry.Address);
                }

                // Раскрытие показывает фактические ссылки и расположение рисунков.
                if (requested)
                    DrawEnvironmentDetails(entry);
            }
        }

        /// <summary>Показывает ассеты окружения и открывает готовую композицию для настройки.</summary>
        private void DrawEnvironmentDetails(BackgroundEnvironmentCatalogEntry entry)
        {
            EditorGUI.indentLevel++;
            if (!string.IsNullOrEmpty(entry.Error))
                EditorGUILayout.HelpBox(entry.Error, MessageType.Warning);

            // Готовая композиция открывается в том же временном режиме редактирования.
            using (new EditorGUI.DisabledScope(!entry.IsReady))
                if (GUILayout.Button("Редактировать"))
                    TryAction(() => OpenSavedComposition(entry.LocationId, entry.Daypart));

            // Четыре роли берутся из готового префаба, а не из исходного Procreate.
            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.ObjectField("Префаб", entry.Prefab, typeof(GameObject), false);
            for (var i = 0; i < BackgroundAuthoringSession.Roles.Length; i++)
            {
                using (new EditorGUI.DisabledScope(true))
                    EditorGUILayout.ObjectField(RoleNames[i], entry.Sprites[i], typeof(Sprite), false);
                if (!string.IsNullOrEmpty(entry.SpritePaths[i]))
                    EditorGUILayout.SelectableLabel(entry.SpritePaths[i], GUILayout.Height(EditorGUIUtility.singleLineHeight));
            }

            // Project-переходы позволяют найти префаб и общую папку экспортированных PNG.
            EditorGUILayout.LabelField("Адрес", entry.Address);
            EditorGUILayout.SelectableLabel(entry.PrefabPath, GUILayout.Height(EditorGUIUtility.singleLineHeight));
            using (new EditorGUI.DisabledScope(entry.Prefab == null))
                if (GUILayout.Button("Показать префаб в Project"))
                    QueueSceneAction(() => ShowInProject(entry.PrefabPath));
            EditorGUILayout.SelectableLabel(entry.SpriteDirectory, GUILayout.Height(EditorGUIUtility.singleLineHeight));
            using (new EditorGUI.DisabledScope(!AssetDatabase.IsValidFolder(entry.SpriteDirectory)))
                if (GUILayout.Button("Показать папку PNG в Project"))
                    QueueSceneAction(() => ShowInProject(entry.SpriteDirectory));
            EditorGUI.indentLevel--;
        }

        /// <summary>Выбирает сохранённый ассет в окне Project.</summary>
        private static void ShowInProject(string assetPath)
        {
            // Находим сохранённый ассет по его пути.
            var asset = AssetDatabase.LoadMainAssetAtPath(assetPath);
            if (asset == null)
                return;
            // Выделяем ассет в существующем окне Project.
            EditorUtility.FocusProjectWindow();
            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
        }

        /// <summary>Обновляет неподвижные копии после изменения высоты или устройства.</summary>
        private void OnEditorUpdate()
        {
            if (_session == null || IsSceneActionPending)
                return;
            if (!_session.IsActive)
                CloseSession(false);
            else if (_session.UpdatePreview())
                Repaint();
        }

        /// <summary>Показывает результат Undo/Redo в симуляторе.</summary>
        private void OnUndoRedo()
        {
            _session?.RefreshPreview();
            Repaint();
        }

        /// <summary>Освобождает черновик после внешней смены сцены.</summary>
        private void OnSceneChanged(Scene previous, Scene current)
        {
            // Внешняя смена сцены отменяет ещё не начатое создание композиции.
            if (!_runningSceneAction)
                CancelPendingSceneAction();

            // Уже выполняемая операция завершает собственный переход целиком.
            if (_session != null && !_session.IsActive)
            {
                CloseSession(false);
            }
        }

        /// <summary>Освобождает превью и завершает временную сцену перед перезагрузкой сборок.</summary>
        private void OnBeforeReload()
        {
            // Действие старого окна не переносится через перезагрузку сборок.
            CancelPendingSceneAction();
            // Текстура превью не переживает перезагрузку сборок.
            ClearLayerPreview();
            // Черновик завершается возвратом в Bootstrap.
            CloseSession(true);
        }
        /// <summary>Отмечает завершение редактора без открытия следующей сцены.</summary>
        private void OnQuitting() => _quitting = true;

        /// <summary>Отделяет сессию от окна до освобождения ресурсов и смены сцены.</summary>
        private void CloseSession(bool openBootstrap)
        {
            var session = _session;
            _session = null;
            session?.Dispose(openBootstrap);
        }

        /// <summary>Назначает одну операцию со сценой на следующий редакторский callback.</summary>
        private void QueueSceneAction(Action action)
        {
            if (this == null || !_windowEnabled || _quitting || IsSceneActionPending)
                return;
            _pendingSceneAction = action;
            EditorApplication.delayCall += ExecutePendingSceneAction;
            Repaint();
        }

        /// <summary>Выполняет отложенную операцию вне OnGUI, пока окно остаётся активным.</summary>
        private void ExecutePendingSceneAction()
        {
            // Забираем действие один раз и проверяем жизненный цикл редактора.
            EditorApplication.delayCall -= ExecutePendingSceneAction;
            var action = _pendingSceneAction;
            _pendingSceneAction = null;
            if (action == null || this == null || !_windowEnabled || _quitting || EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            // Повторный ввод и обновление черновика ждут завершения native операций.
            _runningSceneAction = true;
            try
            {
                TryAction(action);
            }
            finally
            {
                _runningSceneAction = false;
                if (this != null && _windowEnabled)
                    Repaint();
            }
        }

        /// <summary>Отменяет ещё не начатую операцию закрытого или перезагружаемого окна.</summary>
        private void CancelPendingSceneAction()
        {
            EditorApplication.delayCall -= ExecutePendingSceneAction;
            _pendingSceneAction = null;
        }

        /// <summary>Показывает ошибку операции в текущем окне.</summary>
        private void TryAction(Action action)
        {
            try
            {
                _message = null;
                action();
            }
            catch (Exception exception) when (!(exception is ExitGUIException))
            {
                _message = exception.Message;
                Debug.LogException(exception);
            }
        }

        /// <summary>Показывает результат последней операции.</summary>
        private void DrawMessage()
        {
            if (!string.IsNullOrEmpty(_message))
                EditorGUILayout.HelpBox(_message, MessageType.Info);
        }

        /// <summary>Декодирует выбранный рисунок для базового превью в памяти.</summary>
        private void ShowLayerPreview(ProcreateDocument document, ProcreateNode node)
        {
            if (ReferenceEquals(document, _previewDocument) && ReferenceEquals(node, _previewNode))
                return;

            // Один выбранный слой владеет единственной текстурой превью.
            ClearLayerPreview();
            var data = BackgroundTexturePreparation.Prepare(ProcreateLayerDecoder.Decode(document, node));
            _previewTexture = data.CreateTexture();

            // Служебные пиксели находятся за пределами видимой области превью.
            var rect = data.SpriteRect;
            _previewUv = new Rect(rect.x / data.Width, rect.y / data.Height,
                rect.width / data.Width, rect.height / data.Height);
            _previewSize = new Vector2Int(data.SourceBounds.width, data.SourceBounds.height);
            _previewDocument = document;
            _previewNode = node;
            Repaint();
        }

        /// <summary>Показывает выбранный слой без изменения пропорций и служебных полей.</summary>
        private void DrawLayerPreview()
        {
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Предпросмотр слоя", EditorStyles.boldLabel);
            if (_previewTexture == null)
            {
                EditorGUILayout.LabelField("Кликните по имени слоя, чтобы увидеть рисунок.");
                return;
            }

            // Подпись и область просмотра сохраняют исходные пропорции рисунка.
            EditorGUILayout.LabelField($"{_previewNode.Name} — {_previewSize.x}×{_previewSize.y}");
            var available = GUILayoutUtility.GetRect(1f, 150f, GUILayout.ExpandWidth(true));
            var scale = Mathf.Min(available.width / _previewSize.x, available.height / _previewSize.y);
            var size = new Vector2(_previewSize.x * scale, _previewSize.y * scale);
            var fitted = new Rect(available.center - size * .5f, size);

            // Серый фон делает прозрачные части видимыми без дополнительных ресурсов.
            if (Event.current.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(fitted, new Color(.25f, .25f, .25f));
                GUI.DrawTextureWithTexCoords(fitted, _previewTexture, _previewUv, true);
            }
        }

        /// <summary>Освобождает единственную текстуру и выбор базового превью.</summary>
        private void ClearLayerPreview()
        {
            if (_previewTexture != null)
                DestroyImmediate(_previewTexture);
            _previewTexture = null;
            _previewDocument = null;
            _previewNode = null;
            _previewUv = default;
            _previewSize = default;
        }
    }
}
