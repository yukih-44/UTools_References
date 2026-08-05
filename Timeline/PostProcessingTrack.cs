using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Rendering;
using UnityEngine.Timeline;

[TrackColor(0.35f, 0.55f, 0.90f)]
[TrackClipType(typeof(PostProcessingClip))]
[TrackBindingType(typeof(Volume))]
public sealed class PostProcessingTrack : TrackAsset
{
    public override Playable CreateTrackMixer(
        PlayableGraph graph,
        GameObject owner,
        int inputCount)
    {
        return ScriptPlayable<PostProcessingMixer>.Create(graph, inputCount);
    }
}

/// <summary>
/// Lightweight global invalidation token used by the editor Refresh button.
/// Incrementing this does not allocate and causes every active mixer to recache
/// its bound Volume Profile on the next evaluation.
/// </summary>
public static class PostProcessingTimelineRefresh
{
    public static int Version { get; private set; }

    public static void RequestRefresh()
    {
        unchecked
        {
            Version++;
        }
    }
}
