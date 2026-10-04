# Drawing in 2D

2D drawing is shapes, text and textures placed in pixels from the window's top left corner, or in
a world of its own seen through a 2D camera. Each call draws for the current frame only, so a
program draws its whole picture every frame between `BeginDrawing` and `EndDrawing`.

## Shapes

Shapes take their position in pixels and a `Color`. The `shapes_basic_2d` example draws the common
ones:

```csharp
DrawText("some basic shapes available", 20, 20, 20, Color.DarkGray);

DrawCircle(GetScreenWidth() / 5, 120, 35, Color.DarkBlue);
DrawCircleLines(GetScreenWidth() / 5, 220, 80, Color.DarkBlue);

DrawRectangle(GetScreenWidth() / 4 * 2 - 60, 100, 120, 60, Color.Red);
DrawRectangleLines(GetScreenWidth() / 4 * 2 - 40, 320, 80, 60, Color.Orange);

DrawTriangle(new Vector2(GetScreenWidth() / 4f * 3, 80), new Vector2(GetScreenWidth() / 4f * 3 - 60, 150),
    new Vector2(GetScreenWidth() / 4f * 3 + 60, 150), Color.Violet);

DrawLine(18, 42, GetScreenWidth() - 18, 42, Color.Black);
```

Most shapes come as a filled call and a `Lines` call for the outline. Past those, the flat API has
raylib's whole set: rectangles that are rounded, turned (`DrawRectanglePro`) or blended between
corner colors, polygons, rings and arcs, ellipses, lines of a width (`DrawLineEx`) and strips and
fans of triangles. Angles are in degrees, clockwise on the screen from the right.

Shapes are drawn in the order they are called, each over the ones before, so a background is drawn
first and a user interface last.

## Colors

`Color` holds red, green, blue and alpha as bytes, as raylib's does, and has raylib's named colors
(`Color.RayWhite`, `Color.Maroon` and the rest).

```csharp
var sky = new Color(102, 191, 255);          // opaque
var shade = Color.Black.Fade(0.5f);          // half transparent
var hue = ColorFromHSV(200, 0.7f, 0.9f);     // from hue, saturation and value
```

A color with alpha below 255 is laid over what is behind it. `BeginBlendMode(BlendMode.Additive)`
adds colors instead, as light does, until `EndBlendMode`.

## Text

`DrawText` draws in the default font at a size in pixels, and `MeasureText` says how wide a string
will be, so text can be centered:

```csharp
var message = "Game over";
var width = MeasureText(message, 40);
DrawText(message, (GetScreenWidth() - width) / 2, 200, 40, Color.Maroon);
```

The default font is baked at the size it is drawn at, so it stays crisp. Fonts loaded from files,
characters past Latin-1 and text drawn turned are in the cheatsheet's [Text and fonts](CHEATSHEET.md#text-and-fonts) section.

## Splines

A spline is a curve through or near a list of points, drawn a number of pixels thick with its
corners joined. The `core_2d_camera` example draws one through the tops of its buildings:

```csharp
// A line through the rooftops, which a thick spline draws.
var roofs = buildings.Select(b => new Vector2(b.Rect.X + b.Rect.Width / 2, b.Rect.Y)).ToArray();
// ...
DrawSplineCatmullRom(roofs, 3, Color.SkyBlue);
```

`DrawSplineLinear` joins the points with straight lines, `DrawSplineCatmullRom` passes through
them, `DrawSplineBasis` passes near them, and the two Bezier forms take control points between
their ends. `GetSplinePointCatmullRom` and the others give the point a fraction of the way along a
segment, for something that moves along the curve.

## A 2D camera

A `Camera2D` sees a world of its own, larger than the window, which `BeginMode2D` draws through
until `EndMode2D`. It holds where its target appears on the screen (`Offset`), the point of the
world it looks at (`Target`), a turn in degrees and a zoom. From the `core_2d_camera` example:

```csharp
var camera = new Camera2D(new Vector2(400, 225), new Vector2(player.X + 20, player.Y + 20));
// ...
camera.Target = new Vector2(player.X + 20, player.Y + 20);
if (IsKeyDown(Key.A)) camera.Rotation--;
if (IsKeyDown(Key.S)) camera.Rotation++;
camera.Rotation = Math.Clamp(camera.Rotation, -40, 40);
camera.Zoom = Math.Clamp(MathF.Exp(MathF.Log(camera.Zoom) + GetMouseWheelMove() * 0.1f), 0.1f, 3f);

BeginDrawing();
ClearBackground(Color.RayWhite);

BeginMode2D(camera);
DrawRectangle(-6000, 320, 13000, 8000, Color.DarkGray);
foreach (var (rect, color) in buildings) DrawRectangleRec(rect, color);
DrawRectangleRec(player, Color.Red);
EndMode2D();

DrawText("SCREEN AREA", 640, 10, 20, Color.Red);
```

Everything between `BeginMode2D` and `EndMode2D` is in world units, and what comes after is in
screen pixels again, which suits a user interface over a scrolling world. `GetScreenToWorld2D`
turns the mouse's position into the world point under it, and `GetWorldToScreen2D` the other way.

## Collision

The 2D collision functions answer whether shapes overlap, as a game asks every frame:

```csharp
if (CheckCollisionPointRec(GetMousePosition(), button)) hovered = true;
if (CheckCollisionCircles(ball, 10, player, 20)) score++;
var overlap = GetCollisionRec(a, b);         // the rectangle where two overlap
```

There are tests for points, lines, circles, rectangles, triangles and polygons, listed in the
cheatsheet's [Collision](CHEATSHEET.md#collision) section.

## Keeping drawing to a rectangle

`BeginScissorMode(x, y, width, height)` keeps what is drawn after it inside a rectangle of pixels
until `EndScissorMode`, as a scrolling list inside a panel needs.

## See also

- Examples: [`shapes_basic_2d`](../3DEngine.Examples/Shapes/ShapesBasic2D.cs),
  [`core_2d_camera`](../3DEngine.Examples/Core/Core2DCamera.cs)
- The cheatsheet's [2D shapes](CHEATSHEET.md#2d-shapes), [Collision](CHEATSHEET.md#collision) and
  [Colors](CHEATSHEET.md#colors)
- Previous: [The window and the frame](window-and-frame.md)
- Next: [Drawing in 3D and cameras](drawing-3d-and-cameras.md)
