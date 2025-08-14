using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Manages the safe area borders for devices with notches or rounded corners. This uses a
/// container element on top of all UIs that need to respect the safe area. Then, it adjusts the
/// borderWidth property to match the Screen.safeArea property values.
/// </summary>
[ExecuteInEditMode]
public class SaveArea : MonoBehaviour
{
    [Tooltip("UI document that contains the UXML hierarchy")]
    [SerializeField] UIDocument _document;
    [Tooltip("Color for the border area. Use a transparent color to show the background.")]
    [SerializeField] Color _borderColor = Color.black;
    [Tooltip("Name of top-level element container. Leave empty to use rootVisualElement.")]
    [SerializeField] string _element;
    [Tooltip("Percentage multiplier for safe area distance")]
    [Range(0, 1f)]
    [SerializeField] float _multiplier = 1f;
    // Start is called before the first frame update
    VisualElement _root;
    float _leftBorder;
    float _rightBorder;
    float _topBorder;
    float _bottomBorder;

    public VisualElement RootElement => _root;
    public float LeftBorder => _leftBorder;
    public float RightBorder => _rightBorder;
    public float TopBorder => _topBorder;
    public float BottomBorder => _bottomBorder;

    public float Multiplier { get => _multiplier; set => _multiplier = value; }

    void Awake()
    {
        Initialize();
    }

    public void Initialize()
    {

        if (_document == null || _document.rootVisualElement == null)
        {
            Debug.LogWarning("UIDocument or rootVisualElement is null. Delaying initialization.");
            return;
        }

        // Choose the root VisualElement if nothing is specified
        if (string.IsNullOrEmpty(_element))
        {
            _root = _document.rootVisualElement;
        }
        // Otherwise, try to find the container by name
        else
        {
            _root = _document.rootVisualElement.Q<VisualElement>(_element);
        }

        if (_root == null)
        {
            return;
        }

        // Register a callback for when the UI geometry changes
        _root.RegisterCallback<GeometryChangedEvent>(evt => OnGeometryChangedEvent());

        ApplySafeArea();
    }

    void OnGeometryChangedEvent()
    {
        ApplySafeArea();
    }

    void OnValidate()
    {
        // Call ApplySafeArea when m_Multiplier is changed
        ApplySafeArea();
    }

    // Applies the safe area to the borders
    void ApplySafeArea()
    {
        if (_root == null)
            return;

        Rect safeArea = Screen.safeArea;

        // Calculate borders based on safe area rect
        _leftBorder = safeArea.x;
        _rightBorder = Screen.width - safeArea.xMax;
        _topBorder = Screen.height - safeArea.yMax;
        _bottomBorder = safeArea.y;


        // Set border widths regardless of orientation
        _root.style.borderTopWidth = _topBorder * _multiplier;
        _root.style.borderBottomWidth = _bottomBorder * _multiplier;
        _root.style.borderLeftWidth = _leftBorder * _multiplier;
        _root.style.borderRightWidth = _rightBorder * _multiplier;


        // Apply border color
        _root.style.borderBottomColor = _borderColor;
        _root.style.borderTopColor = _borderColor;
        _root.style.borderLeftColor = _borderColor;
        _root.style.borderRightColor = _borderColor;
    }
}

