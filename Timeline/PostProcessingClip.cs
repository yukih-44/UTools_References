using System;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

#if UNITY_EDITOR
using UnityEditor;
#endif

public enum PostProcessingFloatProperty
{
    BloomIntensity,
    BloomThreshold,
    BloomScatter,

    VignetteIntensity,
    VignetteSmoothness,

    PostExposure,
    Contrast,
    HueShift,
    Saturation,

    ChromaticAberrationIntensity,
    FilmGrainIntensity,
    LensDistortionIntensity,

    DepthOfFieldFocusDistance,
    DepthOfFieldAperture,
    DepthOfFieldFocalLength,

    Count
}

[Serializable]
public sealed class PostProcessingParameterAnimation
{
    [Tooltip("The URP Volume parameter controlled by this entry.")]
    public PostProcessingFloatProperty property;

    [Tooltip("Value at normalized clip time 0.")]
    public float startValue;

    [Tooltip("Value reached at Middle Time.")]
    public float middleValue = 1f;

    [Tooltip("Value at normalized clip time 1.")]
    public float endValue;

    [Range(0.01f, 0.99f)]
    [Tooltip("Normalized point in the clip at which Middle Value is reached.")]
    public float middleTime = 0.5f;

    [Tooltip(
        "Remaps normalized clip time before evaluating Start → Middle → End. " +
        "The default is linear.")]
    public AnimationCurve timingCurve =
        AnimationCurve.Linear(0f, 0f, 1f, 1f);

    public float Evaluate(float normalizedTime)
    {
        float remappedTime = timingCurve != null
            ? timingCurve.Evaluate(normalizedTime)
            : normalizedTime;

        if (remappedTime <= middleTime)
        {
            float firstHalfT = middleTime > 0f
                ? remappedTime / middleTime
                : 1f;

            return Mathf.LerpUnclamped(
                startValue,
                middleValue,
                firstHalfT);
        }

        float secondHalfDuration = 1f - middleTime;
        float secondHalfT = secondHalfDuration > 0f
            ? (remappedTime - middleTime) / secondHalfDuration
            : 1f;

        return Mathf.LerpUnclamped(
            middleValue,
            endValue,
            secondHalfT);
    }

    public void EnsureDefaults()
    {
        if (timingCurve == null || timingCurve.length == 0)
        {
            timingCurve =
                AnimationCurve.Linear(0f, 0f, 1f, 1f);
        }

        middleTime = Mathf.Clamp(middleTime, 0.01f, 0.99f);
    }
}

[Serializable]
public sealed class PostProcessingClipBehaviour : PlayableBehaviour
{
    public PostProcessingParameterAnimation[] parameters =
        Array.Empty<PostProcessingParameterAnimation>();
}

public sealed class PostProcessingClip : PlayableAsset, ITimelineClipAsset
{
    [Tooltip(
        "Add every post-processing parameter animated by this clip. " +
        "Each entry has Start, Middle, End, Middle Time, and a timing curve.")]
    public PostProcessingParameterAnimation[] parameters =
        Array.Empty<PostProcessingParameterAnimation>();

    public ClipCaps clipCaps =>
        ClipCaps.Blending |
        ClipCaps.Extrapolation |
        ClipCaps.SpeedMultiplier |
        ClipCaps.ClipIn;

    public override Playable CreatePlayable(
        PlayableGraph graph,
        GameObject owner)
    {
        EnsureParameterDefaults();

        ScriptPlayable<PostProcessingClipBehaviour> playable =
            ScriptPlayable<PostProcessingClipBehaviour>.Create(graph);

        playable.GetBehaviour().parameters = parameters;
        return playable;
    }

    private void OnEnable()
    {
        EnsureParameterDefaults();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        EnsureParameterDefaults();
    }
#endif

    private void EnsureParameterDefaults()
    {
        if (parameters == null)
        {
            parameters =
                Array.Empty<PostProcessingParameterAnimation>();
            return;
        }

        for (int i = 0; i < parameters.Length; i++)
        {
            parameters[i]?.EnsureDefaults();
        }
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(PostProcessingClip))]
public sealed class PostProcessingClipEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space();

        if (GUILayout.Button("Refresh Volume Cache"))
        {
            PostProcessingTimelineRefresh.RequestRefresh();
            EditorUtility.SetDirty(target);
            SceneView.RepaintAll();
        }

        EditorGUILayout.HelpBox(
            "Use Refresh Volume Cache after adding or replacing overrides in " +
            "the bound Volume Profile if Timeline does not immediately see them.",
            MessageType.Info);
    }
}
#endif
