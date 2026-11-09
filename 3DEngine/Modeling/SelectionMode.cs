namespace Engine;

/// <summary>What a selection of an <see cref="EditMesh"/> does with what was selected before it.</summary>
public enum SelectionMode
{
    /// <summary>What it picks is all that is selected after it.</summary>
    Replace,

    /// <summary>What it picks is selected as well as what was.</summary>
    Add,

    /// <summary>What it picks is taken out of the selection, and the rest stays as it was.</summary>
    Remove,
}
