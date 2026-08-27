// VFXPrefabValidator.ResultsOptimized.cs
// Reference implementation for a partial VFXPrefabValidator EditorWindow.
// Purpose: avoid LINQ/allocation-heavy result processing inside OnGUI.

using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace VFXArtTools
{
    public partial class VFXPrefabValidator : EditorWindow
    {
        private sealed class CachedResultGroup
        {
            public string Key;
            public readonly List<ValidationIssue> Issues = new List<ValidationIssue>();
            public int ErrorCount;
            public int WarningCount;
            public int InfoCount;

            public int Count => Issues.Count;
        }

        private readonly List<CachedResultGroup> _cachedResultGroups = new List<CachedResultGroup>();
        private readonly Dictionary<string, CachedResultGroup> _cachedResultGroupLookup =
            new Dictionary<string, CachedResultGroup>(StringComparer.Ordinal);

        private bool _resultsCacheDirty = true;

        private int _cachedErrorCount;
        private int _cachedWarningCount;
        private int _cachedInfoCount;

        private const int AutoCollapseIssueThreshold = 500;

        private void MarkResultsDirty()
        {
            _resultsCacheDirty = true;
        }

        private void ClearResults()
        {
            _issues.Clear();
            _prefabFoldouts.Clear();
            MarkResultsDirty();
        }

        private void FinishValidationResults()
        {
            MarkResultsDirty();

            if (_issues.Count >= AutoCollapseIssueThreshold)
            {
                _prefabFoldouts.Clear();
            }

            Repaint();
        }

        private void RebuildResultsCacheIfNeeded()
        {
            if (!_resultsCacheDirty)
                return;

            _resultsCacheDirty = false;

            _cachedResultGroups.Clear();
            _cachedResultGroupLookup.Clear();

            _cachedErrorCount = 0;
            _cachedWarningCount = 0;
            _cachedInfoCount = 0;

            for (int i = 0; i < _issues.Count; i++)
            {
                var issue = _issues[i];

                switch (issue.severity)
                {
                    case Severity.Error:
                        _cachedErrorCount++;
                        break;
                    case Severity.Warning:
                        _cachedWarningCount++;
                        break;
                    case Severity.Info:
                        _cachedInfoCount++;
                        break;
                }

                if (!IsVisible(issue))
                    continue;

                var key = issue.prefabPath ?? string.Empty;

                if (!_cachedResultGroupLookup.TryGetValue(key, out var group))
                {
                    group = new CachedResultGroup
                    {
                        Key = key
                    };

                    _cachedResultGroupLookup.Add(key, group);
                    _cachedResultGroups.Add(group);
                }

                group.Issues.Add(issue);

                switch (issue.severity)
                {
                    case Severity.Error:
                        group.ErrorCount++;
                        break;
                    case Severity.Warning:
                        group.WarningCount++;
                        break;
                    case Severity.Info:
                        group.InfoCount++;
                        break;
                }
            }

            _cachedResultGroups.Sort((a, b) =>
                string.Compare(a.Key, b.Key, StringComparison.OrdinalIgnoreCase));
        }

        private void DrawResultsSectionOptimized()
        {
            BeginSection($"Results ({_issues.Count})");

            var filtersChanged = false;

            EditorGUILayout.BeginHorizontal();

            var newShowErrors = GUILayout.Toggle(_showErrors, "Errors", "Button");
            if (newShowErrors != _showErrors)
            {
                _showErrors = newShowErrors;
                filtersChanged = true;
            }

            var newShowWarnings = GUILayout.Toggle(_showWarnings, "Warnings", "Button");
            if (newShowWarnings != _showWarnings)
            {
                _showWarnings = newShowWarnings;
                filtersChanged = true;
            }

            var newShowInfo = GUILayout.Toggle(_showInfo, "Info", "Button");
            if (newShowInfo != _showInfo)
            {
                _showInfo = newShowInfo;
                filtersChanged = true;
            }

            var newCategoryFilter = EditorGUILayout.Popup(
                _categoryFilter,
                CategoryFilterNames,
                GUILayout.Width(120f));

            if (newCategoryFilter != _categoryFilter)
            {
                _categoryFilter = newCategoryFilter;
                filtersChanged = true;
            }

            if (GUILayout.Button("Clear", GUILayout.Width(60f)))
            {
                ClearResults();
            }

            EditorGUILayout.EndHorizontal();

            if (filtersChanged)
                MarkResultsDirty();

            RebuildResultsCacheIfNeeded();

            EditorGUILayout.LabelField(
                $"Errors: {_cachedErrorCount}    Warnings: {_cachedWarningCount}    Info: {_cachedInfoCount}",
                EditorStyles.miniLabel);

            if (_issues.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    "No results yet. Choose a target and run one of the checks.",
                    MessageType.None);

                EndSection();
                return;
            }

            _resultScroll = EditorGUILayout.BeginScrollView(
                _resultScroll,
                GUILayout.MinHeight(180f),
                GUILayout.MaxHeight(700f));

            for (int groupIndex = 0; groupIndex < _cachedResultGroups.Count; groupIndex++)
            {
                var group = _cachedResultGroups[groupIndex];

                if (!_prefabFoldouts.TryGetValue(group.Key, out var isOpen))
                {
                    isOpen = _issues.Count < AutoCollapseIssueThreshold;
                    _prefabFoldouts[group.Key] = isOpen;
                }

                var assetName = string.IsNullOrEmpty(group.Key)
                    ? "(Unknown Asset)"
                    : Path.GetFileName(group.Key);

                EditorGUILayout.Space(4f);
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.BeginHorizontal();

                isOpen = EditorGUILayout.Foldout(isOpen, GUIContent.none, true);
                _prefabFoldouts[group.Key] = isOpen;

                GUILayout.Label(assetName, _prefabHeaderStyle);
                GUILayout.FlexibleSpace();

                GUILayout.Label(
                    $"{group.Count} issues   E:{group.ErrorCount} W:{group.WarningCount}",
                    EditorStyles.miniBoldLabel);

                if (GUILayout.Button("Ping", GUILayout.Width(46f)))
                {
                    var asset = AssetDatabase.LoadMainAssetAtPath(group.Key);

                    if (asset != null)
                    {
                        EditorGUIUtility.PingObject(asset);
                        Selection.activeObject = asset;
                        EditorUtility.FocusProjectWindow();
                    }
                }

                EditorGUILayout.EndHorizontal();

                if (isOpen)
                {
                    EditorGUI.indentLevel++;

                    var groupIssues = group.Issues;
                    for (int issueIndex = 0; issueIndex < groupIssues.Count; issueIndex++)
                    {
                        DrawIssue(groupIssues[issueIndex]);
                    }

                    EditorGUI.indentLevel--;
                }

                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.EndScrollView();
            EndSection();
        }

        // Integration notes:
        //
        // 1) Replace your current DrawResultsSection() body with this one,
        //    or rename DrawResultsSectionOptimized() to DrawResultsSection().
        //
        // 2) Whenever a full validation pass finishes, call:
        //       FinishValidationResults();
        //
        //    Call it ONCE after the batch, not after every AddIssue().
        //
        // 3) If another code path mutates _issues directly, call:
        //       MarkResultsDirty();
        //
        // 4) If your field names differ, adapt _issues/_showErrors/etc.
        //
        // 5) This removes repeated Count/Where/GroupBy/OrderBy/ToList work
        //    from OnGUI. With thousands of rows, IMGUI drawing itself can still
        //    dominate, so large scans start collapsed intentionally.
    }
}
