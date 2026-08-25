using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public class VFXPrefabValidator : EditorWindow
{
    private enum TargetMode
    {
        SelectedPrefab,
        Folder
    }

    private enum Severity
    {
        Error,
        Warning,
        Info
    }

    private enum Category
    {
        Structure,
        Transform,
        Particle,
        Material,
        Texture,
        Reference,
        Empty
    }

    [Serializable]
    private class ValidationIssue
    {
        public string prefabPath;
        public string objectPath;
        public string message;
        public Severity severity;
        public Category category;
        public UnityEngine.Object asset;
    }

    private const string PrefRoot = "VFXPrefabValidator_";

    [SerializeField] private TargetMode targetMode = TargetMode.SelectedPrefab;
    [SerializeField] private GameObject selectedPrefab;
    [SerializeField] private DefaultAsset selectedFolder;

    // Defaults are deliberately easy to change in the UI.
    [SerializeField] private string rootPrefix = "VFX_";
    [SerializeField] private string locatorName = "Locator";
    [SerializeField] private string materialFolder = "Assets/Project/ExternalAssets/Effect/Materials";
    [SerializeField] private string textureFolder = "Assets/Project/ExternalAssets/Effect/Textures";
    [SerializeField] private int requiredSortingOrder = 1;
    [SerializeField] private float requiredMaxParticleSize = 10f;
    [SerializeField] private bool locatorMustBeDirectChild = true;
    [SerializeField] private bool reportTransformOnlyObjects = true;

    private readonly List<ValidationIssue> issues = new List<ValidationIssue>();
    private readonly Dictionary<string, bool> prefabFoldouts = new Dictionary<string, bool>();

    private Vector2 mainScroll;
    private Vector2 resultScroll;
    private bool showSettings = true;
    private bool showErrors = true;
    private bool showWarnings = true;
    private bool showInfo = true;
    private int categoryFilter = 0;

    private static readonly string[] CategoryFilterNames =
    {
        "All",
        "Structure",
        "Transform",
        "Particle",
        "Material",
        "Texture",
        "Reference",
        "Empty"
    };

    [MenuItem("Pixie Tools/VFX Prefab Validator")]
    public static void ShowWindow()
    {
        var window = GetWindow<VFXPrefabValidator>();
        window.titleContent = new GUIContent("VFX Validator");
        window.minSize = new Vector2(560f, 520f);
        window.Show();
    }

    private void OnEnable()
    {
        LoadSettings();
    }

    private void OnDisable()
    {
        SaveSettings();
    }

    private void OnGUI()
    {
        DrawHeader();

        mainScroll = EditorGUILayout.BeginScrollView(mainScroll);
        DrawTargetSection();
        EditorGUILayout.Space(6f);
        DrawValidationSection();
        EditorGUILayout.Space(6f);
        DrawSettingsSection();
        EditorGUILayout.Space(8f);
        DrawResultsSection();
        EditorGUILayout.EndScrollView();
    }

    private void DrawHeader()
    {
        Rect rect = EditorGUILayout.GetControlRect(false, 44f);
        EditorGUI.DrawRect(rect, new Color(0.12f, 0.12f, 0.15f, 1f));

        GUIStyle title = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 17,
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = Color.white }
        };

        EditorGUI.LabelField(rect, "VFX Prefab Validator", title);
    }

    private void DrawTargetSection()
    {
        BeginSection("Target");

        targetMode = (TargetMode)GUILayout.Toolbar((int)targetMode, new[] { "Selected Prefab", "Folder" });
        EditorGUILayout.Space(4f);

        if (targetMode == TargetMode.SelectedPrefab)
        {
            selectedPrefab = (GameObject)EditorGUILayout.ObjectField("Prefab", selectedPrefab, typeof(GameObject), false);

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Use Project Selection", GUILayout.Width(160f)))
            {
                TryUseCurrentPrefabSelection();
            }
            EditorGUILayout.EndHorizontal();
        }
        else
        {
            selectedFolder = (DefaultAsset)EditorGUILayout.ObjectField("Folder", selectedFolder, typeof(DefaultAsset), false);

            string folderPath = GetSelectedFolderPath();
            if (!string.IsNullOrEmpty(folderPath))
            {
                int count = AssetDatabase.FindAssets("t:Prefab", new[] { folderPath }).Length;
                EditorGUILayout.LabelField($"Prefabs found: {count}", EditorStyles.miniLabel);
            }

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Use Project Selection", GUILayout.Width(160f)))
            {
                TryUseCurrentFolderSelection();
            }
            EditorGUILayout.EndHorizontal();
        }

        EndSection();
    }

    private void DrawValidationSection()
    {
        BeginSection("Validation");

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Structure")) RunValidation(Category.Structure);
        if (GUILayout.Button("Transforms")) RunValidation(Category.Transform);
        if (GUILayout.Button("Particles")) RunValidation(Category.Particle);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Materials")) RunValidation(Category.Material);
        if (GUILayout.Button("Textures")) RunValidation(Category.Texture);
        if (GUILayout.Button("References")) RunValidation(Category.Reference);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Empty Objects")) RunValidation(Category.Empty);
        if (GUILayout.Button("CHECK ALL", GUILayout.Height(26f))) RunValidation(null);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.HelpBox(
            "This first version is report-only. It does not rename, delete, move, or modify prefab contents.",
            MessageType.Info);

        EndSection();
    }

    private void DrawSettingsSection()
    {
        BeginSection("Rules / Settings");

        showSettings = EditorGUILayout.Foldout(showSettings, "Validation Rules", true);
        if (showSettings)
        {
            EditorGUI.indentLevel++;
            rootPrefix = EditorGUILayout.TextField("Root Prefix", rootPrefix);
            locatorName = EditorGUILayout.TextField("Locator Name", locatorName);
            locatorMustBeDirectChild = EditorGUILayout.Toggle("Locator Is Direct Child", locatorMustBeDirectChild);

            EditorGUILayout.Space(3f);
            materialFolder = EditorGUILayout.TextField("Material Folder", materialFolder);
            textureFolder = EditorGUILayout.TextField("Texture Folder", textureFolder);

            EditorGUILayout.Space(3f);
            requiredSortingOrder = EditorGUILayout.IntField("Sorting Order", requiredSortingOrder);
            requiredMaxParticleSize = EditorGUILayout.FloatField("Max Particle Size", requiredMaxParticleSize);
            reportTransformOnlyObjects = EditorGUILayout.Toggle("Report Transform-only Objects", reportTransformOnlyObjects);
            EditorGUI.indentLevel--;
        }

        EndSection();
    }

    private void DrawResultsSection()
    {
        BeginSection($"Results ({issues.Count})");

        EditorGUILayout.BeginHorizontal();
        showErrors = GUILayout.Toggle(showErrors, "Errors", "Button");
        showWarnings = GUILayout.Toggle(showWarnings, "Warnings", "Button");
        showInfo = GUILayout.Toggle(showInfo, "Info", "Button");
        categoryFilter = EditorGUILayout.Popup(categoryFilter, CategoryFilterNames, GUILayout.Width(120f));
        if (GUILayout.Button("Clear", GUILayout.Width(60f))) issues.Clear();
        EditorGUILayout.EndHorizontal();

        int errorCount = issues.Count(i => i.severity == Severity.Error);
        int warningCount = issues.Count(i => i.severity == Severity.Warning);
        int infoCount = issues.Count(i => i.severity == Severity.Info);
        EditorGUILayout.LabelField($"Errors: {errorCount}    Warnings: {warningCount}    Info: {infoCount}", EditorStyles.miniLabel);

        if (issues.Count == 0)
        {
            EditorGUILayout.HelpBox("No results yet. Choose a target and run one of the checks.", MessageType.None);
            EndSection();
            return;
        }

        resultScroll = EditorGUILayout.BeginScrollView(resultScroll, GUILayout.MinHeight(180f), GUILayout.MaxHeight(420f));

        foreach (var group in issues.GroupBy(i => i.prefabPath).OrderBy(g => g.Key))
        {
            List<ValidationIssue> visibleIssues = group.Where(IsVisible).ToList();
            if (visibleIssues.Count == 0)
                continue;

            if (!prefabFoldouts.ContainsKey(group.Key))
                prefabFoldouts[group.Key] = true;

            string prefabName = Path.GetFileNameWithoutExtension(group.Key);
            int groupErrors = visibleIssues.Count(i => i.severity == Severity.Error);
            int groupWarnings = visibleIssues.Count(i => i.severity == Severity.Warning);

            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            prefabFoldouts[group.Key] = EditorGUILayout.Foldout(
                prefabFoldouts[group.Key],
                $"{prefabName}  ({visibleIssues.Count})",
                true);

            GUILayout.FlexibleSpace();
            GUILayout.Label($"E:{groupErrors} W:{groupWarnings}", EditorStyles.miniLabel);

            if (GUILayout.Button("Ping", GUILayout.Width(46f)))
            {
                var prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(group.Key);
                if (prefabAsset != null)
                {
                    EditorGUIUtility.PingObject(prefabAsset);
                    Selection.activeObject = prefabAsset;
                }
            }
            EditorGUILayout.EndHorizontal();

            if (!prefabFoldouts[group.Key])
                continue;

            EditorGUI.indentLevel++;
            foreach (ValidationIssue issue in visibleIssues)
                DrawIssue(issue);
            EditorGUI.indentLevel--;
        }

        EditorGUILayout.EndScrollView();
        EndSection();
    }

    private void DrawIssue(ValidationIssue issue)
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.BeginHorizontal();

        string icon = issue.severity == Severity.Error ? "❌" : issue.severity == Severity.Warning ? "⚠" : "ⓘ";
        GUILayout.Label($"{icon} [{issue.category}]", EditorStyles.boldLabel, GUILayout.Width(110f));
        GUILayout.Label(issue.message, EditorStyles.wordWrappedLabel);

        if (issue.asset != null && GUILayout.Button("Ping", GUILayout.Width(46f)))
        {
            EditorGUIUtility.PingObject(issue.asset);
            Selection.activeObject = issue.asset;
        }

        EditorGUILayout.EndHorizontal();

        if (!string.IsNullOrEmpty(issue.objectPath))
            EditorGUILayout.LabelField(issue.objectPath, EditorStyles.miniLabel);

        EditorGUILayout.EndVertical();
    }

    private bool IsVisible(ValidationIssue issue)
    {
        if (issue.severity == Severity.Error && !showErrors) return false;
        if (issue.severity == Severity.Warning && !showWarnings) return false;
        if (issue.severity == Severity.Info && !showInfo) return false;

        if (categoryFilter > 0)
        {
            Category selectedCategory = (Category)(categoryFilter - 1);
            if (issue.category != selectedCategory) return false;
        }

        return true;
    }

    private void RunValidation(Category? onlyCategory)
    {
        issues.Clear();

        List<string> prefabPaths = GetTargetPrefabPaths();
        if (prefabPaths.Count == 0)
        {
            EditorUtility.DisplayDialog("VFX Validator", "No prefab target was found.", "OK");
            return;
        }

        try
        {
            for (int i = 0; i < prefabPaths.Count; i++)
            {
                string prefabPath = prefabPaths[i];

                if (prefabPaths.Count > 10)
                {
                    float progress = (float)i / prefabPaths.Count;
                    if (EditorUtility.DisplayCancelableProgressBar(
                        "VFX Prefab Validator",
                        $"Checking {Path.GetFileNameWithoutExtension(prefabPath)} ({i + 1}/{prefabPaths.Count})",
                        progress))
                    {
                        break;
                    }
                }

                ValidatePrefab(prefabPath, onlyCategory);
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        Repaint();
    }

    private void ValidatePrefab(string prefabPath, Category? onlyCategory)
    {
        GameObject root = null;

        try
        {
            root = PrefabUtility.LoadPrefabContents(prefabPath);
            if (root == null)
            {
                AddIssue(prefabPath, string.Empty, "Could not load prefab contents.", Severity.Error, Category.Reference);
                return;
            }

            if (ShouldRun(onlyCategory, Category.Structure)) ValidateStructure(prefabPath, root);
            if (ShouldRun(onlyCategory, Category.Transform)) ValidateTransforms(prefabPath, root);
            if (ShouldRun(onlyCategory, Category.Particle)) ValidateParticles(prefabPath, root);
            if (ShouldRun(onlyCategory, Category.Material)) ValidateMaterials(prefabPath, root);
            if (ShouldRun(onlyCategory, Category.Texture)) ValidateTextures(prefabPath, root);
            if (ShouldRun(onlyCategory, Category.Reference)) ValidateReferences(prefabPath, root);
            if (ShouldRun(onlyCategory, Category.Empty)) ValidateEmptyObjects(prefabPath, root);
        }
        catch (Exception ex)
        {
            AddIssue(prefabPath, string.Empty, $"Validation exception: {ex.Message}", Severity.Error, Category.Reference);
        }
        finally
        {
            if (root != null)
                PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static bool ShouldRun(Category? onlyCategory, Category category)
    {
        return !onlyCategory.HasValue || onlyCategory.Value == category;
    }

    private void ValidateStructure(string prefabPath, GameObject root)
    {
        if (!root.name.StartsWith(rootPrefix, StringComparison.Ordinal))
        {
            AddIssue(prefabPath, root.name,
                $"Root name '{root.name}' does not start with '{rootPrefix}'.",
                Severity.Error, Category.Structure);
        }

        Transform locator = FindChildRecursive(root.transform, locatorName);
        if (locator == null)
        {
            AddIssue(prefabPath, root.name,
                $"Missing required '{locatorName}' object.",
                Severity.Error, Category.Structure);
            return;
        }

        if (locatorMustBeDirectChild && locator.parent != root.transform)
        {
            AddIssue(prefabPath, GetHierarchyPath(locator, root.transform),
                $"'{locatorName}' exists but is not a direct child of the prefab root.",
                Severity.Warning, Category.Structure);
        }
    }

    private void ValidateTransforms(string prefabPath, GameObject root)
    {
        const float epsilon = 0.0001f;

        if (!Approximately(root.transform.localPosition, Vector3.zero, epsilon))
        {
            AddIssue(prefabPath, root.name,
                $"Root local position is {FormatVector(root.transform.localPosition)}; expected (0, 0, 0).",
                Severity.Error, Category.Transform);
        }

        if (Quaternion.Angle(root.transform.localRotation, Quaternion.identity) > 0.01f)
        {
            AddIssue(prefabPath, root.name,
                $"Root local rotation is {FormatVector(root.transform.localEulerAngles)}; expected (0, 0, 0).",
                Severity.Error, Category.Transform);
        }

        Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
        foreach (Transform t in transforms)
        {
            if (!Approximately(t.localScale, Vector3.one, epsilon))
            {
                AddIssue(prefabPath, GetHierarchyPath(t, root.transform),
                    $"Local scale is {FormatVector(t.localScale)}; expected (1, 1, 1).",
                    Severity.Error, Category.Transform);
            }
        }
    }

    private void ValidateParticles(string prefabPath, GameObject root)
    {
        ParticleSystem[] systems = root.GetComponentsInChildren<ParticleSystem>(true);
        foreach (ParticleSystem system in systems)
        {
            string path = GetHierarchyPath(system.transform, root.transform);
            ParticleSystem.MainModule main = system.main;

            if (main.scalingMode != ParticleSystemScalingMode.Hierarchy)
            {
                AddIssue(prefabPath, path,
                    $"Particle Scaling Mode is {main.scalingMode}; expected Hierarchy.",
                    Severity.Error, Category.Particle);
            }

            ParticleSystemRenderer renderer = system.GetComponent<ParticleSystemRenderer>();
            if (renderer == null)
                continue;

            if (renderer.sortingOrder != requiredSortingOrder)
            {
                AddIssue(prefabPath, path,
                    $"Sorting Order is {renderer.sortingOrder}; expected {requiredSortingOrder}.",
                    Severity.Warning, Category.Particle);
            }

            if (!Mathf.Approximately(renderer.maxParticleSize, requiredMaxParticleSize))
            {
                AddIssue(prefabPath, path,
                    $"Max Particle Size is {renderer.maxParticleSize}; expected {requiredMaxParticleSize}.",
                    Severity.Warning, Category.Particle);
            }
        }
    }

    private void ValidateMaterials(string prefabPath, GameObject root)
    {
        foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            Material[] materials = renderer.sharedMaterials;
            for (int i = 0; i < materials.Length; i++)
            {
                Material material = materials[i];
                string objectPath = GetHierarchyPath(renderer.transform, root.transform);

                if (material == null)
                {
                    AddIssue(prefabPath, objectPath,
                        $"Renderer material slot {i} is empty or missing.",
                        Severity.Error, Category.Material);
                    continue;
                }

                string path = AssetDatabase.GetAssetPath(material);
                if (string.IsNullOrEmpty(path))
                {
                    AddIssue(prefabPath, objectPath,
                        $"Material '{material.name}' is not a persistent project asset.",
                        Severity.Error, Category.Material, material);
                    continue;
                }

                if (!IsInsideFolder(path, materialFolder))
                {
                    AddIssue(prefabPath, objectPath,
                        $"Material '{material.name}' is outside the required folder: {path}",
                        Severity.Error, Category.Material, material);
                }
            }
        }
    }

    private void ValidateTextures(string prefabPath, GameObject root)
    {
        HashSet<Material> visitedMaterials = new HashSet<Material>();

        foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            foreach (Material material in renderer.sharedMaterials)
            {
                if (material == null || !visitedMaterials.Add(material))
                    continue;

                string[] texturePropertyNames;
                try
                {
                    texturePropertyNames = material.GetTexturePropertyNames();
                }
                catch
                {
                    continue;
                }

                foreach (string propertyName in texturePropertyNames)
                {
                    Texture texture = material.GetTexture(propertyName);
                    if (texture == null)
                        continue; // Null shader texture properties are often intentional.

                    string texturePath = AssetDatabase.GetAssetPath(texture);
                    if (string.IsNullOrEmpty(texturePath))
                    {
                        AddIssue(prefabPath, material.name,
                            $"Texture '{texture.name}' ({propertyName}) is not a persistent project asset.",
                            Severity.Error, Category.Texture, texture);
                        continue;
                    }

                    if (!IsInsideFolder(texturePath, textureFolder))
                    {
                        AddIssue(prefabPath, material.name,
                            $"Texture '{texture.name}' ({propertyName}) is outside the required folder: {texturePath}",
                            Severity.Error, Category.Texture, texture);
                    }
                }
            }
        }
    }

    private void ValidateReferences(string prefabPath, GameObject root)
    {
        foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
        {
            GameObject go = transform.gameObject;
            string path = GetHierarchyPath(transform, root.transform);

            int missingScriptCount = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(go);
            if (missingScriptCount > 0)
            {
                AddIssue(prefabPath, path,
                    $"Missing script component(s): {missingScriptCount}.",
                    Severity.Error, Category.Reference);
            }

            Component[] components = go.GetComponents<Component>();
            foreach (Component component in components)
            {
                if (component == null)
                    continue; // Already represented by missing-script count.

                InspectSerializedMissingReferences(prefabPath, path, component);
            }
        }
    }

    private void InspectSerializedMissingReferences(string prefabPath, string objectPath, Component component)
    {
        SerializedObject serializedObject;
        try
        {
            serializedObject = new SerializedObject(component);
        }
        catch
        {
            return;
        }

        SerializedProperty iterator = serializedObject.GetIterator();
        bool enterChildren = true;

        while (iterator.NextVisible(enterChildren))
        {
            enterChildren = false;

            if (iterator.propertyType != SerializedPropertyType.ObjectReference)
                continue;

            // Unity uses a non-zero instance ID for a broken serialized object reference.
            if (iterator.objectReferenceValue == null && iterator.objectReferenceInstanceIDValue != 0)
            {
                AddIssue(prefabPath, objectPath,
                    $"{component.GetType().Name}.{iterator.displayName} has a missing object reference.",
                    Severity.Error, Category.Reference);
            }
        }
    }

    private void ValidateEmptyObjects(string prefabPath, GameObject root)
    {
        if (!reportTransformOnlyObjects)
            return;

        foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
        {
            if (transform == root.transform)
                continue;

            GameObject go = transform.gameObject;
            Component[] components = go.GetComponents<Component>();

            // Transform-only helper/pivot objects can be intentional, so this is Info only.
            bool transformOnly = components.Length == 1 && components[0] is Transform;
            if (transformOnly && transform.childCount == 0)
            {
                AddIssue(prefabPath, GetHierarchyPath(transform, root.transform),
                    "Object has no components other than Transform and has no children.",
                    Severity.Info, Category.Empty);
            }
        }
    }

    private List<string> GetTargetPrefabPaths()
    {
        if (targetMode == TargetMode.SelectedPrefab)
        {
            if (selectedPrefab == null)
                return new List<string>();

            string path = AssetDatabase.GetAssetPath(selectedPrefab);
            if (string.IsNullOrEmpty(path) || !path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
                return new List<string>();

            return new List<string> { path };
        }

        string folderPath = GetSelectedFolderPath();
        if (string.IsNullOrEmpty(folderPath))
            return new List<string>();

        return AssetDatabase.FindAssets("t:Prefab", new[] { folderPath })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(path => path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
            .Distinct()
            .OrderBy(path => path)
            .ToList();
    }

    private string GetSelectedFolderPath()
    {
        if (selectedFolder == null)
            return string.Empty;

        string path = AssetDatabase.GetAssetPath(selectedFolder);
        return AssetDatabase.IsValidFolder(path) ? path : string.Empty;
    }

    private void TryUseCurrentPrefabSelection()
    {
        GameObject candidate = Selection.activeObject as GameObject;
        if (candidate == null)
            return;

        string path = AssetDatabase.GetAssetPath(candidate);
        if (!string.IsNullOrEmpty(path) && path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
            selectedPrefab = candidate;
    }

    private void TryUseCurrentFolderSelection()
    {
        DefaultAsset candidate = Selection.activeObject as DefaultAsset;
        if (candidate == null)
            return;

        string path = AssetDatabase.GetAssetPath(candidate);
        if (AssetDatabase.IsValidFolder(path))
            selectedFolder = candidate;
    }

    private static Transform FindChildRecursive(Transform root, string exactName)
    {
        foreach (Transform child in root)
        {
            if (child.name == exactName)
                return child;

            Transform nested = FindChildRecursive(child, exactName);
            if (nested != null)
                return nested;
        }

        return null;
    }

    private static string GetHierarchyPath(Transform transform, Transform prefabRoot)
    {
        if (transform == null)
            return string.Empty;

        List<string> names = new List<string>();
        Transform current = transform;

        while (current != null)
        {
            names.Add(current.name);
            if (current == prefabRoot)
                break;
            current = current.parent;
        }

        names.Reverse();
        return string.Join("/", names);
    }

    private static bool Approximately(Vector3 a, Vector3 b, float epsilon)
    {
        return Mathf.Abs(a.x - b.x) <= epsilon &&
               Mathf.Abs(a.y - b.y) <= epsilon &&
               Mathf.Abs(a.z - b.z) <= epsilon;
    }

    private static string FormatVector(Vector3 value)
    {
        return $"({value.x:0.###}, {value.y:0.###}, {value.z:0.###})";
    }

    private static bool IsInsideFolder(string assetPath, string requiredFolder)
    {
        if (string.IsNullOrWhiteSpace(requiredFolder))
            return true;

        string normalizedPath = assetPath.Replace('\\', '/').TrimEnd('/');
        string normalizedFolder = requiredFolder.Replace('\\', '/').TrimEnd('/');

        return normalizedPath.Equals(normalizedFolder, StringComparison.OrdinalIgnoreCase) ||
               normalizedPath.StartsWith(normalizedFolder + "/", StringComparison.OrdinalIgnoreCase);
    }

    private void AddIssue(
        string prefabPath,
        string objectPath,
        string message,
        Severity severity,
        Category category,
        UnityEngine.Object asset = null)
    {
        issues.Add(new ValidationIssue
        {
            prefabPath = prefabPath,
            objectPath = objectPath,
            message = message,
            severity = severity,
            category = category,
            asset = asset
        });
    }

    private static void BeginSection(string title)
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
    }

    private static void EndSection()
    {
        EditorGUILayout.EndVertical();
    }

    private void LoadSettings()
    {
        rootPrefix = EditorPrefs.GetString(PrefRoot + "RootPrefix", rootPrefix);
        locatorName = EditorPrefs.GetString(PrefRoot + "LocatorName", locatorName);
        materialFolder = EditorPrefs.GetString(PrefRoot + "MaterialFolder", materialFolder);
        textureFolder = EditorPrefs.GetString(PrefRoot + "TextureFolder", textureFolder);
        requiredSortingOrder = EditorPrefs.GetInt(PrefRoot + "SortingOrder", requiredSortingOrder);
        requiredMaxParticleSize = EditorPrefs.GetFloat(PrefRoot + "MaxParticleSize", requiredMaxParticleSize);
        locatorMustBeDirectChild = EditorPrefs.GetBool(PrefRoot + "LocatorDirectChild", locatorMustBeDirectChild);
        reportTransformOnlyObjects = EditorPrefs.GetBool(PrefRoot + "ReportTransformOnly", reportTransformOnlyObjects);
    }

    private void SaveSettings()
    {
        EditorPrefs.SetString(PrefRoot + "RootPrefix", rootPrefix);
        EditorPrefs.SetString(PrefRoot + "LocatorName", locatorName);
        EditorPrefs.SetString(PrefRoot + "MaterialFolder", materialFolder);
        EditorPrefs.SetString(PrefRoot + "TextureFolder", textureFolder);
        EditorPrefs.SetInt(PrefRoot + "SortingOrder", requiredSortingOrder);
        EditorPrefs.SetFloat(PrefRoot + "MaxParticleSize", requiredMaxParticleSize);
        EditorPrefs.SetBool(PrefRoot + "LocatorDirectChild", locatorMustBeDirectChild);
        EditorPrefs.SetBool(PrefRoot + "ReportTransformOnly", reportTransformOnlyObjects);
    }
}
