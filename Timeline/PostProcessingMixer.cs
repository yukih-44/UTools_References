using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public sealed class PostProcessingMixer : PlayableBehaviour
{
    private const int PropertyCount =
        (int)PostProcessingFloatProperty.Count;

    private readonly FloatParameter[] parameters =
        new FloatParameter[PropertyCount];

    private readonly float[] originalValues =
        new float[PropertyCount];

    private readonly float[] weightedValues =
        new float[PropertyCount];

    private readonly float[] totalWeights =
        new float[PropertyCount];

    private readonly bool[] captured =
        new bool[PropertyCount];

    private readonly bool[] touchedThisFrame =
        new bool[PropertyCount];

    private Volume boundVolume;
    private VolumeProfile cachedProfile;
    private int cachedComponentCount = -1;
    private int observedRefreshVersion = -1;
    private bool initialized;
    private bool hasAnyCapturedState;

    public override void ProcessFrame(
        Playable playable,
        FrameData info,
        object playerData)
    {
        Volume volume = playerData as Volume;

        if (!TryInitialize(volume))
        {
            RestoreAll();
            return;
        }

        ClearFrameAccumulators();

        int inputCount = playable.GetInputCount();

        for (int inputIndex = 0;
             inputIndex < inputCount;
             inputIndex++)
        {
            float clipWeight =
                playable.GetInputWeight(inputIndex);

            if (clipWeight <= 0f)
                continue;

            ScriptPlayable<PostProcessingClipBehaviour> input =
                (ScriptPlayable<PostProcessingClipBehaviour>)
                playable.GetInput(inputIndex);

            PostProcessingParameterAnimation[] animations =
                input.GetBehaviour().parameters;

            if (animations == null || animations.Length == 0)
                continue;

            float normalizedTime = GetNormalizedTime(input);

            for (int animationIndex = 0;
                 animationIndex < animations.Length;
                 animationIndex++)
            {
                PostProcessingParameterAnimation animation =
                    animations[animationIndex];

                if (animation == null)
                    continue;

                int propertyIndex = (int)animation.property;

                if ((uint)propertyIndex >= PropertyCount)
                    continue;

                FloatParameter parameter =
                    parameters[propertyIndex];

                // The selected override is absent from this profile.
                if (parameter == null)
                    continue;

                CaptureIfNeeded(propertyIndex, parameter);

                float value =
                    animation.Evaluate(normalizedTime);

                weightedValues[propertyIndex] +=
                    value * clipWeight;

                totalWeights[propertyIndex] +=
                    clipWeight;

                touchedThisFrame[propertyIndex] = true;
            }
        }

        ApplyFrame();
    }

    public override void OnBehaviourPause(
        Playable playable,
        FrameData info)
    {
        RestoreAll();
    }

    public override void OnPlayableDestroy(Playable playable)
    {
        RestoreAll();
        InvalidateCache();
    }

    private static float GetNormalizedTime(
        ScriptPlayable<PostProcessingClipBehaviour> playable)
    {
        double duration = playable.GetDuration();

        if (duration <= 0.0)
            return 0f;

        return Mathf.Clamp01(
            (float)(playable.GetTime() / duration));
    }

    private bool TryInitialize(Volume volume)
    {
        if (volume == null)
            return false;

        VolumeProfile profile = volume.profile;

        if (profile == null)
            return false;

        int componentCount = profile.components.Count;
        int refreshVersion =
            PostProcessingTimelineRefresh.Version;

        bool cacheIsCurrent =
            initialized &&
            ReferenceEquals(volume, boundVolume) &&
            ReferenceEquals(profile, cachedProfile) &&
            componentCount == cachedComponentCount &&
            refreshVersion == observedRefreshVersion;

        if (cacheIsCurrent)
            return true;

        // Restore values on the old cache before replacing references.
        RestoreAll();

        boundVolume = volume;
        cachedProfile = profile;
        cachedComponentCount = componentCount;
        observedRefreshVersion = refreshVersion;

        CacheParameters(profile);
        initialized = true;

        return true;
    }

    private void CacheParameters(VolumeProfile profile)
    {
        ClearParameterCache();

        if (profile.TryGet(out Bloom bloom))
        {
            parameters[
                (int)PostProcessingFloatProperty.BloomIntensity] =
                bloom.intensity;

            parameters[
                (int)PostProcessingFloatProperty.BloomThreshold] =
                bloom.threshold;

            parameters[
                (int)PostProcessingFloatProperty.BloomScatter] =
                bloom.scatter;
        }

        if (profile.TryGet(out Vignette vignette))
        {
            parameters[
                (int)PostProcessingFloatProperty.VignetteIntensity] =
                vignette.intensity;

            parameters[
                (int)PostProcessingFloatProperty.VignetteSmoothness] =
                vignette.smoothness;
        }

        if (profile.TryGet(out ColorAdjustments colorAdjustments))
        {
            parameters[
                (int)PostProcessingFloatProperty.PostExposure] =
                colorAdjustments.postExposure;

            parameters[
                (int)PostProcessingFloatProperty.Contrast] =
                colorAdjustments.contrast;

            parameters[
                (int)PostProcessingFloatProperty.HueShift] =
                colorAdjustments.hueShift;

            parameters[
                (int)PostProcessingFloatProperty.Saturation] =
                colorAdjustments.saturation;
        }

        if (profile.TryGet(
            out ChromaticAberration chromaticAberration))
        {
            parameters[
                (int)PostProcessingFloatProperty
                    .ChromaticAberrationIntensity] =
                chromaticAberration.intensity;
        }

        if (profile.TryGet(out FilmGrain filmGrain))
        {
            parameters[
                (int)PostProcessingFloatProperty.FilmGrainIntensity] =
                filmGrain.intensity;
        }

        if (profile.TryGet(out LensDistortion lensDistortion))
        {
            parameters[
                (int)PostProcessingFloatProperty
                    .LensDistortionIntensity] =
                lensDistortion.intensity;
        }

        if (profile.TryGet(out DepthOfField depthOfField))
        {
            parameters[
                (int)PostProcessingFloatProperty
                    .DepthOfFieldFocusDistance] =
                depthOfField.focusDistance;

            parameters[
                (int)PostProcessingFloatProperty
                    .DepthOfFieldAperture] =
                depthOfField.aperture;

            parameters[
                (int)PostProcessingFloatProperty
                    .DepthOfFieldFocalLength] =
                depthOfField.focalLength;
        }
    }

    private void CaptureIfNeeded(
        int index,
        FloatParameter parameter)
    {
        if (captured[index])
            return;

        originalValues[index] = parameter.value;
        captured[index] = true;
        hasAnyCapturedState = true;
    }

    private void ApplyFrame()
    {
        for (int i = 0; i < PropertyCount; i++)
        {
            if (!captured[i])
                continue;

            FloatParameter parameter = parameters[i];

            if (parameter == null)
                continue;

            if (!touchedThisFrame[i])
            {
                parameter.value = originalValues[i];
                captured[i] = false;
                continue;
            }

            float totalWeight = totalWeights[i];

            // Ease from the value captured before contribution begins.
            // Normalize if overlapping clip weights exceed one.
            parameter.value = totalWeight <= 1f
                ? originalValues[i] * (1f - totalWeight) +
                  weightedValues[i]
                : weightedValues[i] / totalWeight;
        }

        RecalculateCapturedState();
    }

    private void RestoreAll()
    {
        if (!hasAnyCapturedState)
            return;

        for (int i = 0; i < PropertyCount; i++)
        {
            if (!captured[i])
                continue;

            FloatParameter parameter = parameters[i];

            if (parameter != null)
                parameter.value = originalValues[i];

            captured[i] = false;
        }

        hasAnyCapturedState = false;
    }

    private void ClearFrameAccumulators()
    {
        for (int i = 0; i < PropertyCount; i++)
        {
            weightedValues[i] = 0f;
            totalWeights[i] = 0f;
            touchedThisFrame[i] = false;
        }
    }

    private void ClearParameterCache()
    {
        for (int i = 0; i < PropertyCount; i++)
        {
            parameters[i] = null;
        }
    }

    private void RecalculateCapturedState()
    {
        hasAnyCapturedState = false;

        for (int i = 0; i < PropertyCount; i++)
        {
            if (!captured[i])
                continue;

            hasAnyCapturedState = true;
            return;
        }
    }

    private void InvalidateCache()
    {
        initialized = false;
        boundVolume = null;
        cachedProfile = null;
        cachedComponentCount = -1;
        observedRefreshVersion = -1;

        ClearParameterCache();
    }
}
