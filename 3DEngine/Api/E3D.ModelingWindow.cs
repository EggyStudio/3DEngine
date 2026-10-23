using System.Numerics;
using ImGuiNET;

namespace Engine;

public static partial class E3D
{
    // -- The modeling window, which shapes an edit mesh by hand in the program's own frame, since
    // the engine has no editor (DESIGN.md section 7).

    /// <summary>Draws an ImGui window that shapes an edit mesh by hand, by buttons for its operations and by the mouse selecting and dragging its faces in the 3D view.</summary>
    /// <param name="mesh">The mesh, whose model the program draws.</param>
    /// <param name="position">Where the program draws the mesh's model, unturned and unscaled, which the pointer's ray is taken to.</param>
    /// <remarks>
    /// <para>
    /// Called between <c>BeginDrawing</c> and <c>EndDrawing</c> after the frame's first
    /// <c>BeginMode3D</c>, whose camera the pointer's ray is cast through, as any ImGui window is
    /// drawn. The window lists the recipe with buttons to undo and redo its lines, says the
    /// selection's counts and offers each operation with its measures as fields and a button, a
    /// slot's look and the selection's texture coordinates among them, the coordinates drawn over the
    /// texture of the first selected face's slot. Each
    /// button runs the operation's line of the recipe, so the window writes the lines the calls and
    /// the model commands write, and a line the mesh refuses is shown with the reason.
    /// </para>
    /// <para>
    /// A click over the 3D view selects the face under the pointer, found by testing the ray against
    /// the mesh's faces one by one (<see cref="GetRayCollisionEditMesh"/>), shift adding it to the
    /// selection and control taking it away. Pressed on a selected face and dragged, the pointer
    /// moves along the face's normal, and the selection is extruded, or moved where the window says
    /// so, by whole steps of the grid when it is let go, the distance shown beside the pointer
    /// while it is held. The face under the pointer and the selection are outlined over the model.
    /// The program builds the mesh's model again where the window says the mesh changed, with
    /// <see cref="UpdateModelFromEditMesh"/>.
    /// </para>
    /// </remarks>
    /// <returns>Whether the mesh's shape or slots changed since the window was last drawn, by the window, the program or the model commands, a selection alone leaving them as they were.</returns>
    public static bool DrawModelingWindow(EditMesh mesh, Vector3 position = default)
    {
        if (Edited(mesh, nameof(DrawModelingWindow)) is not { } data) return false;
        if (!_modelingPanels.TryGetValue(mesh.Id, out var panel))
            _modelingPanels[mesh.Id] = panel = new ModelingPanel { Grid = data.GridStep, Version = data.Version };

        var camera = TryRes<Mode3DCamera>(out var mode3D) ? mode3D.Camera : null;
        var gui = ImGui.GetCurrentContext() != IntPtr.Zero;
        if (camera is { } view) ModelingPointer(panel, data, position, view, gui && ImGui.GetIO().WantCaptureMouse);
        if (gui) ModelingPanelWindow(panel, data, mesh);

        var changed = data.Version != panel.Version;
        panel.Version = data.Version;
        return changed;
    }

    /// <summary>The face of an edit mesh a ray meets first, from either side, the mesh placed at <paramref name="position"/>, with where it meets it.</summary>
    /// <param name="ray">The ray, as <see cref="GetScreenToWorldRay"/> gives one from the pointer.</param>
    /// <param name="mesh">The mesh.</param>
    /// <param name="position">Where the mesh's model is drawn, unturned and unscaled.</param>
    /// <param name="face">The face's index, or -1 where the ray meets none.</param>
    /// <remarks>
    /// Each face is tested whole, cut into triangles round its own outline, so a concave face is met
    /// only inside it, and the face is the one a modeling tool selects under the pointer. The normal
    /// is the face's own.
    /// </remarks>
    public static RayCollision GetRayCollisionEditMesh(Ray ray, EditMesh mesh, Vector3 position, out int face)
    {
        face = -1;
        if (Edited(mesh, nameof(GetRayCollisionEditMesh)) is not { } data || ray.Direction == Vector3.Zero) return default;
        var direction = Vector3.Normalize(ray.Direction);
        if (data.Cast(ray.Position - position, direction) is not { } hit) return default;
        face = hit.Face;
        return new RayCollision(true, hit.Distance, ray.Position + direction * hit.Distance, data.Normal(hit.Face));
    }

    // Each modeling window's fields and what its pointer holds, by the edit mesh's id.
    private static readonly Dictionary<int, ModelingPanel> _modelingPanels = [];

    private static readonly Color ModelingSelected = new(255, 160, 40, 255), ModelingHovered = new(255, 255, 255, 255), ModelingDragged = new(90, 200, 255, 255);

    private sealed class ModelingPanel
    {
        public string Line = "";
        public string? Said;
        public Vector3 Size = new(2, 2, 2), At, MoveBy = new(0, 1, 0);
        public bool Centered, DragMoves;
        public float Radius = 1, Height = 2, Thickness = 0.2f, Rise = 0.2f, Run = 0.3f, Width = 1.2f;
        public int Sides = 8, Steps = 8, Segments = 1, Cuts = 1, Axis = 1;
        public float Distance = 1, Inset = 0.25f, Bevel = 0.1f, Degrees = 90, Scale = 2, Merge = 0.001f;
        public float OpeningWidth = 1.2f, OpeningHeight = 1, Along = 1, Sill = 1, DoorHeight = 2.1f;
        public string Slot = "wall", Rename = "", Texture = "";
        public Vector4 Color = Vector4.One;
        public float Metallic, Roughness = 0.85f, UvDensity = 256;
        public Vector2 UvMove, UvScale = new(2, 2);
        public float UvDegrees = 90;
        public bool UvEach;
        public int UvAxis = 1, UvWidth = 512;
        public float Grid = 1;
        public int Hovered = -1, Version;
        public ModelingDrag? Drag;
    }

    // A press on a face held, the point it was pressed at and the face's normal, both in the mesh's
    // own space, the face selected alone where it is let go without moving, and the whole steps of
    // the grid the pointer has gone along the normal.
    private sealed class ModelingDrag(Vector3 anchor, Vector3 normal, Vector2 from, int selectOnRelease)
    {
        public readonly Vector3 Anchor = anchor, Normal = normal;
        public readonly Vector2 From = from;
        public readonly int SelectOnRelease = selectOnRelease;
        public bool Moved;
        public int Steps;
    }

    private static void ModelingPointer(ModelingPanel panel, EditMeshData data, Vector3 position, Camera3D camera, bool overGui)
    {
        var mouse = GetMousePosition();
        var ray = GetScreenToWorldRay(mouse, camera);
        var local = new Ray(ray.Position - position, ray.Direction);
        panel.Hovered = overGui ? -1 : data.Cast(local.Position, local.Direction) is { } hit ? hit.Face : -1;

        if (panel.Drag is { } drag)
        {
            if (IsMouseButtonDown(MouseButton.Left))
            {
                drag.Moved |= Vector2.Distance(mouse, drag.From) > 3;
                drag.Steps = drag.Moved ? ModelingSteps(drag.Anchor, drag.Normal, local, panel.Grid) : 0;
            }
            else
            {
                if (drag.Moved && drag.Steps != 0)
                {
                    var distance = drag.Steps * panel.Grid;
                    ModelingRun(panel, data, panel.DragMoves ? $"move {EditMeshData.V(drag.Normal * distance)}" : $"extrude {EditMeshData.F(distance)}");
                }
                else if (!drag.Moved && drag.SelectOnRelease >= 0) ModelingRun(panel, data, $"select face {drag.SelectOnRelease}");
                panel.Drag = null;
            }
        }
        else if (!overGui && panel.Hovered >= 0 && IsMouseButtonPressed(MouseButton.Left))
        {
            var face = panel.Hovered;
            var adding = IsKeyDown(Key.LeftShift) || IsKeyDown(Key.RightShift);
            var removing = IsKeyDown(Key.LeftControl) || IsKeyDown(Key.RightControl);
            var selected = data.IsFaceSelected(face);
            if (removing) ModelingRun(panel, data, $"select remove face {face}");
            else if (adding) ModelingRun(panel, data, $"select add face {face}");
            else if (!selected) ModelingRun(panel, data, $"select face {face}");

            // A press on one face of a selection of several keeps the selection, to drag it whole,
            // and selects the face alone where the button is let go without moving.
            if (!removing && data.Cast(local.Position, local.Direction) is { } pressed)
                panel.Drag = new ModelingDrag(local.Position + Vector3.Normalize(local.Direction) * pressed.Distance, data.Normal(face), mouse,
                    !adding && selected && data.SelectedFaceCount > 1 ? face : -1);
        }

        ModelingOutlines(panel, data, position, camera);
        if (panel.Drag is { Moved: true } held && !overGui && ImGui.GetCurrentContext() != IntPtr.Zero)
            ImGui.SetTooltip($"{(panel.DragMoves ? "move" : "extrude")} {EditMeshData.F(held.Steps * panel.Grid)} m, {held.Steps} steps of {EditMeshData.F(panel.Grid)}");
    }

    /// <summary>
    /// The whole steps of the grid along a face's normal from where it was pressed to the point of
    /// the normal's line nearest the pointer's ray, or none where the ray runs along the normal.
    /// </summary>
    internal static int ModelingSteps(Vector3 anchor, Vector3 normal, Ray ray, float grid)
    {
        var direction = Vector3.Normalize(ray.Direction);
        var along = Vector3.Dot(normal, direction);
        var across = 1 - along * along;
        if (across < 1e-4f || grid <= 0) return 0;
        var offset = anchor - ray.Position;
        var t = (along * Vector3.Dot(direction, offset) - Vector3.Dot(normal, offset)) / across;
        return (int)MathF.Round(t / grid);
    }

    // The selection outlined, moved along the drag where one is held, and the face under the
    // pointer, each lifted a millimeter off its face so it is drawn over the model, through the
    // camera the pointer's ray was cast through and back to what was drawn through before.
    private static void ModelingOutlines(ModelingPanel panel, EditMeshData data, Vector3 position, Camera3D camera)
    {
        var (view, projection, depth) = (_rlView, _rlProjection, _rl3D);
        var (width, height) = DrawingSize();
        SetRlCamera(camera.View, camera.ProjectionMatrix((float)width / Math.Max(1, height)), depthTest: true);

        var shift = panel.Drag is { Moved: true } drag ? drag.Normal * (drag.Steps * panel.Grid) : Vector3.Zero;
        for (int face = 0; face < data.FaceCount; face++)
            if (data.IsFaceSelected(face)) ModelingOutline(data, face, position + shift, shift == Vector3.Zero ? ModelingSelected : ModelingDragged);
        if (panel.Hovered >= 0 && panel.Drag is null) ModelingOutline(data, panel.Hovered, position, ModelingHovered);

        SetRlCamera(view, projection, depth);
    }

    private static void ModelingOutline(EditMeshData data, int face, Vector3 position, Color color)
    {
        var corners = data.Corners(face);
        var lift = position + data.Normal(face) * 0.001f;
        for (int i = 0; i < corners.Length; i++)
            DrawLine3D(data.Position(corners[i]) + lift, data.Position(corners[(i + 1) % corners.Length]) + lift, color);
    }

    private static void ModelingRun(ModelingPanel panel, EditMeshData data, string line)
    {
        try
        {
            data.Run(line);
            panel.Said = null;
        }
        catch (Exception ex) when (ex is FormatException or ArgumentException or InvalidOperationException or IOException or UnauthorizedAccessException)
        {
            panel.Said = $"{line} was refused. {ex.Message}";
        }
    }

    private static void ModelingPanelWindow(ModelingPanel panel, EditMeshData data, EditMesh mesh)
    {
        // At the window's right edge the first time, leaving its middle, where the model is, to the pointer.
        ImGui.SetNextWindowPos(new Vector2(Math.Max(10, GetScreenWidth() - 370), 10), ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowSize(new Vector2(360, 560), ImGuiCond.FirstUseEver);
        if (!ImGui.Begin($"Modeling###modeling{mesh.Id}"))
        {
            ImGui.End();
            return;
        }

        ImGui.TextUnformatted($"{data.VertexCount} vertices, {data.EdgeCount} edges, {data.FaceCount} faces, {(data.OpenEdges == 0 && data.FaceCount > 0 ? "closed" : $"{data.OpenEdges} open edges")}");
        ImGui.TextUnformatted($"{data.SelectedFaceCount} faces, {data.SelectedEdgeCount} edges and {data.SelectedVertexCount} vertices selected");

        // The recipe, the lines taken back after it grayed, the next to be done again first.
        if (ImGui.BeginChild("recipe", new Vector2(0, 150), ImGuiChildFlags.Borders))
        {
            for (int i = 0; i < data.Recipe.Count; i++) ImGui.TextUnformatted($"{i + 1,3}  {data.Recipe[i]}");
            foreach (var undone in data.Undone) ImGui.TextDisabled($"     {undone}");
        }
        ImGui.EndChild();
        if (ImGui.Button("Undo")) data.Undo();
        ImGui.SameLine();
        if (ImGui.Button("Redo")) data.Redo();
        ImGui.SameLine();
        ImGui.SetNextItemWidth(-60);
        var entered = ImGui.InputText("##line", ref panel.Line, 512, ImGuiInputTextFlags.EnterReturnsTrue);
        ImGui.SameLine();
        if ((ImGui.Button("Run") || entered) && panel.Line.Trim().Length > 0) ModelingRun(panel, data, panel.Line);
        if (panel.Said is { } said)
        {
            ImGui.PushTextWrapPos(0);
            ImGui.TextColored(new Vector4(1, 0.45f, 0.35f, 1), said);
            ImGui.PopTextWrapPos();
        }

        ImGui.Separator();
        ImGui.TextUnformatted("A drag along a face's normal");
        ImGui.SameLine();
        if (ImGui.RadioButton("extrudes", !panel.DragMoves)) panel.DragMoves = false;
        ImGui.SameLine();
        if (ImGui.RadioButton("moves", panel.DragMoves)) panel.DragMoves = true;

        if (ImGui.CollapsingHeader("Select", ImGuiTreeNodeFlags.DefaultOpen))
        {
            foreach (var what in new[] { "all", "none", "invert" })
            {
                if (ImGui.Button(what)) ModelingRun(panel, data, $"select {what}");
                ImGui.SameLine();
            }
            ImGui.NewLine();
            foreach (var way in new[] { "+x", "-x", "+y", "-y", "+z", "-z" })
            {
                if (ImGui.Button(way)) ModelingRun(panel, data, $"select {way}");
                ImGui.SameLine();
            }
            ImGui.NewLine();
        }

        if (ImGui.CollapsingHeader("Add"))
        {
            var place = EditMeshData.Placement(panel.At, panel.Centered);
            ImGui.InputFloat3("at", ref panel.At);
            ImGui.SameLine();
            ImGui.Checkbox("center", ref panel.Centered);
            ModelingButton(panel, data, "Box", $"box {EditMeshData.V(panel.Size)}{place}", () => ImGui.InputFloat3("size##box", ref panel.Size));
            ModelingButton(panel, data, "Room", $"room {EditMeshData.V(panel.Size)} {EditMeshData.F(panel.Thickness)}{place}",
                () => ImGui.InputFloat("thickness##room", ref panel.Thickness));
            ModelingButton(panel, data, "Cylinder", $"cylinder {EditMeshData.F(panel.Radius)} {EditMeshData.F(panel.Height)} {panel.Sides}{place}", () =>
            {
                ImGui.InputFloat("radius##cylinder", ref panel.Radius);
                ImGui.InputFloat("height##cylinder", ref panel.Height);
                ImGui.InputInt("sides##cylinder", ref panel.Sides);
            });
            ModelingButton(panel, data, "Stairs", $"stairs {panel.Steps} {EditMeshData.F(panel.Rise)} {EditMeshData.F(panel.Run)} {EditMeshData.F(panel.Width)}{place}", () =>
            {
                ImGui.InputInt("steps##stairs", ref panel.Steps);
                ImGui.InputFloat("rise##stairs", ref panel.Rise);
                ImGui.InputFloat("run##stairs", ref panel.Run);
                ImGui.InputFloat("width##stairs", ref panel.Width);
            });
        }

        if (ImGui.CollapsingHeader("Shape", ImGuiTreeNodeFlags.DefaultOpen))
        {
            ModelingButton(panel, data, "Extrude", $"extrude {EditMeshData.F(panel.Distance)}", () => ImGui.InputFloat("distance##extrude", ref panel.Distance));
            ModelingButton(panel, data, "Inset", $"inset {EditMeshData.F(panel.Inset)}", () => ImGui.InputFloat("distance##inset", ref panel.Inset));
            ModelingButton(panel, data, "Bevel", $"bevel {EditMeshData.F(panel.Bevel)} {panel.Segments}", () =>
            {
                ImGui.InputFloat("width##bevel", ref panel.Bevel);
                ImGui.InputInt("segments##bevel", ref panel.Segments);
            });
            ModelingButton(panel, data, "Subdivide", $"subdivide {panel.Cuts}", () => ImGui.InputInt("cuts##subdivide", ref panel.Cuts));
            ModelingButton(panel, data, "Move", $"move {EditMeshData.V(panel.MoveBy)}", () => ImGui.InputFloat3("by##move", ref panel.MoveBy));
            ModelingButton(panel, data, "Rotate", $"rotate {"xyz"[Math.Clamp(panel.Axis, 0, 2)]} {EditMeshData.F(panel.Degrees)}", () =>
            {
                ImGui.Combo("axis##rotate", ref panel.Axis, "x\0y\0z\0");
                ImGui.InputFloat("degrees##rotate", ref panel.Degrees);
            });
            ModelingButton(panel, data, "Scale", $"scale {EditMeshData.F(panel.Scale)}", () => ImGui.InputFloat("factor##scale", ref panel.Scale));
            ModelingButton(panel, data, "Merge", $"merge {EditMeshData.F(panel.Merge)}", () => ImGui.InputFloat("distance##merge", ref panel.Merge));
            foreach (var word in new[] { "delete", "dissolve", "flip", "triangulate", "duplicate", "fill", "bridge" })
            {
                if (ImGui.Button(word)) ModelingRun(panel, data, word);
                ImGui.SameLine();
            }
            ImGui.NewLine();
        }

        if (ImGui.CollapsingHeader("Openings"))
        {
            ImGui.InputFloat("width##opening", ref panel.OpeningWidth);
            ImGui.InputFloat("along##opening", ref panel.Along);
            ModelingButton(panel, data, "Window", $"cut window {EditMeshData.F(panel.OpeningWidth)} {EditMeshData.F(panel.OpeningHeight)} {EditMeshData.F(panel.Along)} {EditMeshData.F(panel.Sill)}", () =>
            {
                ImGui.InputFloat("height##window", ref panel.OpeningHeight);
                ImGui.InputFloat("sill##window", ref panel.Sill);
            });
            ModelingButton(panel, data, "Door", $"cut door {EditMeshData.F(panel.OpeningWidth)} {EditMeshData.F(panel.DoorHeight)} {EditMeshData.F(panel.Along)}",
                () => ImGui.InputFloat("height##door", ref panel.DoorHeight));
        }

        if (ImGui.CollapsingHeader("Slot and grid", ImGuiTreeNodeFlags.DefaultOpen))
        {
            ModelingButton(panel, data, "Material", $"material {panel.Slot.Trim()}", () => ImGui.InputText("slot", ref panel.Slot, 64));
            ModelingButton(panel, data, "Grid", $"grid {EditMeshData.F(panel.Grid)}", () => ImGui.InputFloat("step", ref panel.Grid));
            if (ImGui.Button("Snap")) ModelingRun(panel, data, "snap");
        }

        if (ImGui.CollapsingHeader("Slot's look")) ModelingLook(panel, data);
        if (ImGui.CollapsingHeader("Texture coordinates")) ModelingUvs(panel, data);
        ImGui.End();
    }

    // The slot named in the window's field, added, renamed, taken away and given a look.
    private static void ModelingLook(ModelingPanel panel, EditMeshData data)
    {
        var slot = panel.Slot.Trim();
        var index = data.Slots.ToList().IndexOf(slot);
        ImGui.TextUnformatted(index < 0 ? $"{slot} is not a slot yet, and a look given makes it"
            : data.LookAt(index).IsPrototype ? $"{slot} is drawn with its prototype" : $"{slot}, {data.LookAt(index).Words()}");
        if (ImGui.Button("Add")) ModelingRun(panel, data, $"slot add {slot}");
        ImGui.SameLine();
        if (ImGui.Button("Remove")) ModelingRun(panel, data, $"slot remove {slot}");
        ModelingButton(panel, data, "Rename", $"slot rename {slot} {panel.Rename.Trim()}", () => ImGui.InputText("to##rename", ref panel.Rename, 64));
        var c = panel.Color;
        ModelingButton(panel, data, "Color", $"slot color {slot} {EditMeshData.F(c.X)} {EditMeshData.F(c.Y)} {EditMeshData.F(c.Z)} {EditMeshData.F(c.W)}",
            () => ImGui.ColorEdit4("##color", ref panel.Color));
        var texture = panel.Texture.Trim();
        ModelingButton(panel, data, "Texture", $"slot texture {slot} {(texture.Length == 0 ? "none" : texture)}", () => ImGui.InputText("path##texture", ref panel.Texture, 256));
        ModelingButton(panel, data, "Metallic", $"slot metallic {slot} {EditMeshData.F(panel.Metallic)}", () => ImGui.SliderFloat("##metallic", ref panel.Metallic, 0, 1));
        ModelingButton(panel, data, "Roughness", $"slot roughness {slot} {EditMeshData.F(panel.Roughness)}", () => ImGui.SliderFloat("##roughness", ref panel.Roughness, 0, 1));
    }

    // The operations on the selection's texture coordinates, and the coordinates drawn over the
    // texture of the first selected face's slot, repeated over the whole units they span.
    private static void ModelingUvs(ModelingPanel panel, EditMeshData data)
    {
        foreach (var way in new[] { "+x", "-x", "+y", "-y", "+z", "-z" })
        {
            if (ImGui.Button($"{way}##planar")) ModelingRun(panel, data, $"uv planar {way}");
            ImGui.SameLine();
        }
        ImGui.TextUnformatted("planar");
        if (ImGui.Button("box")) ModelingRun(panel, data, "uv box");
        ImGui.SameLine();
        if (ImGui.Button("sphere")) ModelingRun(panel, data, "uv sphere");
        ImGui.SameLine();
        if (ImGui.Button("reset")) ModelingRun(panel, data, "uv reset");
        ModelingButton(panel, data, "Cylinder", $"uv cylinder {"xyz"[Math.Clamp(panel.UvAxis, 0, 2)]}", () => ImGui.Combo("axis##cylinder", ref panel.UvAxis, "x\0y\0z\0"));
        ModelingButton(panel, data, "Move", $"uv move {EditMeshData.F(panel.UvMove.X)} {EditMeshData.F(panel.UvMove.Y)}", () => ImGui.InputFloat2("by##uvmove", ref panel.UvMove));
        var each = panel.UvEach ? " each" : "";
        ModelingButton(panel, data, "Scale", $"uv scale {EditMeshData.F(panel.UvScale.X)} {EditMeshData.F(panel.UvScale.Y)}{each}", () => ImGui.InputFloat2("by##uvscale", ref panel.UvScale));
        ModelingButton(panel, data, "Rotate", $"uv rotate {EditMeshData.F(panel.UvDegrees)}{each}", () => ImGui.InputFloat("degrees##uvrotate", ref panel.UvDegrees));
        ImGui.Checkbox("each face about its own middle", ref panel.UvEach);
        var known = data.SelectedTextureWidth();
        ModelingButton(panel, data, "Density", $"uv density {EditMeshData.F(panel.UvDensity)} {known ?? panel.UvWidth}", () =>
        {
            ImGui.InputFloat("pixels a meter", ref panel.UvDensity);
            if (known is { } width) ImGui.TextUnformatted($"of the slot's texture, {width} wide");
            else ImGui.InputInt("texture width", ref panel.UvWidth);
        });

        var faces = Enumerable.Range(0, data.FaceCount).Where(data.IsFaceSelected).ToArray();
        if (faces.Length == 0)
        {
            ImGui.TextDisabled("Select faces to see their coordinates.");
            return;
        }

        var loops = faces.Select(data.UvsOf).ToArray();
        var (low, high) = (new Vector2(float.MaxValue), new Vector2(float.MinValue));
        foreach (var uv in loops.SelectMany(loop => loop)) (low, high) = (Vector2.Min(low, uv), Vector2.Max(high, uv));
        ImGui.TextUnformatted($"u {EditMeshData.F(MathF.Round(low.X, 3))} to {EditMeshData.F(MathF.Round(high.X, 3))}, v {EditMeshData.F(MathF.Round(low.Y, 3))} to {EditMeshData.F(MathF.Round(high.Y, 3))}");

        // The whole units the coordinates span, at most eight across, fitted square into the canvas.
        var (from, to) = (new Vector2(MathF.Floor(low.X), MathF.Floor(low.Y)), new Vector2(MathF.Ceiling(high.X), MathF.Ceiling(high.Y)));
        to = Vector2.Max(to, from + Vector2.One);
        to = Vector2.Min(to, from + new Vector2(8));
        var span = MathF.Max(to.X - from.X, to.Y - from.Y);
        var side = MathF.Min(ImGui.GetContentRegionAvail().X, 320);
        var origin = ImGui.GetCursorScreenPos();
        var scale = side / span;
        Vector2 At(Vector2 uv) => origin + (uv - from) * scale;

        var draw = ImGui.GetWindowDrawList();
        var slot = data.SlotIndexOf(faces[0]);
        var look = data.LookAt(slot);
        var picture = look.Texture is { } path ? SlotTexture(path) : default;
        var id = GetImGuiTextureId(picture.IsValid ? picture : PrototypeGrid());
        for (var u = from.X; u < from.X + span; u++)
            for (var v = from.Y; v < from.Y + span; v++)
                draw.AddImage(id, At(new Vector2(u, v)), At(new Vector2(u + 1, v + 1)));
        var outline = ImGui.GetColorU32(new Vector4(1, 0.63f, 0.16f, 1));
        foreach (var loop in loops)
            for (int i = 0; i < loop.Length; i++) draw.AddLine(At(loop[i]), At(loop[(i + 1) % loop.Length]), outline, 1.5f);
        ImGui.Dummy(new Vector2(side, side));
    }

    // A button that runs an operation's line, beside the fields of its measures, which are drawn
    // before the line is read on the next frame's press.
    private static void ModelingButton(ModelingPanel panel, EditMeshData data, string label, string line, Action fields)
    {
        if (ImGui.Button(label)) ModelingRun(panel, data, line);
        ImGui.SameLine();
        ImGui.BeginGroup();
        ImGui.PushItemWidth(160);
        fields();
        ImGui.PopItemWidth();
        ImGui.EndGroup();
    }

    // The modeling windows' fields, forgotten with the window, since the meshes' ids start again
    // with the next app's.
    private static void ForgetModelingWindows() => _modelingPanels.Clear();
}
