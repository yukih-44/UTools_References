using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public class VFXPrefabValidatorUpdated : EditorWindow
{
    private enum TargetMode { SelectedPrefab, Folder }
    private enum Severity { Error, Warning, Info }
    private enum Category { Structure, Transform, Particle, Material, Texture, Mesh, Reference, Empty }
    private enum SortingRuleMode { Exact, Minimum }

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

    private const string PrefRoot = "VFXPrefabValidatorUpdated_";

    [SerializeField] private TargetMode targetMode = TargetMode.SelectedPrefab;
    [SerializeField] private GameObject selectedPrefab;
    [SerializeField] private DefaultAsset selectedFolder;
    [SerializeField] private string manualFolderPath = string.Empty;

    // Structure
    [SerializeField] private string rootPrefix = "VFX_";
    [SerializeField] private string locatorName = "Locator";
    [SerializeField] private bool locatorMustBeDirectChild = true;

    // Particles
    [SerializeField] private SortingRuleMode sortingRuleMode = SortingRuleMode.Exact;
    [SerializeField] private int requiredSortingOrder = 1;
    [SerializeField] private float requiredMaxParticleSize = 10f;

    // Materials
    [SerializeField] private string materialPrefix = "MAT_";
    [SerializeField] private string materialFolder = "Assets/Project/ExternalAssets/Effect/Materials";

    // Textures
    [SerializeField] private string texturePrefix = "TEX_";
    [SerializeField] private string textureFolder = "Assets/Project/ExternalAssets/Effect/Textures";

    // Meshes
    [SerializeField] private string meshPrefix = "MESH_";
    [SerializeField] private string meshFolder = "Assets/Project/ExternalAssets/Effect/Meshes";

    // Misc
    [SerializeField] private bool reportTransformOnlyObjects = true;

    private readonly List<ValidationIssue> issues = new List<ValidationIssue>();
    private readonly Dictionary<string, bool> prefabFoldouts = new Dictionary<string, bool>();
    private readonly HashSet<string> assetIssueKeys = new HashSet<string>();

    private Vector2 mainScroll;
    private Vector2 resultScroll;
    private bool showSettings = true;
    private bool showErrors = true;
    private bool showWarnings = true;
    private bool showInfo = true;
    private int categoryFilter;

    private GUIStyle prefabHeaderStyle;
    private GUIStyle settingsHeaderStyle;

    private static readonly string[] CategoryFilterNames =
    {
        "All", "Structure", "Transform", "Particle", "Material", "Texture", "Mesh", "Reference", "Empty"
    };

    [MenuItem("Pixie Tools/VFX Prefab Validator - Updated")]
    public static void ShowWindow()
    {
        var window = GetWindow<VFXPrefabValidatorUpdated>();
        window.titleContent = new GUIContent("VFX Validator+");
        window.minSize = new Vector2(600f, 560f);
        window.Show();
    }

    private void OnEnable() => LoadSettings();
    private void OnDisable() => SaveSettings();

    private void OnGUI()
    {
        EnsureStyles();
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

    private void EnsureStyles()
    {
        if (prefabHeaderStyle == null)
        {
            prefabHeaderStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 13
            };
            prefabHeaderStyle.normal.textColor = EditorGUIUtility.isProSkin
                ? new Color(0.45f, 0.82f, 1f)
                : new Color(0.05f, 0.35f, 0.65f);
        }

        if (settingsHeaderStyle == null)
        {
            settingsHeaderStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 12
            };
        }
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
            if (GUILayout.Button("Use Project Selection", GUILayout.Width(160f))) TryUseCurrentPrefabSelection();
            EditorGUILayout.EndHorizontal();
        }
        else
        {
            selectedFolder = (DefaultAsset)EditorGUILayout.ObjectField("Folder", selectedFolder, typeof(DefaultAsset), false);

            EditorGUILayout.BeginHorizontal();
            manualFolderPath = EditorGUILayout.TextField("Folder Path", manualFolderPath);
            if (GUILayout.Button("Apply", GUILayout.Width(55f))) ApplyManualFolderPath();
            EditorGUILayout.EndHorizontal();

            string folderPath = GetSelectedFolderPath();
            if (!string.IsNullOrEmpty(folderPath))
            {
                int count = AssetDatabase.FindAssets("t:Prefab", new[] { folderPath }).Length;
                EditorGUILayout.LabelField($"Active path: {folderPath}", EditorStyles.miniLabel);
                EditorGUILayout.LabelField($"Prefabs found: {count}", EditorStyles.miniLabel);
            }

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Use Project Selection", GUILayout.Width(160f))) TryUseCurrentFolderSelection();
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
        if (GUILayout.Button("Meshes")) RunValidation(Category.Mesh);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("References")) RunValidation(Category.Reference);
        if (GUILayout.Button("Empty Objects")) RunValidation(Category.Empty);
        if (GUILayout.Button("CHECK ALL", GUILayout.Height(26f))) RunValidation(null);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.HelpBox("Report-only: this validator does not rename, delete, move, or modify prefab contents.", MessageType.Info);
        EndSection();
    }

    private void DrawSettingsSection()
    {
        BeginSection("Rules / Settings");
        showSettings = EditorGUILayout.Foldout(showSettings, "Validation Rules", true);

        if (showSettings)
        {
            EditorGUI.indentLevel++;

            DrawSettingsCategory("Structure");
            rootPrefix = EditorGUILayout.TextField("Root Prefix", rootPrefix);
            locatorName = EditorGUILayout.TextField("Locator Name", locatorName);
            locatorMustBeDirectChild = EditorGUILayout.Toggle("Locator Is Direct Child", locatorMustBeDirectChild);

            DrawSettingsCategory("Particles");
            EditorGUILayout.LabelField("Scaling Mode", "Hierarchy (required)");
            sortingRuleMode = (SortingRuleMode)EditorGUILayout.EnumPopup("Sorting Order Rule", sortingRuleMode);
            requiredSortingOrder = EditorGUILayout.IntField(
                sortingRuleMode == SortingRuleMode.Exact ? "Sorting Order" : "Minimum Sorting Order",
                requiredSortingOrder);
            requiredMaxParticleSize = EditorGUILayout.FloatField("Max Particle Size", requiredMaxParticleSize);

            DrawSettingsCategory("Materials");
            materialPrefix = EditorGUILayout.TextField("Required Prefix", materialPrefix);
            materialFolder = EditorGUILayout.TextField("Required Folder", materialFolder);

            DrawSettingsCategory("Textures");
            texturePrefix = EditorGUILayout.TextField("Required Prefix", texturePrefix);
            textureFolder = EditorGUILayout.TextField("Required Folder", textureFolder);

            DrawSettingsCategory("Meshes");
            meshPrefix = EditorGUILayout.TextField("Required Prefix", meshPrefix);
            meshFolder = EditorGUILayout.TextField("Required Folder", meshFolder);

            DrawSettingsCategory("References / Empty Objects");
            reportTransformOnlyObjects = EditorGUILayout.Toggle("Report Transform-only Objects", reportTransformOnlyObjects);

            EditorGUI.indentLevel--;
        }

        EndSection();
    }

    private void DrawSettingsCategory(string label)
    {
        EditorGUILayout.Space(7f);
        Rect line = EditorGUILayout.GetControlRect(false, 1f);
        EditorGUI.DrawRect(line, EditorGUIUtility.isProSkin ? new Color(0.3f, 0.3f, 0.3f) : new Color(0.7f, 0.7f, 0.7f));
        EditorGUILayout.LabelField(label, settingsHeaderStyle);
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

        resultScroll = EditorGUILayout.BeginScrollView(resultScroll, GUILayout.MinHeight(180f), GUILayout.MaxHeight(440f));

        foreach (IGrouping<string, ValidationIssue> group in issues.GroupBy(i => i.prefabPath).OrderBy(g => g.Key))
        {
            List<ValidationIssue> visibleIssues = group.Where(IsVisible).ToList();
            if (visibleIssues.Count == 0) continue;

            if (!prefabFoldouts.ContainsKey(group.Key)) prefabFoldouts[group.Key] = true;

            string prefabName = Path.GetFileNameWithoutExtension(group.Key);
            int groupErrors = visibleIssues.Count(i => i.severity == Severity.Error);
            int groupWarnings = visibleIssues.Count(i => i.severity == Severity.Warning);

            EditorGUILayout.Space(4f);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            prefabFoldouts[group.Key] = EditorGUILayout.Foldout(prefabFoldouts[group.Key], GUIContent.none, true, GUILayout.Width(16f));
            GUILayout.Label(prefabName, prefabHeaderStyle);
            GUILayout.FlexibleSpace();
            GUILayout.Label($"{visibleIssues.Count} issues   E:{groupErrors} W:{groupWarnings}", EditorStyles.miniBoldLabel);
            if (GUILayout.Button("Ping", GUILayout.Width(46f)))
            {
                GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(group.Key);
                if (prefabAsset != null)
                {
                    EditorGUIUtility.PingObject(prefabAsset);
                    Selection.activeObject = prefabAsset;
                }
            }
            EditorGUILayout.EndHorizontal();

            if (prefabFoldouts[group.Key])
            {
                EditorGUI.indentLevel++;
                foreach (ValidationIssue issue in visibleIssues) DrawIssue(issue);
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndVertical();
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
        if (!string.IsNullOrEmpty(issue.objectPath)) EditorGUILayout.LabelField(issue.objectPath, EditorStyles.miniLabel);
        EditorGUILayout.EndVertical();
    }

    private bool IsVisible(ValidationIssue issue)
    {
        if (issue.severity == Severity.Error && !showErrors) return false;
        if (issue.severity == Severity.Warning && !showWarnings) return false;
        if (issue.severity == Severity.Info && !showInfo) return false;
        if (categoryFilter > 0 && issue.category != (Category)(categoryFilter - 1)) return false;
        return true;
    }

    private void RunValidation(Category? onlyCategory)
    {
        issues.Clear();
        assetIssueKeys.Clear();

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
                        progress)) break;
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
            if (ShouldRun(onlyCategory, Category.Mesh)) ValidateMeshes(prefabPath, root);
            if (ShouldRun(onlyCategory, Category.Reference)) ValidateReferences(prefabPath, root);
            if (ShouldRun(onlyCategory, Category.Empty)) ValidateEmptyObjects(prefabPath, root);
        }
        catch (Exception ex)
        {
            AddIssue(prefabPath, string.Empty, $"Validation exception: {ex.Message}", Severity.Error, Category.Reference);
        }
        finally
        {
            if (root != null) PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static bool ShouldRun(Category? onlyCategory, Category category) => !onlyCategory.HasValue || onlyCategory.Value == category;

    private void ValidateStructure(string prefabPath, GameObject root)
    {
        if (!string.IsNullOrEmpty(rootPrefix) && !root.name.StartsWith(rootPrefix, StringComparison.Ordinal))
            AddIssue(prefabPath, root.name, $"Root name '{root.name}' does not start with '{rootPrefix}'.", Severity.Error, Category.Structure);

        Transform locator = FindChildRecursive(root.transform, locatorName);
        if (locator == null)
        {
            AddIssue(prefabPath, root.name, $"Missing required '{locatorName}' object.", Severity.Error, Category.Structure);
            return;
        }

        if (locatorMustBeDirectChild && locator.parent != root.transform)
            AddIssue(prefabPath, GetHierarchyPath(locator, root.transform), $"'{locatorName}' exists but is not a direct child of the prefab root.", Severity.Warning, Category.Structure);
    }

    private void ValidateTransforms(string prefabPath, GameObject root)
    {
        const float epsilon = 0.0001f;
        if (!Approximately(root.transform.localPosition, Vector3.zero, epsilon))
            AddIssue(prefabPath, root.name, $"Root local position is {FormatVector(root.transform.localPosition)}; expected (0, 0, 0).", Severity.Error, Category.Transform);

        if (Quaternion.Angle(root.transform.localRotation, Quaternion.identity) > 0.01f)
            AddIssue(prefabPath, root.name, $"Root local rotation is {FormatVector(root.transform.localEulerAngles)}; expected (0, 0, 0).", Severity.Error, Category.Transform);

        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
        {
            if (!Approximately(t.localScale, Vector3.one, epsilon))
                AddIssue(prefabPath, GetHierarchyPath(t, root.transform), $"Local scale is {FormatVector(t.localScale)}; expected (1, 1, 1).", Severity.Error, Category.Transform);
        }
    }

    private void ValidateParticles(string prefabPath, GameObject root)
    {
        foreach (ParticleSystem system in root.GetComponentsInChildren<ParticleSystem>(true))
        {
            string path = GetHierarchyPath(system.transform, root.transform);
            ParticleSystem.MainModule main = system.main;

            if (main.scalingMode != ParticleSystemScalingMode.Hierarchy)
                AddIssue(prefabPath, path, $"Particle Scaling Mode is {main.scalingMode}; expected Hierarchy.", Severity.Error, Category.Particle);

            ParticleSystemRenderer renderer = system.GetComponent<ParticleSystemRenderer>();
            if (renderer == null) continue;

            bool sortingInvalid = sortingRuleMode == SortingRuleMode.Exact
                ? renderer.sortingOrder != requiredSortingOrder
                : renderer.sortingOrder < requiredSortingOrder;

            if (sortingInvalid)
            {
                string expectation = sortingRuleMode == SortingRuleMode.Exact
                    ? requiredSortingOrder.ToString()
                    : $"{requiredSortingOrder} or higher";
                AddIssue(prefabPath, path, $"Sorting Order is {renderer.sortingOrder}; expected {expectation}.", Severity.Warning, Category.Particle);
            }

            if (!Mathf.Approximately(renderer.maxParticleSize, requiredMaxParticleSize))
                AddIssue(prefabPath, path, $"Max Particle Size is {renderer.maxParticleSize}; expected {requiredMaxParticleSize}.", Severity.Warning, Category.Particle);
        }
    }

    private void ValidateMaterials(string prefabPath, GameObject root)
    {
        HashSet<Material> visited = new HashSet<Material>();
        foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            Material[] materials = renderer.sharedMaterials;
            string objectPath = GetHierarchyPath(renderer.transform, root.transform);

            for (int i = 0; i < materials.Length; i++)
            {
                Material material = materials[i];
                if (material == null)
                {
                    AddIssue(prefabPath, objectPath, $"Renderer material slot {i} is empty or missing.", Severity.Error, Category.Material);
                    continue;
                }

                if (visited.Add(material))
                    ValidateProjectAsset(prefabPath, objectPath, material, materialPrefix, materialFolder, Category.Material, "Material");
            }
        }
    }

    private void ValidateTextures(string prefabPath, GameObject root)
    {
        HashSet<Material> visitedMaterials = new HashSet<Material>();
        HashSet<Texture> visitedTextures = new HashSet<Texture>();

        foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            foreach (Material material in renderer.sharedMaterials)
            {
                if (material == null || !visitedMaterials.Add(material)) continue;

                string[] propertyNames;
                try { propertyNames = material.GetTexturePropertyNames(); }
                catch { continue; }

                foreach (string propertyName in propertyNames)
                {
                    Texture texture = material.GetTexture(propertyName);
                    if (texture == null || !visitedTextures.Add(texture)) continue;
                    ValidateProjectAsset(prefabPath, material.name, texture, texturePrefix, textureFolder, Category.Texture, "Texture", propertyName);
                }
            }
        }
    }

    private void ValidateMeshes(string prefabPath, GameObject root)
    {
        HashSet<Mesh> visited = new HashSet<Mesh>();

        foreach (MeshFilter meshFilter in root.GetComponentsInChildren<MeshFilter>(true))
        {
            string objectPath = GetHierarchyPath(meshFilter.transform, root.transform);
            Mesh mesh = meshFilter.sharedMesh;
            if (mesh == null)
            {
                AddIssue(prefabPath, objectPath, "MeshFilter has no mesh assigned.", Severity.Error, Category.Mesh);
                continue;
            }
            if (visited.Add(mesh)) ValidateProjectAsset(prefabPath, objectPath, mesh, meshPrefix, meshFolder, Category.Mesh, "Mesh");
        }

        foreach (SkinnedMeshRenderer renderer in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            string objectPath = GetHierarchyPath(renderer.transform, root.transform);
            Mesh mesh = renderer.sharedMesh;
            if (mesh == null)
            {
                AddIssue(prefabPath, objectPath, "SkinnedMeshRenderer has no mesh assigned.", Severity.Error, Category.Mesh);
                continue;
            }
            if (visited.Add(mesh)) ValidateProjectAsset(prefabPath, objectPath, mesh, meshPrefix, meshFolder, Category.Mesh, "Mesh");
        }

        foreach (ParticleSystemRenderer renderer in root.GetComponentsInChildren<ParticleSystemRenderer>(true))
        {
            if (renderer.renderMode != ParticleSystemRenderMode.Mesh) continue;

            string objectPath = GetHierarchyPath(renderer.transform, root.transform);
            int meshCount = renderer.meshCount;
            if (meshCount <= 0)
            {
                AddIssue(prefabPath, objectPath, "Particle System Renderer uses Mesh mode but has no mesh assigned.", Severity.Error, Category.Mesh);
                continue;
            }

            Mesh[] meshes = new Mesh[meshCount];
            int returnedCount = renderer.GetMeshes(meshes);
            for (int i = 0; i < returnedCount; i++)
            {
                Mesh mesh = meshes[i];
                if (mesh == null)
                {
                    AddIssue(prefabPath, objectPath, $"Particle mesh slot {i} is empty or missing.", Severity.Error, Category.Mesh);
                    continue;
                }
                if (visited.Add(mesh)) ValidateProjectAsset(prefabPath, objectPath, mesh, meshPrefix, meshFolder, Category.Mesh, "Mesh");
            }
        }
    }

    private void ValidateProjectAsset(
        string prefabPath,
        string objectPath,
        UnityEngine.Object asset,
        string requiredPrefix,
        string requiredFolder,
        Category category,
        string assetType,
        string detail = null)
    {
        string assetPath = AssetDatabase.GetAssetPath(asset);
        string detailText = string.IsNullOrEmpty(detail) ? string.Empty : $" ({detail})";

        if (string.IsNullOrEmpty(assetPath))
        {
            AddUniqueAssetIssue(prefabPath, objectPath, $"{assetType} '{asset.name}'{detailText} is not a persistent project asset.", Severity.Error, category, asset, "persistent");
            return;
        }

        if (!string.IsNullOrWhiteSpace(requiredPrefix) && !asset.name.StartsWith(requiredPrefix, StringComparison.Ordinal))
            AddUniqueAssetIssue(prefabPath, objectPath, $"{assetType} '{asset.name}'{detailText} does not start with '{requiredPrefix}'.", Severity.Error, category, asset, "prefix");

        if (!IsInsideFolder(assetPath, requiredFolder))
            AddUniqueAssetIssue(prefabPath, objectPath, $"{assetType} '{asset.name}'{detailText} is outside the required folder: {assetPath}", Severity.Error, category, asset, "folder");
    }

    private void AddUniqueAssetIssue(string prefabPath, string objectPath, string message, Severity severity, Category category, UnityEngine.Object asset, string rule)
    {
        string key = $"{prefabPath}|{asset.GetInstanceID()}|{category}|{rule}";
        if (assetIssueKeys.Add(key)) AddIssue(prefabPath, objectPath, message, severity, category, asset);
    }

    private void ValidateReferences(string prefabPath, GameObject root)
    {
        foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
        {
            GameObject go = transform.gameObject;
            string path = GetHierarchyPath(transform, root.transform);

            int missingScriptCount = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(go);
            if (missingScriptCount > 0)
                AddIssue(prefabPath, path, $"Missing script component(s): {missingScriptCount}.", Severity.Error, Category.Reference);

            foreach (Component component in go.GetComponents<Component>())
            {
                if (component != null) InspectSerializedMissingReferences(prefabPath, path, component);
            }
        }
    }

    private void InspectSerializedMissingReferences(string prefabPath, string objectPath, Component component)
    {
        SerializedObject serializedObject;
        try { serializedObject = new SerializedObject(component); }
        catch { return; }

        SerializedProperty iterator = serializedObject.GetIterator();
        bool enterChildren = true;
        while (iterator.NextVisible(enterChildren))
        {
            enterChildren = false;
            if (iterator.propertyType != SerializedPropertyType.ObjectReference) continue;
            if (iterator.objectReferenceValue == null && iterator.objectReferenceInstanceIDValue != 0)
                AddIssue(prefabPath, objectPath, $"{component.GetType().Name}.{iterator.displayName} has a missing object reference.", Severity.Error, Category.Reference);
        }
    }

    private void ValidateEmptyObjects(string prefabPath, GameObject root)
    {
        if (!reportTransformOnlyObjects) return;

        foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
        {
            if (transform == root.transform) continue;
            Component[] components = transform.gameObject.GetComponents<Component>();
            bool transformOnly = components.Length == 1 && components[0] is Transform;
            if (transformOnly && transform.childCount == 0)
                AddIssue(prefabPath, GetHierarchyPath(transform, root.transform), "Object has no components other than Transform and has no children.", Severity.Info, Category.Empty);
        }
    }

    private List<string> GetTargetPrefabPaths()
    {
        if (targetMode == TargetMode.SelectedPrefab)
        {
            if (selectedPrefab == null) return new List<string>();
            string path = AssetDatabase.GetAssetPath(selectedPrefab);
            if (string.IsNullOrEmpty(path) || !path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase)) return new List<string>();
            return new List<string> { path };
        }

        string folderPath = GetSelectedFolderPath();
        if (string.IsNullOrEmpty(folderPath)) return new List<string>();

        return AssetDatabase.FindAssets("t:Prefab", new[] { folderPath })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(path => path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
            .Distinct()
            .OrderBy(path => path)
            .ToList();
    }

    private string GetSelectedFolderPath()
    {
        if (selectedFolder != null)
        {
            string selectedPath = AssetDatabase.GetAssetPath(selectedFolder);
            if (AssetDatabase.IsValidFolder(selectedPath)) return selectedPath;
        }

        string normalizedManual = NormalizeAssetPath(manualFolderPath);
        return AssetDatabase.IsValidFolder(normalizedManual) ? normalizedManual : string.Empty;
    }

    private void ApplyManualFolderPath()
    {
        manualFolderPath = NormalizeAssetPath(manualFolderPath);
        if (!AssetDatabase.IsValidFolder(manualFolderPath))
        {
            EditorUtility.DisplayDialog("VFX Validator", $"Folder not found:\n{manualFolderPath}\n\nUse a project-relative path beginning with Assets/.", "OK");
            return;
        }

        selectedFolder = AssetDatabase.LoadAssetAtPath<DefaultAsset>(manualFolderPath);
        SaveSettings();
    }

    private void TryUseCurrentPrefabSelection()
    {
        GameObject candidate = Selection.activeObject as GameObject;
        if (candidate == null) return;
        string path = AssetDatabase.GetAssetPath(candidate);
        if (!string.IsNullOrEmpty(path) && path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase)) selectedPrefab = candidate;
    }

    private void TryUseCurrentFolderSelection()
    {
        DefaultAsset candidate = Selection.activeObject as DefaultAsset;
        if (candidate == null) return;
        string path = AssetDatabase.GetAssetPath(candidate);
        if (!AssetDatabase.IsValidFolder(path)) return;
        selectedFolder = candidate;
        manualFolderPath = path;
        SaveSettings();
    }

    private static Transform FindChildRecursive(Transform root, string exactName)
    {
        foreach (Transform child in root)
        {
            if (child.name == exactName) return child;
            Transform nested = FindChildRecursive(child, exactName);
            if (nested != null) return nested;
        }
        return null;
    }

    private static string GetHierarchyPath(Transform transform, Transform prefabRoot)
    {
        if (transform == null) return string.Empty;
        List<string> names = new List<string>();
        Transform current = transform;
        while (current != null)
        {
            names.Add(current.name);
            if (current == prefabRoot) break;
            current = current.parent;
        }
        names.Reverse();
        return string.Join("/", names);
    }

    private static bool Approximately(Vector3 a, Vector3 b, float epsilon) =>
        Mathf.Abs(a.x - b.x) <= epsilon && Mathf.Abs(a.y - b.y) <= epsilon && Mathf.Abs(a.z - b.z) <= epsilon;

    private static string FormatVector(Vector3 value) => $"({value.x:0.###}, {value.y:0.###}, {value.z:0.###})";

    private static string NormalizeAssetPath(string path) => string.IsNullOrWhiteSpace(path) ? string.Empty : path.Trim().Replace('\\', '/').TrimEnd('/');

    private static bool IsInsideFolder(string assetPath, string requiredFolder)
    {
        if (string.IsNullOrWhiteSpace(requiredFolder)) return true;
        string normalizedPath = NormalizeAssetPath(assetPath);
        string normalizedFolder = NormalizeAssetPath(requiredFolder);
        return normalizedPath.Equals(normalizedFolder, StringComparison.OrdinalIgnoreCase) ||
               normalizedPath.StartsWith(normalizedFolder + "/", StringComparison.OrdinalIgnoreCase);
    }

    private void AddIssue(string prefabPath, string objectPath, string message, Severity severity, Category category, UnityEngine.Object asset = null)
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

    private static void EndSection() => EditorGUILayout.EndVertical();

    private void LoadSettings()
    {
        rootPrefix = EditorPrefs.GetString(PrefRoot + "RootPrefix", rootPrefix);
        locatorName = EditorPrefs.GetString(PrefRoot + "LocatorName", locatorName);
        locatorMustBeDirectChild = EditorPrefs.GetBool(PrefRoot + "LocatorDirectChild", locatorMustBeDirectChild);

        sortingRuleMode = (SortingRuleMode)EditorPrefs.GetInt(PrefRoot + "SortingRuleMode", (int)sortingRuleMode);
        requiredSortingOrder = EditorPrefs.GetInt(PrefRoot + "SortingOrder", requiredSortingOrder);
        requiredMaxParticleSize = EditorPrefs.GetFloat(PrefRoot + "MaxParticleSize", requiredMaxParticleSize);

        materialPrefix = EditorPrefs.GetString(PrefRoot + "MaterialPrefix", materialPrefix);
        materialFolder = EditorPrefs.GetString(PrefRoot + "MaterialFolder", materialFolder);
        texturePrefix = EditorPrefs.GetString(PrefRoot + "TexturePrefix", texturePrefix);
        textureFolder = EditorPrefs.GetString(PrefRoot + "TextureFolder", textureFolder);
        meshPrefix = EditorPrefs.GetString(PrefRoot + "MeshPrefix", meshPrefix);
        meshFolder = EditorPrefs.GetString(PrefRoot + "MeshFolder", meshFolder);

        manualFolderPath = EditorPrefs.GetString(PrefRoot + "ManualFolderPath", manualFolderPath);
        reportTransformOnlyObjects = EditorPrefs.GetBool(PrefRoot + "ReportTransformOnly", reportTransformOnlyObjects);
    }

    private void SaveSettings()
    {
        EditorPrefs.SetString(PrefRoot + "RootPrefix", rootPrefix);
        EditorPrefs.SetString(PrefRoot + "LocatorName", locatorName);
        EditorPrefs.SetBool(PrefRoot + "LocatorDirectChild", locatorMustBeDirectChild);

        EditorPrefs.SetInt(PrefRoot + "SortingRuleMode", (int)sortingRuleMode);
        EditorPrefs.SetInt(PrefRoot + "SortingOrder", requiredSortingOrder);
        EditorPrefs.SetFloat(PrefRoot + "MaxParticleSize", requiredMaxParticleSize);

        EditorPrefs.SetString(PrefRoot + "MaterialPrefix", materialPrefix);
        EditorPrefs.SetString(PrefRoot + "MaterialFolder", materialFolder);
        EditorPrefs.SetString(PrefRoot + "TexturePrefix", texturePrefix);
        EditorPrefs.SetString(PrefRoot + "TextureFolder", textureFolder);
        EditorPrefs.SetString(PrefRoot + "MeshPrefix", meshPrefix);
        EditorPrefs.SetString(PrefRoot + "MeshFolder", meshFolder);

        EditorPrefs.SetString(PrefRoot + "ManualFolderPath", manualFolderPath);
        EditorPrefs.SetBool(PrefRoot + "ReportTransformOnly", reportTransformOnlyObjects);
    }
}
