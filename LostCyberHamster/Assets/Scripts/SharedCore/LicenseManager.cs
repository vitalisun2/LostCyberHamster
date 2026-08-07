using System;
using System.Collections;
using Assets.Scripts.Entry_Points;
using LostCyberHamster.UI;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UIElements;

public class LicenseManager : MonoBehaviour
{
    private const string _timeApiUrl = "https://www.timeapi.io/api/time/current/zone?timeZone=Europe%2FMoscow";
    private DateTime _expirationDate = DateTime.Parse("2025-09-01");
    private const float _networkCheckInterval = 3f;  // Check network every 3 seconds
    private VisualElement _root;
    private VisualElement _fullScreenView;
    private DateTime? _currentTime = null;


    void Start()
    {
        var uiDocument = GameObject.Find("[UI]").GetComponent<UIDocument>();
        _root = uiDocument.rootVisualElement;
       StartCoroutine(CheckNetworkReachability());
    }

    private IEnumerator CheckNetworkReachability()
    {
        while (true)
        {
            // Check network availability
            bool networkAvailable = IsNetworkAvailable();

            if (!networkAvailable)
            {
                ShowFullScreenView("No Network", "Please check your internet connection.");
            }
            else
            {
                HideFullScreenView();
            }

            if (DateTime.Now < _expirationDate && networkAvailable)
            {
                if (_currentTime == null)
                {
                    yield return FetchCurrentTime();
                }
            }

            yield return new WaitForSeconds(_networkCheckInterval);
        }
    }

    private IEnumerator FetchCurrentTime()
    {
       UnityWebRequest request = UnityWebRequest.Get(_timeApiUrl);
        yield return request.SendWebRequest();

        if (request.result == UnityWebRequest.Result.Success)
        {
            string jsonResponse = request.downloadHandler.text;
            _currentTime = ParseTimeFromResponse(jsonResponse);

            if (_currentTime > _expirationDate)
            {
                ShowFullScreenView("License Expired", "This is a test build. Please contact support.");
            }
            else
            {
           }
        }
        else
        {
            Debug.LogError($"Failed to fetch current time. Error: {request.error}");
        }
    }

    private void ShowFullScreenView(string title, string message)
    {
        // Remove any existing full-screen view
        HideFullScreenView();

        // Create a new full-screen VisualElement
        _fullScreenView = new VisualElement
        {
            style =
            {
                width = new Length(100, LengthUnit.Percent),
                height = new Length(100, LengthUnit.Percent),
                flexDirection = FlexDirection.Column,
                justifyContent = Justify.Center,
                alignItems = Align.Center,
                position = Position.Absolute
            }
        };
        _fullScreenView.AddToClassList("license-overlay");

        // Create a title label
        var titleLabel = new Label(title)
        {
            style =
            {
                fontSize = 24,
                unityTextAlign = TextAnchor.MiddleCenter,
                marginBottom = 10
            }
        };
        titleLabel.AddToClassList("license-overlay-text");

        // Create a message label
        var messageLabel = new Label(message)
        {
            style =
            {
                fontSize = 18,
                unityTextAlign = TextAnchor.MiddleCenter
            }
        };
        messageLabel.AddToClassList("license-overlay-text");

        // Add labels to the full-screen view
        _fullScreenView.Add(titleLabel);
        _fullScreenView.Add(messageLabel);

        // Add the full-screen view to the root element
        _root.Add(_fullScreenView);
    }

    private void HideFullScreenView()
    {
        if (_fullScreenView != null && _root.Contains(_fullScreenView))
        {
            _root.Remove(_fullScreenView);
            _fullScreenView = null;
        }
    }

    private DateTime ParseTimeFromResponse(string jsonResponse)
    {
        var timeData = JsonUtility.FromJson<TimeApiResponse>(jsonResponse);
        return DateTime.Parse(timeData.dateTime);
    }

    [Serializable]
    private class TimeApiResponse
    {
        public int year;
        public int month;
        public int day;
        public int hour;
        public int minute;
        public int seconds;
        public int milliSeconds;
        public string dateTime; // "2025-01-20T16:50:57.8645434"
        public string date;     // "01/20/2025"
        public string time;     // "16:50"
        public string timeZone; // "Europe/Moscow"
        public string dayOfWeek; // "Monday"
        public bool dstActive;
    }

    public static bool IsNetworkAvailable()
    {
        switch (Application.internetReachability)
        {
            case NetworkReachability.NotReachable:
               return false;

            case NetworkReachability.ReachableViaCarrierDataNetwork:
            case NetworkReachability.ReachableViaLocalAreaNetwork:
               return true;

            default:
               return false;
        }
    }
}
