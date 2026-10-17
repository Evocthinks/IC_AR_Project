using TMPro;
using UnityEngine;

// Simulierte Messwerte des Schaltschranks als einfacher digitaler Zwilling.
// Verschmutzter Filter: hoher Differenzdruck und steigende Innentemperatur, nach dem Tausch normal.
public class SensorTwin : MonoBehaviour
{
    [SerializeField] TMP_Text valuesLabel;
    [SerializeField] float refreshInterval = 0.25f;
    [SerializeField] float pressureWarning = 120f;
    [SerializeField] float temperatureWarning = 40f;

    bool filterReplaced;
    bool fanRunning = true;
    float timer;
    float temperature = 43f;

    public void SetFilterReplaced(bool value) => filterReplaced = value;
    public void SetFanRunning(bool value) => fanRunning = value;

    void Update()
    {
        timer += Time.deltaTime;
        if (timer < refreshInterval)
            return;
        timer = 0f;

        float noise = Mathf.PerlinNoise(Time.time * 0.3f, 0f) - 0.5f;

        float pressure = !fanRunning ? 0f : (filterReplaced ? 45f : 150f) + noise * 10f;
        float rpm = fanRunning ? (filterReplaced ? 2800f : 2350f) + noise * 60f : 0f;

        // Temperatur läuft langsam auf ihren Zielwert zu statt zu springen
        float targetTemp = !fanRunning ? 48f : (filterReplaced ? 31f : 43f);
        temperature = Mathf.MoveTowards(temperature, targetTemp + noise, 0.4f);

        valuesLabel.text =
            $"Innentemperatur: {Colored(temperature, temperatureWarning, "°C")}\n" +
            $"Differenzdruck Filter: {Colored(pressure, pressureWarning, "Pa")}\n" +
            $"Lüfterdrehzahl: {rpm:0} 1/min";
    }

    static string Colored(float value, float warning, string unit)
    {
        string color = value >= warning ? "#E53935" : "#43A047";
        return $"<color={color}>{value:0.0} {unit}</color>";
    }
}
