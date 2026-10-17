using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Vuforia;
using Image = UnityEngine.UI.Image;
using Object = UnityEngine.Object;

// Richtet das Projekt nach den Abschnitten 3.4 bis 3.7 und 3.10 der Anleitung ein:
// Szene "Wartung" mit Image Target, Schrankmodell und UI, Szene "Main" als Menü,
// Scene List, Player Settings und Versionsverwaltung. Menü: Tools > AR Wartung.
// Mehrfaches Ausführen ist möglich, die erzeugten Objekte werden dann neu aufgebaut.
public static class WartungSetup
{
    // Breite des Markers in Metern beim ersten Einrichten, später über Tools > AR Wartung > Markerbreite setzen änderbar
    const float MarkerWidth = 0.15f;

    const string SamplePath = "Assets/Scenes/SampleScene.unity";
    const string WartungPath = "Assets/Scenes/Wartung.unity";
    const string MainPath = "Assets/Scenes/Main.unity";
    const string ImagesDir = "Assets/Images";
    const string PreferredMarker = "Assets/Images/Marker_Schrank.png";
    const string JsonPath = "Assets/Data/wartungsschritte.json";
    const string MaterialsDir = "Assets/Materials";
    const string TmpSettingsPath = "Assets/TextMesh Pro/Resources/TMP Settings.asset";
    const string ApkPath = "Builds/AR_Wartung.apk";

    static readonly string[] GeneratedRoots = { "SchrankTarget", "Canvas", "EventSystem", "Logik" };

    [MenuItem("Tools/AR Wartung/Alles einrichten", priority = 0)]
    static void SetupAll()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("AR Wartung", "Bitte zuerst den Play Mode beenden.", "OK");
            return;
        }

        AssetDatabase.Refresh();

        // TextMeshPro braucht seine Grundressourcen, sonst haben die Texte keine Schrift
        if (!File.Exists(TmpSettingsPath))
        {
            AssetDatabase.importPackageCompleted += OnTmpImported;
            TMP_PackageResourceImporter.ImportResources(true, false, false);
            Debug.Log("AR Wartung: TMP-Ressourcen werden importiert, danach geht die Einrichtung automatisch weiter. " +
                      "Falls nicht, Tools > AR Wartung > Alles einrichten noch einmal wählen.");
            return;
        }

        string markerPath = FindMarkerPath();
        if (markerPath == null)
        {
            EditorUtility.DisplayDialog("AR Wartung", $"Kein Markerbild (PNG oder JPG) in {ImagesDir} gefunden.", "OK");
            return;
        }

        bool rebuild = File.Exists(MainPath) || File.Exists(WartungPath);
        if (rebuild && !EditorUtility.DisplayDialog("AR Wartung",
                "Die Szenen sind schon eingerichtet. Neu aufbauen?\n\n" +
                "SchrankTarget, Canvas, EventSystem und Logik in \"Wartung\" sowie die ganze Szene \"Main\" " +
                "werden ersetzt. Eigene Änderungen daran gehen verloren.",
                "Neu aufbauen", "Abbrechen"))
            return;

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        try
        {
            Texture2D marker = PrepareMarkerTexture(markerPath);
            bool markerAssigned = BuildWartungScene(marker, out string error);
            if (error != null)
            {
                EditorUtility.DisplayDialog("AR Wartung", error, "OK");
                return;
            }

            BuildMainScene();
            ConfigureProject();

            Scene wartung = EditorSceneManager.OpenScene(WartungPath, OpenSceneMode.Single);
            GameObject target = wartung.GetRootGameObjects().FirstOrDefault(g => g.name == "SchrankTarget");
            Selection.activeGameObject = target;
            EditorGUIUtility.PingObject(target);

            string message = "Fertig. Szenen \"Main\" und \"Wartung\" sind gebaut und gespeichert.\n\n" +
                             $"Markerbreite: {CurrentMarkerWidth() * 100f:0.#} cm. Weicht die gedruckte oder am Bildschirm " +
                             "angezeigte Breite ab, über Tools > AR Wartung > Markerbreite setzen anpassen.";
            if (!markerAssigned)
                message += "\n\nDas Markerbild konnte nicht automatisch zugewiesen werden. Am SchrankTarget im Inspector " +
                           $"Type auf \"From Image\" stellen und {Path.GetFileName(markerPath)} zuweisen.";
            Debug.Log("AR Wartung: " + message);

            if (EditorUserBuildSettings.activeBuildTarget == BuildTarget.Android)
            {
                EditorUtility.DisplayDialog("AR Wartung", message, "OK");
            }
            else if (EditorUtility.DisplayDialog("AR Wartung",
                         message + "\n\nJetzt auf Android umschalten? Das dauert einige Minuten.",
                         "Auf Android umschalten", "Später"))
            {
                EditorApplication.delayCall += () =>
                    EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
            }
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            EditorUtility.DisplayDialog("AR Wartung", "Einrichtung abgebrochen: " + e.Message, "OK");
        }
    }

    [MenuItem("Tools/AR Wartung/Android Build And Run", priority = 20)]
    static void BuildAndRun()
    {
        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
        {
            EditorUtility.DisplayDialog("AR Wartung",
                "Die aktive Plattform ist nicht Android. Erst über Tools > AR Wartung > Alles einrichten " +
                "oder File > Build Profiles auf Android umschalten.", "OK");
            return;
        }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        Directory.CreateDirectory(Path.GetDirectoryName(ApkPath));
        EditorUserBuildSettings.buildAppBundle = false;

        var options = new BuildPlayerOptions
        {
            scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
            locationPathName = ApkPath,
            target = BuildTarget.Android,
            targetGroup = BuildTargetGroup.Android,
            options = BuildOptions.AutoRunPlayer
        };

        BuildReport report = BuildPipeline.BuildPlayer(options);
        if (report.summary.result == BuildResult.Succeeded)
        {
            Debug.Log($"AR Wartung: Build erfolgreich, {ApkPath} ({report.summary.totalSize / (1024 * 1024)} MB).");
            EditorUtility.RevealInFinder(ApkPath);
        }
        else
        {
            Debug.LogError($"AR Wartung: Build fehlgeschlagen ({report.summary.result}), Details in der Console.");
        }
    }

    static void OnTmpImported(string packageName)
    {
        if (packageName != "TMP Essential Resources")
            return;
        AssetDatabase.importPackageCompleted -= OnTmpImported;
        EditorApplication.delayCall += SetupAll;
    }

    // ---------- Marker ----------

    static string FindMarkerPath()
    {
        if (File.Exists(PreferredMarker))
            return PreferredMarker;
        if (!AssetDatabase.IsValidFolder(ImagesDir))
            return null;
        return AssetDatabase.FindAssets("t:Texture2D", new[] { ImagesDir })
            .Select(AssetDatabase.GUIDToAssetPath)
            .FirstOrDefault(p => p.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
                              || p.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)
                              || p.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase));
    }

    // Vuforia liest das Bild zur Laufzeit aus der Textur, dafür muss sie lesbar und unkomprimiert sein
    static Texture2D PrepareMarkerTexture(string path)
    {
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Default;
        importer.isReadable = true;
        importer.mipmapEnabled = false;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.alphaSource = TextureImporterAlphaSource.None;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.maxTextureSize = 2048;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    // ---------- Szene "Wartung" (3.5 und 3.6) ----------

    static bool BuildWartungScene(Texture2D marker, out string error)
    {
        error = null;

        if (!File.Exists(WartungPath))
        {
            if (!File.Exists(SamplePath))
            {
                error = "Weder Wartung.unity noch SampleScene.unity in Assets/Scenes gefunden.";
                return false;
            }
            // Offene Szene schließen, bevor sie umbenannt wird
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            string renameError = AssetDatabase.RenameAsset(SamplePath, Path.GetFileNameWithoutExtension(WartungPath));
            if (!string.IsNullOrEmpty(renameError))
            {
                error = "SampleScene konnte nicht umbenannt werden: " + renameError;
                return false;
            }
        }

        Scene scene = EditorSceneManager.OpenScene(WartungPath, OpenSceneMode.Single);

        VuforiaBehaviour arCamera = FindInScene<VuforiaBehaviour>(scene).FirstOrDefault();
        if (arCamera == null)
        {
            error = "In der Szene Wartung fehlt die AR Camera (GameObject > Vuforia Engine > AR Camera).";
            return false;
        }

        // Beim Neuaufbau die bisher eingetragene Markerbreite behalten
        float width = MarkerWidth;
        ImageTargetBehaviour previous = FindSchrankTarget(scene);
        if (previous != null && previous.GetSize().x > 0f)
            width = previous.GetSize().x;

        foreach (GameObject old in scene.GetRootGameObjects().Where(g => GeneratedRoots.Contains(g.name)))
            Object.DestroyImmediate(old);

        // Antippen von 3D-Objekten braucht einen Physics Raycaster an der Kamera
        if (arCamera.GetComponent<PhysicsRaycaster>() == null)
            arCamera.gameObject.AddComponent<PhysicsRaycaster>();

        ImageTargetBehaviour target = CreateImageTarget(scene);
        if (target == null)
        {
            error = "Das Image Target konnte nicht über GameObject > Vuforia Engine > Image Target angelegt werden.";
            return false;
        }
        bool markerAssigned = ConfigureFromImage(target, marker, width);

        if (target.GetComponent<DefaultObserverEventHandler>() == null)
            target.gameObject.AddComponent<DefaultObserverEventHandler>();
        var status = target.gameObject.AddComponent<TargetStatusHandler>();

        PartHighlighter[] parts = BuildCabinet(target.transform);

        // UI
        Canvas canvas = CreateCanvas();
        CreateEventSystem();
        Transform root = canvas.transform;

        GameObject hinweis = CreatePanel("HinweisPanel", root, 0.1f, 0.44f, 0.9f, 0.56f, new Color(0f, 0f, 0f, 0.6f));
        CreateText("HinweisText", hinweis.transform, "Marker anvisieren", 64, TextAlignmentOptions.Center, 0f, 0f, 1f, 1f);

        GameObject anleitung = CreatePanel("AnleitungPanel", root, 0f, 0f, 1f, 0.33f, new Color(0f, 0f, 0f, 0.75f));
        TMP_Text titel = CreateText("TitelText", anleitung.transform, "Titel", 54, TextAlignmentOptions.TopLeft, 0.05f, 0.78f, 0.72f, 0.95f);
        titel.fontStyle = FontStyles.Bold;
        TMP_Text fortschritt = CreateText("FortschrittText", anleitung.transform, "Schritt 1 von 6", 36, TextAlignmentOptions.TopRight, 0.72f, 0.78f, 0.95f, 0.95f);
        TMP_Text beschreibung = CreateText("BeschreibungText", anleitung.transform, "Beschreibung", 40, TextAlignmentOptions.TopLeft, 0.05f, 0.30f, 0.95f, 0.76f);
        Button zurueck = CreateButton("ZurueckButton", anleitung.transform, "Zurück", 0.05f, 0.05f, 0.33f, 0.24f);
        Button neuStart = CreateButton("NeuStartenButton", anleitung.transform, "Neu starten", 0.36f, 0.05f, 0.64f, 0.24f);
        Button weiter = CreateButton("WeiterButton", anleitung.transform, "Weiter", 0.67f, 0.05f, 0.95f, 0.24f);

        GameObject messwerte = CreatePanel("MesswertePanel", root, 0.40f, 0.85f, 0.98f, 0.985f, new Color(0f, 0f, 0f, 0.6f));
        TMP_Text messwerteText = CreateText("MesswerteText", messwerte.transform, "Messwerte", 34, TextAlignmentOptions.TopLeft, 0.05f, 0.08f, 0.95f, 0.92f);
        messwerteText.richText = true;

        Button menue = CreateButton("MenueButton", root, "Menü", 0.02f, 0.925f, 0.22f, 0.98f);

        GameObject info = CreatePanel("InfoPanel", root, 0.08f, 0.38f, 0.92f, 0.66f, new Color(0.1f, 0.1f, 0.12f, 0.92f));
        TMP_Text infoTitel = CreateText("InfoTitel", info.transform, "Bauteil", 50, TextAlignmentOptions.TopLeft, 0.06f, 0.76f, 0.94f, 0.94f);
        infoTitel.fontStyle = FontStyles.Bold;
        TMP_Text infoText = CreateText("InfoText", info.transform, "Beschreibung", 38, TextAlignmentOptions.TopLeft, 0.06f, 0.28f, 0.94f, 0.74f);
        Button schliessen = CreateButton("SchliessenButton", info.transform, "Schließen", 0.3f, 0.06f, 0.7f, 0.22f);

        GameObject fertig = CreatePanel("FertigPanel", root, 0.08f, 0.35f, 0.92f, 0.65f, new Color(0.08f, 0.28f, 0.14f, 0.95f));
        CreateText("FertigText", fertig.transform, "Wartung abgeschlossen, Protokoll gespeichert.", 50, TextAlignmentOptions.Center, 0.06f, 0.40f, 0.94f, 0.92f);
        Button fertigNeuStart = CreateButton("NeuStartenButton", fertig.transform, "Neu starten", 0.3f, 0.08f, 0.7f, 0.30f);

        // Logik (3.6, Schritt 4 und 5)
        var logik = new GameObject("Logik");
        var guide = logik.AddComponent<MaintenanceGuide>();
        var twin = logik.AddComponent<SensorTwin>();
        var log = logik.AddComponent<MaintenanceLog>();
        var loader = logik.AddComponent<SceneLoader>();

        var json = AssetDatabase.LoadAssetAtPath<TextAsset>(JsonPath);
        if (json == null)
        {
            error = $"{JsonPath} nicht gefunden.";
            return false;
        }

        var guideSo = new SerializedObject(guide);
        Set(guideSo, "procedureJson", json);
        Set(guideSo, "titleLabel", titel);
        Set(guideSo, "bodyLabel", beschreibung);
        Set(guideSo, "progressLabel", fortschritt);
        Set(guideSo, "finishedPanel", fertig);
        SerializedProperty partsProp = guideSo.FindProperty("parts");
        partsProp.arraySize = parts.Length;
        for (int i = 0; i < parts.Length; i++)
            partsProp.GetArrayElementAtIndex(i).objectReferenceValue = parts[i];
        Set(guideSo, "sensorTwin", twin);
        Set(guideSo, "log", log);
        guideSo.ApplyModifiedPropertiesWithoutUndo();

        var twinSo = new SerializedObject(twin);
        Set(twinSo, "valuesLabel", messwerteText);
        twinSo.ApplyModifiedPropertiesWithoutUndo();

        foreach (PartInfo partInfo in parts.Select(p => p.GetComponent<PartInfo>()))
        {
            var so = new SerializedObject(partInfo);
            Set(so, "infoPanel", info);
            Set(so, "infoTitle", infoTitel);
            Set(so, "infoBody", infoText);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // Buttons (3.6, Schritt 6)
        AddCall(weiter, guide.Next);
        AddCall(zurueck, guide.Previous);
        AddCall(neuStart, guide.Restart);
        AddCall(fertigNeuStart, guide.Restart);
        UnityEventTools.AddBoolPersistentListener(schliessen.onClick, info.SetActive, false);
        UnityEventTools.AddStringPersistentListener(menue.onClick, loader.LoadScene, "Main");
        EditorUtility.SetDirty(schliessen);
        EditorUtility.SetDirty(menue);

        // Marker gefunden / verloren (3.6, Schritt 7)
        UnityEvent found = GetEvent(status, "onTargetFound");
        UnityEvent lost = GetEvent(status, "onTargetLost");
        UnityEventTools.AddBoolPersistentListener(found, hinweis.SetActive, false);
        UnityEventTools.AddBoolPersistentListener(found, anleitung.SetActive, true);
        UnityEventTools.AddBoolPersistentListener(found, messwerte.SetActive, true);
        UnityEventTools.AddBoolPersistentListener(lost, hinweis.SetActive, true);
        UnityEventTools.AddBoolPersistentListener(lost, anleitung.SetActive, false);
        UnityEventTools.AddBoolPersistentListener(lost, messwerte.SetActive, false);
        EditorUtility.SetDirty(status);

        info.SetActive(false);
        fertig.SetActive(false);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, WartungPath);
        return markerAssigned;
    }

    static ImageTargetBehaviour CreateImageTarget(Scene scene)
    {
        var before = new HashSet<ImageTargetBehaviour>(FindInScene<ImageTargetBehaviour>(scene));
        Selection.activeObject = null;
        if (!EditorApplication.ExecuteMenuItem("GameObject/Vuforia Engine/Image Target"))
            return null;

        ImageTargetBehaviour target = FindInScene<ImageTargetBehaviour>(scene).FirstOrDefault(t => !before.Contains(t));
        if (target == null)
            return null;

        target.name = "SchrankTarget";
        target.transform.SetParent(null, false);
        target.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        return target;
    }

    // Stellt das Target auf "From Image" um. Vuforia dokumentiert die Feldnamen nicht,
    // deshalb wird jedes Feld geprüft und bei Abweichung false zurückgegeben.
    static bool ConfigureFromImage(ImageTargetBehaviour target, Texture2D marker, float markerWidth)
    {
        var so = new SerializedObject(target);
        SerializedProperty type = so.FindProperty("mImageTargetType");
        SerializedProperty texture = so.FindProperty("mRuntimeTexture");
        SerializedProperty width = so.FindProperty("mWidth");
        if (type == null || texture == null || width == null || marker == null)
        {
            Debug.LogWarning("AR Wartung: Felder des Image Targets nicht gefunden, Bild bitte von Hand zuweisen.");
            return false;
        }

        if (type.propertyType == SerializedPropertyType.Enum)
        {
            int index = Array.FindIndex(type.enumNames, n => n.IndexOf("INSTANT", StringComparison.OrdinalIgnoreCase) >= 0);
            if (index < 0)
                return false;
            type.enumValueIndex = index;
        }
        else
        {
            Type enumType = target.GetType().GetProperty("ImageTargetType")?.PropertyType;
            if (enumType == null || !enumType.IsEnum)
                return false;
            string name = Enum.GetNames(enumType).FirstOrDefault(n => n.IndexOf("INSTANT", StringComparison.OrdinalIgnoreCase) >= 0);
            if (name == null)
                return false;
            type.intValue = Convert.ToInt32(Enum.Parse(enumType, name));
        }

        texture.objectReferenceValue = marker;
        SerializedProperty ratio = so.FindProperty("mAspectRatio");
        if (ratio != null)
            ratio.floatValue = (float)marker.height / marker.width;
        so.ApplyModifiedPropertiesWithoutUndo();

        return WriteSize(target, markerWidth);
    }

    // Im Editor wirkt SetWidth nicht, es braucht einen laufenden Observer. Deshalb wie der Vuforia-Inspector
    // direkt über die Felder. mInitializedInEditor verhindert, dass der Inspector beim ersten Anzeigen
    // seine Standardgröße von 0,2 m einträgt.
    static bool WriteSize(ImageTargetBehaviour target, float width)
    {
        var so = new SerializedObject(target);
        SerializedProperty widthProp = so.FindProperty("mWidth");
        if (widthProp == null)
            return false;

        // Ohne Namen scheitert Vuforia im Play Mode mit INVALID_TARGET_NAME. Der Inspector trägt sonst
        // den Texturnamen ein; hier der Objektname, damit das Log "Target SchrankTarget: ..." aus 3.8 passt.
        SerializedProperty trackableName = so.FindProperty("mTrackableName");
        if (trackableName != null && string.IsNullOrEmpty(trackableName.stringValue))
            trackableName.stringValue = target.name;

        SerializedProperty ratio = so.FindProperty("mAspectRatio");
        float aspect = ratio != null && ratio.floatValue > 0f ? ratio.floatValue : 1f;
        widthProp.floatValue = width;
        SerializedProperty height = so.FindProperty("mHeight");
        if (height != null)
            height.floatValue = width * aspect;
        SerializedProperty initialized = so.FindProperty("mInitializedInEditor");
        if (initialized != null && initialized.propertyType == SerializedPropertyType.Boolean)
            initialized.boolValue = true;
        so.ApplyModifiedProperties();

        RefreshVuforiaPreview();
        return true;
    }

    // Dasselbe, was der Vuforia-Inspector nach einer Größenänderung aufruft. Die Klasse ist nicht öffentlich
    // dokumentiert, deshalb per Reflection und ohne Abbruch, falls es sie nicht gibt.
    static void RefreshVuforiaPreview()
    {
        try
        {
            Type manager = Type.GetType("Vuforia.EditorClasses.VuforiaSceneManager, Vuforia.Unity.Editor");
            object instance = manager?.GetProperty("Instance", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)?.GetValue(null);
            MethodInfo apply = manager?.GetMethod("ApplyDataSetProperties",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, Type.EmptyTypes, null);
            if (instance != null && apply != null)
                apply.Invoke(instance, null);
        }
        catch (Exception e)
        {
            Debug.LogWarning("AR Wartung: Vuforia-Vorschau nicht aktualisiert: " + e.Message);
        }
    }

    // ---------- Markerbreite (gedruckt oder am Bildschirm) ----------

    static ImageTargetBehaviour FindSchrankTarget(Scene scene)
    {
        return FindInScene<ImageTargetBehaviour>(scene).FirstOrDefault(t => t.name == "SchrankTarget");
    }

    // Breite in Metern aus der offenen Szene, -1 wenn dort kein SchrankTarget ist
    internal static float CurrentMarkerWidth()
    {
        ImageTargetBehaviour target = FindSchrankTarget(SceneManager.GetActiveScene());
        return target != null ? target.GetSize().x : -1f;
    }

    internal static bool ApplyMarkerWidth(float meters)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("AR Wartung", "Bitte zuerst den Play Mode beenden.", "OK");
            return false;
        }
        if (meters < 0.02f || meters > 2f)
        {
            EditorUtility.DisplayDialog("AR Wartung", "Die Breite muss zwischen 2 und 200 cm liegen.", "OK");
            return false;
        }
        if (!File.Exists(WartungPath))
        {
            EditorUtility.DisplayDialog("AR Wartung", "Szene Wartung fehlt, zuerst Tools > AR Wartung > Alles einrichten.", "OK");
            return false;
        }

        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != WartungPath)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return false;
            scene = EditorSceneManager.OpenScene(WartungPath, OpenSceneMode.Single);
        }

        ImageTargetBehaviour target = FindSchrankTarget(scene);
        if (target == null)
        {
            EditorUtility.DisplayDialog("AR Wartung", "SchrankTarget nicht gefunden, zuerst Tools > AR Wartung > Alles einrichten.", "OK");
            return false;
        }

        if (!WriteSize(target, meters))
        {
            EditorUtility.DisplayDialog("AR Wartung", "Das Breitenfeld am SchrankTarget wurde nicht gefunden.", "OK");
            return false;
        }
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"AR Wartung: Markerbreite auf {target.GetSize().x * 100f:0.#} cm gesetzt, Szene gespeichert.");
        return true;
    }

    // Schrank aus Primitiven, Maße in Metern relativ zum Marker.
    // Vuforia: Y zeigt aus dem Marker heraus zur Kamera, Z zur Oberkante des Bildes.
    // Die Front liegt deshalb auf +Y, damit sie bei einem Marker an der Schranktür zur Kamera zeigt.
    static PartHighlighter[] BuildCabinet(Transform target)
    {
        CreatePart(target, "Schrank", PrimitiveType.Cube, new Vector3(0f, 0.025f, 0f), new Vector3(0.12f, 0.05f, 0.16f),
            new Color(0.62f, 0.64f, 0.67f), false);

        // Gitter halbtransparent, damit die Filtermatte dahinter beim Hervorheben sichtbar bleibt.
        // Die Matte ragt unten etwas heraus, so bleibt sie auch antippbar.
        GameObject gitter = CreatePart(target, "Gitter", PrimitiveType.Cube, new Vector3(0f, 0.0565f, -0.025f),
            new Vector3(0.05f, 0.005f, 0.05f), new Color(0.15f, 0.15f, 0.17f, 0.45f), true);
        GameObject filter = CreatePart(target, "Filter", PrimitiveType.Cube, new Vector3(0f, 0.052f, -0.035f),
            new Vector3(0.045f, 0.004f, 0.045f), new Color(0.85f, 0.82f, 0.70f), false);
        // Zylinder zeigt mit seiner Achse (Y) schon zur Kamera, eine Drehung ist hier nicht nötig
        GameObject schalter = CreatePart(target, "Hauptschalter", PrimitiveType.Cylinder, new Vector3(0.035f, 0.055f, 0.05f),
            new Vector3(0.02f, 0.005f, 0.02f), new Color(0.12f, 0.12f, 0.12f), false);
        GameObject anzeige = CreatePart(target, "Anzeige", PrimitiveType.Cube, new Vector3(-0.025f, 0.0515f, 0.05f),
            new Vector3(0.04f, 0.003f, 0.02f), new Color(0.10f, 0.30f, 0.20f), false);

        return new[]
        {
            MakeInteractive(gitter, "gitter", "Lüftergitter",
                "Schützt den Lüfter und hält die Filtermatte. Wird zum Filtertausch über die Rasthaken abgenommen."),
            MakeInteractive(filter, "filter", "Filtermatte",
                "Hält Staub aus der Kühlluft fern. Tausch bei hohem Differenzdruck."),
            MakeInteractive(schalter, "hauptschalter", "Hauptschalter",
                "Trennt den Schaltschrank vom Netz. Vor Arbeiten ausschalten und gegen Wiedereinschalten sichern."),
            MakeInteractive(anzeige, "anzeige", "Anzeige",
                "Zeigt Innentemperatur, Differenzdruck am Filter und Lüfterdrehzahl."),
        };
    }

    static GameObject CreatePart(Transform parent, string name, PrimitiveType type, Vector3 position, Vector3 scale,
        Color color, bool transparent)
    {
        GameObject go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = position;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = GetMaterial("M_" + name, color, transparent);
        return go;
    }

    static PartHighlighter MakeInteractive(GameObject part, string partId, string displayName, string description)
    {
        var highlighter = part.AddComponent<PartHighlighter>();
        var highlighterSo = new SerializedObject(highlighter);
        highlighterSo.FindProperty("partId").stringValue = partId;
        highlighterSo.ApplyModifiedPropertiesWithoutUndo();

        var info = part.AddComponent<PartInfo>();
        var infoSo = new SerializedObject(info);
        infoSo.FindProperty("displayName").stringValue = displayName;
        infoSo.FindProperty("description").stringValue = description;
        infoSo.ApplyModifiedPropertiesWithoutUndo();
        return highlighter;
    }

    // Ein Material je Teil, die Hervorhebung arbeitet mit _BaseColor des URP-Lit-Shaders
    static Material GetMaterial(string name, Color color, bool transparent)
    {
        if (!AssetDatabase.IsValidFolder(MaterialsDir))
            AssetDatabase.CreateFolder("Assets", Path.GetFileName(MaterialsDir));

        string path = $"{MaterialsDir}/{name}.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, path);
        }

        mat.SetColor("_BaseColor", color);
        if (transparent)
        {
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 0f);
            mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            mat.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_ZWrite", 0f);
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = (int)RenderQueue.Transparent;
        }
        EditorUtility.SetDirty(mat);
        return mat;
    }

    // ---------- Szene "Main" (3.7) ----------

    static void BuildMainScene()
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

        Camera cam = FindInScene<Camera>(scene).FirstOrDefault();
        if (cam != null)
        {
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.11f, 0.13f, 0.16f);
        }

        Canvas canvas = CreateCanvas();
        CreateEventSystem();

        GameObject panel = CreatePanel("Panel", canvas.transform, 0f, 0f, 1f, 1f, new Color(0.11f, 0.13f, 0.16f, 1f));
        TMP_Text titel = CreateText("TitelText", panel.transform, "AR-Wartungsassistent", 80, TextAlignmentOptions.Center, 0.06f, 0.70f, 0.94f, 0.82f);
        titel.fontStyle = FontStyles.Bold;
        CreateText("AblaufText", panel.transform,
            "Marker am Schaltschrank anvisieren und den Schritten folgen. Jeder erledigte Schritt wird mit \"Weiter\" " +
            "bestätigt. Am Ende wird ein Wartungsprotokoll gespeichert.",
            42, TextAlignmentOptions.Top, 0.08f, 0.45f, 0.92f, 0.68f);
        Button start = CreateButton("StartButton", panel.transform, "Wartung starten", 0.15f, 0.30f, 0.85f, 0.38f);
        Button beenden = CreateButton("BeendenButton", panel.transform, "Beenden", 0.15f, 0.19f, 0.85f, 0.27f);

        var logik = new GameObject("Logik");
        var loader = logik.AddComponent<SceneLoader>();
        UnityEventTools.AddStringPersistentListener(start.onClick, loader.LoadScene, "Wartung");
        AddCall(beenden, loader.QuitApp);
        EditorUtility.SetDirty(start);

        EditorSceneManager.SaveScene(scene, MainPath);
    }

    // ---------- Projekteinstellungen (3.9.3 und 3.10) ----------

    static void ConfigureProject()
    {
        EditorBuildSettings.scenes = new[]
        {
            new EditorBuildSettingsScene(MainPath, true),
            new EditorBuildSettingsScene(WartungPath, true),
        };

        PlayerSettings.productName = "AR Wartung";
        PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "at.mci.arwartung");
        PlayerSettings.Android.minSdkVersion = (AndroidSdkVersions)29; // Android 10
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;

        VersionControlSettings.mode = "Visible Meta Files";
        EditorSettings.serializationMode = SerializationMode.ForceText;

        AssetDatabase.SaveAssets();
    }

    // ---------- UI-Bausteine ----------

    static Canvas CreateCanvas()
    {
        var go = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        go.layer = LayerMask.NameToLayer("UI");
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        scaler.matchWidthOrHeight = 0.5f;
        return canvas;
    }

    // Das Projekt nutzt nur das neue Input System, deshalb InputSystemUIInputModule
    static void CreateEventSystem()
    {
        new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
    }

    static RectTransform CreateRect(string name, Transform parent, float xMin, float yMin, float xMax, float yMax)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = new Vector2(xMin, yMin);
        rt.anchorMax = new Vector2(xMax, yMax);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        return rt;
    }

    static GameObject CreatePanel(string name, Transform parent, float xMin, float yMin, float xMax, float yMax, Color color)
    {
        RectTransform rt = CreateRect(name, parent, xMin, yMin, xMax, yMax);
        var image = rt.gameObject.AddComponent<Image>();
        image.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd");
        image.type = Image.Type.Sliced;
        image.color = color;
        return rt.gameObject;
    }

    static TMP_Text CreateText(string name, Transform parent, string text, float size, TextAlignmentOptions alignment,
        float xMin, float yMin, float xMax, float yMax)
    {
        RectTransform rt = CreateRect(name, parent, xMin, yMin, xMax, yMax);
        var label = rt.gameObject.AddComponent<TextMeshProUGUI>();
        if (TMP_Settings.defaultFontAsset != null)
            label.font = TMP_Settings.defaultFontAsset;
        label.text = text;
        label.fontSize = size;
        label.alignment = alignment;
        label.color = Color.white;
        label.textWrappingMode = TextWrappingModes.Normal;
        return label;
    }

    static Button CreateButton(string name, Transform parent, string label, float xMin, float yMin, float xMax, float yMax)
    {
        RectTransform rt = CreateRect(name, parent, xMin, yMin, xMax, yMax);
        var image = rt.gameObject.AddComponent<Image>();
        image.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        image.type = Image.Type.Sliced;
        image.color = new Color(0.95f, 0.95f, 0.95f);
        var button = rt.gameObject.AddComponent<Button>();
        button.targetGraphic = image;

        TMP_Text text = CreateText("Text (TMP)", rt, label, 40, TextAlignmentOptions.Center, 0f, 0f, 1f, 1f);
        text.color = new Color(0.1f, 0.1f, 0.1f);
        return button;
    }

    // ---------- Hilfsfunktionen ----------

    static void AddCall(Button button, UnityAction call)
    {
        UnityEventTools.AddPersistentListener(button.onClick, call);
        EditorUtility.SetDirty(button);
    }

    static void Set(SerializedObject so, string property, Object value)
    {
        SerializedProperty prop = so.FindProperty(property)
            ?? throw new InvalidOperationException($"Feld {property} an {so.targetObject.GetType().Name} nicht gefunden.");
        prop.objectReferenceValue = value;
    }

    // Die Events im TargetStatusHandler sind privat, deshalb per Reflection
    static UnityEvent GetEvent(Object owner, string field)
    {
        FieldInfo info = owner.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
            ?? throw new InvalidOperationException($"Feld {field} an {owner.GetType().Name} nicht gefunden.");
        var unityEvent = (UnityEvent)info.GetValue(owner);
        if (unityEvent == null)
        {
            unityEvent = new UnityEvent();
            info.SetValue(owner, unityEvent);
        }
        return unityEvent;
    }

    static IEnumerable<T> FindInScene<T>(Scene scene) where T : Component
    {
        return scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<T>(true));
    }
}
