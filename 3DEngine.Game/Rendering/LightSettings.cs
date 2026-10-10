using static Engine.Engine3D;

namespace Engine.Game;

/// <summary>The settings of the light that bounces and of the frame's post-processing, which the settings window changes.</summary>
public sealed class LightSettings
{
    /// <summary>The light that bounces, at the highest quality, which this game is here to show.</summary>
    public GlobalIllumination Quality = GlobalIllumination.High;

    // Four cascades of 0.25 cover 128 blocks across around the eye, half of the 256 drawn at the
    // default render distance, with four cells to a block, so the middle of a tunnel one block wide
    // is two cells clear of its walls.
    public int Cascades = 4;
    public float CellSize = 0.25f;
    // Two cascades built again a frame, so the four an edit touches are built within two frames of
    // its meshes settling.
    public int Budget = 2;

    public float Bloom = 0.5f;
    public float Occlusion;
    public bool AutoExposure;
    public float Exposure = 1;

    public void ApplyAll()
    {
        ApplyField();
        SetGlobalIllumination(Quality);
        SetBloom(Bloom);
        SetAmbientOcclusion(Occlusion);
        ApplyExposure();
        SetShadowDistance(96);
    }

    public void ApplyField() => SetSceneField(Cascades, CellSize, Budget);

    public void ApplyExposure()
    {
        SetAutoExposure(AutoExposure);
        if (!AutoExposure) SetExposure(Exposure);
    }

    /// <summary>Steps to the next quality, past High back to Off.</summary>
    public void NextQuality()
    {
        Quality = (GlobalIllumination)(((int)Quality + 1) % 4);
        SetGlobalIllumination(Quality);
    }
}
