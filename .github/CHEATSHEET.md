# Cheatsheet

Every public function of the flat API, one line each, grouped as raylib groups its own. A program
imports them with `using static Engine.Engine3D;`. [DESIGN.md](DESIGN.md) has the rules they follow.

## Window and timing

```csharp
void InitWindow(int width, int height, string title);    // Open a window and build the app behind it
void CloseWindow();                                      // Run Cleanup, close the window and free what the app holds
bool WindowShouldClose();                                // Process events; true once the window or the exit key asks to close
bool IsWindowReady();                                    // Whether a window is open
void SetExitKey(Key key);                                // Key that closes the window (Escape by default, Key.Unknown for none)
void SetWindowTitle(string title);                       // Set the window's title
int GetScreenWidth();                                    // Window width
int GetScreenHeight();                                   // Window height
App GetApp();                                            // The app InitWindow built, for plugins, systems and resources

void SetTargetFPS(int fps);                              // Cap the frame rate (0 for no cap)
float GetFrameTime();                                    // Seconds the last frame took
double GetTime();                                        // Seconds since the first frame
int GetFPS();                                            // Frames per second, smoothed
```

## Frame and cameras

```csharp
void BeginDrawing();                                     // Start a frame; Draw and ImGui calls work until EndDrawing
void EndDrawing();                                       // Render and present the frame, then wait for the target frame rate
void ClearBackground(Color color);                       // Color the frame is cleared to
void BeginMode3D(Camera3D camera);                       // Draw the following shapes through a camera, depth tested
void EndMode3D();                                        // Return to screen space, in pixels from the top left
void UpdateCamera(ref Camera3D camera, CameraMode mode); // Move a camera from input (Free or Orbital)
```

`Camera3D` holds `Position`, `Target`, `Up`, `FovY` (degrees) and `Projection` (`Perspective` or
`Orthographic`). `CameraMode.Free` moves with W, A, S, D, Q and E, turns while the right mouse
button is dragged, and goes faster with Shift.

## Input

```csharp
bool IsKeyPressed(Key key);                              // Key went down this frame
bool IsKeyDown(Key key);                                 // Key is held
bool IsKeyReleased(Key key);                             // Key came up this frame
bool IsKeyUp(Key key);                                   // Key is not held

bool IsMouseButtonPressed(MouseButton button);           // Button went down this frame
bool IsMouseButtonDown(MouseButton button);              // Button is held
bool IsMouseButtonReleased(MouseButton button);          // Button came up this frame
bool IsMouseButtonUp(MouseButton button);                // Button is not held
Vector2 GetMousePosition();                              // Pointer position in the window
int GetMouseX();                                         // Pointer x
int GetMouseY();                                         // Pointer y
Vector2 GetMouseDelta();                                 // How far the pointer moved this frame
float GetMouseWheelMove();                               // How far the wheel turned this frame
```

## 2D shapes

```csharp
void DrawLine(int startX, int startY, int endX, int endY, Color color);           // Line
void DrawLineV(Vector2 start, Vector2 end, Color color);                           // Line
void DrawTriangle(Vector2 v1, Vector2 v2, Vector2 v3, Color color);                // Filled triangle
void DrawRectangle(int x, int y, int width, int height, Color color);             // Filled rectangle
void DrawRectangleV(Vector2 position, Vector2 size, Color color);                  // Filled rectangle
void DrawRectangleLines(int x, int y, int width, int height, Color color);        // Rectangle outline
void DrawCircle(int centerX, int centerY, float radius, Color color);              // Filled circle
void DrawCircleV(Vector2 center, float radius, Color color);                       // Filled circle
void DrawCircleLines(int centerX, int centerY, float radius, Color color);         // Circle outline
```

## 3D shapes

```csharp
void DrawLine3D(Vector3 start, Vector3 end, Color color);                                       // Line
void DrawTriangle3D(Vector3 v1, Vector3 v2, Vector3 v3, Color color);                           // Filled triangle
void DrawCube(Vector3 position, float width, float height, float length, Color color);         // Box
void DrawCubeV(Vector3 position, Vector3 size, Color color);                                    // Box
void DrawCubeWires(Vector3 position, float width, float height, float length, Color color);    // Box edges
void DrawCubeWiresV(Vector3 position, Vector3 size, Color color);                               // Box edges
void DrawSphere(Vector3 center, float radius, Color color);                                     // Sphere
void DrawSphereEx(Vector3 center, float radius, int rings, int slices, Color color);            // Sphere with a chosen detail
void DrawSphereWires(Vector3 center, float radius, int rings, int slices, Color color);         // Sphere as lines
void DrawPlane(Vector3 center, Vector2 size, Color color);                                      // Rectangle on the XZ plane
void DrawGrid(int slices, float spacing);                                                       // Grid on the XZ plane
```

## Text

```csharp
void DrawText(string text, int x, int y, int fontSize, Color color);  // Text, top left at (x, y), always on top
int MeasureText(string text, int fontSize);                           // Width DrawText would draw text at
void DrawFPS(int x, int y);                                           // The frame rate
```

## Colors

`Color(byte r, byte g, byte b, byte a = 255)`, with `Fade(alpha)` and `ToVector4()`, and raylib's
named colors: `LightGray`, `Gray`, `DarkGray`, `Yellow`, `Gold`, `Orange`, `Pink`, `Red`, `Maroon`,
`Green`, `Lime`, `DarkGreen`, `SkyBlue`, `Blue`, `DarkBlue`, `Purple`, `Violet`, `DarkPurple`,
`Beige`, `Brown`, `DarkBrown`, `White`, `Black`, `Blank`, `Magenta`, `RayWhite`.

## ImGui

Every `ImGui.*` call works between `BeginDrawing` and `EndDrawing`, and is not wrapped here. ImGui
is drawn over everything else. F2 shows the engine's performance window.
