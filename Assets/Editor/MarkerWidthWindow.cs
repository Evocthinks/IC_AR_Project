using UnityEditor;
using UnityEngine;

// Fenster zum Eintragen der tatsächlichen Markerbreite, gedruckt oder am Bildschirm gemessen.
// Vuforia rechnet mit dieser Breite die Entfernung aus, davon hängt die Größe des Schrankmodells ab.
public class MarkerWidthWindow : EditorWindow
{
    float widthCm = 15f;

    [MenuItem("Tools/AR Wartung/Markerbreite setzen …", priority = 10)]
    static void Open()
    {
        var window = GetWindow<MarkerWidthWindow>(true, "Markerbreite", true);
        window.minSize = new Vector2(380f, 200f);
        float current = WartungSetup.CurrentMarkerWidth();
        if (current > 0f)
            window.widthCm = Mathf.Round(current * 1000f) / 10f;
    }

    void OnGUI()
    {
        EditorGUILayout.HelpBox(
            "Sichtbare Breite des Markers messen, von Außenkante zu Außenkante des schwarzen Rahmens.\n" +
            "Am Bildschirm: Bild anzeigen, Zoom einstellen, messen und den Zoom danach nicht mehr ändern.",
            MessageType.Info);
        EditorGUILayout.Space();
        widthCm = EditorGUILayout.FloatField("Breite in cm", widthCm);
        EditorGUILayout.Space();

        if (GUILayout.Button("Übernehmen und Szene speichern"))
        {
            if (WartungSetup.ApplyMarkerWidth(widthCm / 100f))
                Close();
            // Szene speichern und Dialoge mitten im Layout würden sonst GUI-Fehler auslösen
            GUIUtility.ExitGUI();
        }
    }
}
