using System.Collections;
using System.Collections.Generic;
using System;
using Assets.Scripts.Diagnostics;
using Assets.Scripts.System;
using UnityEngine;
using UnityEngine.UIElements;
using Vues.GameCore;

public class Intro : MonoBehaviour
{
    private List<VisualElement> _introImages = new();
    private float _shiftSpeed = 100f;
    private UIDocument _uiDocument;
    private VisualElement _introScreen;
    private LocalizedButton _skipButton;

    private VisualElement _container;

    private float _imageHeightFactor = 0.7f;
    private float _fadeDuration = 3f;
    private int _fadeSteps = 10;
    private float _waitAfterFade = 2f;
    private float _gapBetweenImages = 40f;
    private float _timeBetweenImageScrollStarts = 9f;
    private float _imageWidth;
    private float _imageHeight;
    private float _initialImageLeft;
    private float _initialImageTop;
    private float _imagePitch;

    // Храним запущенную корутину интро, флаг одноразового завершения и ссылку на обработчик "Skip"
    private Coroutine _introRoutine;           // чтобы корректно останавливать реальную корутину
    private bool _ended;                       // чтобы EndIntro выполнился один раз
    private EventCallback<ClickEvent> _skipHandler; // чтобы корректно отписаться от клика

    public void Initialize(List<Sprite> introSprites)
    {
        try
        {
            _uiDocument = GameObject.Find("[UI]").GetComponent<UIDocument>();
            CreateIntroScreen(introSprites);

            if (_introImages.Count == 1)
            {
                _introRoutine = StartCoroutine(PlaySingleImageIntro(_introImages[0]));
            }
            else
            {
                _introRoutine = StartCoroutine(PlayIntroSequence());
            }

            DebugManager.DiagStability($"[INTRO] initialize completed images={_introImages.Count}");
        }
        catch (Exception exception)
        {
            LogIntroException("initialize", exception);
            throw;
        }
    }

    private void CreateIntroScreen(List<Sprite> introSprites)
    {
        _introScreen = new VisualElement
        {
            style =
            {
                width = Length.Percent(100),
                height = Length.Percent(100),
                backgroundColor = new StyleColor(new Color(0.663f, 0.416f, 0.235f, 1)),
                position = Position.Absolute,
                justifyContent = Justify.Center
            }
        };
        _uiDocument.rootVisualElement.Add(_introScreen);
        InitImages(introSprites);
        AddSkipButton();
    }

    private void InitImages(List<Sprite> introSprites)
    {
        // Calculate individual image dimensions
        _imageHeight = Screen.height * _imageHeightFactor;
        _imageWidth = _imageHeight; // Square images based on height
        _initialImageLeft = (Screen.width - _imageWidth) * 0.5f;
        _initialImageTop = (Screen.height - _imageHeight) * 0.5f;
        _shiftSpeed = (_imageWidth + _gapBetweenImages) / _timeBetweenImageScrollStarts;


        // Create the container and set its position
        _container = new VisualElement
        {
            style =
            {
                width = Length.Percent(100),
                height = Length.Percent(100),
                position = Position.Relative,
                overflow = Overflow.Hidden,
                justifyContent = Justify.Center,
                alignItems = Align.Center,
                alignContent = Align.Center,

            }
        };

        // Add the container to the root screen
        _introScreen.Add(_container);

        foreach (var sprite in introSprites)
        {
            // Create the image element
            VisualElement imageElement = new VisualElement
            {
                style =
            {
                position = Position.Absolute,
                left = _initialImageLeft,
                top = _initialImageTop,
                width = _imageWidth,
                height = _imageHeight,
                opacity = 0,
            }
            };

            // Set the background image
            Texture2D texture = sprite.texture;
            imageElement.style.backgroundImage = new StyleBackground(texture);

            // Add the image to the container
            _introImages.Add(imageElement);
        }
    }


    private void AddSkipButton()
    {
        _skipButton = new LocalizedButton { key = "btn_skip" };

        _skipButton.style.position = Position.Absolute;
        _skipButton.style.right = 50;
        _skipButton.style.bottom = 30;

        // Сохраняем делегат, чтобы потом отписаться им же (лямбда в Unregister не сработает)
        _skipHandler = _ => SkipIntro();
        _skipButton.RegisterCallback(_skipHandler);

        _skipButton.AddToClassList("lcs_btn");
        _introScreen.Add(_skipButton);
    }

    public void SkipIntro()
    {
        // Останавливаем именно ту корутину, что была запущена
        if (_introRoutine != null)
        {
            StopCoroutine(_introRoutine);
            _introRoutine = null;
        }

        // Подстраховка от параллельных корутин, если такие появятся
        StopAllCoroutines();

        EndIntro();
    }

    private IEnumerator PlaySingleImageIntro(VisualElement image)
    {
        yield return WaitForContainerLayout();
        UpdateImageLayoutMetrics();
        ResetImagePosition(image);
        AddImageBehindMovingImages(image);

        float fadeStepTime = _fadeDuration / _fadeSteps;

        for (int step = 1; step <= _fadeSteps; step++)
        {
            image.style.opacity = step / (float)_fadeSteps;
            yield return new WaitForSeconds(fadeStepTime);
        }

        yield return new WaitForSeconds(_waitAfterFade);
        EndIntro();
    }

    private IEnumerator PlayIntroSequence()
    {
        yield return WaitForContainerLayout();
        UpdateImageLayoutMetrics();
        AddImagesToTape();

        yield return FadeInFirstImage();
        yield return MoveTapeUntilLastImageExits();
        EndIntro();
    }

    private IEnumerator FadeInFirstImage()
    {
        var image = _introImages[0];
        float elapsed = 0f;

        while (elapsed < _fadeDuration)
        {
            elapsed += Time.deltaTime;
            image.style.opacity = Mathf.Clamp01(elapsed / _fadeDuration);
            yield return null;
        }

        image.style.opacity = 1f;
    }

    private IEnumerator MoveTapeUntilLastImageExits()
    {
        while (_introImages[^1].style.left.value.value + _imageWidth > 0f)
        {
            MoveTapeLeft();
            UpdateTapeOpacity();
            yield return null;
        }

        RemoveTapeImages();
    }

    private void MoveTapeLeft()
    {
        float shift = _shiftSpeed * Time.deltaTime;
        foreach (var image in _introImages)
        {
            image.style.left = image.style.left.value.value - shift;
        }
    }

    private void UpdateTapeOpacity()
    {
        for (int index = 1; index < _introImages.Count; index++)
        {
            var image = _introImages[index];
            float distanceToCenter = image.style.left.value.value - _initialImageLeft;
            image.style.opacity = Mathf.Clamp01(1f - distanceToCenter / _imagePitch);
        }
    }

    private void AddImagesToTape()
    {
        for (int index = 0; index < _introImages.Count; index++)
        {
            var image = _introImages[index];
            image.style.left = _initialImageLeft + _imagePitch * index;
            image.style.top = _initialImageTop;
            image.style.opacity = 0f;
            _container.Add(image);
        }
    }

    private void RemoveTapeImages()
    {
        foreach (var image in _introImages)
        {
            image.RemoveFromHierarchy();
        }
    }

    private void ResetImagePosition(VisualElement image)
    {
        image.style.left = _initialImageLeft;
        image.style.top = _initialImageTop;
        image.style.opacity = 0f;
    }

    private IEnumerator WaitForContainerLayout()
    {
        const int maxWaitFrames = 10;

        for (int frame = 0; frame < maxWaitFrames && !HasResolvedContainerSize(); frame++)
        {
            yield return null;
        }
    }

    private bool HasResolvedContainerSize()
    {
        return IsUsableSize(_container.resolvedStyle.width) &&
               IsUsableSize(_container.resolvedStyle.height);
    }

    private void UpdateImageLayoutMetrics()
    {
        float containerWidth = GetUsableSize(_container.resolvedStyle.width, Screen.width);
        float containerHeight = GetUsableSize(_container.resolvedStyle.height, Screen.height);

        _imageHeight = containerHeight * _imageHeightFactor;
        _imageWidth = _imageHeight;
        _initialImageLeft = (containerWidth - _imageWidth) * 0.5f;
        _initialImageTop = (containerHeight - _imageHeight) * 0.5f;
        _imagePitch = _imageWidth + _gapBetweenImages;
        _shiftSpeed = _imagePitch / _timeBetweenImageScrollStarts;

        foreach (var image in _introImages)
        {
            image.style.width = _imageWidth;
            image.style.height = _imageHeight;
            ResetImagePosition(image);
        }
    }

    private static bool IsUsableSize(float value)
    {
        return !float.IsNaN(value) && value > 0f;
    }

    private static float GetUsableSize(float value, float fallback)
    {
        return IsUsableSize(value) ? value : fallback;
    }

    private void AddImageBehindMovingImages(VisualElement image)
    {
        if (_container.childCount == 0)
        {
            _container.Add(image);
            return;
        }

        _container.Insert(0, image);
    }

    private void EndIntro()
    {
        // Делает EndIntro идемпотентным — вызовется один раз
        if (_ended)
        {
            return;
        }

        _ended = true;

        // Корректно отписываем кнопку Skip (важно — тем же делегатом)
        if (_skipHandler != null)
        {
            _skipButton.UnregisterCallback(_skipHandler);
            _skipHandler = null;
        }

        _introScreen.RemoveFromHierarchy();

        // Запуск игры допускаем только из состояния INTRO, чтобы не «оживать» после луз-модалки
        var gm = LevelController.Instance.LevelData.GameManager;
        if (gm.State == Assets.Scripts.GameManagerLogic.GameState.INTRO)
        {
            try
            {
                gm.StartGame();
            }
            catch (Exception exception)
            {
                LogIntroException("start_game", exception);
                throw;
            }
        }

        LevelDataProvider.ReleaseIntroSprites();
    }

    private static void LogIntroException(string context, Exception exception)
    {
        DebugManager.DiagStability(
            $"[INTRO] exception context={context} type={exception.GetType().FullName} " +
            $"message={exception.Message} stack={exception.StackTrace}");
        Debug.LogException(exception);
        DeviceLogUploader.UploadDiagnosticLog("intro_exception");
    }

}
