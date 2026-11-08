namespace Engine;

/// <summary>Which faces of a path-traced reference's paths reflect by their specular share as well as their diffuse share.</summary>
internal enum ReferenceGloss
{
    /// <summary>
    /// None goes on by its specular share, the first face lit by the lights as the model pass lights
    /// it and every face after it as global illumination lights one.
    /// </summary>
    None,

    /// <summary>
    /// The first face the camera sees goes on by its specular share too, a way drawn about its
    /// mirror way by GGX's visible normals, so it reflects the indirect light as the model pass does,
    /// and every face after it as global illumination lights one. Global illumination's tests read
    /// against this.
    /// </summary>
    First,

    /// <summary>
    /// Every face a path meets, a camera's or a probe's, is lit and goes on as the first is, its
    /// metal reflecting and its lights' specular share reflected, so the gloss of what a path meets
    /// carries light between bounces, as the bridge's reference traces it and the probes' rays
    /// carry it.
    /// </summary>
    Every,
}
