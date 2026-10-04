namespace Engine;

/// <summary>Attachment load operation when a render pass begins.</summary>
public enum LoadOp
{
    /// <summary>Clear the attachment to a specified value.</summary>
    Clear,
    /// <summary>Preserve existing contents.</summary>
    Load,
    /// <summary>Contents are undefined (don't care).</summary>
    DontCare
}

/// <summary>Attachment store operation when a render pass ends.</summary>
public enum StoreOp
{
    /// <summary>Store the attachment contents for later use.</summary>
    Store,
    /// <summary>Contents are not needed after the render pass (don't care).</summary>
    DontCare
}
