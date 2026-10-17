using System;
using TMPro;
using UnityEngine;

[Serializable]
public class MaintenanceStep
{
    public string id;
    public string title;
    public string text;
    public string part;
    public bool fanOff;
    public bool replacesFilter;
}

[Serializable]
public class MaintenanceProcedure
{
    public string name;
    public MaintenanceStep[] steps;
}

// Führt Schritt für Schritt durch den Ablauf aus der JSON-Datei, hebt das passende Bauteil hervor
// und koppelt den Fortschritt an die simulierten Messwerte und das Protokoll.
public class MaintenanceGuide : MonoBehaviour
{
    [SerializeField] TextAsset procedureJson;
    [SerializeField] TMP_Text titleLabel;
    [SerializeField] TMP_Text bodyLabel;
    [SerializeField] TMP_Text progressLabel;
    [SerializeField] GameObject finishedPanel;
    [SerializeField] PartHighlighter[] parts;
    [SerializeField] SensorTwin sensorTwin;
    [SerializeField] MaintenanceLog log;

    MaintenanceProcedure procedure;
    int current;

    void Awake()
    {
        procedure = JsonUtility.FromJson<MaintenanceProcedure>(procedureJson.text);
    }

    void Start()
    {
        Restart();
    }

    public void Next()
    {
        log.Write(procedure.name, procedure.steps[current].id, "erledigt");

        if (current == procedure.steps.Length - 1)
        {
            finishedPanel.SetActive(true);
            log.Write(procedure.name, "-", "ablauf abgeschlossen");
            HighlightPart(null);
            return;
        }
        ShowStep(current + 1);
    }

    public void Previous()
    {
        if (current > 0)
            ShowStep(current - 1);
    }

    public void Restart()
    {
        finishedPanel.SetActive(false);
        ShowStep(0);
    }

    void ShowStep(int index)
    {
        current = index;
        MaintenanceStep step = procedure.steps[index];

        titleLabel.text = step.title;
        bodyLabel.text = step.text;
        progressLabel.text = $"Schritt {index + 1} von {procedure.steps.Length}";
        HighlightPart(step.part);

        // Messwerte aus dem bisherigen Fortschritt ableiten, damit auch "Zurück" stimmt
        bool fanOff = false;
        bool filterNew = false;
        for (int i = 0; i <= index; i++)
        {
            fanOff = procedure.steps[i].fanOff;
            filterNew |= procedure.steps[i].replacesFilter && i < index;
        }
        sensorTwin.SetFanRunning(!fanOff);
        sensorTwin.SetFilterReplaced(filterNew);
    }

    void HighlightPart(string partId)
    {
        foreach (PartHighlighter p in parts)
            p.SetHighlighted(p.PartId == partId);
    }
}
