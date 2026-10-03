using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Assets.Scripts;
using Assets.Scripts.Common.Models;
using Assets.Scripts.Gameplay;
using Assets.Scripts.System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace LostCyberHamster.Editor.Backgrounds
{
    /// <summary>Хранит несохранённую композицию четырёх слоёв и её временные ресурсы.</summary>
    public sealed class BackgroundAuthoringSession : IDisposable
    {
        public static readonly EnvironmentLayerRole[] Roles =
        {
            EnvironmentLayerRole.Road, EnvironmentLayerRole.Background,
            EnvironmentLayerRole.Background2, EnvironmentLayerRole.Sky
        };

        private readonly Dictionary<EnvironmentLayerRole, BackgroundTextureData> _textures;
        private readonly List<Object> _temporaryResources = new List<Object>();
        private readonly Dictionary<EnvironmentLayerRole, SpriteRenderer> _authoredLayers =
            new Dictionary<EnvironmentLayerRole, SpriteRenderer>();
        private readonly Dictionary<EnvironmentLayerRole, Sprite> _authoredSprites =
            new Dictionary<EnvironmentLayerRole, Sprite>();
        private readonly Dictionary<EnvironmentLayerRole, Vector3> _initialPositions =
            new Dictionary<EnvironmentLayerRole, Vector3>();
        private readonly Dictionary<EnvironmentLayerRole, Vector3> _previewPositions =
            new Dictionary<EnvironmentLayerRole, Vector3>();
        private readonly Dictionary<EnvironmentLayerRole, EnvironmentStrip> _previewStrips =
            new Dictionary<EnvironmentLayerRole, EnvironmentStrip>();
        private readonly Scene _scene;
        private bool _disposed;
        private float _previewAspect;
        private double _previousPreviewTime;

        /// <summary>Создаёт временную сцену из подготовленных рисунков либо готового префаба.</summary>
        private BackgroundAuthoringSession(Dictionary<EnvironmentLayerRole, BackgroundTextureData> textures,
            string locationId, string daypart, GameObject savedPrefab = null)
        {
            _textures = textures;
            LocationId = locationId;
            Daypart = daypart;
            SavedPrefabPath = savedPrefab != null ? AssetDatabase.GetAssetPath(savedPrefab) : null;
            _scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            try
            {
                // Создаём игровую камеру и корень нижнего края дороги.
                var cameraObject = new GameObject("Background preview camera", typeof(Camera));
                Camera = cameraObject.GetComponent<Camera>();
                Camera.tag = "MainCamera";
                Camera.clearFlags = CameraClearFlags.SolidColor;
                Camera.backgroundColor = Color.black;
                LocationEnvironment.ConfigureCamera(Camera);
                var root = savedPrefab != null ? Object.Instantiate(savedPrefab)
                    : new GameObject($"environment_{LocationSlug}_{Daypart}", typeof(LocationEnvironment));
                root.name = $"environment_{LocationSlug}_{Daypart}";
                root.transform.position = new Vector3(0f, LocationEnvironment.RoadBottomWorldY, 0f);
                Environment = root.GetComponent<LocationEnvironment>();

                // Готовая композиция сохраняет ссылки и позиции четырёх авторских слоёв.
                if (savedPrefab != null)
                    foreach (var role in Roles)
                        RememberLayer(role, Environment.GetLayer(role));
                else
                    CreateSourceLayers();
                Environment.Configure(_authoredLayers[EnvironmentLayerRole.Road], _authoredLayers[EnvironmentLayerRole.Background],
                    _authoredLayers[EnvironmentLayerRole.Background2], _authoredLayers[EnvironmentLayerRole.Sky]);
                RefreshPreview();
                Selection.activeGameObject = Environment.GetLayer(EnvironmentLayerRole.Background).gameObject;
                EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        public string LocationId { get; }
        public string Daypart { get; }
        public string LocationSlug => GetLocationSlug(LocationId);
        public LocationEnvironment Environment { get; }
        public Camera Camera { get; }
        /// <summary>Путь префаба, открытого для повторного редактирования.</summary>
        public string SavedPrefabPath { get; }
        /// <summary>Горизонтальный сдвиг камеры только для текущего просмотра.</summary>
        public float PreviewOffsetX { get; private set; }
        /// <summary>Признак редакторской прокрутки текущей композиции.</summary>
        public bool IsPreviewPlaying { get; private set; }
        /// <summary>Диапазон просмотра одного полного повторения самого широкого слоя.</summary>
        public float PreviewScrollRange => Roles.Max(role =>
            _authoredSprites[role].rect.width / _authoredSprites[role].pixelsPerUnit);
        public bool IsActive => !_disposed && _scene.IsValid() && _scene.isLoaded && Environment != null &&
                                Camera != null && Roles.All(role => Environment.GetLayer(role) != null);

        /// <summary>Декодирует четыре выбранных слоя и открывает черновую сцену.</summary>
        public static BackgroundAuthoringSession Create(ProcreateDocument document,
            IReadOnlyDictionary<EnvironmentLayerRole, ProcreateNode> layers, string locationId, string daypart)
        {
            // Проверяем принадлежность выбора документу и канонические параметры экспорта.
            if (document == null || layers == null || Roles.Any(role => !layers.ContainsKey(role)) || layers.Count != 4)
                throw new ArgumentException("Нужны документ и четыре назначенные роли.");
            if (Roles.Select(role => layers[role].Id).Distinct().Count() != 4 ||
                Roles.Any(role => layers[role].IsGroup || !ContainsNode(document.Roots, layers[role])))
                throw new ArgumentException("Каждой роли нужен отдельный растровый слой выбранного документа.");
            if (string.IsNullOrEmpty(locationId) || Path.GetFileName(locationId) != locationId ||
                !Directory.Exists($"Assets/Content/locations/{locationId}/levels"))
                throw new ArgumentException("Выберите существующую локацию.");
            if (!Enum.TryParse<PartOfDayEnum>(daypart, true, out var part) || !Enum.IsDefined(typeof(PartOfDayEnum), part))
                throw new ArgumentException("Выберите время суток.");

            // Подготавливаем всё до замены текущей сцены.
            var textures = new Dictionary<EnvironmentLayerRole, BackgroundTextureData>();
            try
            {
                foreach (var role in Roles)
                {
                    EditorUtility.DisplayProgressBar("Подготовка фонов", layers[role].Name, textures.Count / 4f);
                    textures.Add(role, BackgroundTexturePreparation.Prepare(ProcreateLayerDecoder.Decode(document, layers[role])));
                }
                return new BackgroundAuthoringSession(textures, locationId, part.ToString().ToLowerInvariant());
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        /// <summary>Открывает сохранённую композицию с её спрайтами и относительными позициями.</summary>
        public static BackgroundAuthoringSession OpenSaved(string locationId, string daypart)
        {
            // Проверяем актуальный префаб и его адрес до замены текущей сцены.
            if (!Enum.TryParse<PartOfDayEnum>(daypart, true, out var part) || !Enum.IsDefined(typeof(PartOfDayEnum), part))
                throw new ArgumentException("Выберите время суток.");
            var entry = BackgroundEnvironmentCatalog.Read(new[] { locationId }, new[] { part.ToString() }).Single();
            if (!entry.IsReady)
                throw new InvalidOperationException(entry.Error ?? $"Готовое окружение не найдено: {entry.PrefabPath}");

            // Импортированные спрайты принадлежат ассетам, а временной сессии принадлежит их копия в сцене.
            return new BackgroundAuthoringSession(new Dictionary<EnvironmentLayerRole, BackgroundTextureData>(),
                locationId, part.ToString().ToLowerInvariant(), entry.Prefab);
        }

        /// <summary>Создаёт четыре временных спрайта относительно исходного нижнего края дороги.</summary>
        private void CreateSourceLayers()
        {
            var roadBounds = _textures[EnvironmentLayerRole.Road].SourceBounds;
            foreach (var role in Roles)
            {
                // Временные пиксели и спрайты принадлежат текущей сессии.
                var data = _textures[role];
                var texture = data.CreateTexture();
                _temporaryResources.Add(texture);
                var sprite = Sprite.Create(texture, data.SpriteRect, new Vector2(.5f, .5f),
                    BackgroundTexturePreparation.PixelsPerUnit, 0, SpriteMeshType.FullRect);
                sprite.name = BackgroundAssetExporter.GetSpriteName(role, LocationSlug, Daypart);
                sprite.hideFlags = HideFlags.DontSave;
                _temporaryResources.Add(sprite);

                // Обрезка рисунка сохраняет положение относительно исходной дороги.
                var layerObject = new GameObject(role.ToString(), typeof(SpriteRenderer));
                layerObject.transform.SetParent(Environment.transform, false);
                var bounds = data.SourceBounds;
                layerObject.transform.localPosition = new Vector3((bounds.center.x - roadBounds.center.x) /
                    BackgroundTexturePreparation.PixelsPerUnit,
                    (bounds.y - roadBounds.y + bounds.height * .5f) /
                    BackgroundTexturePreparation.PixelsPerUnit, 0f);
                var renderer = layerObject.GetComponent<SpriteRenderer>();
                renderer.sprite = sprite;
                renderer.sharedMaterial = AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat");
                renderer.sortingLayerName = LocationEnvironment.GetSortingLayer(role);
                RememberLayer(role, renderer);
            }
        }

        /// <summary>Фиксирует авторский слой, его спрайт и исходную позицию для предпросмотра.</summary>
        private void RememberLayer(EnvironmentLayerRole role, SpriteRenderer renderer)
        {
            _authoredLayers.Add(role, renderer);
            _authoredSprites.Add(role, renderer.sprite);
            _initialPositions.Add(role, renderer.transform.localPosition);
            _previewPositions.Add(role, renderer.transform.localPosition);
        }

        /// <summary>Возвращает подготовленные данные выбранной роли.</summary>
        public BackgroundTextureData GetTextureData(EnvironmentLayerRole role) => _textures[role];

        /// <summary>Меняет высоту слоя с поддержкой Undo/Redo.</summary>
        public void SetLayerY(EnvironmentLayerRole role, float y)
        {
            if (!IsActive || role == EnvironmentLayerRole.Road || !float.IsFinite(y))
                return;

            // Undo хранит изменение основного слоя, авторская позиция сохраняет высоту отдельно от фазы.
            var transform = Environment.GetLayer(role).transform;
            Undo.RecordObject(transform, "Высота слоя фона");
            var position = transform.localPosition;
            position.y = y;
            transform.localPosition = position;
            var authoredPosition = _previewPositions[role];
            authoredPosition.y = y;
            _previewPositions[role] = authoredPosition;
            RefreshPreview();
        }

        /// <summary>Возвращает сохранённую скорость роли для просмотра и игры.</summary>
        public float GetPreviewSpeed(EnvironmentLayerRole role) => Environment.GetLayerScrollSpeed(role);

        /// <summary>Меняет сохраняемую скорость роли с Undo без сброса фазы просмотра.</summary>
        public void SetPreviewSpeed(EnvironmentLayerRole role, float speed)
        {
            if (!IsActive || !float.IsFinite(speed))
                return;

            // Настройка принадлежит компоненту будущего префаба.
            speed = Mathf.Max(0f, speed);
            if (Mathf.Approximately(Environment.GetLayerScrollSpeed(role), speed))
                return;
            Undo.RecordObject(Environment, "Скорость слоя фона");
            Environment.SetLayerScrollSpeed(role, speed);
        }

        /// <summary>Запускает редакторскую прокрутку четырёх ролей из авторских позиций.</summary>
        public void StartPreviewPlayback()
        {
            if (!IsActive || IsPreviewPlaying || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isPaused)
                return;

            // Фиксируем авторскую композицию до создания прокручиваемых повторов.
            UpdatePreview();
            Environment.ClearCopies();
            Environment.ValidateConfiguration();
            var copiesRoot = new GameObject("_EnvironmentRepeats").transform;
            copiesRoot.SetParent(Environment.transform, false);
            foreach (var role in Roles)
                _previewStrips.Add(role, new EnvironmentStrip(_authoredLayers[role], copiesRoot, GetPreviewSpeed(role)));

            // Фаза и редакторское время принадлежат текущему запуску просмотра.
            _previousPreviewTime = EditorApplication.timeSinceStartup;
            IsPreviewPlaying = true;
            RefreshPreview();
        }

        /// <summary>Останавливает прокрутку и возвращает слои к авторским позициям.</summary>
        public void StopPreviewPlayback()
        {
            EndPreviewPlayback();
            RefreshPreview();
        }

        /// <summary>Удаляет анимационные повторы и восстанавливает авторские координаты.</summary>
        private void EndPreviewPlayback()
        {
            // При остановке учитываем последнюю настройку высоты в Inspector или Scene View.
            if (IsPreviewPlaying)
                foreach (var role in Roles)
                    if (role != EnvironmentLayerRole.Road && _authoredLayers[role] != null &&
                        float.IsFinite(_authoredLayers[role].transform.localPosition.y))
                    {
                        var position = _previewPositions[role];
                        position.y = _authoredLayers[role].transform.localPosition.y;
                        _previewPositions[role] = position;
                    }

            // Убираем фазу до восстановления слоя и возможного обновления окна.
            IsPreviewPlaying = false;
            _previewStrips.Clear();
            _previousPreviewTime = 0d;

            // Авторские высоты сохраняются отдельно от горизонтального движения.
            foreach (var role in Roles)
                if (_authoredLayers.TryGetValue(role, out var renderer) && renderer != null)
                    renderer.transform.localPosition = _previewPositions[role];
            if (Environment != null)
                Environment.ClearCopies();
        }

        /// <summary>Убирает редакторскую прокрутку перед переключением Play Mode.</summary>
        private void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode)
                EndPreviewPlayback();
        }

        /// <summary>Сдвигает просмотр по горизонтали, сохраняя авторские позиции слоёв.</summary>
        public void SetPreviewOffsetX(float x)
        {
            if (!IsActive || !float.IsFinite(x))
                return;
            PreviewOffsetX = Mathf.Clamp(x, -PreviewScrollRange, PreviewScrollRange);
            UpdatePreview();
        }

        /// <summary>Фиксирует авторскую настройку, обновляет камеру и прокручивает активный просмотр.</summary>
        public bool UpdatePreview()
        {
            if (!IsActive)
                return false;
            var changed = false;

            // Play Mode завершает редакторскую прокрутку до игровых обновлений.
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                if (IsPreviewPlaying)
                    EndPreviewPlayback();
                return false;
            }

            // Редакторская пауза возвращает авторскую композицию.
            if (IsPreviewPlaying && EditorApplication.isPaused)
            {
                EndPreviewPlayback();
                changed = true;
            }

            // Фиксируем дорожный якорь, масштаб и горизонтальные позиции.
            var root = Environment.transform;
            var rootPosition = new Vector3(0f, LocationEnvironment.RoadBottomWorldY, 0f);
            changed |= root.position != rootPosition || root.rotation != Quaternion.identity || root.localScale != Vector3.one;
            if (root.position != rootPosition || root.rotation != Quaternion.identity)
                root.SetPositionAndRotation(rootPosition, Quaternion.identity);
            if (root.localScale != Vector3.one)
                root.localScale = Vector3.one;
            // Горизонтальный просмотр принадлежит камере вне корня экспортируемого префаба.
            var cameraPosition = Consts.CameraPosition + Vector3.right * PreviewOffsetX;
            if (!Camera.orthographic || !Mathf.Approximately(Camera.orthographicSize, Consts.CameraSize) ||
                Camera.transform.position != cameraPosition || Camera.transform.rotation != Quaternion.identity)
            {
                LocationEnvironment.ConfigureCamera(Camera);
                Camera.transform.position = cameraPosition;
                changed = true;
            }

            // В композиции меняется только высота трёх задних слоёв.
            foreach (var role in Roles)
            {
                var transform = Environment.GetLayer(role).transform;
                var position = _initialPositions[role];
                if (role != EnvironmentLayerRole.Road && float.IsFinite(transform.localPosition.y))
                    position.y = transform.localPosition.y;
                var displayPosition = position;
                if (IsPreviewPlaying)
                    displayPosition.x = _previewStrips[role].SourceX - root.position.x;
                changed |= transform.localPosition != displayPosition || transform.localRotation != Quaternion.identity ||
                           transform.localScale != Vector3.one || _previewPositions[role] != position;
                if (transform.localPosition != displayPosition)
                    transform.localPosition = displayPosition;
                _previewPositions[role] = position;
                if (transform.localRotation != Quaternion.identity)
                    transform.localRotation = Quaternion.identity;
                if (transform.localScale != Vector3.one)
                    transform.localScale = Vector3.one;
            }

            // Новое устройство меняет необходимую ширину повторения.
            if (changed || !Mathf.Approximately(_previewAspect, Camera.aspect))
            {
                RefreshPreview();
                changed = true;
            }

            // Скорости читаются на каждом кадре; копии сохраняются между кадрами.
            if (IsPreviewPlaying)
            {
                var now = EditorApplication.timeSinceStartup;
                UpdatePreviewStrips(Math.Max(0d, now - _previousPreviewTime));
                _previousPreviewTime = now;
                SceneView.RepaintAll();
                EditorApplication.QueuePlayerLoopUpdate();
                changed = true;
            }
            return changed;
        }

        /// <summary>Повторяет четыре слоя на ширину текущего устройства.</summary>
        public void RefreshPreview()
        {
            if (!IsActive)
                return;
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            // Активная прокрутка сохраняет пул, неподвижный просмотр строит статические повторы.
            _previewAspect = Camera.aspect;
            if (IsPreviewPlaying)
                UpdatePreviewStrips(0d);
            else
            {
                var halfWidth = Camera.orthographicSize * Mathf.Max(_previewAspect, 1792f / 828f);
                Environment.PopulateStatic(Camera.transform.position.x - halfWidth, Camera.transform.position.x + halfWidth);
            }

            // Служебные копии видны в симуляторе, а редактируются только авторские слои.
            foreach (Transform child in Environment.transform)
            {
                if (Roles.Any(role => Environment.GetLayer(role).transform == child))
                    continue;
                child.gameObject.hideFlags = HideFlags.HideInHierarchy | HideFlags.DontSaveInEditor;
                SceneVisibilityManager.instance.DisablePicking(child.gameObject, true);
            }
            SceneView.RepaintAll();
            EditorApplication.QueuePlayerLoopUpdate();
        }

        /// <summary>Покрывает ширину устройства четырьмя полосами с независимыми скоростями.</summary>
        private void UpdatePreviewStrips(double deltaTime)
        {
            // Область повторов следует устройству и горизонтальному сдвигу камеры.
            var halfWidth = Camera.orthographicSize * Mathf.Max(Camera.aspect, 1792f / 828f);
            var center = Camera.transform.position.x;

            // Undo и настройка скорости применяются с сохранением текущей фазы.
            foreach (var role in Roles)
            {
                var strip = _previewStrips[role];
                strip.SetSpeed(GetPreviewSpeed(role));
                strip.Update(center - halfWidth, center + halfWidth, deltaTime);
            }
        }

        /// <summary>Экспортирует композицию, затем возвращается в Bootstrap.</summary>
        public string Save()
        {
            if (!IsActive)
                throw new InvalidOperationException("Черновая композиция закрыта.");
            var path = BackgroundAssetExporter.Save(this);
            Dispose();
            return path;
        }

        /// <summary>Отбрасывает черновую сцену и освобождает временные пиксели.</summary>
        public void Dispose() => Dispose(true);

        /// <summary>Освобождает ресурсы; при завершении редактирования открывает Bootstrap.</summary>
        public void Dispose(bool openBootstrap)
        {
            if (_disposed)
                return;

            // Сначала завершаем прокрутку и отключаем подписку сессии.
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EndPreviewPlayback();
            _disposed = true;

            // Удаляем черновые объекты до освобождения спрайтов и текстур.
            if (Environment != null)
                Object.DestroyImmediate(Environment.gameObject);
            if (Camera != null)
                Object.DestroyImmediate(Camera.gameObject);
            foreach (var resource in _temporaryResources)
                if (resource != null)
                    Object.DestroyImmediate(resource);
            _temporaryResources.Clear();
            _textures.Clear();

            // Сцена композиции существует только в текущей редакторской сессии.
            if (openBootstrap && _scene.IsValid() && _scene.isLoaded && !EditorApplication.isPlayingOrWillChangePlaymode)
                EditorSceneManager.OpenScene("Assets/Scenes/Bootstrap.unity", OpenSceneMode.Single);
        }

        /// <summary>Получает техническое имя существующей локации.</summary>
        public static string GetLocationSlug(string locationId) => LocationAssetFallback.ToLocationSlug(locationId);

        /// <summary>Проверяет принадлежность четырёх рисунков текущему черновику.</summary>
        public void ValidateForSave()
        {
            if (!IsActive)
                throw new InvalidOperationException("Черновая композиция закрыта.");
            // Экспорт получает только авторские позиции и четыре исходных слоя.
            EndPreviewPlayback();

            // Проверяем принадлежность исходных рисунков перед сохранением.
            foreach (var role in Roles)
            {
                var renderer = Environment.GetLayer(role);
                if (renderer != _authoredLayers[role] || renderer.sprite != _authoredSprites[role] ||
                    renderer.sortingLayerName != LocationEnvironment.GetSortingLayer(role))
                    throw new InvalidOperationException($"Слой {role} изменён за пределами вертикальной настройки. Создайте композицию заново.");
            }
        }

        /// <summary>Проверяет, что выбранный узел принадлежит исходному документу.</summary>
        private static bool ContainsNode(IReadOnlyList<ProcreateNode> nodes, ProcreateNode target)
        {
            foreach (var node in nodes)
                if (ReferenceEquals(node, target) || ContainsNode(node.Children, target))
                    return true;
            return false;
        }
    }
}
