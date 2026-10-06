using System.Numerics;
using A = Assimp;

namespace Engine;

internal sealed partial class AssimpModelReader
{
    // -- Animations

    private static SceneAnimationPayload? ConvertAnimation(A.Animation anim)
    {
        if (anim.NodeAnimationChannelCount == 0 && anim.MeshMorphAnimationChannelCount == 0) return null;

        double tps = anim.TicksPerSecond > 0.0 ? anim.TicksPerSecond : 25.0;
        double durTicks = anim.DurationInTicks;
        var channels = new List<SceneAnimationChannel>(anim.NodeAnimationChannelCount * 3);

        // A morph channel keys every target of a node's mesh at once, each key naming targets and
        // their weights, which become a channel per target, its weight in x.
        foreach (var ch in anim.MeshMorphAnimationChannels)
        {
            var byTarget = new SortedDictionary<int, List<(float Time, float Weight)>>();
            foreach (var key in ch.MeshMorphKeys)
                for (int v = 0; v < key.Values.Count && v < key.Weights.Count; v++)
                {
                    if (!byTarget.TryGetValue(key.Values[v], out var keys)) byTarget[key.Values[v]] = keys = [];
                    keys.Add(((float)(key.Time / tps), (float)key.Weights[v]));
                }
            foreach (var (target, keys) in byTarget)
                channels.Add(new SceneAnimationChannel
                {
                    TargetNodePath = "/" + ch.Name,
                    Property = SceneAnimationProperty.MorphWeight,
                    TimesSeconds = [.. keys.Select(k => k.Time)],
                    Values = [.. keys.Select(k => new Vector4(k.Weight, 0, 0, 0))],
                    MorphTarget = target,
                });
        }

        foreach (var ch in anim.NodeAnimationChannels)
        {
            string targetPath = "/" + ch.NodeName; // resolved by name; spawner does the lookup

            if (ch.PositionKeyCount > 0)
            {
                var times = new float[ch.PositionKeyCount];
                var values = new Vector4[ch.PositionKeyCount];
                for (int k = 0; k < ch.PositionKeyCount; k++)
                {
                    var key = ch.PositionKeys[k];
                    times[k] = (float)(key.Time / tps);
                    values[k] = new Vector4(key.Value.X, key.Value.Y, key.Value.Z, 0f);
                }
                channels.Add(new SceneAnimationChannel
                {
                    TargetNodePath = targetPath,
                    Property = SceneAnimationProperty.Translation,
                    TimesSeconds = times,
                    Values = values,
                });
            }

            if (ch.RotationKeyCount > 0)
            {
                var times = new float[ch.RotationKeyCount];
                var values = new Vector4[ch.RotationKeyCount];
                for (int k = 0; k < ch.RotationKeyCount; k++)
                {
                    var key = ch.RotationKeys[k];
                    times[k] = (float)(key.Time / tps);
                    values[k] = new Vector4(key.Value.X, key.Value.Y, key.Value.Z, key.Value.W);
                }
                channels.Add(new SceneAnimationChannel
                {
                    TargetNodePath = targetPath,
                    Property = SceneAnimationProperty.Rotation,
                    TimesSeconds = times,
                    Values = values,
                });
            }

            if (ch.ScalingKeyCount > 0)
            {
                var times = new float[ch.ScalingKeyCount];
                var values = new Vector4[ch.ScalingKeyCount];
                for (int k = 0; k < ch.ScalingKeyCount; k++)
                {
                    var key = ch.ScalingKeys[k];
                    times[k] = (float)(key.Time / tps);
                    values[k] = new Vector4(key.Value.X, key.Value.Y, key.Value.Z, 0f);
                }
                channels.Add(new SceneAnimationChannel
                {
                    TargetNodePath = targetPath,
                    Property = SceneAnimationProperty.Scale,
                    TimesSeconds = times,
                    Values = values,
                });
            }
        }

        return new SceneAnimationPayload
        {
            Name = string.IsNullOrEmpty(anim.Name) ? "Animation" : anim.Name,
            DurationSeconds = (float)(durTicks / tps),
            Channels = channels,
        };
    }
}
