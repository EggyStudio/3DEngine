# Cheatsheet

Every public function of the flat API, one line each, grouped as raylib groups its own. A program
imports them with `using static Engine.Engine3D;`. [DESIGN.md](.github/DESIGN.md) has the rules they follow.

## Window and timing

```csharp
void SetConfigFlags(ConfigFlags flags);                  // Ask the next window for vsync, fullscreen, no border, topmost, high density, transparency, ...
void SetConfigSamples(int samples);                      // Samples a pixel of the next window, 1 for none, 4 unless asked
void SetWindowState(ConfigFlags flags);                  // Turn flags on for the open window (fullscreen, resizable, topmost, vsync, ...)
void ClearWindowState(ConfigFlags flags);                // Turn them off
bool IsWindowState(ConfigFlags flags);                   // Whether the window has all of them
void InitWindow(int width, int height, string title);    // Open a window and build the app behind it
void CloseWindow();                                      // Run Cleanup, close the window and free what the app holds
bool WindowShouldClose();                                // Process events; true once the window or the exit key asks to close
bool IsWindowReady();                                    // Whether a window is open
void SetExitKey(Key key);                                // Key that closes the window (Escape by default, Key.Unknown for none)
void EnableEventWaiting();                               // WindowShouldClose waits for input, up to a tenth of a second, for tools
void DisableEventWaiting();                              // Back to returning at once
void SetWindowTitle(string title);                       // Set the window's title
int GetScreenWidth();                                    // Window width
int GetScreenHeight();                                   // Window height
bool IsWindowResized();                                  // Whether its size changed this frame
bool IsWindowFullscreen();                               // Whether it is fullscreen, by ToggleFullscreen rather than borderless
bool IsWindowMaximized();                                // Whether it is maximized
bool IsWindowMinimized();                                // Whether it is minimized
bool IsWindowFocused();                                  // Whether it has the keyboard focus
bool IsWindowHidden();                                   // Whether it is hidden (or there is no window)
void ToggleFullscreen();                                 // Between fullscreen and a window
void MaximizeWindow();                                   // Maximize
void MinimizeWindow();                                   // Minimize
void RestoreWindow();                                    // Back from maximized or minimized
void SetWindowSize(int width, int height);               // Resize
void SetWindowMinSize(int width, int height);            // Smallest size a resize may reach
void SetWindowMaxSize(int width, int height);            // Largest size a resize may reach
void SetWindowPosition(int x, int y);                    // Move on the desktop
Vector2 GetWindowPosition();                             // Top left corner on the desktop
Vector2 GetWindowScaleDPI();                             // Pixels for each unit of its size, 2 on a doubled monitor
int GetRenderWidth();                                    // The width in pixels its content is drawn at
int GetRenderHeight();                                   // The height in pixels its content is drawn at
void SetWindowOpacity(float opacity);                    // How opaque it is, 0 to 1
void SetWindowFocused();                                 // Raise it and ask for the keyboard focus
void ToggleBorderlessWindowed();                         // Between a window and a borderless one covering the monitor
void SetWindowIcon(Image image);                         // The icon the desktop shows for it
void SetWindowIcons(Image[] images);                     // Several sizes of it, the desktop picking
nint GetWindowHandle();                                  // The window's SDL window, for a library of your own

int GetMonitorCount();                                   // Connected monitors
int GetCurrentMonitor();                                 // The monitor the window is on
int GetMonitorWidth(int monitor);                        // Its width in its current mode
int GetMonitorHeight(int monitor);                       // Its height in its current mode
int GetMonitorRefreshRate(int monitor);                  // Its refresh rate in hertz
Vector2 GetMonitorPosition(int monitor);                 // Its top left corner on the desktop
string GetMonitorName(int monitor);                      // Its name
MonitorMode[] GetMonitorModes(int monitor);              // The sizes and rates it can be set to in fullscreen
void SetWindowFullscreenMode(MonitorMode mode);          // Fullscreen at the closest mode, or the desktop's with default
void SetWindowMonitor(int monitor);                      // Move the window to a monitor, centered
void SetClipboardText(string text);                      // Put text on the clipboard
string GetClipboardText();                               // The text on the clipboard
Image GetClipboardImage();                               // An image on the clipboard, or an invalid one
App GetApp();                                            // The app InitWindow built, for plugins, systems and resources
void TakeScreenshot(string fileName);                    // Write the frame being drawn to a PNG once it is presented

void SetTargetFPS(int fps);                              // Cap the frame rate (0 for no cap)
float GetFrameTime();                                    // Seconds the last frame took
double GetTime();                                        // Seconds since the first frame
int GetFPS();                                            // Frames per second, smoothed
void SetProfileValue(string name, double value);         // A number of the program's own in the frame profile
double GetProfileAverage(string name);                   // A profiled average in milliseconds, as "work" or "gpu.models"
void DrawProfileWindow();                                // The frame profile in an ImGui window

void SetRandomSeed(uint seed);                           // Seed the generator, so a run's random values repeat
int GetRandomValue(int min, int max);                    // A whole number from min to max, both included
int[] LoadRandomSequence(int count, int min, int max);   // That many different values from the range, empty when it holds fewer
```

## Frame and cameras

```csharp
void BeginDrawing();                                     // Start a frame; Draw and ImGui calls work until EndDrawing
void EndDrawing();                                       // Render and present the frame, then wait for the target frame rate
void ClearBackground(Color color);                       // Color the frame is cleared to
void BeginMode3D(Camera3D camera);                       // Draw the following shapes through a camera, depth tested
void EndMode3D();                                        // Return to screen space, in pixels from the top left
void UpdateCamera(ref Camera3D camera, CameraMode mode); // Move a camera from the keys, mouse and pad, as raylib's does in that mode
void UpdateCameraPro(ref Camera3D camera, Vector3 movement, Vector3 rotation, float zoom); // Move and turn it by amounts of the program's own
Vector3 GetCameraForward(Camera3D camera);               // The way it looks, of length one
Vector3 GetCameraUp(Camera3D camera);                    // Its up, of length one
Vector3 GetCameraRight(Camera3D camera);                 // Its right, of length one
void CameraMoveForward(ref Camera3D camera, float distance, bool moveInWorldPlane); // Move it and its target the way it looks, level if asked
void CameraMoveUp(ref Camera3D camera, float distance);  // Move it and its target along its up
void CameraMoveRight(ref Camera3D camera, float distance, bool moveInWorldPlane); // Move it and its target to its right, level if asked
void CameraMoveToTarget(ref Camera3D camera, float delta); // Move it farther from its target, or nearer when negative
void CameraYaw(ref Camera3D camera, float angle, bool rotateAroundTarget); // Turn it left in radians, about itself or its target
void CameraPitch(ref Camera3D camera, float angle, bool lockView, bool rotateAroundTarget, bool rotateUp); // Tip it up in radians
void CameraRoll(ref Camera3D camera, float angle);       // Roll it about the way it looks, in radians
Matrix4x4 GetCameraViewMatrix(Camera3D camera);          // The world to the camera
Matrix4x4 GetCameraProjectionMatrix(Camera3D camera, float aspect); // Its projection for a picture of that width over height
void BeginMode2D(Camera2D camera);                       // Draw the following 2D calls in world units through a 2D camera
void EndMode2D();                                        // Return to screen pixels
Vector2 GetWorldToScreen2D(Vector2 position, Camera2D camera); // Where a world point appears on the screen
Vector2 GetScreenToWorld2D(Vector2 position, Camera2D camera); // The world point under a screen point
Matrix4x4 GetCameraMatrix2D(Camera2D camera);            // The camera's world to screen transform
Vector2 GetWorldToScreen(Vector3 position, Camera3D camera); // Where a world point appears in the window
Vector2 GetWorldToScreenEx(Vector3 position, Camera3D camera, int width, int height); // The same for a view of a given size
bool IsPointInFrontOfCamera(Vector3 position, Camera3D camera); // Whether the point is ahead of the camera, so it shows there
Matrix4x4 GetCameraMatrix(Camera3D camera);              // The camera's world to view transform

RenderTexture2D LoadRenderTexture(int width, int height); // An image drawing can be sent to, its depth in .Depth
void UnloadRenderTexture(RenderTexture2D target);        // Free it
bool IsRenderTextureValid(RenderTexture2D target);       // Whether it is loaded
void BeginTextureMode(RenderTexture2D target);           // Draw into the image until EndTextureMode
void EndTextureMode();                                   // Return to the window

void BeginBlendMode(BlendMode mode);                     // Lay what is drawn over by Alpha, Additive, Multiplied, AddColors, SubtractColors, AlphaPremultiply, Custom or CustomSeparate
void EndBlendMode();                                     // Back to Alpha
void BeginScissorMode(int x, int y, int width, int height); // Keep shapes, textures and text to a rectangle of pixels
void EndScissorMode();                                   // Draw over the whole window or target again
```

Inside texture mode, `ClearBackground` clears the target, 2D drawing is in its pixels and cameras
use its shape. `target.Texture` draws like any texture. A target is drawn before the window in a
frame that sends anything to it, and keeps its picture in frames that do not.

`Camera2D` holds `Offset` (where on the screen the target appears), `Target`, `Rotation`
(degrees) and `Zoom`, as raylib's. `Camera3D` holds `Position`, `Target`, `Up`, `FovY` (degrees) and `Projection` (`Perspective` or
`Orthographic`). `CameraMode.Free` moves with W, A, S, D, Q and E, turns while the right mouse
button is dragged, and goes faster with Shift. `Orbital` circles the target. `FirstPerson` turns
with the mouse and walks along the ground with W, A, S and D, and `ThirdPerson` does the same
around its target, which the program draws as the player. Both are meant with `DisableCursor`.

## Input

```csharp
bool IsKeyPressed(Key key);                              // Key went down this frame
bool IsKeyDown(Key key);                                 // Key is held
bool IsKeyReleased(Key key);                             // Key came up this frame
bool IsKeyPressedRepeat(Key key);                        // Key repeated while held this frame, as in a text field
bool IsKeyUp(Key key);                                   // Key is not held
Key GetKeyPressed();                                     // Next key pressed this frame, Unknown when none is left
string GetKeyName(Key key);                              // The key as the keyboard's layout prints it
int GetCharPressed();                                    // Next character typed this frame (a code point), 0 when none is left

bool IsMouseButtonPressed(MouseButton button);           // Button went down this frame
bool IsMouseButtonDown(MouseButton button);              // Button is held
bool IsMouseButtonReleased(MouseButton button);          // Button came up this frame
bool IsMouseButtonUp(MouseButton button);                // Button is not held
Vector2 GetMousePosition();                              // Pointer position in the window
void SetMouseOffset(int offsetX, int offsetY);           // Add to the pointer's position, before its scale
void SetMouseScale(float scaleX, float scaleY);          // Scale the pointer's position, as for a letterboxed render texture
void SetMousePosition(int x, int y);                     // Move the pointer within the window
int GetMouseX();                                         // Pointer x
int GetMouseY();                                         // Pointer y
Vector2 GetMouseDelta();                                 // How far the pointer moved this frame
float GetMouseWheelMove();                               // How far the wheel turned this frame
Vector2 GetMouseWheelMoveV();                            // The same on both axes, x scrolling sideways
void SetMouseCursor(MouseCursor cursor);                 // The pointer's shape (IBeam, PointingHand, ResizeEW, ...)
void ShowCursor();                                       // Show the cursor
void HideCursor();                                       // Hide the cursor
bool IsCursorHidden();                                   // Whether it is hidden
bool IsCursorOnScreen();                                 // Whether the pointer is over the window
void DisableCursor();                                    // Hide the cursor and hold it, for mouse look
void EnableCursor();                                     // Release and show it
int GetTouchPointCount();                                // Fingers on the screen (1 while the left button is held with none)
Vector2 GetTouchPosition(int index);                     // Where a finger is (the pointer for 0 with none down)
int GetTouchPointId(int index);                          // The id a finger keeps while it stays down
int GetTouchX();                                         // The first finger's x
int GetTouchY();                                         // The first finger's y
void SetGesturesEnabled(Gesture flags);                  // Which gestures are recognized (all)
bool IsGestureDetected(Gesture gesture);                 // Tap, DoubleTap, Hold, Drag, Swipe*, PinchIn or PinchOut this frame
Gesture GetGestureDetected();                            // This frame's gesture
float GetGestureHoldDuration();                          // Seconds the hold has lasted
Vector2 GetGestureDragVector();                          // How far the drag went, in fractions of the window
float GetGestureDragAngle();                             // Its angle in degrees
Vector2 GetGesturePinchVector();                         // Between a pinch's fingers
float GetGesturePinchAngle();                            // Its angle in degrees

bool IsGamepadAvailable(int gamepad);                                  // Whether a pad is connected at that index
string GetGamepadName(int gamepad);                                    // Its name
bool IsGamepadButtonPressed(int gamepad, GamepadButton button);        // Button went down this frame
GamepadButton? GetGamepadButtonPressed();                              // The button pressed last on any pad while held, null for none
int SetGamepadMappings(string mappings);                               // Add SDL_GameControllerDB lines, how many were added
bool IsGamepadButtonDown(int gamepad, GamepadButton button);           // Button is held
bool IsGamepadButtonReleased(int gamepad, GamepadButton button);       // Button came up this frame
bool IsGamepadButtonUp(int gamepad, GamepadButton button);             // Button is not held
float GetGamepadAxisMovement(int gamepad, GamepadAxis axis);           // Stick -1 to 1, trigger 0 to 1
int GetGamepadAxisCount(int gamepad);                                  // Six for a connected pad
void SetGamepadVibration(int gamepad, float left, float right, float seconds); // Rumble
bool IsGamepadMotionAvailable(int gamepad);                            // Whether it has a gyro and an accelerometer
Vector3 GetGamepadGyro(int gamepad);                                   // Turning, radians a second about its axes
Vector3 GetGamepadAccelerometer(int gamepad);                          // Acceleration, gravity in it
int GetGamepadTouchCount(int gamepad);                                 // Fingers on its touchpad
Vector2 GetGamepadTouchPosition(int gamepad, int index);               // Where one is, 0 to 1 across and down
void SetGamepadLight(int gamepad, Color color);                        // Color its light bar
```

Pads are indexed in the order they connected. Buttons are named by position (`South`, `East`,
`West`, `North`, `DpadUp`, `LeftShoulder`, `Start`, ...) rather than by the letter printed on them.

## 2D shapes

```csharp
void DrawLine(int startX, int startY, int endX, int endY, Color color);            // Line
void DrawLineV(Vector2 start, Vector2 end, Color color);                           // Line
void DrawTriangle(Vector2 v1, Vector2 v2, Vector2 v3, Color color);                // Filled triangle
void DrawRectangle(int x, int y, int width, int height, Color color);              // Filled rectangle
void DrawRectangleV(Vector2 position, Vector2 size, Color color);                  // Filled rectangle
void DrawRectangleLines(int x, int y, int width, int height, Color color);         // Rectangle outline
void DrawCircle(int centerX, int centerY, float radius, Color color);              // Filled circle
void DrawCircleV(Vector2 center, float radius, Color color);                       // Filled circle
void DrawCircleLines(int centerX, int centerY, float radius, Color color);         // Circle outline
void DrawPixel(int x, int y, Color color);                                         // One pixel
void DrawPixelV(Vector2 position, Color color);                                    // One pixel
void DrawLineEx(Vector2 start, Vector2 end, float thick, Color color);             // Line of a width
void DrawLineStrip(ReadOnlySpan<Vector2> points, Color color);                     // Lines joining the points in turn
void DrawLineDashed(Vector2 startPos, Vector2 endPos, int dashSize, int spaceSize, Color color); // A line in dashes
void DrawLineBezier(Vector2 start, Vector2 end, float thick, Color color);         // Curve easing in and out
void DrawRectangleRec(Rectangle rec, Color color);                                 // Filled rectangle
void DrawRectanglePro(Rectangle rec, Vector2 origin, float rotation, Color color); // Filled rectangle turned about an origin
void DrawRectangleGradientV(int x, int y, int width, int height, Color top, Color bottom); // Blended top to bottom
void DrawRectangleGradientH(int x, int y, int width, int height, Color left, Color right); // Blended left to right
void DrawRectangleGradientEx(Rectangle rec, Color topLeft, Color bottomLeft, Color bottomRight, Color topRight); // Blended between corners
void DrawRectangleLinesEx(Rectangle rec, float lineThick, Color color);            // Outline of a width, inside the edge
void DrawRectangleRounded(Rectangle rec, float roundness, int segments, Color color); // Rounded corners, roundness 0 to 1
void DrawRectangleRoundedLines(Rectangle rec, float roundness, int segments, Color color); // Its outline
void DrawRectangleRoundedLinesEx(Rectangle rec, float roundness, int segments, float lineThick, Color color); // Its outline, thick, outside the edge
void DrawTriangleLines(Vector2 v1, Vector2 v2, Vector2 v3, Color color);           // Triangle outline
void DrawTriangleLinesEx(Vector2 v1, Vector2 v2, Vector2 v3, float thick, Color color); // Its outline, thick
void DrawTriangleGradient(Vector2 v1, Vector2 v2, Vector2 v3, Color c1, Color c2, Color c3); // A color at each corner
void DrawTriangleFan(ReadOnlySpan<Vector2> points, Color color);                   // Triangles fanning from the first point
void DrawTriangleStrip(ReadOnlySpan<Vector2> points, Color color);                 // A strip of triangles
void DrawPoly(Vector2 center, int sides, float radius, float rotation, Color color); // Filled regular polygon
void DrawPolyLines(Vector2 center, int sides, float radius, float rotation, Color color); // Its outline
void DrawPolyLinesEx(Vector2 center, int sides, float radius, float rotation, float lineThick, Color color); // Outline of a width
void DrawCircleSector(Vector2 center, float radius, float startAngle, float endAngle, int segments, Color color); // Filled slice
void DrawCircleSectorLines(Vector2 center, float radius, float startAngle, float endAngle, int segments, Color color); // Its outline
void DrawCircleSectorLinesEx(Vector2 center, float radius, float startAngle, float endAngle, int segments, float thick, Color color); // Its outline, thick
void DrawCircleGradient(Vector2 center, float radius, Color inner, Color outer);       // Blended from the middle out
void DrawCircleLinesV(Vector2 center, float radius, Color color);                  // Circle outline
void DrawCircleLinesEx(Vector2 center, float radius, float thick, Color color);     // Circle outline, thick
void DrawEllipse(int centerX, int centerY, float radiusH, float radiusV, Color color); // Filled ellipse
void DrawEllipseLines(int centerX, int centerY, float radiusH, float radiusV, Color color); // Its outline
void DrawEllipseV(Vector2 center, float radiusH, float radiusV, Color color);      // Filled ellipse around a point
void DrawEllipseLinesV(Vector2 center, float radiusH, float radiusV, Color color); // Its outline
void DrawEllipseLinesEx(Vector2 center, float radiusH, float radiusV, float thick, Color color); // Its outline, thick
void DrawRing(Vector2 center, float innerRadius, float outerRadius, float startAngle, float endAngle, int segments, Color color); // Filled ring or arc
void DrawRingLines(Vector2 center, float innerRadius, float outerRadius, float startAngle, float endAngle, int segments, Color color); // Its outline
void DrawRingLinesEx(Vector2 center, float innerRadius, float outerRadius, float startAngle, float endAngle, int segments, float thick, Color color); // Its outline, thick
void DrawSplineLinear(ReadOnlySpan<Vector2> points, float thick, Color color);     // Lines joining the points, corners closed
void DrawSplineBasis(ReadOnlySpan<Vector2> points, float thick, Color color);      // B-spline passing near the points, at least 4
void DrawSplineCatmullRom(ReadOnlySpan<Vector2> points, float thick, Color color); // Through every point but the first and last, at least 4
void DrawSplineBezierQuadratic(ReadOnlySpan<Vector2> points, float thick, Color color); // Start, control, end, control, end and so on
void DrawSplineBezierCubic(ReadOnlySpan<Vector2> points, float thick, Color color); // Start, two controls, end, two controls, end and so on
void DrawSplineSegmentLinear(Vector2 p1, Vector2 p2, float thick, Color color);    // One segment of each
void DrawSplineSegmentBasis(Vector2 p1, Vector2 p2, Vector2 p3, Vector2 p4, float thick, Color color);
void DrawSplineSegmentCatmullRom(Vector2 p1, Vector2 p2, Vector2 p3, Vector2 p4, float thick, Color color);
void DrawSplineSegmentBezierQuadratic(Vector2 p1, Vector2 c2, Vector2 p3, float thick, Color color);
void DrawSplineSegmentBezierCubic(Vector2 p1, Vector2 c2, Vector2 c3, Vector2 p4, float thick, Color color);
Vector2 GetSplinePointLinear(Vector2 startPos, Vector2 endPos, float t);           // The point a fraction t along each, 0 to 1
Vector2 GetSplinePointBasis(Vector2 p1, Vector2 p2, Vector2 p3, Vector2 p4, float t);
Vector2 GetSplinePointCatmullRom(Vector2 p1, Vector2 p2, Vector2 p3, Vector2 p4, float t);
Vector2 GetSplinePointBezierQuad(Vector2 p1, Vector2 c2, Vector2 p3, float t);
Vector2 GetSplinePointBezierQuadratic(Vector2 p1, Vector2 c2, Vector2 p3, float t); // The same, by raylib's newer name
Vector2 GetSplinePointBezierCubic(Vector2 p1, Vector2 c2, Vector2 c3, Vector2 p4, float t);
```

Angles are in degrees, clockwise on the screen from right, and a segment count of 0 lets the
size choose how many pieces a curve is drawn in.

## Collision

```csharp
bool CheckCollisionRecs(Rectangle a, Rectangle b);                                  // Two rectangles overlap
bool CheckCollisionCircles(Vector2 center1, float radius1, Vector2 center2, float radius2); // Two circles overlap
bool CheckCollisionCircleRec(Vector2 center, float radius, Rectangle rec);          // A circle and a rectangle overlap
bool CheckCollisionPointRec(Vector2 point, Rectangle rec);                          // A point is inside a rectangle
bool CheckCollisionPointCircle(Vector2 point, Vector2 center, float radius);        // A point is inside a circle
Rectangle GetCollisionRec(Rectangle a, Rectangle b);                                // The rectangle two share, empty when they do not overlap
bool CheckCollisionPointTriangle(Vector2 point, Vector2 p1, Vector2 p2, Vector2 p3); // A point is inside a triangle
bool CheckCollisionPointLine(Vector2 point, Vector2 p1, Vector2 p2, int threshold); // A point is near a segment
bool CheckCollisionPointPoly(Vector2 point, ReadOnlySpan<Vector2> points);           // A point is inside a polygon
bool CheckCollisionLines(Vector2 startPos1, Vector2 endPos1, Vector2 startPos2, Vector2 endPos2, out Vector2 collisionPoint); // Two segments cross, and where
bool CheckCollisionCircleLine(Vector2 center, float radius, Vector2 p1, Vector2 p2); // A circle touches a segment
bool CheckCollisionSpheres(Vector3 center1, float radius1, Vector3 center2, float radius2); // Two spheres overlap
bool CheckCollisionBoxes(BoundingBox box1, BoundingBox box2);                       // Two boxes overlap
bool CheckCollisionBoxSphere(BoundingBox box, Vector3 center, float radius);        // A box and a sphere overlap
RayCollision GetRayCollisionSphere(Ray ray, Vector3 center, float radius);          // Where a ray meets a sphere
RayCollision GetRayCollisionBox(Ray ray, BoundingBox box);                          // Where a ray meets a box
RayCollision GetRayCollisionTriangle(Ray ray, Vector3 p1, Vector3 p2, Vector3 p3);  // Where a ray meets a triangle
RayCollision GetRayCollisionQuad(Ray ray, Vector3 p1, Vector3 p2, Vector3 p3, Vector3 p4); // Where a ray meets a quad
RayCollision GetRayCollisionMesh(Ray ray, ModelMesh mesh, Matrix4x4 transform);     // Where a ray first meets a placed mesh
BoundingBox GetMeshBoundingBox(ModelMesh mesh);                                     // The box around a mesh
```

## 3D shapes

```csharp
void DrawLine3D(Vector3 start, Vector3 end, Color color);                                       // Line
void DrawTriangle3D(Vector3 v1, Vector3 v2, Vector3 v3, Color color);                           // Filled triangle
void DrawCube(Vector3 position, float width, float height, float length, Color color);          // Box
void DrawCubeV(Vector3 position, Vector3 size, Color color);                                    // Box
void DrawCubeWires(Vector3 position, float width, float height, float length, Color color);     // Box edges
void DrawCubeWiresV(Vector3 position, Vector3 size, Color color);                               // Box edges
void DrawSphere(Vector3 center, float radius, Color color);                                     // Sphere
void DrawSphereEx(Vector3 center, float radius, int rings, int slices, Color color);            // Sphere with a chosen detail
void DrawSphereWires(Vector3 center, float radius, int rings, int slices, Color color);         // Sphere as lines, a diagonal across each face
void DrawPlane(Vector3 center, Vector2 size, Color color);                                      // Rectangle on the XZ plane
void DrawGrid(int slices, float spacing);                                                       // Grid on the XZ plane
void DrawPoint3D(Vector3 position, Color color);                                                // A point, as a small cross
void DrawRay(Ray ray, Color color);                                                             // A ray, a hundred units of it
void DrawCircle3D(Vector3 center, float radius, Vector3 rotationAxis, float rotationAngle, Color color); // A circle turned about an axis
void DrawTriangleStrip3D(ReadOnlySpan<Vector3> points, Color color);                            // A strip of triangles
void DrawCylinder(Vector3 position, float radiusTop, float radiusBottom, float height, int slices, Color color); // Upright cylinder or cone
void DrawCylinderEx(Vector3 startPos, Vector3 endPos, float startRadius, float endRadius, int sides, Color color); // From one point to another
void DrawCylinderWires(Vector3 position, float radiusTop, float radiusBottom, float height, int slices, Color color); // Its edges
void DrawCylinderWiresEx(Vector3 startPos, Vector3 endPos, float startRadius, float endRadius, int sides, Color color); // Its edges
void DrawCapsule(Vector3 startPos, Vector3 endPos, float radius, int slices, int rings, Color color); // Capsule between two points
void DrawCapsuleWires(Vector3 startPos, Vector3 endPos, float radius, int slices, int rings, Color color); // Its edges
```

## rlgl

raylib's layer under its shapes, the calls its examples make. Vertices go into the frame's draw
list, moved by the matrix stack, which moves every shape, text and model drawn inside a push too.

```csharp
void rlPushMatrix();                                       // Keep the transform, for rlPopMatrix
void rlPopMatrix();                                        // Back to the transform kept
void rlTranslatef(float x, float y, float z);              // Move what is drawn after
void rlRotatef(float angle, float x, float y, float z);    // Turn it by degrees about an axis
void rlScalef(float x, float y, float z);                  // Scale it
void rlBegin(RlDrawMode mode);                             // Start vertices as Lines, Triangles or Quads
void rlEnd();                                              // End them
void rlVertex2f(float x, float y);                         // A vertex in 2D
void rlVertex3f(float x, float y, float z);                // A vertex in 3D
void rlTexCoord2f(float x, float y);                       // The next vertices' texture coordinate
void rlNormal3f(float x, float y, float z);                // Taken and not kept, the vertices being unlit
void rlColor4ub(byte r, byte g, byte b, byte a);           // The next vertices' color
void rlColor4f(float r, float g, float b, float a);        // The same from 0 to 1
void rlSetTexture(int id);                                 // The next primitives' texture, 0 for none
bool rlCheckRenderBatchLimit(int vertexCount);             // False, the draw list growing as it needs
void rlEnableBackfaceCulling();                            // Leave out back faces, shapes and text too
void rlDisableBackfaceCulling();                           // Draw both faces, models whatever their material says
void rlSetCullFace(RlCullFace mode);                       // Which faces culling leaves out, Front or Back
void rlEnablePointMode();                                  // Draw models as a point at each corner
void rlDisablePointMode();                                 // Draw them filled again
void rlSetBlendFactors(RlBlendFactor glSrcFactor, RlBlendFactor glDstFactor, RlBlendEquation glEquation); // What BlendMode.Custom combines by
void rlSetBlendFactorsSeparate(RlBlendFactor glSrcRGB, RlBlendFactor glDstRGB, RlBlendFactor glSrcAlpha, RlBlendFactor glDstAlpha, RlBlendEquation glEqRGB, RlBlendEquation glEqAlpha); // What BlendMode.CustomSeparate combines by, color apart from alpha
void rlSetBlendMode(BlendMode mode);                       // BeginBlendMode under rlgl's name
void rlDrawRenderBatchActive();                            // Nothing to do, the draw list keeping each state's shapes in order
void rlMatrixMode(RlMatrixMode mode);                      // Have the calls after change the Modelview or the Projection
void rlLoadIdentity();                                     // That matrix to the identity
void rlMultMatrixf(ReadOnlySpan<float> matf);              // Multiply sixteen values in, in raymath's order
void rlSetMatrixProjection(Matrix4x4 proj);                // The projection, as System.Numerics makes one
void rlEnableDepthTest();                                  // Test what is drawn after against the depth, in 2D too
void rlDisableDepthTest();                                 // Draw over whatever the depth
void rlEnableDepthMask();                                  // Write the depth of what passes the test
void rlDisableDepthMask();                                 // Test against the depth without writing it
```

## Images and textures

```csharp
Image LoadImage(string fileName);                                                      // Read PNG, JPEG, BMP, TGA, PSD, GIF or HDR into memory
Image LoadImageRaw(string fileName, int width, int height, PixelFormat format, int headerSize); // Read pixels alone, laid out in a format, after a header
void ImageFormat(ref Image image, PixelFormat newFormat);                              // Keep of each pixel what a format keeps
Image GenImageColor(int width, int height, Color color);                               // An image of one color
Image GenImageText(int width, int height, string text);                                // Text's bytes as gray pixels, then black
Image GenImageChecked(int width, int height, int checksX, int checksY, Color first, Color second); // A checkerboard of checksX by checksY pixel squares
Image GenImageGradientLinear(int width, int height, int direction, Color start, Color end); // A blend along a direction (0 top to bottom, 90 left to right)
Image GenImageGradientRadial(int width, int height, float density, Color inner, Color outer); // A blend from the center outward
Image GenImageWhiteNoise(int width, int height, float factor);                         // White pixels with the chance factor, the rest black
Image GenImagePerlinNoise(int width, int height, int offsetX, int offsetY, float scale); // Six octaves of Perlin noise in grays
Image GenImageCellular(int width, int height, int tileSize);                           // Cells around a point in each square
Image ImageCopy(Image image);                                                          // A copy with pixels of its own
Image ImageFromImage(Image image, Rectangle rec);                                      // A new image of part of one
Color GetImageColor(Image image, int x, int y);                                        // One pixel's color
Color[] LoadImageColors(Image image);                                                  // Every pixel's color, row by row
Color[] LoadImagePalette(Image image, int maxPaletteSize);                             // Its different colors, in the order met
Image GenImageGradientSquare(int width, int height, float density, Color inner, Color outer); // A square gradient, inner at the center
Image ImageFromChannel(Image image, int selectedChannel);                              // One channel (0 red to 3 alpha) as grayscale
void ImageAlphaClear(ref Image image, Color color, float threshold);                   // Pixels below an alpha given a color
void ImageDither(ref Image image, int rBpp, int gBpp, int bBpp, int aBpp);             // Reduce to so many bits a channel, dithered
void ImageAlphaMask(ref Image image, Image alphaMask);                                 // Alpha from a mask's brightness
void ImageAlphaPremultiply(ref Image image);                                           // Color multiplied by alpha
Rectangle GetImageAlphaBorder(Image image, float threshold);                           // The box around the pixels above an alpha
void ImageBlurGaussian(ref Image image, int blurSize);                                 // Blur by a Gaussian of about that many pixels
void ImageKernelConvolution(ref Image image, float[] kernel);                          // Convolve the color with an odd square kernel
void ImageRotate(ref Image image, int degrees);                                        // Turn clockwise, the canvas grown to fit
Image LoadImageAnim(string fileName, out int frames);                                  // Every frame of an animated GIF, stacked from the top
Image LoadImageAnimFromMemory(string fileType, byte[] fileData, out int frames);       // The same from a GIF's bytes
Image LoadImageFromMemory(string fileType, byte[] fileData);                           // Decode an image file's bytes
Image LoadImageFromTexture(Texture2D texture);                                         // Read a texture or render texture back, waiting for the GPU
bool IsImageValid(Image image);                                                        // Whether it holds pixels
byte[] ExportImageToMemory(Image image, string fileType);                              // An image as a PNG file's bytes
bool ExportImage(Image image, string fileName);                                        // Write a PNG file
void UnloadImage(Image image);                                                         // Nothing (images are managed memory)

void ImageCrop(ref Image image, Rectangle rec);                                        // Keep part of an image
void ImageAlphaCrop(ref Image image, float threshold);                                 // Crop to the pixels above an alpha
void ImageToPOT(ref Image image, Color fill);                                          // Grow the canvas to powers of two
void ImageResize(ref Image image, int newWidth, int newHeight);                        // Scale, blending pixels
void ImageResizeNN(ref Image image, int newWidth, int newHeight);                      // Scale by the nearest pixel
void ImageResizeCanvas(ref Image image, int newWidth, int newHeight, int offsetX, int offsetY, Color fill); // Change the size without scaling
void ImageFlipVertical(ref Image image);                                               // Upside down
void ImageFlipHorizontal(ref Image image);                                             // Mirrored
void ImageRotateCW(ref Image image);                                                   // A quarter turn clockwise
void ImageRotateCCW(ref Image image);                                                  // A quarter turn counterclockwise
void ImageColorTint(ref Image image, Color color);                                     // Multiply every pixel
void ImageColorInvert(ref Image image);                                                // Invert red, green and blue
void ImageColorGrayscale(ref Image image);                                             // Gray by brightness
void ImageColorContrast(ref Image image, float contrast);                              // -100 to 100
void ImageColorBrightness(ref Image image, int brightness);                            // -255 to 255
void ImageColorReplace(ref Image image, Color color, Color replace);                   // Swap one exact color

void ImageClearBackground(ref Image image, Color color);                               // Fill the whole image
void ImageDrawPixel(ref Image image, int x, int y, Color color);                       // One pixel
void ImageDrawPixelV(ref Image image, Vector2 position, Color color);                  // One pixel
void ImageDrawLine(ref Image image, int startX, int startY, int endX, int endY, Color color); // A line
void ImageDrawLineV(ref Image image, Vector2 start, Vector2 end, Color color);          // A line
void ImageDrawLineStrip(ref Image dst, Vector2[] points, Color color);                 // Lines joining the points
void ImageDrawCircle(ref Image image, int centerX, int centerY, int radius, Color color);      // A filled circle
void ImageDrawCircleLines(ref Image image, int centerX, int centerY, int radius, Color color); // A circle's outline
void ImageDrawCircleLinesV(ref Image dst, Vector2 center, int radius, Color color);    // A circle's outline around a point
void ImageDrawCircleGradient(ref Image dst, Vector2 center, float radius, Color inner, Color outer); // A circle blending outward
void ImageDrawLineEx(ref Image dst, Vector2 start, Vector2 end, int thick, Color color); // A line of a width
void ImageDrawTriangle(ref Image dst, Vector2 v1, Vector2 v2, Vector2 v3, Color color); // A filled triangle
void ImageDrawTriangleGradient(ref Image dst, Vector2 v1, Vector2 v2, Vector2 v3, Color c1, Color c2, Color c3); // A color at each corner
void ImageDrawTriangleLines(ref Image dst, Vector2 v1, Vector2 v2, Vector2 v3, Color color); // A triangle's outline
void ImageDrawTriangleFan(ref Image dst, Vector2[] points, Color color);               // Triangles fanning from the first point
void ImageDrawTriangleStrip(ref Image dst, Vector2[] points, Color color);             // A strip of triangles
void ImageDrawCircleV(ref Image dst, Vector2 center, int radius, Color color);         // A filled circle around a point
void ImageDrawRectangle(ref Image image, int x, int y, int width, int height, Color color);    // A filled rectangle
void ImageDrawRectangleV(ref Image dst, Vector2 position, Vector2 size, Color color);  // The same by vectors
void ImageDrawRectanglePro(ref Image dst, Rectangle rec, Vector2 origin, float rotation, Color color); // Turned
void ImageDrawRectangleLinesEx(ref Image dst, Rectangle rec, int thick, Color color); // Its outline, thick
void ImageDrawRectangleGradientEx(ref Image dst, Rectangle rec, Color topLeft, Color bottomLeft, Color bottomRight, Color topRight); // A color at each corner
void ImageDrawRectangleRec(ref Image image, Rectangle rec, Color color);               // A filled rectangle
void ImageDrawRectangleLines(ref Image image, Rectangle rec, int thick, Color color);  // A rectangle's outline
void ImageDraw(ref Image dst, Image src, Rectangle srcRec, Rectangle dstRec, Color tint); // Part of an image into another, blended
void ImageDrawImage(ref Image dst, Image src, int posX, int posY, Color tint);         // A whole image at a pixel
void ImageDrawImageRec(ref Image dst, Image src, Rectangle srcRec, Vector2 position, Color tint); // Part of one at a position
void ImageDrawImageEx(ref Image dst, Image src, Vector2 position, float rotation, float scale, Color tint); // Scaled and turned
void ImageDrawImagePro(ref Image dst, Image src, Rectangle srcRec, Rectangle dstRec, Vector2 origin, float rotation, Color tint); // Part, into a rectangle, turned
void ImageDrawText(ref Image dst, string text, int x, int y, int fontSize, Color color); // Text in the default font
void ImageDrawTextEx(ref Image dst, Font font, string text, Vector2 position, float fontSize, float spacing, Color tint); // Text in a font
void ImageDrawTextPro(ref Image dst, Font font, string text, Vector2 position, Vector2 origin, float rotation, float fontSize, float spacing, Color tint); // Turned
Image ImageText(string text, int fontSize, Color color);                               // A new image of text in the default font, as large as the text
Image ImageTextEx(Font font, string text, float fontSize, float spacing, Color tint);  // The same in a font

Texture2D LoadTexture(string fileName);                                                // Read an image file into a texture
Texture2D LoadTextureFromImage(Image image);                                           // Upload an image into a texture
void UnloadTexture(Texture2D texture);                                                 // Free a texture
bool IsTextureValid(Texture2D texture);                                                // Whether a texture is loaded
bool UpdateTexture(Texture2D texture, Image image);                                    // Replace a texture's pixels with an image of the same size
bool UpdateTextureRec(Texture2D texture, Rectangle rec, byte[] pixels);                 // Replace a rectangle of it, the rest kept
void SetTextureFilter(Texture2D texture, TextureFilter filter);                        // Point, Bilinear (the default), Trilinear or Anisotropic4x, 8x, 16x
void SetTextureWrap(Texture2D texture, TextureWrap wrap);                              // Repeat (the default), Clamp or MirrorRepeat past its edges
void GenTextureMipmaps(ref Texture2D texture);                                         // Make mip levels on the GPU, so it stays smooth drawn small

void DrawTexture(Texture2D texture, int x, int y, Color tint);                                         // Texture at a position
void DrawTextureV(Texture2D texture, Vector2 position, Color tint);                                    // Texture at a position
void DrawTextureEx(Texture2D texture, Vector2 position, float rotation, float scale, Color tint);      // Rotated (degrees) and scaled
void DrawTextureRec(Texture2D texture, Rectangle source, Vector2 position, Color tint);                // Part of a texture
void DrawTexturePro(Texture2D texture, Rectangle source, Rectangle dest, Vector2 origin, float rotation, Color tint); // Part of a texture into a rectangle, rotated around origin
void DrawTextureNPatch(Texture2D texture, NPatchInfo nPatchInfo, Rectangle dest, Vector2 origin, float rotation, Color tint); // Stretched into a rectangle, its borders kept
void DrawBillboard(Camera3D camera, Texture2D texture, Vector3 position, float size, Color tint);      // Texture in 3D, facing the camera
void DrawBillboardRec(Camera3D camera, Texture2D texture, Rectangle source, Vector3 position, Vector2 size, Color tint); // Part of one, standing upright and turned toward the camera
void DrawBillboardPro(Camera3D camera, Texture2D texture, Rectangle source, Vector3 position, Vector3 up, Vector2 size, Vector2 origin, float rotation, Color tint); // The same along an up of its own, about an origin
```

An `Image` is RGBA bytes in memory. The `Image*` functions change the image passed by `ref`, as
raylib's take a pointer: shapes replace the pixels they cover, alpha included, and `ImageDraw`
blends by the source's alpha. A changed image reaches the screen through `LoadTextureFromImage`
or `UpdateTexture`.

A file name is looked for as given, then beside the program, then under `source/` beside it. A file
that cannot be read gives an invalid image or texture and a warning in the log, and drawing an
invalid texture draws nothing. `CloseWindow` logs how many textures were still loaded.

## Shaders

```csharp
Shader LoadShader(string fileName);                                  // Compile a Slang file with fragmentMain (and vertexMain if it moves vertices)
Shader LoadShaderFromMemory(string code, string name);               // The same from source in memory
bool IsShaderValid(Shader shader);                                   // Whether it is loaded
void UnloadShader(Shader shader);                                    // Free it
void BeginShaderMode(Shader shader);                                 // Draw shapes, textures and text with it until EndShaderMode
void EndShaderMode();                                                // Return to the engine's shader
int GetShaderLocation(Shader shader, string uniformName);            // A uniform declared at the top level, by name (-1 when there is none)
void SetShaderValue(Shader shader, int location, float value);       // A named uniform, or slot 0 to 3 read as param(slot)
void SetShaderValue(Shader shader, int location, Vector2 value);     // (also Vector3, Vector4 and int)
void SetShaderValueMatrix(Shader shader, int location, Matrix4x4 value); // A named float4x4 uniform
void SetShaderValueV<T>(Shader shader, int location, ReadOnlySpan<T> values); // A named array uniform, spaced as std140 lays it out
void SetShaderValueTexture(Shader shader, int location, Texture2D texture); // A texture the shader declares, as Sampler2D detail;
```

A shader imports the engine's module, which gives it `VertexOutput` (position, uv, color),
`boundTexture` and `param(slot)`. Uniforms and textures (`Sampler2D detail;`) it declares at the
top level are set by name too, each value holding for what is drawn after it:

```slang
import engine;

[shader("fragment")]
float4 fragmentMain(VertexOutput input) : SV_Target
{
    float4 color = input.color * boundTexture.Sample(input.uv);
    float gray = dot(color.rgb, float3(0.299, 0.587, 0.114));
    return float4(lerp(color.rgb, float3(gray, gray, gray), param(0).x), color.a);
}
```

Inside `BeginShaderMode`, a shader applies to the immediate pass: shapes, textures, text and
render textures drawn as textures. A model takes one through its material,
`model.Materials[0].Shader = shader;`, and such a shader imports `modelpass` instead, which gives
it `ModelVertexOutput` (position, normal, world position, uv), `baseColor(input)` and
`lit(color, input)`, the model pass's own lighting. A model shader with a vertex stage of its own
takes a `ModelInstance` after the mesh's position, normal and uv, and hands it to
`transformModelVertex`. A model shader's uniforms are set by name:

```slang
import modelpass;

uniform float4 tint;

[shader("fragment")]
float4 fragmentMain(ModelVertexOutput input) : SV_Target
{
    float4 color = baseColor(input) * tint;
    return float4(lit(color.rgb, input), color.a);
}
```

```csharp
SetShaderValue(shader, GetShaderLocation(shader, "tint"), new Vector4(1, 0.5f, 0.5f, 1));
```

A value set before a draw is the one that draw uses, so one shader can draw with several in a
frame.

### Compute

```csharp
Shader LoadComputeShader(string fileName);                           // Compile a Slang file with a [shader("compute")] function
Shader LoadComputeShaderFromMemory(string code, string name);        // The same from source in memory
ShaderBuffer LoadShaderBuffer(int size);                             // A storage buffer of that many bytes, zeroed
ShaderBuffer LoadShaderBuffer<T>(ReadOnlySpan<T> data);              // One holding the data
void UnloadShaderBuffer(ShaderBuffer buffer);                        // Free it
bool IsShaderBufferValid(ShaderBuffer buffer);                       // Whether it is loaded
void UpdateShaderBuffer<T>(ShaderBuffer buffer, ReadOnlySpan<T> data, int offset); // Write into it
void ReadShaderBuffer<T>(ShaderBuffer buffer, Span<T> destination, int offset);    // Read it back, once the dispatches before have run
void SetShaderValueBuffer(Shader shader, int location, ShaderBuffer buffer); // A buffer the shader declares, as RWStructuredBuffer<float> values; or StructuredBuffer to draw from
void ComputeShaderDispatch(Shader shader, int groupsX, int groupsY, int groupsZ); // Run it over groups of its threads
```

A compute shader's uniforms are set by name as any shader's are, and its storage buffers the same
way, with `SetShaderValueBuffer`:

```slang
uniform uint count;
RWStructuredBuffer<float> values;

[shader("compute")]
[numthreads(64, 1, 1)]
void computeMain(uint3 id : SV_DispatchThreadID)
{
    if (id.x < count) values[id.x] *= 2;
}
```

A dispatch runs on the GPU before the frame being drawn, while the program goes on, and
`ReadShaderBuffer` waits for it. A compute shader writes a texture it declares as
`RWTexture2D<float4> image;` and samples a `Sampler2D`, each set with `SetShaderValueTexture`, once
the texture has reached the GPU, which it does in the frame after it is loaded, and a render
texture's color as well where the GPU can store to the window's format. A shader that draws reads
the same buffer as a `StructuredBuffer`, set with `SetShaderValueBuffer`, so what a dispatch wrote
is drawn with no copy through the CPU. An immediate shader reads it in `BeginShaderMode`, and a
model shader drawn with `DrawMeshInstanced` picks each copy's values from it by `SV_InstanceID`.

## Models and meshes

```csharp
Model LoadModel(string fileName);                                          // Read a model through Assimp (glTF, FBX, OBJ, ...) with its base colors and textures, embedded ones too
Model LoadModelFromMesh(ModelMesh mesh);                                   // A model of one mesh with a white material
void UnloadModel(Model model);                                             // Free a model's meshes and the textures it loaded
bool IsModelValid(Model model);                                            // Whether a model's meshes are loaded
BoundingBox GetModelBoundingBox(Model model);                              // The box around a model

ModelMesh GenMeshCube(float width, float height, float length);            // A box
ModelMesh GenMeshSphere(float radius, int rings, int slices);              // A sphere
ModelMesh GenMeshPlane(float width, float length, int resX, int resZ);     // A flat rectangle facing up
ModelMesh GenMeshPoly(int sides, float radius);                            // A flat regular polygon facing up
ModelMesh GenMeshHemiSphere(float radius, int rings, int slices);          // The upper half of a sphere, closed
ModelMesh GenMeshCylinder(float radius, float height, int slices);         // A closed cylinder standing on y 0
ModelMesh GenMeshCone(float radius, float height, int slices);             // A cone standing on y 0
ModelMesh GenMeshTorus(float radius, float size, int radSeg, int sides);   // A ring of size / 2, standing, its tube radius of that thick
ModelMesh GenMeshKnot(float radius, float size, int radSeg, int sides);    // A trefoil knot scaled by size, its tube radius / 10
ModelMesh GenMeshHeightmap(Image heightmap, Vector3 size);                 // Terrain raised by each pixel's brightness
ModelMesh GenMeshCubicmap(Image cubicmap, Vector3 cubeSize);               // A maze, walls where pixels are white
bool ExportMesh(ModelMesh mesh, string fileName);                          // Write a Wavefront OBJ file of its shape
Mesh GetMeshComponent(ModelMesh mesh);                                     // Its triangles as a Mesh component, for entities to draw
ModelMesh UploadMesh(ModelVertex[] vertices, uint[] indices);              // A mesh of the program's own triangles
void UpdateMeshVertices(ModelMesh mesh, ModelVertex[] vertices);           // Replace a mesh's vertices, keeping its triangles
void UnloadMesh(ModelMesh mesh);                                           // Free a mesh

void SetEnvironmentMap(Image equirectangular, float intensity = 1);         // Light models from all around by a sky image, which smooth and metal surfaces reflect
bool SetEnvironmentMap(string fileName, float intensity = 1);              // The same from a file, a Radiance .hdr keeping light past white
void UnloadEnvironmentMap();                                               // Back to the fixed light, or the light entities alone
void DrawSkybox();                                                         // Draw the environment map as the sky, inside BeginMode3D
void DrawSkybox(Color tint);                                               // The same, tinted

ModelAnimation[] LoadModelAnimations(string fileName);                     // Every clip of a model file, sampled at AnimationFps (60) frames a second
void UpdateModelAnimation(Model model, ModelAnimation anim, float frame);  // Pose a model's skinned meshes at a frame of a clip, a fraction blending two
void UpdateModelAnimationAt(Model model, ModelAnimation anim, float seconds); // Pose a model between frames, at a time
void UpdateModelAnimationBlend(Model model, ModelAnimation from, float fromSeconds, ModelAnimation to, float toSeconds, float weight); // Between two clips
void UpdateModelAnimationEx(Model model, ModelAnimation animA, float frameA, ModelAnimation animB, float frameB, float blend); // The same by frame
void UpdateModelAnimationLayer(Model model, ModelAnimation under, float underSeconds, ModelAnimation over, float overSeconds, string bone, float weight = 1); // A clip on a bone and those below it, over another
void SetModelMorphWeight(Model model, string target, float weight);       // Move a mesh toward a morph target by name, 0 to 1
bool IsModelAnimationValid(Model model, ModelAnimation anim);              // Whether a clip moves the bones a model has
void UnloadModelAnimation(ModelAnimation animation);                       // Let a clip go
void UnloadModelAnimations(ModelAnimation[] animations);                   // Let clips go

void DrawModel(Model model, Vector3 position, float scale, Color tint);                                               // A model
void DrawModelEx(Model model, Vector3 position, Vector3 rotationAxis, float rotationAngle, Vector3 scale, Color tint); // Rotated (degrees) and scaled
void DrawModelWires(Model model, Vector3 position, float scale, Color tint);                                          // A model's triangle edges
void DrawModelWiresEx(Model model, Vector3 position, Vector3 rotationAxis, float rotationAngle, Vector3 scale, Color tint); // Rotated and scaled
void DrawModelPoints(Model model, Vector3 position, float scale, Color tint);                                         // Its vertices as points
void DrawModelPointsEx(Model model, Vector3 position, Vector3 rotationAxis, float rotationAngle, Vector3 scale, Color tint); // Rotated and scaled
ModelMaterial LoadMaterialDefault();                                                                                  // A white material with no maps
ModelMaterial[] LoadMaterials(string fileName);                                                                       // A model file's materials, without its meshes
bool IsMaterialValid(ModelMaterial material);                                                                         // Whether its maps are loaded
void SetMaterialTexture(ref ModelMaterial material, MaterialMapIndex mapType, Texture2D texture);                     // A map by raylib's name for it
void SetModelMeshMaterial(Model model, int meshId, int materialId);                                                   // Which material a mesh draws with
void DrawMesh(ModelMesh mesh, ModelMaterial material, Matrix4x4 transform);                                           // One mesh at a transform
void DrawMeshInstanced(ModelMesh mesh, ModelMaterial material, ReadOnlySpan<Matrix4x4> transforms);                   // Copies of it, one draw, SV_InstanceID from 0
void DrawBoundingBox(BoundingBox box, Color color);                                                                   // A box's edges
```

A `Model` has `Meshes`, `Materials` and `MeshMaterial`, as raylib's does, and a `Transform`, and
a model with a skeleton has `Bones` and `BindPose`, which a `ModelAnimation` of the same file
poses frame by frame. A
`ModelMaterial` is a `Color` and a `Texture`, so `model.Materials[0].Texture = texture;` textures a
mesh, with `Metallic`, `Roughness`, a `NormalMap` and its `NormalScale`, a `MetallicRoughnessMap`
as glTF packs one, an `Emissive` color with its `EmissiveIntensity` and `EmissiveMap`, and an
`OcclusionMap` with its `OcclusionStrength`, an `AlphaMode` (`Blend` by default, `Mask` below
its `AlphaCutoff`, or `Opaque`) and `DoubleSided` (false by default), both of which a glTF file sets,
and `CastsShadows` (true by default), false for a glow that leaves no shadow. Models are lit by one fixed light from above, unless
the ECS holds `Light` entities, and draw through the camera `BeginMode3D` set.

## States

```csharp
void AddState<TState>(TState initial);                  // A state machine over an enum, as a menu, play and pause
void AddSubState<TSub, TParent>(TParent whileIn, TSub initial);         // One that exists only while another is at a value
void AddComputedState<TComputed, TSource>(Func<TSource, TComputed?> compute); // One worked out from another, none where null
TState GetState<TState>();                              // The value it is in
void SetState<TState>(TState value);                    // Move it there at the start of the next frame
bool IsState<TState>(TState value);                     // Whether it is there, false for one with no value
```

Behaviors follow the same machines, with `[OnEnter(Screen.Play)]`, `[OnExit(...)]`,
`[OnTransition(Screen.Pause, Screen.Play)]` and `[InState(...)]`. A sub-state or computed state
can be declared rather than added:

```csharp
[SubStateOf(Screen.Playing)]
public enum Pause { Running, Paused }

[ComputedState]
public static InGame? FromScreen(Screen screen) => screen is Screen.Playing ? InGame.Yes : null;
```

An entity tied to a value with `ecs.DespawnOnExit(entity, Screen.Playing)` is despawned, with what is
below it, when the state leaves that value, so a level goes with the state that built it.

## Scenes

```csharp
IReadOnlyList<Entity> LoadScene(string fileName);                    // Spawn a scene file's entities into the ECS, found beside the program
void SaveScene(string fileName, IEnumerable<Entity>? entities = null); // Write the ECS's entities, or some, to a scene file
```

A scene file is JSON of entities and their components by name. The engine's components are saved,
and a program's own are when marked `[SceneComponent]`, as `[SceneComponent] public struct Crate;`.
A `Collider` and a `RigidBody` say what is solid, and the body is made when the scene loads, so
`ecs.GetReadOnly<PhysicsBody>(entity)` gives it to the physics functions. ARCHITECTURE.md describes
the format.

## Lights

```csharp
LightHandle CreateDirectionalLight(Vector3 direction, Color color, float intensity = 1, bool castsShadows = false); // The sun's, from far away, which may cast the shadow
LightHandle CreatePointLight(Vector3 position, Color color, float intensity = 1, float range = 0, bool castsShadows = false); // Every way from a point, to a range or every distance
LightHandle CreateSpotLight(Vector3 position, Vector3 direction, Color color, float intensity = 1, float innerAngle = 25, float outerAngle = 30, float range = 0, bool castsShadows = false); // A cone
void SetLightPosition(LightHandle light, Vector3 position);                // Move a point or spot light
void SetLightDirection(LightHandle light, Vector3 direction);              // Turn a directional or spot light
void SetLightColor(LightHandle light, Color color, float intensity = 1);   // Recolor it
void SetLightCastsShadows(LightHandle light, bool castsShadows);           // Turn its shadows on or off
void SetShadowDistance(float distance);                                    // How far the sun's shadows reach (150), sharper when nearer
void SetShadowMapSize(int size);                                           // Texels a shadow tile is wide (2048), a game's shadow quality
void SetBloom(float intensity, float threshold = 1);                      // Light past the threshold glows into its surroundings, 0 for off
void SetExposure(float exposure);                                          // Scale the scene's light before its curve (1)
void SetAutoExposure(bool enabled, float min = 0.25f, float max = 4, float speed = 2); // The exposure follows the scene, as an eye adapts
void SetTonemap(Tonemap curve);                                            // The curve light past 1 is brought under it by: Engine, Reinhard, Aces or Clamp
void SetColorGrading(float contrast, float saturation, Color tint);        // Grade the scene's color, 1, 1 and white for as it is
void SetVignette(float intensity, float radius = 0.5f);                    // Darken toward the corners, 0 for none
void SetFxaa(bool enabled);                                                // Smooth the edges multisampling leaves
void SetDepthOfField(float focusDistance, float focusRange, float blur);   // Blur what is out of focus, 0 for none
void SetMotionBlur(float amount);                                          // Blur along the camera's movement, 0.5 as a shutter, 0 for none
void UnloadLight(LightHandle light);                                       // Remove it
void SetAmbientLight(Color color, float intensity);                       // The light from all around, one at a time, 0 to remove it
void SetAmbientOcclusion(float intensity, float radius = 1);              // Darken that light where nearby surfaces close it off, 0 for off
ReflectionProbeHandle CreateReflectionProbe(Vector3 position, Vector3 size, float intensity = 1); // A box that reflects the room around its middle, not the sky
void UpdateReflectionProbe(ReflectionProbeHandle probe);                   // Capture it again, after its room changed
bool IsReflectionProbeReady(ReflectionProbeHandle probe);                  // Whether its capture is made, a face a frame and twice over
void UnloadReflectionProbe(ReflectionProbeHandle probe);                   // Remove it
```

With no lights, models are lit by one fixed light from above. Lights are `Light` entities in the
ECS, so lights made here and light entities of a program's own light the same models.

## Particles

```csharp
ParticleEmitterHandle CreateParticleEmitter(Vector3 position, ParticleEmitter? emitter = null); // A stream of particles, a small white fountain unless set
void SetParticleEmitterPosition(ParticleEmitterHandle emitter, Vector3 position); // Move where its next particles start
void SetParticleEmitter(ParticleEmitterHandle emitter, ParticleEmitter settings); // Give it new rate, life, velocity, gravity, sizes or colors
ParticleEmitter GetParticleEmitter(ParticleEmitterHandle emitter);       // Its settings, to change one with `with`
void SetParticleEmitterActive(ParticleEmitterHandle emitter, bool emitting); // Start or stop its stream
void EmitParticles(ParticleEmitterHandle emitter, int count);            // Give off that many at once, as a hit or an explosion
void UnloadParticleEmitter(ParticleEmitterHandle emitter);               // Remove it and its particles
```

A `ParticleEmitter` holds the rate, the life, the velocity and the cone it is spread over, gravity,
drag, the size and color at birth and at death, how bright an unlit one is, whether lights light
it, whether it adds its light or is laid over by alpha, and a texture each particle is drawn as in
place of a round dot, whole or as a sheet of frames played through over its life. Its particles
are stepped by a compute shader and drawn after the window's meshes through its camera, and into
a render texture through the camera of its `BeginMode3D`, and an entity with the component in the
ECS is drawn the same way.

## Physics

```csharp
PhysicsBody CreatePhysicsBox(Vector3 position, Vector3 size, float mass = 1);   // A box that falls, collides and is pushed
PhysicsBody CreatePhysicsSphere(Vector3 position, float radius, float mass = 1); // A ball
PhysicsBody CreatePhysicsStaticBox(Vector3 position, Vector3 size);              // A box that never moves, for floors and walls
PhysicsBody CreatePhysicsKinematicBox(Vector3 position, Vector3 size);           // A box only the program moves, which pushes what it meets
PhysicsBody CreatePhysicsCapsule(Vector3 position, float radius, float height, float mass = 1); // An upright capsule, height end to end
PhysicsBody CreatePhysicsTrigger(Vector3 position, Vector3 size);               // A box that reports what enters it and stops nothing
void SetPhysicsBodyMaterial(PhysicsBody body, float friction, float bounce);     // Ice or rubber, a dead or a bouncing ball
void SetPhysicsBodyTrigger(PhysicsBody body, bool trigger);                     // Make a body a trigger, or solid again
void SetPhysicsBodyLayer(PhysicsBody body, int layer);                          // Put it on one of 32 layers, 0 to begin with
int GetPhysicsBodyLayer(PhysicsBody body);                                      // The layer it is on
void SetPhysicsLayersCollide(int a, int b, bool collide);                       // Whether two layers' bodies collide, all do to begin with
void SetPhysicsBodyContinuous(PhysicsBody body, bool continuous);               // Sweep a fast body over each step, so it does not cross a thin wall
PhysicsBody CreatePhysicsStaticModel(Model model, Vector3 position, float scale = 1); // Level geometry shaped as a model's triangles
PhysicsBody CreatePhysicsConvexHull(Model model, Vector3 position, float mass = 1, float scale = 1); // Falls and is pushed, shaped as the hull of the model
PhysicsJoint CreatePhysicsBallJoint(PhysicsBody a, PhysicsBody b, Vector3 point); // Join two bodies at a point, free to turn
PhysicsJoint CreatePhysicsHingeJoint(PhysicsBody a, PhysicsBody b, Vector3 point, Vector3 axis); // Turning only around an axis
PhysicsJoint CreatePhysicsWeldJoint(PhysicsBody a, PhysicsBody b);              // Join two bodies rigidly
PhysicsJoint CreatePhysicsDistanceJoint(PhysicsBody a, PhysicsBody b, Vector3 pointA, Vector3 pointB, float minimum, float maximum); // A rope or a rod
void SetPhysicsHingeLimits(PhysicsJoint hinge, float minimumDegrees, float maximumDegrees); // Keep a hinge between two angles
void SetPhysicsHingeMotor(PhysicsJoint hinge, float degreesPerSecond, float maximumTorque); // Drive a hinge at a speed
void SetPhysicsBallJointLimits(PhysicsJoint ball, Vector3 axis, float swingDegrees, float twistDegrees); // Keep a ball joint in a cone
void SetPhysicsDistanceJointRange(PhysicsJoint joint, float minimum, float maximum); // Lengthen or shorten a rope
PhysicsJoint CreatePhysicsSliderJoint(PhysicsBody a, PhysicsBody b, Vector3 axis); // Sliding along an axis, not turning, as a drawer or a lift
void SetPhysicsSliderLimits(PhysicsJoint slider, float minimum, float maximum);  // Keep a slider between two distances along its axis
void SetPhysicsSliderMotor(PhysicsJoint slider, float speed, float maximumForce); // Drive a slider at a speed
float GetPhysicsSliderPosition(PhysicsJoint slider);                            // How far along its axis it is
void DestroyPhysicsJoint(PhysicsJoint joint);                                   // Remove a joint
void SetPhysicsCharacterStepHeight(PhysicsBody body, float height);             // The highest step a character climbs (its radius)
bool SetPhysicsCharacterHeight(PhysicsBody body, float height);                 // Crouch or stand, false when a ceiling is in the way
bool IsPhysicsJointValid(PhysicsJoint joint);                                   // Whether it still exists
PhysicsBody CreatePhysicsVehicle(Vector3 position, Vector3 size, float mass = 1000, Vehicle? settings = null); // A box on raycast wheels as springs, facing -Z, a car unless set
void SetPhysicsVehicleInput(PhysicsBody vehicle, float throttle, float steer, bool brake = false); // Drive it, -1 to 1, a positive steer turning left
void SetPhysicsVehicle(PhysicsBody vehicle, Vehicle settings);                   // Its wheels, springs, engine, brakes, grip, steering, drag
Vehicle GetPhysicsVehicle(PhysicsBody vehicle);                                  // Those, to change one with `with`
VehicleWheel[] GetPhysicsVehicleWheels(PhysicsBody vehicle);                     // Where each wheel is, whether it touches ground, its slide and spin
PhysicsBody CreatePhysicsCharacter(Vector3 feet, float radius, float height, float mass = 80); // A character controller, an upright capsule that walls stop and that slides along them
void MovePhysicsCharacter(PhysicsBody body, Vector3 velocity);                   // Walk it along the ground until given another, leaving its fall to gravity
void JumpPhysicsCharacter(PhysicsBody body, float speed);                        // Jump, when it stands on ground
bool IsPhysicsCharacterGrounded(PhysicsBody body);                               // Whether it stands on ground it can walk on
void SetPhysicsCharacterMaxSlope(PhysicsBody body, float degrees);               // The steepest ground it walks on, 45 to begin with
void DestroyPhysicsBody(PhysicsBody body);                                       // Remove a body
bool IsPhysicsBodyValid(PhysicsBody body);                                       // Whether a body exists

Matrix4x4 GetPhysicsBodyTransform(PhysicsBody body);                             // Where to draw it this frame, blended between steps, for model.Transform
Vector3 GetPhysicsBodyPosition(PhysicsBody body);                                // Where it is as of its last step
void SetPhysicsBodyPosition(PhysicsBody body, Vector3 position);                 // Move it at once
Vector3 GetPhysicsBodyVelocity(PhysicsBody body);                                // How fast and which way it moves
void SetPhysicsBodyVelocity(PhysicsBody body, Vector3 velocity);                 // Set that, waking it
void ApplyPhysicsImpulse(PhysicsBody body, Vector3 impulse);                     // Push it at its center
void ApplyPhysicsImpulseAt(PhysicsBody body, Vector3 impulse, Vector3 point);    // Push it at a point, which turns it too
Quaternion GetPhysicsBodyRotation(PhysicsBody body);                             // How it is turned as of its last step
void SetPhysicsBodyRotation(PhysicsBody body, Quaternion rotation);              // Turn it at once, as righting a car
Vector3 GetPhysicsBodyAngularVelocity(PhysicsBody body);                         // How fast it turns, about which axis
void SetPhysicsBodyAngularVelocity(PhysicsBody body, Vector3 velocity);          // Set that, waking it
Vector3 GetPhysicsBodyPointVelocity(PhysicsBody body, Vector3 point);            // How fast a point of it moves, as a wheel's contact
void SetPhysicsGravity(Vector3 gravity);                                         // What every body falls by
void SetPhysicsPaused(bool paused);                                              // Hold the simulation still, or let it run
bool IsPhysicsPaused();                                                          // Whether it is held still

PhysicsRayCollision GetRayCollisionPhysics(Ray ray, float maxDistance);         // The first body a ray meets, past triggers, and whether it met one
PhysicsRayCollision GetRayCollisionPhysicsEx(Ray ray, float maxDistance, PhysicsBody ignore); // The same past one body, as a ray from inside a car
PhysicsRayCollision GetSphereCastPhysics(Ray ray, float radius, float maxDistance); // The first body a ball moving along a ray meets, as a thick shot
PhysicsRayCollision GetSphereCastPhysicsEx(Ray ray, float radius, float maxDistance, PhysicsBody ignore); // The same past one body
PhysicsBody[] GetPhysicsBodiesInSphere(Vector3 center, float radius);           // Every body a sphere overlaps, as what an explosion reaches
IReadOnlyList<ContactStarted> GetPhysicsContacts();                              // Pairs that started touching this frame, where, which way and how hard
IReadOnlyList<ContactEnded> GetPhysicsContactsEnded();                           // Pairs that stopped touching this frame, as a body leaving a trigger
bool IsPhysicsBodyHit(PhysicsBody body);                                         // Whether a body started touching anything this frame
float GetPhysicsContactImpulse(PhysicsBody a, PhysicsBody b);                    // How hard two touching bodies press, 0 when apart
Ray GetScreenToWorldRay(Vector2 position, Camera3D camera);                      // The ray through a point of the window
Ray GetScreenToWorldRayEx(Vector2 position, Camera3D camera, int width, int height); // The same for a view of a given size
Ray GetMouseRay(Vector2 mousePosition, Camera3D camera);                         // GetScreenToWorldRay by raylib's older name
```

Bodies are BepuPhysics's, stepped at the fixed rate inside `BeginDrawing`, so a box is drawn by
setting a model's `Transform` to `GetPhysicsBodyTransform(body)` before `DrawModel`. The ECS reaches
the same world as `PhysicsWorld`, with contacts as `ContactStarted` and `ContactEnded` events, and
walks a character through a `CharacterController` component beside its `PhysicsBody`.

## Audio

```csharp
void InitAudioDevice();                                   // Open the audio device (sounds are silent until then)
void CloseAudioDevice();                                  // Stop every sound and music the flat API started
bool IsAudioDeviceReady();                                // Whether the device is open with a backend that makes sound
void SetMasterVolume(float volume);                       // Volume every sound is multiplied by (0 to 1)
float GetMasterVolume();                                  // That volume

Sound LoadSound(string fileName);                         // Read a WAV, Ogg Vorbis, MP3 or FLAC file into memory
bool IsSoundValid(Sound sound);                           // Whether a sound has samples
void UnloadSound(Sound sound);                            // Stop a sound
Sound LoadSoundAlias(Sound source);                       // A sound sharing its samples that plays apart, to be heard over itself
void UnloadSoundAlias(Sound alias);                       // Stop an alias
void PlaySound(Sound sound);                              // Play from the start, restarting it if it was playing
void StopSound(Sound sound);                              // Stop
void PauseSound(Sound sound);                             // Pause
void ResumeSound(Sound sound);                            // Resume
bool IsSoundPlaying(Sound sound);                         // Whether it is playing
void SetSoundVolume(Sound sound, float volume);           // Volume (0 to 1), now and for the next play
void SetSoundPitch(Sound sound, float pitch);             // Speed, where 1 is as recorded
void SetSoundPan(Sound sound, float pan);                 // Balance, 0 left, 0.5 middle, 1 right

Wave LoadWave(string fileName);                           // A sound file's samples in memory, to cut, convert and write
Wave LoadWaveFromMemory(string fileType, byte[] fileData); // The same from a file's bytes, by its type (".ogg")
bool IsWaveValid(Wave wave);                              // Whether it has samples
void UnloadWave(Wave wave);                               // Nothing to free, kept for raylib's programs
Sound LoadSoundFromWave(Wave wave);                       // A sound of its samples
Wave WaveCopy(Wave wave);                                 // A copy with samples of its own
void WaveCrop(ref Wave wave, int initFrame, int finalFrame); // Keep the frames from init up to final
void WaveFormat(ref Wave wave, int sampleRate, int sampleSize, int channels); // Resample, and mix to one channel or spread to more
float[] LoadWaveSamples(Wave wave);                       // A copy of its samples, interleaved, -1 to 1
void UnloadWaveSamples(float[] samples);                  // Nothing to free, kept for raylib's programs
bool ExportWave(Wave wave, string fileName);              // Write it to a 16-bit WAV file

Music LoadMusicStream(string fileName);                   // Open a WAV, Ogg Vorbis, MP3 or FLAC file as music, streamed as it plays
Music LoadMusicStreamFromMemory(string fileType, byte[] data); // The same from a file's bytes, by its type (".ogg")
void UnloadMusicStream(Music music);                      // Stop music and close its file
bool IsMusicValid(Music music);                           // Whether music has samples
void PlayMusicStream(Music music);                        // Play from the start, looping unless music.Looping is false
void UpdateMusicStream(Music music);                      // Feed it from the file (every frame it plays)
void StopMusicStream(Music music);                        // Stop
void PauseMusicStream(Music music);                       // Pause
void ResumeMusicStream(Music music);                      // Resume
void SeekMusicStream(Music music, float position);        // Move to a time in seconds
bool IsMusicStreamPlaying(Music music);                   // Whether it is playing
void SetMusicVolume(Music music, float volume);           // Volume (0 to 1)
void SetMusicPitch(Music music, float pitch);             // Speed, where 1 is as recorded
void SetMusicPan(Music music, float pan);                 // Balance, 0 left, 0.5 middle, 1 right
float GetMusicTimeLength(Music music);                    // Length in seconds
float GetMusicTimePlayed(Music music);                    // How far into it the music heard is, in seconds

AudioStream LoadAudioStream(int sampleRate, int sampleSize, int channels); // A voice the program feeds samples of its own
bool IsAudioStreamValid(AudioStream stream);              // Whether it can play
void UnloadAudioStream(AudioStream stream);               // Stop it and let its voice go
void UpdateAudioStream(AudioStream stream, ReadOnlySpan<float> samples); // Queue samples, interleaved, -1 to 1 (or 16-bit shorts)
bool IsAudioStreamProcessed(AudioStream stream);          // Whether it has played enough to take more
void PlayAudioStream(AudioStream stream);                 // Play it
void PauseAudioStream(AudioStream stream);                // Pause, keeping what is queued
void ResumeAudioStream(AudioStream stream);               // Resume
void StopAudioStream(AudioStream stream);                 // Stop, dropping what is queued
bool IsAudioStreamPlaying(AudioStream stream);            // Whether it is playing
void SetAudioStreamVolume(AudioStream stream, float volume); // Volume (0 to 1)
void SetAudioStreamPitch(AudioStream stream, float pitch);   // Speed, where 1 is its sample rate
void SetAudioStreamPan(AudioStream stream, float pan);       // Balance, 0 left, 0.5 middle, 1 right
void SetAudioStreamBufferSizeDefault(int size);           // Frames a new stream keeps queued before asking for more (4096)
void SetAudioStreamCallback(AudioStream stream, AudioCallback? callback); // Feed it from a callback at each frame's end
void AttachAudioStreamProcessor(AudioStream stream, AudioCallback processor); // Change what it queues, after the processors before (music.Stream for music)
void DetachAudioStreamProcessor(AudioStream stream, AudioCallback processor); // Stop changing it
void AttachAudioMixedProcessor(AudioCallback processor);  // Change the mixed samples the device plays, on the audio thread
void DetachAudioMixedProcessor(AudioCallback processor);  // Stop changing them
```

A sound is decoded whole when it loads. Music is read from its file half a second ahead of what
is heard, by `UpdateMusicStream`, so a program calls it every frame the music plays, as with
raylib. Pausing one sound or piece of music leaves the others playing.

## Text and fonts

```csharp
void DrawText(string text, int x, int y, int fontSize, Color color);                       // Text in the default font, top left at (x, y)
int MeasureText(string text, int fontSize);                                                 // Width DrawText would draw it at
void DrawFPS(int x, int y);                                                                 // The frame rate

Font GetFontDefault();                                                                      // The default font (ProggyClean, 13 pixels)
Font GetFontDefault(int size);                                                              // The default font baked at a size, kept for reuse
Font LoadFont(string fileName);                                                             // A TrueType or OpenType font, baked at 32 pixels, an image font (.png) or a BMFont (.fnt)
Font LoadFontFromImage(Image image, Color key, int firstChar);                              // A font drawn as an image, glyphs separated by the key color
Font LoadFontEx(string fileName, int fontSize);                                             // Baked at a size, with the Latin-1 characters
Font LoadFontEx(string fileName, int fontSize, int[] codepoints);                           // Baked with exactly these characters (Greek, Cyrillic, ...)
Font LoadFontEx(string fileName, int fontSize, int[]? codepoints, FontType type);          // FontType.Sdf bakes a distance field, sharp at any size
int[] LoadCodepoints(string text);                                                          // The characters of a text, each in order, for LoadFontEx
void UnloadFont(Font font);                                                                 // Free its atlas
void DrawTextEx(Font font, string text, Vector2 position, float fontSize, float spacing, Color tint); // Text in a font
void DrawTextPro(Font font, string text, Vector2 position, Vector2 origin, float rotation, float fontSize, float spacing, Color tint); // Rotated around an origin
Vector2 MeasureTextEx(Font font, string text, float fontSize, float spacing);               // Its width and height
Vector2 MeasureTextCodepoints(Font font, int[] codepoints, float fontSize, float spacing); // The same for codepoints
bool IsFontValid(Font font);                                                                // Whether it has an atlas and glyphs
Font LoadFontFromMemory(string fileType, byte[] fileData, int fontSize, int[]? codepoints); // A font file already in memory (".ttf")
void SetTextLineSpacing(int spacing);                                                       // Lines a size and this many pixels apart
void DrawTextCodepoint(Font font, int codepoint, Vector2 position, float fontSize, Color tint); // One character by its code point
void DrawTextCodepoints(Font font, int[] codepoints, Vector2 position, float fontSize, float spacing, Color tint); // Characters by code point
Glyph? GetGlyphInfo(Font font, int codepoint);                                              // Where a glyph sits and how far it advances, or the font's '?' glyph
Rectangle GetGlyphAtlasRec(Font font, int codepoint);                                       // Where it lies in the atlas, in pixels
```

Text is drawn in the draw list like any shape, so it keeps its place among shapes, reaches render
targets, and draws through `BeginMode3D` on the plane z = 0. `DrawText` bakes the default font at
the size it is drawn, so small text stays sharp, and draws a size below 10 at 10, as raylib's
does. A newline starts a new line.

## Files

```csharp
string GetApplicationDirectory();                        // The folder the program runs from
bool FileExists(string fileName);                        // Whether a file is beside the program or in the working directory
string? LoadFileText(string fileName);                   // A text file's contents, null when there is none
bool SaveFileText(string fileName, string text);         // Write text to a file, beside the program for a relative name
byte[]? LoadFileData(string fileName);                   // A file's bytes, null when there is none
bool SaveFileData(string fileName, ReadOnlySpan<byte> data); // Write bytes to a file, beside the program for a relative name
void OpenURL(string url);                                // Open an http or https address in the browser
void WaitTime(double seconds);                           // Hold the program for some seconds
void TraceLog(LogLevel level, string text);              // A line in the engine's log, under Program
void SetTraceLogLevel(LogLevel level);                   // The least level that reaches the console (Info)
void SetTraceLogCallback(Action<LogLevel, string>? callback); // Hand each console line to the program as well
bool IsFileDropped();                                    // Whether files were dropped on the window since they were last unloaded
string[] LoadDroppedFiles();                             // Their paths, in the order they arrived
void UnloadDroppedFiles();                               // Forget them, for the next drop
```

## Colors

`Color(byte r, byte g, byte b, byte a = 255)`, with `Fade(alpha)` and `ToVector4()`, and raylib's
named colors: `LightGray`, `Gray`, `DarkGray`, `Yellow`, `Gold`, `Orange`, `Pink`, `Red`, `Maroon`,
`Green`, `Lime`, `DarkGreen`, `SkyBlue`, `Blue`, `DarkBlue`, `Purple`, `Violet`, `DarkPurple`,
`Beige`, `Brown`, `DarkBrown`, `White`, `Black`, `Blank`, `Magenta`, `RayWhite`.

```csharp
Color Fade(Color color, float alpha);                    // Its alpha set, from 0 to 1
Color ColorAlpha(Color color, float alpha);              // The same
bool ColorIsEqual(Color col1, Color col2);               // Whether all four channels match
int ColorToInt(Color color);                             // As 0xRRGGBBAA
Color GetColor(uint hexValue);                           // From 0xRRGGBBAA
Vector4 ColorNormalize(Color color);                     // As four floats from 0 to 1
Color ColorFromNormalized(Vector4 normalized);           // From four floats from 0 to 1
Vector3 ColorToHSV(Color color);                         // Hue in degrees, saturation and value from 0 to 1
Color ColorFromHSV(float hue, float saturation, float value); // From a hue, saturation and value
Color ColorTint(Color color, Color tint);                // Multiplied channel by channel
Color ColorBrightness(Color color, float factor);         // Toward black below 0, toward white above, from -1 to 1
Color ColorContrast(Color color, float contrast);        // Toward gray below 0, away above, from -1 to 1
Color ColorAlphaBlend(Color dst, Color src, Color tint); // The tinted source laid over dst by its alpha
Color ColorLerp(Color color1, Color color2, float factor); // Part of the way from one to the other
```

## Math

raymath's functions are C#'s own where `System.Numerics` has them, as `Vector2.Dot`, `Vector3.Cross`,
`Matrix4x4.CreateRotationX` and `Quaternion.Slerp`, and the operators for adding, scaling and
multiplying. A raymath `Matrix` is a `Matrix4x4`, composed in the same order. These are raymath's
with no such counterpart, under raymath's names, and `docs/compared-with-raylib.md` says which
name C# gives the rest.

```csharp
float Normalize(float value, float start, float end);    // Where a value lies from start (0) to end (1)
float Remap(float value, float inputStart, float inputEnd, float outputStart, float outputEnd); // From one range to another
float Wrap(float value, float min, float max);           // Wrapped into the range, as an angle into a turn
bool FloatEquals(float x, float y);                      // Equal within a millionth
float Vector2CrossProduct(Vector2 v1, Vector2 v2);       // The z of the 3D cross product
float Vector2Angle(Vector2 v1, Vector2 v2);              // From one to the other, radians, clockwise on the screen
float Vector2LineAngle(Vector2 start, Vector2 end);      // Of the line, radians, counterclockwise on the screen
Vector2 Vector2Rotate(Vector2 v, float angle);           // Turned, radians, clockwise on the screen
Vector2 Vector2MoveTowards(Vector2 v, Vector2 target, float maxDistance); // At most that far toward it
Vector2 Vector2ClampValue(Vector2 v, float min, float max); // Its length held between the two
bool Vector2Equals(Vector2 p, Vector2 q);                // Equal within a millionth
Vector2 Vector2Refract(Vector2 v, Vector2 n, float r);   // A ray's direction through a surface
Vector3 Vector3Perpendicular(Vector3 v);                 // A vector at right angles to it
float Vector3Angle(Vector3 v1, Vector3 v2);              // Between the two, radians
Vector3 Vector3Project(Vector3 v1, Vector3 v2);          // The part of v1 along v2
Vector3 Vector3Reject(Vector3 v1, Vector3 v2);           // The part of v1 across v2
void Vector3OrthoNormalize(ref Vector3 v1, ref Vector3 v2); // Both of length one and at right angles
Vector3 Vector3RotateByAxisAngle(Vector3 v, Vector3 axis, float angle); // Turned about an axis, radians
Vector3 Vector3MoveTowards(Vector3 v, Vector3 target, float maxDistance); // At most that far toward it
Vector3 Vector3CubicHermite(Vector3 v1, Vector3 tangent1, Vector3 v2, Vector3 tangent2, float amount); // As glTF interpolates
Vector3 Vector3Barycenter(Vector3 p, Vector3 a, Vector3 b, Vector3 c); // A point's coordinates in a triangle
Vector3 Vector3Unproject(Vector3 source, Matrix4x4 projection, Matrix4x4 view); // Back from clip space into the world
Vector3 Vector3ClampValue(Vector3 v, float min, float max); // Its length held between the two
bool Vector3Equals(Vector3 p, Vector3 q);                // Equal within a millionth
Vector3 Vector3Refract(Vector3 v, Vector3 n, float r);   // A ray's direction through a surface
Vector4 Vector4MoveTowards(Vector4 v, Vector4 target, float maxDistance); // At most that far toward it
bool Vector4Equals(Vector4 p, Vector4 q);                // Equal within a millionth
float MatrixTrace(Matrix4x4 mat);                        // The sum of its diagonal
Matrix4x4 MatrixRotateXYZ(Vector3 angle);                // Turns a point about Z, then Y, then X, radians
Matrix4x4 MatrixRotateZYX(Vector3 angle);                // Turns a point about X, then Y, then Z, radians
Matrix4x4 MatrixCompose(Vector3 translation, Quaternion rotation, Vector3 scale); // Scaled, turned, then moved
Quaternion QuaternionLerp(Quaternion q1, Quaternion q2, float amount); // Straight, not of length one
Quaternion QuaternionCubicHermiteSpline(Quaternion q1, Quaternion outTangent1, Quaternion q2, Quaternion inTangent2, float t); // As glTF interpolates
Quaternion QuaternionFromVector3ToVector3(Vector3 from, Vector3 to); // The shortest turn from one to the other
void QuaternionToAxisAngle(Quaternion q, out Vector3 outAxis, out float outAngle); // Its axis and angle
Quaternion QuaternionFromEuler(float pitch, float yaw, float roll); // About X, Y and Z, as raymath composes them
Vector3 QuaternionToEuler(Quaternion q);                 // Its angles about X, Y and Z
Quaternion QuaternionTransform(Quaternion q, Matrix4x4 mat); // Its components through a matrix
bool QuaternionEquals(Quaternion p, Quaternion q);       // The same turn within a millionth
```

## ImGui

Every `ImGui.*` call works between `BeginDrawing` and `EndDrawing`, and is not wrapped here. ImGui
is drawn over everything else, and the keyboard and the first gamepad move through its widgets.
F2 shows the engine's performance window.
