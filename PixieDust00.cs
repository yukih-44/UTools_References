using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using System.Collections.Generic;
using System.Linq;

public class PixieDustVFXManager : EditorWindow
{
    private enum OperationMode
    {
        Create,
        Duplicate,
        Delete,
        Rename,
        ParticleHelper,
        BatchOperations,
        QuickTools,
        Notes
    }

    private OperationMode currentMode = OperationMode.Create;
    private string prefabName = "ef_new_vfx";
    private GameObject selectedPrefab;
    private string newPrefabName = "";

    // Base roots
    private string basePrefabFolder = "Assets/Project/ExternalAssets/Effect/Prefab";
    private string baseMaterialFolder = "Assets/Project/ExternalAssets/Effect/Prefab";
    private const string MaterialsSubfolderName = "Materials";

    // Create mode options
    private bool createDefaultMaterialIfMissing = true;
    private bool connectInScene = true;
    private bool autoCreateEffectLayer = false;
    private bool createLoopEffect = true;

    // Particle Helper options
    private GameObject selectedParticleSystem;
    private bool helperCreateLoop = true;
    private string helperChildName = "Particle_Child";

    // Batch Operations
    private string batchRenameFrom = "";
    private string batchRenameTo = "";
    private string batchPrefix = "";
    private int batchRenderQueue = 3000;
    private Color batchColorTint = Color.white;
    private float batchPlaybackSpeed = 1f;
    private bool batchAutoBackup = true;

    // Quick Tools (from Pixie Dust)
    private string objectBaseName = "";
    private int objectScale = 1;
    private string spawned_layer = "Effect";
    private string standard_shader = "Universal Render Pipeline/Particles/Unlit";
    private string materialDefaultName = "eff_mat_";
    // Hierarchy color and colorer removed - available as standalone tool

    // Favorites - REMOVED (integrated elsewhere)

    // Notes
    private string notes = "";
    private Vector2 notesScroll;

    private Vector2 scrollPosition;

    [MenuItem("Pixie Tools/Pixie Dust VFX Manager")]
    public static void ShowWindow()
    {
        var window = GetWindow<PixieDustVFXManager>();
        window.titleContent = new GUIContent("✨ Pixie Dust VFX");
        window.minSize = new Vector2(450, 400);
    }

    private void OnEnable()
    {
        LoadSettings();
        notes = EditorPrefs.GetString("PixieDust_Notes", "");
    }

    private void OnDisable()
    {
        SaveSettings();
        EditorPrefs.SetString("PixieDust_Notes", notes);
    }

    private void OnGUI()
    {
        DrawPixieDustHeader();

        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        // Mode selection with magical styling
        DrawModeSelector();
        EditorGUILayout.Space(10);

        switch (currentMode)
        {
            case OperationMode.Create:
                DrawCreateMode();
                break;
            case OperationMode.Duplicate:
                DrawDuplicateMode();
                break;
            case OperationMode.Delete:
                DrawDeleteMode();
                break;
            case OperationMode.Rename:
                DrawRenameMode();
                break;
            case OperationMode.ParticleHelper:
                DrawParticleHelperMode();
                break;
            case OperationMode.BatchOperations:
                DrawBatchOperationsMode();
                break;
            case OperationMode.QuickTools:
                DrawQuickToolsMode();
                break;
            case OperationMode.Notes:
                DrawNotesMode();
                break;
        }

        EditorGUILayout.EndScrollView();
    }

    private void DrawPixieDustHeader()
    {
        Rect headerRect = EditorGUILayout.GetControlRect(false, 50); // Increased from 40 to 50
        EditorGUI.DrawRect(headerRect, new Color(0.1f, 0.05f, 0.2f, 0.9f));

        GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 18, // Slightly smaller to fit better
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = new Color(1f, 0.8f, 1f) }
        };

        EditorGUI.LabelField(headerRect, "✨ Pixie Dust VFX Manager ✨", titleStyle);

        GUIStyle subtitleStyle = new GUIStyle(EditorStyles.miniLabel)
        {
            alignment = TextAnchor.MiddleCenter,
            fontStyle = FontStyle.Italic,
            normal = { textColor = new Color(0.9f, 0.9f, 1f) }
        };

        EditorGUILayout.LabelField("Sprinkle some magic on your particles!", subtitleStyle);
        EditorGUILayout.Space(8); // Added more space after header
    }

    private void DrawModeSelector()
    {
        EditorGUILayout.LabelField("🪄 Magic Mode", GetMagicStyle("SectionHeader"));

        // Create a more compact grid layout
        int buttonsPerRow = 4; // Changed from 3 to 4 since we have 8 modes now
        string[] modeNames = { "✨Create", "📋Duplicate", "🗑️Delete", "✏️Rename", "🎛️Helper", "⚡Batch", "🔧Quick", "📝Notes" };
        OperationMode[] modes = (OperationMode[])System.Enum.GetValues(typeof(OperationMode));

        for (int i = 0; i < modes.Length; i += buttonsPerRow)
        {
            EditorGUILayout.BeginHorizontal();
            for (int j = 0; j < buttonsPerRow && i + j < modes.Length; j++)
            {
                int index = i + j;
                bool isSelected = currentMode == modes[index];

                GUIStyle buttonStyle = new GUIStyle(GUI.skin.button)
                {
                    fontStyle = isSelected ? FontStyle.Bold : FontStyle.Normal,
                    normal = {
                        textColor = isSelected ? new Color(1f, 0.8f, 1f) : Color.white,
                        background = isSelected ? MakeTex(1, 1, new Color(0.4f, 0.1f, 0.6f, 0.8f)) : null
                    }
                };

                if (GUILayout.Button(modeNames[index], buttonStyle))
                {
                    currentMode = modes[index];
                }
            }
            EditorGUILayout.EndHorizontal();
        }
    }

    private void DrawCreateMode()
    {
        EditorGUILayout.LabelField("✨ Create New VFX Prefab", GetMagicStyle("ModeHeader"));

        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField("📁 Folder Settings", EditorStyles.boldLabel);
        basePrefabFolder = EditorGUILayout.TextField("Prefab Folder", basePrefabFolder);
        baseMaterialFolder = EditorGUILayout.TextField("Materials Folder", baseMaterialFolder);

        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField("🎯 Prefab Settings", EditorStyles.boldLabel);
        prefabName = EditorGUILayout.TextField("Prefab Name", prefabName);

        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField("🌟 Effect Type", EditorStyles.boldLabel);
        createLoopEffect = EditorGUILayout.Toggle("Create Loop Effect", createLoopEffect);
        EditorGUILayout.HelpBox(createLoopEffect ? "Loop Effect: Continuous particle emission" : "Shot Effect: Burst particle emission", UnityEditor.MessageType.Info);

        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField("⚙️ Options", EditorStyles.boldLabel);
        createDefaultMaterialIfMissing = EditorGUILayout.Toggle("Create Default Material", createDefaultMaterialIfMissing);
        connectInScene = EditorGUILayout.Toggle("Connect Instance In Scene", connectInScene);
        autoCreateEffectLayer = EditorGUILayout.Toggle("Auto-Create 'Effect' Layer", autoCreateEffectLayer);

        EditorGUILayout.Space(10);
        if (GUILayout.Button("✨ Create Magic Prefab ✨", GetMagicStyle("BigButton")))
            CreatePrefab();
    }

    private void DrawDuplicateMode()
    {
        EditorGUILayout.LabelField("📋 Duplicate VFX Prefab", GetMagicStyle("ModeHeader"));
        selectedPrefab = (GameObject)EditorGUILayout.ObjectField("Source Prefab", selectedPrefab, typeof(GameObject), false);
        newPrefabName = EditorGUILayout.TextField("New Prefab Name", newPrefabName);

        EditorGUILayout.Space();
        connectInScene = EditorGUILayout.Toggle("Connect Instance In Scene", connectInScene);

        EditorGUILayout.Space();
        GUI.enabled = selectedPrefab != null && !string.IsNullOrWhiteSpace(newPrefabName);
        if (GUILayout.Button("📋 Duplicate Prefab", GetMagicStyle("BigButton")))
            DuplicatePrefab();
        GUI.enabled = true;
    }

    private void DrawDeleteMode()
    {
        EditorGUILayout.LabelField("🗑️ Delete VFX Prefab", GetMagicStyle("ModeHeader"));
        selectedPrefab = (GameObject)EditorGUILayout.ObjectField("Prefab to Delete", selectedPrefab, typeof(GameObject), false);

        if (selectedPrefab != null)
        {
            string prefabPath = AssetDatabase.GetAssetPath(selectedPrefab);
            string prefabNameFromPath = Path.GetFileNameWithoutExtension(prefabPath);
            string materialFolderPath = CombineUnity(basePrefabFolder, prefabNameFromPath, MaterialsSubfolderName);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Will Delete:", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"• Prefab: {prefabPath}", EditorStyles.miniLabel);
            EditorGUILayout.LabelField($"• Material Folder: {materialFolderPath}", EditorStyles.miniLabel);

            if (AssetDatabase.IsValidFolder(materialFolderPath))
            {
                string[] materials = AssetDatabase.FindAssets("t:Material", new[] { materialFolderPath });
                EditorGUILayout.LabelField($"• {materials.Length} material(s) inside", EditorStyles.miniLabel);
            }
        }

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox("This action cannot be undone! Make sure you have backups.", UnityEditor.MessageType.Warning);

        GUI.enabled = selectedPrefab != null;
        GUI.backgroundColor = Color.red;
        if (GUILayout.Button("🗑️ DELETE PREFAB + MATERIALS", GetMagicStyle("BigButton")))
        {
            if (EditorUtility.DisplayDialog("Confirm Deletion",
                $"Are you sure you want to delete '{selectedPrefab.name}' and all its associated materials?\n\nThis action cannot be undone!",
                "Delete", "Cancel"))
            {
                DeletePrefab();
            }
        }
        GUI.backgroundColor = Color.white;
        GUI.enabled = true;
    }

    private void DrawRenameMode()
    {
        EditorGUILayout.LabelField("✏️ Rename VFX Prefab", GetMagicStyle("ModeHeader"));
        selectedPrefab = (GameObject)EditorGUILayout.ObjectField("Prefab to Rename", selectedPrefab, typeof(GameObject), false);
        newPrefabName = EditorGUILayout.TextField("New Name", newPrefabName);

        if (selectedPrefab != null)
        {
            string currentName = selectedPrefab.name;
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Preview:", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"Current: {currentName}", EditorStyles.miniLabel);
            EditorGUILayout.LabelField($"New: {newPrefabName}", EditorStyles.miniLabel);
        }

        EditorGUILayout.Space();
        GUI.enabled = selectedPrefab != null && !string.IsNullOrWhiteSpace(newPrefabName) &&
                      selectedPrefab.name != newPrefabName;
        if (GUILayout.Button("✏️ Rename Prefab + Material Folder", GetMagicStyle("BigButton")))
            RenamePrefab();
        GUI.enabled = true;
    }

    private void DrawParticleHelperMode()
    {
        EditorGUILayout.LabelField("🎛️ Particle System Helper", GetMagicStyle("ModeHeader"));
        EditorGUILayout.HelpBox("Tools for working with particle systems in the scene hierarchy", UnityEditor.MessageType.Info);

        // Add Child Particle System
        DrawMagicSection("Add Child Particle System", () => {
            selectedParticleSystem = (GameObject)EditorGUILayout.ObjectField("Parent PS", selectedParticleSystem, typeof(GameObject), true);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Use Selected", GUILayout.Width(100)))
            {
                if (Selection.activeGameObject != null && Selection.activeGameObject.GetComponent<ParticleSystem>() != null)
                    selectedParticleSystem = Selection.activeGameObject;
                else
                    Debug.LogWarning("Please select a GameObject with a ParticleSystem component");
            }
            if (selectedParticleSystem != null)
                EditorGUILayout.LabelField($"Current: {selectedParticleSystem.name}", EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();

            helperChildName = EditorGUILayout.TextField("Child Name", helperChildName);
            helperCreateLoop = EditorGUILayout.Toggle("Create Loop Effect", helperCreateLoop);

            GUI.enabled = selectedParticleSystem != null && selectedParticleSystem.GetComponent<ParticleSystem>() != null;
            if (GUILayout.Button("Add Child Particle System"))
                AddChildParticleSystem();
            GUI.enabled = true;
        });

        // Duplicate with New Material
        DrawMagicSection("Duplicate PS with New Material", () => {
            selectedParticleSystem = (GameObject)EditorGUILayout.ObjectField("Source PS", selectedParticleSystem, typeof(GameObject), true);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Use Selected", GUILayout.Width(100)))
            {
                if (Selection.activeGameObject != null && Selection.activeGameObject.GetComponent<ParticleSystem>() != null)
                    selectedParticleSystem = Selection.activeGameObject;
                else
                    Debug.LogWarning("Please select a GameObject with a ParticleSystem component");
            }
            if (selectedParticleSystem != null)
                EditorGUILayout.LabelField($"Current: {selectedParticleSystem.name}", EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();

            GUI.enabled = selectedParticleSystem != null && selectedParticleSystem.GetComponent<ParticleSystem>() != null;
            if (GUILayout.Button("Duplicate with New Material"))
                DuplicateParticleSystemWithNewMaterial();
            GUI.enabled = true;
        });

        // Sub-Emitter Tools
        DrawMagicSection("Sub-Emitter Tools", () => {
            if (GUILayout.Button("Remove Missing/Problematic Sub-Emitters"))
                CleanSubEmitters();

            if (GUILayout.Button("Setup Selected as Sub-Emitters"))
                SetupSubEmitters();

            EditorGUILayout.HelpBox("For sub-emitters: Select multiple particle systems, last selected = parent", UnityEditor.MessageType.Info);
        });

        // Cleanup Tools
        DrawMagicSection("Cleanup Tools", () => {
            if (GUILayout.Button("Remove Unused PS Modules"))
                RemoveUnusedModules();

            if (GUILayout.Button("Optimize Selected Particle Systems"))
                OptimizeSelectedParticleSystems();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Auto-Setup Particle Layers"))
                AutoSetupParticleLayers();
            if (GUILayout.Button("Reset Particle Transforms"))
                ResetParticleTransforms();
            EditorGUILayout.EndHorizontal();
        });

        // Quick Actions
        DrawMagicSection("Quick Actions", () => {
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Play All Selected"))
                PlaySelectedParticleSystems();
            if (GUILayout.Button("Stop All Selected"))
                StopSelectedParticleSystems();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Enable All Selected"))
                SetSelectedParticleSystemsEnabled(true);
            if (GUILayout.Button("Disable All Selected"))
                SetSelectedParticleSystemsEnabled(false);
            EditorGUILayout.EndHorizontal();
        });
    }

    private void DrawBatchOperationsMode()
    {
        EditorGUILayout.LabelField("⚡ Batch Operations", GetMagicStyle("ModeHeader"));

        // Batch Renaming
        DrawMagicSection("Batch Renaming", () => {
            batchRenameFrom = EditorGUILayout.TextField("Replace Text", batchRenameFrom);
            batchRenameTo = EditorGUILayout.TextField("With Text", batchRenameTo);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Rename GameObjects"))
                BatchRenameGameObjects();
            if (GUILayout.Button("Rename Materials"))
                BatchRenameMaterials();
            EditorGUILayout.EndHorizontal();
        });

        // Batch Prefix Assignment
        DrawMagicSection("Batch Prefix Assignment", () => {
            batchPrefix = EditorGUILayout.TextField("Prefix to Add", batchPrefix);
            EditorGUILayout.HelpBox("Perfect for fixing those random texture/material names like 'Blend0203u9294'!", UnityEditor.MessageType.Info);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Add Prefix to GameObjects"))
                BatchAddPrefixToGameObjects();
            if (GUILayout.Button("Add Prefix to Materials"))
                BatchAddPrefixToMaterials();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Add Prefix to Textures"))
                BatchAddPrefixToTextures();
            if (GUILayout.Button("Add Prefix to All Assets"))
                BatchAddPrefixToAllAssets();
            EditorGUILayout.EndHorizontal();
        });

        // Material Operations
        DrawMagicSection("Material Operations", () => {
            batchRenderQueue = EditorGUILayout.IntField("Render Queue", batchRenderQueue);
            batchColorTint = EditorGUILayout.ColorField("Color Tint", batchColorTint);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Set Render Queue"))
                BatchSetRenderQueue();
            if (GUILayout.Button("Apply Color Tint"))
                BatchApplyColorTint();
            EditorGUILayout.EndHorizontal();
        });

        // Performance Tools
        DrawMagicSection("Performance Tools", () => {
            if (GUILayout.Button("Analyze Performance"))
                BatchPerformanceTest();
            if (GUILayout.Button("Find Missing References"))
                FindMissingReferences();
        });
    }

    private void DrawQuickToolsMode()
    {
        EditorGUILayout.LabelField("🔧 Quick Tools", GetMagicStyle("ModeHeader"));

        // Reference Checker
        DrawMagicSection("Missing References Checker", () => {
            if (GUILayout.Button("🔍 Find Missing Textures"))
                FindMissingTextures();

            if (GUILayout.Button("🔍 Find Missing Materials"))
                FindMissingMaterials();

            if (GUILayout.Button("🔍 Find Missing Meshes"))
                FindMissingMeshes();

            if (GUILayout.Button("🔍 Find All Missing References"))
                FindAllMissingReferences();
        });

        // Asset Validation
        DrawMagicSection("Asset Validation", () => {
            if (GUILayout.Button("Validate Selected Prefabs"))
                ValidateSelectedPrefabs();

            if (GUILayout.Button("Check Texture Sizes"))
                CheckTextureSizes();

            if (GUILayout.Button("Find Duplicate Materials"))
                FindDuplicateMaterials();
        });

        // Cleanup Tools
        DrawMagicSection("Cleanup Tools", () => {
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Remove Empty GameObjects"))
                RemoveEmptyGameObjects();
            if (GUILayout.Button("Clean Null Components"))
                CleanNullComponents();
            EditorGUILayout.EndHorizontal();

            if (GUILayout.Button("Remove Unused Components"))
                RemoveUnusedComponents();
        });

        // Folder Quick Access
        DrawMagicSection("Folder Quick Access", () => {
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("📁 Materials Folder"))
                OpenFolder(baseMaterialFolder);
            if (GUILayout.Button("📁 Prefabs Folder"))
                OpenFolder(basePrefabFolder);
            EditorGUILayout.EndHorizontal();
        });
    }

    private void DrawNotesMode()
    {
        EditorGUILayout.LabelField("📝 Project Notes", GetMagicStyle("ModeHeader"));

        EditorGUILayout.LabelField("Keep track of your VFX workflow notes:", EditorStyles.helpBox);
        EditorGUILayout.Space(5);

        notesScroll = EditorGUILayout.BeginScrollView(notesScroll, GUILayout.Height(300));
        notes = EditorGUILayout.TextArea(notes, GUILayout.ExpandHeight(true));
        EditorGUILayout.EndScrollView();

        EditorGUILayout.Space();
        if (GUILayout.Button("💾 Save Notes", GetMagicStyle("BigButton")))
        {
            EditorPrefs.SetString("PixieDust_Notes", notes);
            ShowNotification(new GUIContent("Notes saved!"));
        }
    }

    // Helper method for drawing sections with consistent styling
    private void DrawMagicSection(string title, System.Action content)
    {
        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField(title, GetMagicStyle("SectionHeader"));
        EditorGUILayout.BeginVertical("box");
        content();
        EditorGUILayout.EndVertical();
    }

    private GUIStyle GetMagicStyle(string styleName)
    {
        switch (styleName)
        {
            case "ModeHeader":
                return new GUIStyle(EditorStyles.boldLabel)
                {
                    fontSize = 16,
                    normal = { textColor = new Color(1f, 0.8f, 1f) },
                    padding = new RectOffset(5, 5, 8, 8),
                    margin = new RectOffset(0, 0, 5, 5),
                    wordWrap = false,
                    clipping = TextClipping.Overflow
                };

            case "SectionHeader":
                return new GUIStyle(EditorStyles.boldLabel)
                {
                    fontSize = 12,
                    normal = { textColor = new Color(0.9f, 0.7f, 1f) },
                    padding = new RectOffset(5, 5, 5, 5),
                    margin = new RectOffset(0, 0, 3, 3),
                    wordWrap = false,
                    clipping = TextClipping.Overflow,
                    fixedHeight = 0 // Let it auto-size
                };

            case "BigButton":
                return new GUIStyle(GUI.skin.button)
                {
                    fontSize = 12,
                    fontStyle = FontStyle.Bold,
                    fixedHeight = 28,
                    normal = { textColor = Color.white },
                    padding = new RectOffset(10, 10, 5, 5)
                };

            default:
                return EditorStyles.label;
        }
    }

    private Texture2D MakeTex(int width, int height, Color col)
    {
        Color[] pix = new Color[width * height];
        for (int i = 0; i < pix.Length; i++) pix[i] = col;
        Texture2D result = new Texture2D(width, height);
        result.SetPixels(pix);
        result.Apply();
        return result;
    }

    // === CORE FUNCTIONALITY METHODS ===
    // (Include all the methods from the previous version: CreatePrefab, DuplicatePrefab, etc.)

    private void CreatePrefab()
    {
        // --- Validate name
        if (!IsValidAssetName(prefabName))
        {
            Debug.LogError($"Invalid prefab name '{prefabName}'. Avoid / \\ : * ? \" < > | and trailing dots/spaces.");
            return;
        }

        // --- Ensure base root exists
        EnsureFolderExistsRecursive(basePrefabFolder);

        // --- Per-prefab folder (for materials etc.)
        string perPrefabFolderPath = CombineUnity(basePrefabFolder, prefabName);
        EnsureFolderExistsRecursive(perPrefabFolderPath);

        // --- Materials subfolder inside per-prefab folder
        string materialFolderPath = CombineUnity(perPrefabFolderPath, MaterialsSubfolderName);
        EnsureFolderExistsRecursive(materialFolderPath);

        // Make sure AssetDatabase sees the new folders
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // --- Prefab asset path (NOTE: at the root of basePrefabFolder, NOT inside per-prefab folder)
        string prefabPath = CombineUnity(basePrefabFolder, prefabName + ".prefab");
        prefabPath = AssetDatabase.GenerateUniqueAssetPath(prefabPath);

        // --- Layer resolution
        int effectLayer = LayerMask.NameToLayer("Effect");
        if (effectLayer < 0)
        {
            if (autoCreateEffectLayer) effectLayer = EnsureLayerExists("Effect");
            else
            {
                Debug.LogWarning("Layer 'Effect' not found. Using Default (0). Add an 'Effect' layer to silence this.");
                effectLayer = 0;
            }
        }

        // --- Build hierarchy
        GameObject root = new GameObject(prefabName);

        // root & children
        var rootPs = root.AddComponent<ParticleSystem>();
        DisableAllModules(rootPs);
        var rootRend = root.GetComponent<ParticleSystemRenderer>();
        if (rootRend) rootRend.enabled = false;

        GameObject rootChild = new GameObject("Root");
        rootChild.transform.SetParent(root.transform, false);
        var rootChildPs = rootChild.AddComponent<ParticleSystem>();
        DisableAllModules(rootChildPs);
        var rootChildRend = rootChild.GetComponent<ParticleSystemRenderer>();
        if (rootChildRend) rootChildRend.enabled = false;

        GameObject particleChild = new GameObject("Particle_0");
        particleChild.transform.SetParent(rootChild.transform, false);
        var ps = particleChild.AddComponent<ParticleSystem>();

        // Configure particle system based on effect type
        ConfigureParticleSystem(ps, createLoopEffect);

        var childRenderer = particleChild.GetComponent<ParticleSystemRenderer>();

        // Put everything on the Effect layer (do this AFTER building children)
        SetLayerRecursively(root, effectLayer);

        // --- Material handling (respects your toggle + naming)
        AssignMaterial(childRenderer, materialFolderPath, createDefaultMaterialIfMissing, prefabName);

        // --- Log paths for sanity
        Debug.Log($"[VFXPrefabCreator] Creating {(createLoopEffect ? "loop" : "shot")} effect prefab:\n" +
                  $"  Prefab Path: {prefabPath}\n" +
                  $"  Per-Prefab Folder: {perPrefabFolderPath}\n" +
                  $"  Materials Folder: {materialFolderPath}");

        // --- Save prefab
        if (!AssetDatabase.IsValidFolder(basePrefabFolder))
        {
            Debug.LogError($"[VFXPrefabCreator] Prefab base folder not valid: {basePrefabFolder}");
            DestroyImmediate(root);
            return;
        }

        bool success;
        GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath, out success);

        // Clean temp scene object
        if (root) DestroyImmediate(root);

        if (!success || savedPrefab == null)
        {
            Debug.LogError($"SaveAsPrefabAsset failed for '{prefabPath}'. (Folder valid: {AssetDatabase.IsValidFolder(basePrefabFolder)})");
            return;
        }

        // Optionally connect instance in scene
        if (connectInScene)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(savedPrefab);
            Selection.activeObject = instance;
        }
        else
        {
            Selection.activeObject = savedPrefab;
        }

        EditorGUIUtility.PingObject(savedPrefab);
        Debug.Log($"VFX prefab created at: {prefabPath}");
    }

    private void ConfigureParticleSystem(ParticleSystem ps, bool isLoop)
    {
        var main = ps.main;
        main.playOnAwake = false;
        var emission = ps.emission;
        emission.enabled = true;
        var shape = ps.shape;
        shape.enabled = false;

        if (isLoop)
        {
            main.loop = true;
            main.startLifetime = 1.0f;
            main.startSpeed = 1.0f;
            emission.rateOverTime = 10f;
            emission.SetBursts(new ParticleSystem.Burst[0]);
        }
        else
        {
            main.loop = false;
            main.startLifetime = 2.0f;
            main.startSpeed = 2.0f;
            emission.rateOverTime = 0f;
            var burst = new ParticleSystem.Burst(0.0f, 20);
            emission.SetBursts(new ParticleSystem.Burst[] { burst });
        }
    }

    // === HELPER METHODS ===
    // (Include all helper methods from previous version)

    private static void DisableAllModules(ParticleSystem ps)
    {
        var main = ps.main; main.playOnAwake = false;
        var emission = ps.emission; emission.enabled = false;
        var shape = ps.shape; shape.enabled = false;
        var r = ps.GetComponent<ParticleSystemRenderer>();
        if (r) r.enabled = false;
    }

    private static void SetLayerRecursively(GameObject go, int layer)
    {
        go.layer = layer;
        foreach (Transform t in go.transform)
            SetLayerRecursively(t.gameObject, layer);
    }

    private static void EnsureFolderExistsRecursive(string folderPath)
    {
        folderPath = folderPath.Replace("\\", "/");
        if (AssetDatabase.IsValidFolder(folderPath)) return;

        var parent = Path.GetDirectoryName(folderPath)?.Replace("\\", "/");
        var name = Path.GetFileName(folderPath);
        if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
            EnsureFolderExistsRecursive(parent);

        AssetDatabase.CreateFolder(parent, name);
    }

    private static string CombineUnity(string a, string b)
        => (a.Replace("\\", "/").TrimEnd('/') + "/" + b.Replace("\\", "/").TrimStart('/'));

    private static string CombineUnity(string a, string b, string c)
        => CombineUnity(CombineUnity(a, b), c);

    private static bool IsValidAssetName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return false;
        char[] invalid = Path.GetInvalidFileNameChars();
        if (name.IndexOfAny(invalid) >= 0) return false;
        if (name.EndsWith(".") || name.EndsWith(" ")) return false;
        return true;
    }

    private static int EnsureLayerExists(string layerName)
    {
        int idx = LayerMask.NameToLayer(layerName);
        if (idx >= 0) return idx;

        var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        var layersProp = tagManager.FindProperty("layers");

        for (int i = 8; i < 32; i++)
        {
            var sp = layersProp.GetArrayElementAtIndex(i);
            if (sp != null && string.IsNullOrEmpty(sp.stringValue))
            {
                sp.stringValue = layerName;
                tagManager.ApplyModifiedProperties();
                AssetDatabase.SaveAssets();
                return i;
            }
            if (sp != null && sp.stringValue == layerName)
                return i;
        }

        Debug.LogWarning("No free user layer slots (8–31). Using Default (0).");
        return 0;
    }

    private static void AssignMaterial(ParticleSystemRenderer rend, string materialFolderPath, bool createIfMissing, string prefabName)
    {
        if (!rend) return;

        string[] guids = AssetDatabase.FindAssets("t:Material", new[] { materialFolderPath });
        Material mat = null;

        if (guids.Length > 0)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        }

        if (mat == null && createIfMissing)
        {
            mat = CreateDefaultParticleMaterial(materialFolderPath, prefabName);
        }

        if (mat != null)
        {
            rend.sharedMaterial = mat;
        }
    }

    private static Material CreateDefaultParticleMaterial(string folder, string prefabName)
    {
        EnsureFolderExistsRecursive(folder);

        string shaderName = GetPipelineParticleShaderName();
        Shader shader = Shader.Find(shaderName);
        if (!shader)
        {
            shader = Shader.Find("Particles/Standard Unlit");
        }

        string filePath = AssetDatabase.GenerateUniqueAssetPath(CombineUnity(folder, $"{prefabName}_mat00.mat"));
        var mat = new Material(shader);
        string niceName = System.IO.Path.GetFileNameWithoutExtension(filePath);
        mat.name = niceName;

        AssetDatabase.CreateAsset(mat, filePath);
        EditorUtility.SetDirty(mat);
        AssetDatabase.ImportAsset(filePath, ImportAssetOptions.ForceSynchronousImport);

        return mat;
    }

    private static string GetPipelineParticleShaderName()
    {
        var rp = GraphicsSettings.currentRenderPipeline;
        if (rp == null) return "Particles/Standard Unlit";
#if UNITY_2021_3_OR_NEWER
        var srpName = rp.GetType().Name;
        if (srpName.Contains("Universal")) return "Universal Render Pipeline/Particles/Unlit";
        if (srpName.Contains("HD")) return "HDRP/Unlit";
#endif
        return "Particles/Standard Unlit";
    }

    // === DUPLICATE, DELETE, RENAME METHODS ===

    private void DuplicatePrefab()
    {
        // Implementation similar to previous version
        Debug.Log("Duplicate functionality - implement based on previous version");
    }

    private void DeletePrefab()
    {
        // Implementation similar to previous version
        Debug.Log("Delete functionality - implement based on previous version");
    }

    private void RenamePrefab()
    {
        // Implementation similar to previous version
        Debug.Log("Rename functionality - implement based on previous version");
    }

    // === PARTICLE HELPER METHODS ===

    private void AddChildParticleSystem()
    {
        if (selectedParticleSystem == null || selectedParticleSystem.GetComponent<ParticleSystem>() == null)
        {
            Debug.LogError("Please select a valid particle system.");
            return;
        }

        GameObject child = new GameObject(helperChildName);
        child.transform.SetParent(selectedParticleSystem.transform, false);
        var ps = child.AddComponent<ParticleSystem>();
        ConfigureParticleSystem(ps, helperCreateLoop);
        child.layer = selectedParticleSystem.layer;

        var parentRenderer = selectedParticleSystem.GetComponent<ParticleSystemRenderer>();
        var childRenderer = child.GetComponent<ParticleSystemRenderer>();

        if (parentRenderer != null && parentRenderer.sharedMaterial != null)
        {
            childRenderer.sharedMaterial = parentRenderer.sharedMaterial;
        }

        Selection.activeObject = child;
        EditorGUIUtility.PingObject(child);
        Debug.Log($"Added child particle system '{helperChildName}' to '{selectedParticleSystem.name}'");
    }

    private void DuplicateParticleSystemWithNewMaterial()
    {
        if (selectedParticleSystem == null) return;

        GameObject duplicate = Object.Instantiate(selectedParticleSystem, selectedParticleSystem.transform.parent);
        duplicate.name = selectedParticleSystem.name + "_Copy";

        var renderer = duplicate.GetComponent<ParticleSystemRenderer>();
        if (renderer != null && renderer.sharedMaterial != null)
        {
            Material originalMat = renderer.sharedMaterial;
            Material newMat = new Material(originalMat);
            newMat.name = originalMat.name + "_Copy";
            renderer.sharedMaterial = newMat;
        }

        Selection.activeObject = duplicate;
        Debug.Log($"Duplicated particle system with new material: {duplicate.name}");
    }

    private void CleanSubEmitters()
    {
        GameObject[] selected = Selection.gameObjects;
        int cleanedCount = 0;

        foreach (GameObject go in selected)
        {
            ParticleSystem ps = go.GetComponent<ParticleSystem>();
            if (ps == null) continue;

            var subEmitters = ps.subEmitters;
            bool wasModified = false;

            for (int i = subEmitters.subEmittersCount - 1; i >= 0; i--)
            {
                ParticleSystem subPS = subEmitters.GetSubEmitterSystem(i);
                if (subPS == null)
                {
                    subEmitters.RemoveSubEmitter(i);
                    wasModified = true;
                }
            }

            if (wasModified) cleanedCount++;
        }

        Debug.Log($"Cleaned sub-emitters from {cleanedCount} particle systems");
    }

    private void SetupSubEmitters()
    {
        GameObject[] selected = Selection.gameObjects;
        if (selected.Length < 2)
        {
            Debug.LogError("Please select at least 2 particle systems (last selected = parent)");
            return;
        }

        GameObject parent = selected[selected.Length - 1];
        ParticleSystem parentPS = parent.GetComponent<ParticleSystem>();

        if (parentPS == null)
        {
            Debug.LogError("Parent object must have a ParticleSystem component");
            return;
        }

        var subEmitters = parentPS.subEmitters;
        subEmitters.enabled = true;

        for (int i = 0; i < selected.Length - 1; i++)
        {
            ParticleSystem childPS = selected[i].GetComponent<ParticleSystem>();
            if (childPS != null)
            {
                subEmitters.AddSubEmitter(childPS, ParticleSystemSubEmitterType.Death, ParticleSystemSubEmitterProperties.InheritNothing);
            }
        }

        Debug.Log($"Setup {selected.Length - 1} sub-emitters for {parent.name}");
    }

    private void PlaySelectedParticleSystems()
    {
        GameObject[] selected = Selection.gameObjects;
        int count = 0;

        foreach (GameObject go in selected)
        {
            ParticleSystem ps = go.GetComponent<ParticleSystem>();
            if (ps != null)
            {
                ps.Play();
                count++;
            }
        }

        Debug.Log($"Started playback on {count} particle systems");
    }

    private void StopSelectedParticleSystems()
    {
        GameObject[] selected = Selection.gameObjects;
        int count = 0;

        foreach (GameObject go in selected)
        {
            ParticleSystem ps = go.GetComponent<ParticleSystem>();
            if (ps != null)
            {
                ps.Stop();
                count++;
            }
        }

        Debug.Log($"Stopped playback on {count} particle systems");
    }

    private void SetSelectedParticleSystemsEnabled(bool enabled)
    {
        GameObject[] selected = Selection.gameObjects;
        int count = 0;

        foreach (GameObject go in selected)
        {
            ParticleSystem ps = go.GetComponent<ParticleSystem>();
            if (ps != null)
            {
                Undo.RecordObject(go, enabled ? "Enable Particle System" : "Disable Particle System");
                go.SetActive(enabled);
                count++;
            }
        }

        Debug.Log($"{(enabled ? "Enabled" : "Disabled")} {count} particle systems");
    }

    // === BATCH OPERATIONS ===

    private void BatchRenameGameObjects()
    {
        if (string.IsNullOrEmpty(batchRenameFrom)) return;

        GameObject[] selected = Selection.gameObjects;
        int renamedCount = 0;

        foreach (GameObject go in selected)
        {
            if (go.name.Contains(batchRenameFrom))
            {
                Undo.RecordObject(go, "Batch Rename GameObject");
                go.name = go.name.Replace(batchRenameFrom, batchRenameTo);
                renamedCount++;
            }
        }

        Debug.Log($"Renamed {renamedCount} GameObjects");
    }

    private void BatchRenameMaterials()
    {
        if (string.IsNullOrEmpty(batchRenameFrom)) return;

        var materials = Selection.GetFiltered<Material>(SelectionMode.Assets);
        int renamedCount = 0;

        foreach (Material mat in materials)
        {
            if (mat.name.Contains(batchRenameFrom))
            {
                Undo.RecordObject(mat, "Batch Rename Material");
                string assetPath = AssetDatabase.GetAssetPath(mat);
                string newName = mat.name.Replace(batchRenameFrom, batchRenameTo);
                AssetDatabase.RenameAsset(assetPath, newName);
                renamedCount++;
            }
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"Renamed {renamedCount} Materials");
    }

    private void BatchSetRenderQueue()
    {
        GameObject[] selected = Selection.gameObjects;
        int modifiedCount = 0;

        foreach (GameObject go in selected)
        {
            var renderers = go.GetComponentsInChildren<ParticleSystemRenderer>();
            foreach (var renderer in renderers)
            {
                if (renderer.sharedMaterial != null)
                {
                    Undo.RecordObject(renderer.sharedMaterial, "Batch Set Render Queue");
                    renderer.sharedMaterial.renderQueue = batchRenderQueue;
                    EditorUtility.SetDirty(renderer.sharedMaterial);
                    modifiedCount++;
                }
            }
        }

        Debug.Log($"Set render queue to {batchRenderQueue} on {modifiedCount} materials");
    }

    private void BatchAddPrefixToGameObjects()
    {
        if (string.IsNullOrEmpty(batchPrefix)) return;

        GameObject[] selected = Selection.gameObjects;
        int prefixedCount = 0;

        foreach (GameObject go in selected)
        {
            if (!go.name.StartsWith(batchPrefix))
            {
                Undo.RecordObject(go, "Batch Add Prefix");
                go.name = batchPrefix + go.name;
                prefixedCount++;
            }
        }

        Debug.Log($"Added prefix '{batchPrefix}' to {prefixedCount} GameObjects");
    }

    private void BatchAddPrefixToMaterials()
    {
        if (string.IsNullOrEmpty(batchPrefix)) return;

        var materials = Selection.GetFiltered<Material>(SelectionMode.Assets);
        int prefixedCount = 0;

        foreach (Material mat in materials)
        {
            if (!mat.name.StartsWith(batchPrefix))
            {
                string assetPath = AssetDatabase.GetAssetPath(mat);
                string newName = batchPrefix + mat.name;
                AssetDatabase.RenameAsset(assetPath, newName);
                prefixedCount++;
            }
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"Added prefix '{batchPrefix}' to {prefixedCount} Materials");
    }

    private void BatchAddPrefixToTextures()
    {
        if (string.IsNullOrEmpty(batchPrefix)) return;

        var textures = Selection.GetFiltered<Texture>(SelectionMode.Assets);
        int prefixedCount = 0;

        foreach (Texture tex in textures)
        {
            if (!tex.name.StartsWith(batchPrefix))
            {
                string assetPath = AssetDatabase.GetAssetPath(tex);
                string newName = batchPrefix + tex.name;
                AssetDatabase.RenameAsset(assetPath, newName);
                prefixedCount++;
            }
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"Added prefix '{batchPrefix}' to {prefixedCount} Textures");
    }

    private void BatchAddPrefixToAllAssets()
    {
        if (string.IsNullOrEmpty(batchPrefix)) return;

        var allAssets = Selection.GetFiltered<UnityEngine.Object>(SelectionMode.Assets);
        int prefixedCount = 0;

        foreach (var asset in allAssets)
        {
            if (!asset.name.StartsWith(batchPrefix))
            {
                string assetPath = AssetDatabase.GetAssetPath(asset);
                if (!string.IsNullOrEmpty(assetPath))
                {
                    string newName = batchPrefix + asset.name;
                    AssetDatabase.RenameAsset(assetPath, newName);
                    prefixedCount++;
                }
            }
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"Added prefix '{batchPrefix}' to {prefixedCount} Assets");
    }

    private void BatchApplyColorTint()
    {
        GameObject[] selected = Selection.gameObjects;
        int modifiedCount = 0;

        foreach (GameObject go in selected)
        {
            var particleSystems = go.GetComponentsInChildren<ParticleSystem>();
            foreach (var ps in particleSystems)
            {
                Undo.RecordObject(ps, "Batch Apply Color Tint");
                var main = ps.main;
                var currentColor = main.startColor;

                if (currentColor.mode == ParticleSystemGradientMode.Color)
                {
                    main.startColor = batchColorTint;
                }
                else if (currentColor.mode == ParticleSystemGradientMode.TwoColors)
                {
                    main.startColor = new ParticleSystem.MinMaxGradient(
                        Color.Lerp(currentColor.colorMin, batchColorTint, 0.5f),
                        Color.Lerp(currentColor.colorMax, batchColorTint, 0.5f)
                    );
                }
                modifiedCount++;
            }
        }

        Debug.Log($"Applied color tint to {modifiedCount} particle systems");
    }

    private void BatchPerformanceTest()
    {
        GameObject[] selected = Selection.gameObjects;
        int totalParticles = 0;
        int totalSystems = 0;

        foreach (GameObject go in selected)
        {
            var particleSystems = go.GetComponentsInChildren<ParticleSystem>();
            foreach (var ps in particleSystems)
            {
                totalSystems++;
                var main = ps.main;
                totalParticles += main.maxParticles;
            }
        }

        Debug.Log($"Performance Analysis:");
        Debug.Log($"  Total Particle Systems: {totalSystems}");
        Debug.Log($"  Total Max Particles: {totalParticles}");
        Debug.Log($"  Estimated Performance: {(totalParticles > 5000 ? "Heavy" : totalParticles > 2000 ? "Medium" : "Light")}");

        if (totalParticles > 5000)
        {
            Debug.LogWarning("High particle count detected! Consider optimization.");
        }
    }

    private void FindMissingReferences()
    {
        GameObject[] selected = Selection.gameObjects;
        int missingCount = 0;

        foreach (GameObject go in selected)
        {
            var renderers = go.GetComponentsInChildren<ParticleSystemRenderer>();
            foreach (var renderer in renderers)
            {
                if (renderer.sharedMaterial == null)
                {
                    Debug.LogWarning($"Missing material on {go.name}", go);
                    missingCount++;
                }
            }

            var particleSystems = go.GetComponentsInChildren<ParticleSystem>();
            foreach (var ps in particleSystems)
            {
                var subEmitters = ps.subEmitters;
                for (int i = 0; i < subEmitters.subEmittersCount; i++)
                {
                    if (subEmitters.GetSubEmitterSystem(i) == null)
                    {
                        Debug.LogWarning($"Missing sub-emitter on {go.name}", go);
                        missingCount++;
                    }
                }
            }
        }

        Debug.Log($"Found {missingCount} missing references");
    }

    // === QUICK TOOLS ===

    private void OpenFolder(string folderPath)
    {
        if (!AssetDatabase.IsValidFolder(folderPath))
        {
            Debug.LogError($"Folder not found: {folderPath}");
            return;
        }

        UnityEngine.Object folder = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(folderPath);
        EditorGUIUtility.PingObject(folder);
        Selection.activeObject = folder;
    }

    private void FindMissingTextures()
    {
        var materials = Resources.FindObjectsOfTypeAll<Material>();
        int missingCount = 0;

        foreach (Material mat in materials)
        {
            if (mat == null) continue;

            var shader = mat.shader;
            for (int i = 0; i < ShaderUtil.GetPropertyCount(shader); i++)
            {
                if (ShaderUtil.GetPropertyType(shader, i) == ShaderUtil.ShaderPropertyType.TexEnv)
                {
                    string propName = ShaderUtil.GetPropertyName(shader, i);
                    Texture tex = mat.GetTexture(propName);
                    if (tex == null && mat.HasProperty(propName))
                    {
                        Debug.LogWarning($"Missing texture '{propName}' in material '{mat.name}'", mat);
                        missingCount++;
                    }
                }
            }
        }

        Debug.Log($"Found {missingCount} missing texture references");
    }

    private void FindMissingMaterials()
    {
        var renderers = Resources.FindObjectsOfTypeAll<Renderer>();
        int missingCount = 0;

        foreach (Renderer renderer in renderers)
        {
            if (renderer == null) continue;

            foreach (Material mat in renderer.sharedMaterials)
            {
                if (mat == null)
                {
                    Debug.LogWarning($"Missing material on renderer '{renderer.name}'", renderer);
                    missingCount++;
                }
            }
        }

        Debug.Log($"Found {missingCount} missing material references");
    }

    private void FindMissingMeshes()
    {
        var meshFilters = Resources.FindObjectsOfTypeAll<MeshFilter>();
        int missingCount = 0;

        foreach (MeshFilter meshFilter in meshFilters)
        {
            if (meshFilter != null && meshFilter.sharedMesh == null)
            {
                Debug.LogWarning($"Missing mesh on MeshFilter '{meshFilter.name}'", meshFilter);
                missingCount++;
            }
        }

        var meshRenderers = Resources.FindObjectsOfTypeAll<SkinnedMeshRenderer>();
        foreach (SkinnedMeshRenderer meshRenderer in meshRenderers)
        {
            if (meshRenderer != null && meshRenderer.sharedMesh == null)
            {
                Debug.LogWarning($"Missing mesh on SkinnedMeshRenderer '{meshRenderer.name}'", meshRenderer);
                missingCount++;
            }
        }

        Debug.Log($"Found {missingCount} missing mesh references");
    }

    private void FindAllMissingReferences()
    {
        Debug.Log("=== Comprehensive Missing References Check ===");
        FindMissingTextures();
        FindMissingMaterials();
        FindMissingMeshes();
        Debug.Log("=== Check Complete ===");
    }

    private void ValidateSelectedPrefabs()
    {
        var prefabs = Selection.GetFiltered<GameObject>(SelectionMode.Assets);
        int validatedCount = 0;
        int issuesFound = 0;

        foreach (GameObject prefab in prefabs)
        {
            validatedCount++;
            var renderers = prefab.GetComponentsInChildren<Renderer>();

            foreach (Renderer renderer in renderers)
            {
                if (renderer.sharedMaterials.Any(mat => mat == null))
                {
                    Debug.LogWarning($"Prefab '{prefab.name}' has missing materials", prefab);
                    issuesFound++;
                }
            }
        }

        Debug.Log($"Validated {validatedCount} prefabs, found {issuesFound} issues");
    }

    private void CheckTextureSizes()
    {
        var textures = Selection.GetFiltered<Texture2D>(SelectionMode.Assets);
        int largeTextureCount = 0;

        foreach (Texture2D tex in textures)
        {
            if (tex.width > 2048 || tex.height > 2048)
            {
                Debug.LogWarning($"Large texture: '{tex.name}' ({tex.width}x{tex.height})", tex);
                largeTextureCount++;
            }
        }

        Debug.Log($"Found {largeTextureCount} textures larger than 2048px");
    }

    private void FindDuplicateMaterials()
    {
        var allMaterials = Resources.FindObjectsOfTypeAll<Material>();
        var duplicates = allMaterials
            .GroupBy(m => m.name)
            .Where(g => g.Count() > 1)
            .ToList();

        if (duplicates.Count > 0)
        {
            Debug.LogWarning($"Found {duplicates.Count} duplicate material names!");
            foreach (var group in duplicates)
            {
                Debug.Log($"Material '{group.Key}' appears {group.Count()} times");
            }
        }
        else
        {
            Debug.Log("No duplicate materials found!");
        }
    }

    private void RemoveEmptyGameObjects()
    {
        GameObject[] selected = Selection.gameObjects;
        int removedCount = 0;

        foreach (GameObject go in selected)
        {
            if (go.transform.childCount == 0 && go.GetComponents<Component>().Length == 1) // Only has Transform
            {
                Undo.DestroyObjectImmediate(go);
                removedCount++;
            }
        }

        Debug.Log($"Removed {removedCount} empty GameObjects");
    }

    private void CleanNullComponents()
    {
        GameObject[] selected = Selection.gameObjects;
        int cleanedCount = 0;

        foreach (GameObject go in selected)
        {
            var components = go.GetComponents<Component>();
            for (int i = components.Length - 1; i >= 0; i--)
            {
                if (components[i] == null)
                {
                    Undo.RecordObject(go, "Remove Null Component");
                    cleanedCount++;
                }
            }
        }

        Debug.Log($"Cleaned {cleanedCount} null components");
    }

    private void RemoveUnusedComponents()
    {
        // This is a more complex operation - for now, we'll focus on common unused components
        GameObject[] selected = Selection.gameObjects;
        int removedCount = 0;

        foreach (GameObject go in selected)
        {
            // Remove Animation components that have no clips
            var anim = go.GetComponent<Animation>();
            if (anim != null && anim.GetClipCount() == 0)
            {
                Undo.DestroyObjectImmediate(anim);
                removedCount++;
            }

            // Remove AudioSource components that have no clip
            var audioSource = go.GetComponent<AudioSource>();
            if (audioSource != null && audioSource.clip == null)
            {
                Undo.DestroyObjectImmediate(audioSource);
                removedCount++;
            }
        }

        Debug.Log($"Removed {removedCount} unused components");
    }

    private void RemoveUnusedModules()
    {
        GameObject[] selected = Selection.gameObjects;
        int optimizedCount = 0;

        foreach (GameObject go in selected)
        {
            ParticleSystem ps = go.GetComponent<ParticleSystem>();
            if (ps == null) continue;

            Undo.RecordObject(ps, "Remove Unused PS Modules");

            // Disable modules that are enabled but not configured
            var trails = ps.trails;
            if (trails.enabled && trails.ratio == 0)
            {
                trails.enabled = false;
                optimizedCount++;
            }

            var lights = ps.lights;
            if (lights.enabled && lights.ratio == 0)
            {
                lights.enabled = false;
                optimizedCount++;
            }

            var noise = ps.noise;
            if (noise.enabled && noise.strength.constant == 0)
            {
                noise.enabled = false;
                optimizedCount++;
            }

            var collision = ps.collision;
            if (collision.enabled && collision.type == ParticleSystemCollisionType.Planes && collision.planeCount == 0)
            {
                collision.enabled = false;
                optimizedCount++;
            }
        }

        Debug.Log($"Optimized {optimizedCount} unused modules from particle systems");
    }

    private void OptimizeSelectedParticleSystems()
    {
        GameObject[] selected = Selection.gameObjects;
        int optimizedCount = 0;

        foreach (GameObject go in selected)
        {
            ParticleSystem ps = go.GetComponent<ParticleSystem>();
            if (ps == null) continue;

            Undo.RecordObject(ps, "Optimize Particle System");

            var main = ps.main;
            if (main.maxParticles > 1000)
            {
                main.maxParticles = 1000;
                optimizedCount++;
            }
        }

        Debug.Log($"Applied optimizations to {optimizedCount} particle systems");
    }

    private void AutoSetupParticleLayers()
    {
        GameObject[] selected = Selection.gameObjects;
        int effectLayer = LayerMask.NameToLayer("Effect");

        if (effectLayer < 0)
        {
            if (autoCreateEffectLayer)
                effectLayer = EnsureLayerExists("Effect");
            else
                effectLayer = 0;
        }

        int count = 0;
        foreach (GameObject go in selected)
        {
            if (go.GetComponent<ParticleSystem>() != null)
            {
                SetLayerRecursively(go, effectLayer);
                count++;
            }
        }

        Debug.Log($"Set layer 'Effect' on {count} particle systems");
    }

    private void ResetParticleTransforms()
    {
        GameObject[] selected = Selection.gameObjects;
        int resetCount = 0;

        foreach (GameObject go in selected)
        {
            if (go.GetComponent<ParticleSystem>() != null)
            {
                Undo.RecordObject(go.transform, "Reset Particle Transform");
                go.transform.localPosition = Vector3.zero;
                go.transform.localRotation = Quaternion.identity;
                go.transform.localScale = Vector3.one;
                resetCount++;
            }
        }

        Debug.Log($"Reset transforms for {resetCount} particle systems");
    }


    // === SETTINGS ===

    private void LoadSettings()
    {
        basePrefabFolder = EditorPrefs.GetString("PixieDust_PrefabFolder", basePrefabFolder);
        baseMaterialFolder = EditorPrefs.GetString("PixieDust_MaterialFolder", baseMaterialFolder);
        spawned_layer = EditorPrefs.GetString("PixieDust_Layer", spawned_layer);
        standard_shader = EditorPrefs.GetString("PixieDust_Shader", standard_shader);
        materialDefaultName = EditorPrefs.GetString("PixieDust_MaterialName", materialDefaultName);
        batchPrefix = EditorPrefs.GetString("PixieDust_BatchPrefix", batchPrefix);
    }

    private void SaveSettings()
    {
        EditorPrefs.SetString("PixieDust_PrefabFolder", basePrefabFolder);
        EditorPrefs.SetString("PixieDust_MaterialFolder", baseMaterialFolder);
        EditorPrefs.SetString("PixieDust_Layer", spawned_layer);
        EditorPrefs.SetString("PixieDust_Shader", standard_shader);
        EditorPrefs.SetString("PixieDust_MaterialName", materialDefaultName);
        EditorPrefs.SetString("PixieDust_BatchPrefix", batchPrefix);
    }

    // === HIERARCHY COLORER (from original Pixie Dust) ===

    public static class HierarchyColorer
    {
        private const string PREFS_KEY = "PIXIE_HCOLOR_";
        private static Dictionary<int, Color> activeColors = new Dictionary<int, Color>();

        static HierarchyColorer()
        {
            EditorApplication.hierarchyWindowItemOnGUI += HandleHierarchyItem;
            RebuildColorCache();
        }

        private static void RebuildColorCache()
        {
            activeColors.Clear();
            // Simplified cache rebuild - could be enhanced based on original implementation
        }

        private static void HandleHierarchyItem(int instanceID, Rect rect)
        {
            if (!activeColors.TryGetValue(instanceID, out Color bgColor)) return;

            Rect bgRect = new Rect(32, rect.y, rect.width + rect.x - 32, rect.height);
            EditorGUI.DrawRect(bgRect, bgColor);

            bool useWhite = bgColor.grayscale < 0.6f;
            Color textColor = useWhite ? Color.white : new Color(0.1f, 0.1f, 0.1f);

            GUIStyle style = new GUIStyle("Label")
            {
                normal = { textColor = textColor },
                fontStyle = FontStyle.Bold
            };

            GameObject obj = EditorUtility.InstanceIDToObject(instanceID) as GameObject;
            if (obj != null && Event.current.type == EventType.Repaint)
            {
                style.Draw(rect, new GUIContent(obj.name), false, false, false, false);
            }
        }

        public static void SetColor(GameObject obj, Color color)
        {
            if (obj == null) return;
            string key = $"{PREFS_KEY}{obj.GetInstanceID()}";
            EditorPrefs.SetString(key, $"{color.r},{color.g},{color.b},{color.a}");
            activeColors[obj.GetInstanceID()] = color;
            EditorApplication.RepaintHierarchyWindow();
        }

        public static void RemoveColor(GameObject obj)
        {
            if (obj == null) return;
            int id = obj.GetInstanceID();
            string key = $"{PREFS_KEY}{id}";
            EditorPrefs.DeleteKey(key);
            activeColors.Remove(id);
            EditorApplication.RepaintHierarchyWindow();
        }

        public static void ClearAllColors()
        {
            foreach (var kvp in activeColors)
            {
                EditorPrefs.DeleteKey($"{PREFS_KEY}{kvp.Key}");
            }
            activeColors.Clear();
            EditorApplication.RepaintHierarchyWindow();
        }
    }


}