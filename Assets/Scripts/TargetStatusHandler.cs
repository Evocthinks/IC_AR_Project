using UnityEngine;
using UnityEngine.Events;
using Vuforia;

// Hängt am Image Target. Meldet nur Wechsel zwischen "sichtbar" und "verloren",
// damit UI-Hinweise und Wartungspanel nicht bei jedem Statuswechsel flackern.
[RequireComponent(typeof(ObserverBehaviour))]
public class TargetStatusHandler : MonoBehaviour
{
    [SerializeField] bool countExtendedTracking = true;
    [SerializeField] UnityEvent onTargetFound;
    [SerializeField] UnityEvent onTargetLost;

    ObserverBehaviour observer;
    bool visible;

    void Awake()
    {
        observer = GetComponent<ObserverBehaviour>();
        observer.OnTargetStatusChanged += OnStatusChanged;
    }

    void Start()
    {
        // Startzustand: Marker noch nicht erkannt, Hinweis anzeigen.
        onTargetLost.Invoke();
    }

    void OnDestroy()
    {
        if (observer != null)
            observer.OnTargetStatusChanged -= OnStatusChanged;
    }

    void OnStatusChanged(ObserverBehaviour behaviour, TargetStatus status)
    {
        bool nowVisible = status.Status == Status.TRACKED
            || (countExtendedTracking && status.Status == Status.EXTENDED_TRACKED);

        if (nowVisible == visible)
            return;

        visible = nowVisible;
        Debug.Log($"Target {behaviour.TargetName}: {status.Status} ({status.StatusInfo})");

        if (visible)
            onTargetFound.Invoke();
        else
            onTargetLost.Invoke();
    }
}
