using System.Collections;
using System.Collections.Generic;
using Assets.Scripts.System;
using UnityEngine;
using UnityEngine.UIElements;
using Vues.GameCore;

public class Intro : MonoBehaviour
{
    private List<VisualElement> _introImages = new();
    private float _shiftSpeed = 100f;
    private bool _skipIntro = false;
    private UIDocument _uiDocument;
    private VisualElement _introScreen;
    private LocalizedButton _skipButton;

    private VisualElement _container;

    private float _imageHeightFactor = 0.7f;
    private float _fadeDuration = 1f;
    private int _fadeSteps = 10;
    private float _waitAfterFade = 2f;

    // Храним запущенную корутину интро, флаг одноразового завершения и ссылку на обработчик "Skip"
    private Coroutine _introRoutine;           // чтобы корректно останавливать реальную корутину
    private bool _ended;                       // чтобы EndIntro выполнился один раз
    private EventCallback<ClickEvent> _skipHandler; // чтобы корректно отписаться от клика

    public void Initialize(List<Sprite> introSprites)
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
        // Offset between images in the container
        float offset = 20f;

        // Calculate individual image dimensions
        float imageHeight = Screen.height * 0.7f;
        float imageWidth = imageHeight; // Square images based on height


        // Create the container and set its position
        _container = new VisualElement
        {
            style =
            {
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
                width = imageWidth,
                height = imageHeight,
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
        float fadeStepTime = _fadeDuration / _fadeSteps;

        foreach (var sprite in _introImages)
        {
            _container.Add(sprite);

            // Fade in the image
            for (int step = 1; step <= _fadeSteps; step++)
            {
                sprite.style.opacity = step / (float)_fadeSteps;
                yield return new WaitForSeconds(fadeStepTime);
            }

            // Hold the image for 2 seconds
            yield return new WaitForSeconds(2f);

            // Fade out the image
            for (int step = _fadeSteps - 1; step >= 0; step--)
            {
                sprite.style.opacity = step / (float)_fadeSteps;
                yield return new WaitForSeconds(fadeStepTime);
            }
            _container.Remove(sprite);
        }

        EndIntro();
    }



    private IEnumerator ShiftImagesLeft(float speed)
    {
        while (_introImages[^1].style.left.value.value > 0)
        {
            if (_skipIntro) yield break;

            foreach (var image in _introImages)
            {
                image.style.left = new StyleLength(image.style.left.value.value - speed * Time.deltaTime);
            }
            yield return null;
        }
    }

    private void EndIntro()
    {
        // Делает EndIntro идемпотентным — вызовется один раз
        if (_ended) return;
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
            gm.StartGame();
        }

        LevelDataProvider.ReleaseIntroSprites();
    }
}
