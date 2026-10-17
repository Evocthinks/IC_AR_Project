using System;
using System.IO;
using UnityEngine;

// Schreibt erledigte Schritte mit Zeitstempel als CSV in den App-Datenordner.
public class MaintenanceLog : MonoBehaviour
{
    [SerializeField] string fileName = "wartungsprotokoll.csv";

    string FilePath => Path.Combine(Application.persistentDataPath, fileName);

    public void Write(string procedure, string stepId, string action)
    {
        try
        {
            if (!File.Exists(FilePath))
                File.WriteAllText(FilePath, "zeitstempel;ablauf;schritt;aktion\n");

            string line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss};{procedure};{stepId};{action}\n";
            File.AppendAllText(FilePath, line);
        }
        catch (IOException e)
        {
            Debug.LogWarning($"Protokoll konnte nicht geschrieben werden: {e.Message}");
        }
    }
}
