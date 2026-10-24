# Cheatsheet

Every public function of the flat API, one line each, grouped as raylib groups its own. A program
imports them with `using static Engine.E3D;`. [DESIGN.md](.github/DESIGN.md) has the rules they follow.
Each list is written by `build/cheatsheet.sh` from the files of `3DEngine/Api` its comment names,
a function's line the first sentence of its documentation, and the text around the lists by hand.

## Window and timing

<!-- written by build/cheatsheet.sh from E3D.ConfigFlags.cs, E3D.Window.cs, E3D.DebugOverlay.cs, E3D.Display.cs, E3D.cs, E3D.Profile.cs, E3D.Random.cs -->
```csharp
void SetConfigFlags(ConfigFlags flags);                  // Asks the next InitWindow for these flags, added to any asked before, as raylib's does
void SetConfigSamples(int samples);                      // How many samples a pixel of the next window is drawn with, 1 for none or 2, 4 or 8, rounded down to what the device can do
void SetWindowState(ConfigFlags flags);                  // Turns flags on for the open window, among fullscreen, borderless windowed, resizable, undecorated, hidden, minimized, maximized, topmost, unfocused, always run and vsync
void ClearWindowState(ConfigFlags flags);                // Turns flags off for the open window, as SetWindowState turns them on
bool IsWindowState(ConfigFlags flags);                   // Whether the open window has every one of the flags, its vsync as last set and its MSAA as it opened with

void InitWindow(int width, int height, string title);    // Opens a window and builds the app behind it
void CloseWindow();                                      // Runs Stage.Cleanup, closes the window and frees what the app holds
bool WindowShouldClose();                                // Whether the window was asked to close, by its close button or by the exit key
bool IsWindowReady();                                    // Whether InitWindow has opened a window that is still open
void EnableEventWaiting();                               // Has WindowShouldClose wait for input or a window event before the frame goes on, up to a tenth of a second, so a tool that changes only when used leaves the CPU idle
void DisableEventWaiting();                              // Has WindowShouldClose return at once again, as a game drawing every frame needs
void SetExitKey(Key key);                                // Sets the key that makes WindowShouldClose return true
void SetWindowTitle(string title);                       // Sets the window's title
int GetScreenWidth();                                    // The window's width, in the units mouse positions and 2D drawing use
int GetScreenHeight();                                   // The window's height, in the units mouse positions and 2D drawing use
void TakeScreenshot(string fileName);                    // Writes the frame being drawn to a PNG file, once it is presented at EndDrawing
void StartRecording(string fileName, int intervalMs = 100, float scale = 1);  // Starts recording what the window shows as an animated GIF or PNG, by the extension of fileName, a picture every intervalMs milliseconds of the app's clock, until StopRecording, as raylib's examples record a GIF
void StopRecording();                                    // Stops the recording StartRecording started, whose file is written once its last pictures are back
void SetScreenshotKey(Key key);                          // Sets the key that writes a screenshot in every program, F2 until moved, as Minecraft takes one, or turns it off with Key.Null

void SetTargetFPS(int fps);                              // Caps the frame rate
float GetFrameTime();                                    // Seconds the last frame took
double GetTime();                                        // Seconds since the window opened, read from the clock as it is called, as raylib's GetTime is
int GetFPS();                                            // Frames per second, smoothed over the last few frames
void PollInputEvents();                                  // Processes the window's pending events, as raylib's PollInputEvents does, unless the frame has already, which WindowShouldClose and BeginDrawing do
void SwapScreenBuffer();                                 // Shows the frame drawn, as raylib's SwapScreenBuffer does, which EndDrawing has done already

bool BeginUi();                                          // Opens the scope of the program's interface, which F1 hides, and answers whether it is drawn this frame, false while it is hidden
void EndUi();                                            // Closes the scope BeginUi opened, dropping what was drawn inside it while the interface is hidden
bool IsUiHidden();                                       // Whether F1 has hidden the interface drawn inside BeginUi
void SetUiHiddenKey(Key key);                            // Sets the key that hides and shows the program's interface, F1 until moved, as Minecraft hides its HUD, or turns it off with Key.Null
void SetDebugOverlayKey(Key key);                        // Sets the key that shows and hides the engine's debug overlay, F3 until moved, as Minecraft's debug screen, or turns it off with Key.Null
void AddDebugOverlaySection(string name, Func<IEnumerable<string>> lines);  // Adds a section of the program's own to the debug overlay's left column, under the engine's lines and any section added before, in place of one of the same name
bool RemoveDebugOverlaySection(string name);             // Takes a section of the debug overlay away by its name, and says whether there was one

bool IsWindowResized();                                  // Whether the window's size changed in the events of this frame
bool IsWindowFullscreen();                               // Whether the window covers its monitor in fullscreen, by ToggleFullscreen or the flag for it, rather than as ToggleBorderlessWindowed covers it, as raylib's tells the two apart
bool IsWindowMinimized();                                // Whether the window is minimized
bool IsWindowMaximized();                                // Whether the window is maximized
bool IsWindowFocused();                                  // Whether the window has the keyboard focus
bool IsWindowHidden();                                   // Whether the window is hidden, as it is in a --hidden run
void ToggleFullscreen();                                 // Switches the window between fullscreen on its monitor and a window
void MaximizeWindow();                                   // Maximizes the window
void MinimizeWindow();                                   // Minimizes the window
void RestoreWindow();                                    // Restores a minimized or maximized window to its size before
void SetWindowSize(int width, int height);               // Resizes the window, or the images an offscreen run draws into
void SetWindowMinSize(int width, int height);            // Sets the smallest size the window can be resized to
void SetWindowMaxSize(int width, int height);            // Sets the largest size the window can be resized to
void SetWindowPosition(int x, int y);                    // Moves the window's top left corner to a position on the desktop
Vector2 GetWindowPosition();                             // The window's top left corner on the desktop, or zero with no window

int GetMonitorCount();                                   // How many monitors are connected, or 0 with no video
int GetCurrentMonitor();                                 // The index of the monitor the window is on, or 0
int GetMonitorWidth(int monitor);                        // A monitor's width in pixels, in its current mode, or 0 when there is no such monitor
int GetMonitorHeight(int monitor);                       // A monitor's height in pixels, in its current mode, or 0 when there is no such monitor
int GetMonitorPhysicalWidth(int monitor);                // A monitor's width in millimeters, as raylib's SDL backend works it out, or 0 when there is no such monitor
int GetMonitorPhysicalHeight(int monitor);               // A monitor's height in millimeters, as GetMonitorPhysicalWidth works it out
int GetMonitorRefreshRate(int monitor);                  // A monitor's refresh rate in hertz, rounded, or 0 when it is not known
string GetMonitorName(int monitor);                      // A monitor's name, or an empty string when there is no such monitor
MonitorMode[] GetMonitorModes(int monitor);              // The modes a monitor can be set to in fullscreen, largest first, or none when there is no such monitor
void SetWindowFullscreenMode(MonitorMode mode);          // Makes the window fullscreen on its monitor in the mode closest to mode, changing the monitor's resolution, or at the desktop's mode when it is the default
void SetWindowMonitor(int monitor);                      // Moves the window to a monitor, centered on it

void SetWindowOpacity(float opacity);                    // Sets how opaque the window is, from 0 (clear) to 1, where the desktop allows it
void SetWindowFocused();                                 // Raises the window and asks for the keyboard focus, which the desktop may refuse
int GetRenderWidth();                                    // The width in pixels the window's content is drawn at, which on a doubled monitor is twice GetScreenWidth
int GetRenderHeight();                                   // The height in pixels the window's content is drawn at
Vector2 GetMonitorPosition(int monitor);                 // A monitor's top left corner on the desktop, or zero when there is no such monitor
void ToggleBorderlessWindowed();                         // Switches between a window and a borderless one covering the monitor at its desktop mode, which changes no display mode and so switches at once
Vector2 GetWindowScaleDPI();                             // How many pixels the window's content has for each unit of its size, 1 on most monitors and 2 on a doubled one
void SetWindowIcon(Image image);                         // Sets the window's icon from an image, which the desktop scales to what it shows
void SetWindowIcons(Image[] images);                     // Sets the window's icon from several sizes of one picture, the first the size it is drawn at most and the rest alternates the desktop picks from where it draws the icon larger or smaller
nint GetWindowHandle();                                  // The window's SDL window, for a library of the program's own that needs it, or zero with no window
Image GetClipboardImage();                               // An image on the system clipboard, as a PNG, JPEG or BMP copied there, or an invalid image when it holds none
void SetClipboardText(string text);                      // Puts text on the system clipboard
string GetClipboardText();                               // The text on the system clipboard, or an empty string

App GetApp();                                            // The app InitWindow built, for adding plugins, systems and resources

void SetProfileValue(string name, double value);         // Sets a number of the program's own in the frame profile, which e3d command profile reports, as a stress test's count
double GetProfileAverage(string name);                   // A measured average from the frame profile in milliseconds, as "work", "frame" or "gpu.models", or 0 before it was measured
void DrawProfileWindow();                                // Draws the frame profile in an ImGui window of its own, which a program shows while it is worked on, with the frame and its work, the program's own values, and each group of averages, largest first, which e3d command profile reports too

void SetRandomSeed(uint seed);                           // Sets the seed of the generator GetRandomValue, LoadRandomSequence and the noise images draw from, so the same seed gives the same values in the same order
int GetRandomValue(int min, int max);                    // A random whole number from min to max, both included
int[] LoadRandomSequence(int count, int min, int max);   // count different whole numbers from min to max, both included, in random order, or an empty array when the range holds fewer than that
```

## Frame and cameras

<!-- written by build/cheatsheet.sh from E3D.Drawing.cs, E3D.Vr.cs, E3D.Cameras.cs, E3D.Camera2D.cs, E3D.Camera3D.cs -->
```csharp
void BeginDrawing();                                     // Starts a frame
void EndDrawing();                                       // Ends the frame
void ClearBackground(Color color);                       // Sets the color the frame, or the render target inside BeginTextureMode, is cleared to

RenderTexture2D LoadRenderTexture(int width, int height);  // Makes an image of width by height pixels that drawing can be sent to, with its depth to sample as RenderTexture2D.Depth
RenderTexture2D LoadRenderTexture(int width, int height, params PixelFormat[] formats);  // Makes an image of width by height pixels for each of formats, up to four, that drawing is sent to at once, as a G-buffer is drawn into, with one depth to sample as RenderTexture2D.Depth
RenderTexture2D LoadRenderTextureEx(int width, int height, PixelFormat format);  // An image drawing can be sent to, of one format, as raylib's LoadRenderTextureEx makes one
RenderTexture2D LoadRenderTextureEx(int width, int height, PixelFormat format, int samples);  // An image drawing can be sent to, of one format, drawn at samples samples a pixel, 1 for edges as hard as raylib's render textures have and any other for the window's
void UnloadRenderTexture(RenderTexture2D target);        // Frees a render texture, each image it draws into and its depth
bool IsRenderTextureValid(RenderTexture2D target);       // Whether target is loaded
void BeginTextureMode(RenderTexture2D target);           // Sends the following drawing into target until EndTextureMode, in pixels from its top left corner
void EndTextureMode();                                   // Returns drawing to the window

void BeginBlendMode(BlendMode mode);                     // Lays the following shapes, textures and text over what is there by mode until EndBlendMode
void EndBlendMode();                                     // Returns to laying what is drawn over by its alpha
void BeginScissorMode(int x, int y, int width, int height);  // Keeps the following drawing to a rectangle, in pixels from the top left of the window, or of the render target inside BeginTextureMode, until EndScissorMode
void EndScissorMode();                                   // Returns to drawing over the whole window or render target

void BeginMode3D(Camera3D camera);                       // Draws the following shapes through camera, depth tested, until EndMode3D
void EndMode3D();                                        // Returns to drawing in screen space, in pixels from the top left corner

VrStereoConfig LoadVrStereoConfig(VrDeviceInfo device);  // Works out how to draw for two eyes from a head-mounted display's measures, each eye's projection and offset, and the lens parameters raylib's distortion shader reads
void UnloadVrStereoConfig(VrStereoConfig config);        // Lets a stereo config go, which holds nothing to free, as raylib's holds nothing
void BeginVrStereoMode(VrStereoConfig config);           // Draws what follows inside BeginMode3D once for each eye, the left eye's in the left half of the window or render texture and the right eye's in the right, through each eye's offset from the camera and its projection, until EndVrStereoMode
void EndVrStereoMode();                                  // Draws in 3D through one camera again, as BeginVrStereoMode began otherwise

Vector3 GetCameraForward(Camera3D camera);               // The way camera looks, from its position to its target, of length one
Vector3 GetCameraUp(Camera3D camera);                    // The camera's up, of length one, which need not be square to the way it looks
Vector3 GetCameraRight(Camera3D camera);                 // The camera's right, square to the way it looks and its up, of length one
void CameraMoveForward(ref Camera3D camera, float distance, bool moveInWorldPlane);  // Moves a camera and its target the way it looks by distance, kept level on the plane its up stands on when moveInWorldPlane, as one walks
void CameraMoveUp(ref Camera3D camera, float distance);  // Moves a camera and its target along its up by distance
void CameraMoveRight(ref Camera3D camera, float distance, bool moveInWorldPlane);  // Moves a camera and its target to its right by distance, kept level when moveInWorldPlane
void CameraMoveToTarget(ref Camera3D camera, float delta);  // Moves a camera delta farther from its target, or nearer when negative, no nearer than a thousandth
void CameraYaw(ref Camera3D camera, float angle, bool rotateAroundTarget);  // Turns a camera left by angle radians about its up, or right when negative, about its target when rotateAroundTarget and about itself otherwise
void CameraPitch(ref Camera3D camera, float angle, bool lockView, bool rotateAroundTarget, bool rotateUp);  // Tips a camera up by angle radians about its right, or down when negative
void CameraRoll(ref Camera3D camera, float angle);       // Rolls a camera by angle radians about the way it looks, as a head tilts
Matrix4x4 GetCameraViewMatrix(Camera3D camera);          // The camera's view, the world to the camera, as GetCameraMatrix gives it
Matrix4x4 GetCameraProjectionMatrix(Camera3D camera, float aspect);  // The camera's projection for a picture of aspect, its width over its height, as the engine draws with it
void UpdateCameraPro(ref Camera3D camera, Vector3 movement, Vector3 rotation, float zoom);  // Moves and turns a camera by amounts the program works out, as raylib's does
void UpdateCamera(ref Camera3D camera, CameraMode mode);  // Moves camera from this frame's input, as raylib's does in mode

void BeginMode2D(Camera2D camera);                       // Draws the following 2D calls through camera, in world units, until EndMode2D
void EndMode2D();                                        // Returns to drawing in screen pixels
Matrix4x4 GetCameraMatrix2D(Camera2D camera);            // The camera's world to screen transform
Vector2 GetWorldToScreen2D(Vector2 position, Camera2D camera);  // Where a world point appears on the screen through a 2D camera
Vector2 GetScreenToWorld2D(Vector2 position, Camera2D camera);  // The world point under a screen point through a 2D camera, as the mouse pointer's

Matrix4x4 GetCameraMatrix(Camera3D camera);              // The camera's world to view transform, as raylib's GetCameraMatrix gives it
Vector2 GetWorldToScreen(Vector3 position, Camera3D camera);  // Where a world point appears in the window through a 3D camera, in pixels from the top left
Vector2 GetWorldToScreenEx(Vector3 position, Camera3D camera, int width, int height);  // Where a world point appears in a view width by height pixels through a 3D camera
bool IsPointInFrontOfCamera(Vector3 position, Camera3D camera);  // Whether a world point is in front of a camera, so that GetWorldToScreen places it where it shows
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

<!-- written by build/cheatsheet.sh from E3D.Input.cs, E3D.Automation.cs -->
```csharp
bool IsKeyPressed(Key key);                              // Whether key went down this frame
bool IsKeyDown(Key key);                                 // Whether key is held
bool IsKeyReleased(Key key);                             // Whether key came up this frame
bool IsKeyPressedRepeat(Key key);                        // Whether the system repeated a held key this frame, as it does in a text field
bool IsKeyUp(Key key);                                   // Whether key is not held
Key GetKeyPressed();                                     // The next key pressed this frame, in the order pressed, or Key.Null when none is left
int GetCharPressed();                                    // The next character typed this frame as a Unicode code point, or 0 when none is left
void SetTextInputArea(Rectangle area, int cursor = 0);   // Places the input method's window, where a language composes a character from several keys, beside area of the window, the text box a program reads GetCharPressed into, its caret cursor pixels from the box's left

bool IsMouseButtonPressed(MouseButton button);           // Whether button went down this frame
bool IsMouseButtonDown(MouseButton button);              // Whether button is held
bool IsMouseButtonReleased(MouseButton button);          // Whether button came up this frame
bool IsMouseButtonUp(MouseButton button);                // Whether button is not held

int GetTouchPointCount();                                // How many fingers are on the screen, or 1 while the left mouse button is held with none
Vector2 GetTouchPosition(int index);                     // Where finger index is, in window pixels, or the pointer for 0 with no finger down
int GetTouchPointId(int index);                          // The id finger index keeps while it stays down, or -1 when there is no such finger
int GetTouchX();                                         // The first finger's horizontal position, or the pointer's with no finger down
int GetTouchY();                                         // The first finger's vertical position, or the pointer's with no finger down

void SetGesturesEnabled(Gesture flags);                  // Which gestures are recognized, all of them to begin with
bool IsGestureDetected(Gesture gesture);                 // Whether gesture is this frame's gesture
Gesture GetGestureDetected();                            // This frame's gesture, Gesture.None when there is none
float GetGestureHoldDuration();                          // How long the current hold has lasted, in seconds
Vector2 GetGestureDragVector();                          // How far the current drag has gone, in fractions of the window
float GetGestureDragAngle();                             // The angle of the last drag or swipe, in degrees counterclockwise from right
Vector2 GetGesturePinchVector();                         // The vector between a pinch's two fingers, in fractions of the window
float GetGesturePinchAngle();                            // The angle between a pinch's two fingers, in degrees
Vector2 GetMousePosition();                              // The pointer's position in the window, from the top left corner
void SetMouseOffset(int offsetX, int offsetY);           // Adds an offset to the pointer's position before its scale, as raylib's does
void SetMouseScale(float scaleX, float scaleY);          // Scales the pointer's position, after its offset, so it reads in the pixels of a texture drawn scaled to the window
int GetMouseX();                                         // The pointer's horizontal position in the window
void SetMousePosition(int x, int y);                     // Moves the pointer to a place in the window, which GetMousePosition reports from then on
int GetMouseY();                                         // The pointer's vertical position in the window
Vector2 GetMouseDelta();                                 // How far the pointer moved this frame
float GetMouseWheelMove();                               // How far the wheel turned this frame, positive away from the user
Vector2 GetMouseWheelMoveV();                            // How far the wheel turned this frame on both axes, x for a wheel or trackpad that scrolls sideways
void SetMouseCursor(MouseCursor cursor);                 // Sets the pointer's shape over the window, as a text field's or a link's

void ShowCursor();                                       // Shows the mouse cursor over the window
void HideCursor();                                       // Hides the mouse cursor over the window
bool IsCursorHidden();                                   // Whether the cursor is hidden, by HideCursor or DisableCursor
bool IsCursorOnScreen();                                 // Whether the pointer is over the window, the window having the mouse's focus
string GetKeyName(Key key);                              // The name of a key as the keyboard's layout prints it, as "Q" for Key.A on a French layout, or an empty string with no window or for a key with no name
int SetGamepadMappings(string mappings);                 // Adds gamepad mappings in SDL_GameControllerDB's form, one a line, so pads the engine does not know are read as their makers lay them out, and says how many were added
void DisableCursor();                                    // Hides the cursor and holds it in the window, so the mouse only reports movement, as a first-person camera needs
void EnableCursor();                                     // Releases and shows the cursor that DisableCursor held

bool IsGamepadAvailable(int gamepad);                    // Whether a gamepad is connected at gamepad
string GetGamepadName(int gamepad);                      // The name of the gamepad at gamepad, or an empty string
bool IsGamepadButtonPressed(int gamepad, GamepadButton button);  // Whether button went down this frame
GamepadButton? GetGamepadButtonPressed();                // The button pressed last on any gamepad, while it is held, as a "press any button" screen or a binding waiting for a button asks, or null for none
bool IsGamepadButtonDown(int gamepad, GamepadButton button);  // Whether button is held
bool IsGamepadButtonReleased(int gamepad, GamepadButton button);  // Whether button came up this frame
bool IsGamepadButtonUp(int gamepad, GamepadButton button);  // Whether button is not held, which is true of a pad that is not connected
float GetGamepadAxisMovement(int gamepad, GamepadAxis axis);  // An axis, from -1 to 1 for sticks and 0 to 1 for triggers
int GetGamepadAxisCount(int gamepad);                    // How many axes a gamepad has, which is six for every pad SDL maps
bool IsGamepadMotionAvailable(int gamepad);              // Whether a gamepad reports its motion, through a gyro and an accelerometer, as a DualSense and a Switch Pro Controller do
Vector3 GetGamepadGyro(int gamepad);                     // How fast a gamepad turns about its own axes, in radians a second, x across it, y up out of it and z toward the player, as SDL reports it, or zero for a pad without a gyro
Vector3 GetGamepadAccelerometer(int gamepad);            // A gamepad's acceleration along its own axes, in meters a second squared with gravity in it, or zero for a pad without one
int GetGamepadTouchCount(int gamepad);                   // How many fingers are on a gamepad's touchpad
Vector2 GetGamepadTouchPosition(int gamepad, int index);  // Where a finger is on a gamepad's touchpad, from 0 to 1 across and down it, by its index among the fingers on it, or zero for none
void SetGamepadLight(int gamepad, Color color);          // Sets the color of a gamepad's light, as a DualSense's bar, which a pad without one leaves as it is
void SetGamepadVibration(int gamepad, float leftMotor, float rightMotor, float seconds);  // Rumbles a gamepad, each motor from 0 to 1, for seconds
void SetImGuiGamepadNavigation(bool enabled);            // Sets whether the first gamepad moves through ImGui's windows and presses their widgets, as a menu played with the pad alone needs

AutomationEventList LoadAutomationEventList(string? fileName);  // A list of automation events loaded from a file ExportAutomationEventList wrote, or an empty one to record into when fileName is null
void UnloadAutomationEventList(AutomationEventList list);  // Lets a list go, and stops recording into it if it is the list set
bool ExportAutomationEventList(AutomationEventList list, string fileName);  // Writes a list's events to a text file in raylib's format, which LoadAutomationEventList reads back
void SetAutomationEventList(AutomationEventList list);   // Sets the list StartAutomationEventRecording records into
void SetAutomationEventBaseFrame(int frame);             // Sets the frame count the next frame's events are recorded at, from which it goes on counting
void StartAutomationEventRecording();                    // Records the input of each frame from this one on into the list set, at the end of the frame, as EndDrawing begins
void StopAutomationEventRecording();                     // Stops recording automation events
void PlayAutomationEvent(AutomationEvent automationEvent);  // Plays an event, setting the key, button, pointer, wheel, finger, gamepad or gesture it records as if it had happened now, or doing what a window or action event records
```

Pads are indexed in the order they connected. Buttons are named by position, as raylib names
them (`RightFaceDown` for A or the cross, `RightFaceRight`, `LeftFaceUp` on the directional pad,
`LeftTrigger1`, `MiddleRight` for start, ...), rather than by the letter printed on them.

## 2D shapes

<!-- written by build/cheatsheet.sh from E3D.Shapes.cs, E3D.Splines.cs -->
```csharp
void DrawLine(int startX, int startY, int endX, int endY, Color color);  // Draws a line
void DrawLineV(Vector2 start, Vector2 end, Color color);  // Draws a line
void DrawTriangle(Vector2 v1, Vector2 v2, Vector2 v3, Color color);  // Draws a filled triangle
void DrawRectangle(int x, int y, int width, int height, Color color);  // Draws a filled rectangle
void DrawRectangleV(Vector2 position, Vector2 size, Color color);  // Draws a filled rectangle
void DrawRectangleLines(int x, int y, int width, int height, Color color);  // Draws a rectangle's outline
void DrawCircle(int centerX, int centerY, float radius, Color color);  // Draws a filled circle
void DrawCircleV(Vector2 center, float radius, Color color);  // Draws a filled circle
void DrawCircleLines(int centerX, int centerY, float radius, Color color);  // Draws a circle's outline
void DrawPixel(int x, int y, Color color);               // Draws a pixel
void DrawPixelV(Vector2 position, Color color);          // Draws a pixel
void DrawLineEx(Vector2 start, Vector2 end, float thick, Color color);  // Draws a line thick pixels wide
void DrawLineDashed(Vector2 startPos, Vector2 endPos, int dashSize, int spaceSize, Color color);  // Draws a line in dashes dashSize pixels long with gaps of spaceSize between them
void DrawLineStrip(ReadOnlySpan<Vector2> points, Color color);  // Draws lines joining each point to the next
void DrawLineBezier(Vector2 start, Vector2 end, float thick, Color color);  // Draws a curve from start to end, easing in and out, thick pixels wide
void DrawRectangleRec(Rectangle rec, Color color);       // Draws a filled rectangle
void DrawRectanglePro(Rectangle rec, Vector2 origin, float rotation, Color color);  // Draws a filled rectangle turned rotation degrees about origin, a point measured from its top left corner, which lands at the rectangle's x and y
void DrawRectangleGradientV(int x, int y, int width, int height, Color top, Color bottom);  // Draws a filled rectangle blending from top to bottom
void DrawRectangleGradientH(int x, int y, int width, int height, Color left, Color right);  // Draws a filled rectangle blending from left to right
void DrawRectangleGradientEx(Rectangle rec, Color topLeft, Color bottomLeft, Color bottomRight, Color topRight);  // Draws a filled rectangle blending between a color at each corner, given counterclockwise from the top left
void DrawRectangleLinesEx(Rectangle rec, float lineThick, Color color);  // Draws a rectangle's outline lineThick pixels wide, inside its edge, or outside it for a negative width
void DrawRectangleRounded(Rectangle rec, float roundness, int segments, Color color);  // Draws a filled rectangle with rounded corners, roundness from 0 (square) to 1 (corners as round as the shorter side allows), each corner in segments pieces, or as many as its size needs at 0
void DrawRectangleRoundedLines(Rectangle rec, float roundness, int segments, Color color);  // Draws the outline of a rectangle with rounded corners, as DrawRectangleRounded shapes it
void DrawRectangleRoundedLinesEx(Rectangle rec, float roundness, int segments, float lineThick, Color color);  // Draws a rounded rectangle's outline lineThick pixels wide, inside its edge, or outside it for a negative width, as raylib's does
void DrawTriangleGradient(Vector2 v1, Vector2 v2, Vector2 v3, Color c1, Color c2, Color c3);  // Draws a filled triangle with a color at each corner, blended across it
void DrawTriangleLinesEx(Vector2 v1, Vector2 v2, Vector2 v3, float thick, Color color);  // Draws a triangle's outline thick pixels wide, inside its edges, or outside them for a negative width
void DrawTriangleLines(Vector2 v1, Vector2 v2, Vector2 v3, Color color);  // Draws a triangle's outline
void DrawTriangleFan(ReadOnlySpan<Vector2> points, Color color);  // Draws triangles fanning out from the first point through each pair of the rest
void DrawTriangleStrip(ReadOnlySpan<Vector2> points, Color color);  // Draws a strip of triangles, each from three points in a row, every other one taken in the opposite order so that all face the way the first does, as raylib's are
void DrawPoly(Vector2 center, int sides, float radius, float rotation, Color color);  // Draws a filled regular polygon of sides sides, turned rotation degrees
void DrawPolyLines(Vector2 center, int sides, float radius, float rotation, Color color);  // Draws a regular polygon's outline
void DrawPolyLinesEx(Vector2 center, int sides, float radius, float rotation, float lineThick, Color color);  // Draws a regular polygon's outline lineThick pixels wide across each side, inside its edge, or outside it for a negative width
void DrawCircleSector(Vector2 center, float radius, float startAngle, float endAngle, int segments, Color color);  // Draws a filled slice of a circle from startAngle to endAngle degrees, clockwise on the screen from right
void DrawCircleSectorLines(Vector2 center, float radius, float startAngle, float endAngle, int segments, Color color);  // Draws a slice of a circle's outline, with its two radii
void DrawCircleSectorLinesEx(Vector2 center, float radius, float startAngle, float endAngle, int segments, float thick, Color color);  // Draws a slice of a circle's outline thick pixels wide, with its two radii, inside its edge, or outside it for a negative width
void DrawCircleLinesEx(Vector2 center, float radius, float thick, Color color);  // Draws a circle's outline thick pixels wide, inside its edge, or outside it for a negative width
void DrawCircleGradient(Vector2 center, float radius, Color inner, Color outer);  // Draws a filled circle blending from inner at its middle to outer at its edge
void DrawCircleLinesV(Vector2 center, float radius, Color color);  // Draws a circle's outline
void DrawEllipse(int centerX, int centerY, float radiusH, float radiusV, Color color);  // Draws a filled ellipse
void DrawEllipseLines(int centerX, int centerY, float radiusH, float radiusV, Color color);  // Draws an ellipse's outline
void DrawEllipseV(Vector2 center, float radiusH, float radiusV, Color color);  // Draws a filled ellipse around a point
void DrawEllipseLinesV(Vector2 center, float radiusH, float radiusV, Color color);  // Draws an ellipse's outline around a point
void DrawEllipseLinesEx(Vector2 center, float radiusH, float radiusV, float thick, Color color);  // Draws an ellipse's outline thick pixels wide, inside its edge, or outside it for a negative width, filled where the width leaves no hole
void DrawRing(Vector2 center, float innerRadius, float outerRadius, float startAngle, float endAngle, int segments, Color color);  // Draws a filled ring between innerRadius and outerRadius, from startAngle to endAngle degrees, clockwise on the screen from right, in segments pieces, or where that is fewer than one a quarter turn, in as many as keep it within half a pixel of the circle, as raylib draws it
void DrawRingLines(Vector2 center, float innerRadius, float outerRadius, float startAngle, float endAngle, int segments, Color color);  // Draws a ring's outline, its two arcs and, for less than a whole turn, its two ends
void DrawRingLinesEx(Vector2 center, float innerRadius, float outerRadius, float startAngle, float endAngle, int segments, float thick, Color color);  // Draws a ring's outline thick pixels wide, with its two ends, inside its edges, or outside them for a negative width

void DrawSplineLinear(ReadOnlySpan<Vector2> points, float thick, Color color);  // Draws lines joining each point to the next, thick pixels wide, with their corners joined
void DrawSplineBasis(ReadOnlySpan<Vector2> points, float thick, Color color);  // Draws a uniform B-spline through the control points, at least four, which passes near rather than through them, thick pixels wide
void DrawSplineCatmullRom(ReadOnlySpan<Vector2> points, float thick, Color color);  // Draws a Catmull-Rom spline through points, at least four, which passes through every point but the first and last, thick pixels wide
void DrawSplineBezierQuadratic(ReadOnlySpan<Vector2> points, float thick, Color color);  // Draws quadratic Bezier curves joined end to end, points given as start, control, end, control, end and so on, at least three, thick pixels wide
void DrawSplineBezierCubic(ReadOnlySpan<Vector2> points, float thick, Color color);  // Draws cubic Bezier curves joined end to end, points given as start, two controls, end, two controls, end and so on, at least four, thick pixels wide
void DrawSplineSegmentLinear(Vector2 p1, Vector2 p2, float thick, Color color);  // Draws one line of a linear spline, thick pixels wide
void DrawSplineSegmentBasis(Vector2 p1, Vector2 p2, Vector2 p3, Vector2 p4, float thick, Color color);  // Draws one segment of a B-spline, from its four control points
void DrawSplineSegmentCatmullRom(Vector2 p1, Vector2 p2, Vector2 p3, Vector2 p4, float thick, Color color);  // Draws one segment of a Catmull-Rom spline, from p2 to p3
void DrawSplineSegmentBezierQuadratic(Vector2 p1, Vector2 c2, Vector2 p3, float thick, Color color);  // Draws one quadratic Bezier curve from p1 to p3, pulled toward c2
void DrawSplineSegmentBezierCubic(Vector2 p1, Vector2 c2, Vector2 c3, Vector2 p4, float thick, Color color);  // Draws one cubic Bezier curve from p1 to p4, pulled toward c2 and c3
Vector2 GetSplinePointLinear(Vector2 startPos, Vector2 endPos, float t);  // The point t of the way from startPos to endPos, from 0 to 1
Vector2 GetSplinePointBasis(Vector2 p1, Vector2 p2, Vector2 p3, Vector2 p4, float t);  // The point at t, from 0 to 1, along the B-spline segment of four control points
Vector2 GetSplinePointCatmullRom(Vector2 p1, Vector2 p2, Vector2 p3, Vector2 p4, float t);  // The point at t, from 0 to 1, along the Catmull-Rom segment from p2 to p3
Vector2 GetSplinePointBezierQuadratic(Vector2 p1, Vector2 c2, Vector2 p3, float t);  // The point at t, from 0 to 1, along the quadratic Bezier curve
Vector2 GetSplinePointBezierCubic(Vector2 p1, Vector2 c2, Vector2 c3, Vector2 p4, float t);  // The point at t, from 0 to 1, along the cubic Bezier curve
```

Angles are in degrees, clockwise on the screen from right, and a segment count of 0 lets the
size choose how many pieces a curve is drawn in.

## Collision

<!-- written by build/cheatsheet.sh from E3D.Collision.cs -->
```csharp
bool CheckCollisionRecs(Rectangle a, Rectangle b);       // Whether two rectangles overlap
bool CheckCollisionCircles(Vector2 center1, float radius1, Vector2 center2, float radius2);  // Whether two circles overlap
bool CheckCollisionCircleRec(Vector2 center, float radius, Rectangle rec);  // Whether a circle and a rectangle overlap
bool CheckCollisionPointRec(Vector2 point, Rectangle rec);  // Whether a point is inside a rectangle
bool CheckCollisionPointCircle(Vector2 point, Vector2 center, float radius);  // Whether a point is inside a circle
Rectangle GetCollisionRec(Rectangle a, Rectangle b);     // The rectangle two rectangles share, empty when they do not overlap
bool CheckCollisionPointTriangle(Vector2 point, Vector2 p1, Vector2 p2, Vector2 p3);  // Whether point is inside the triangle, or on its edge
bool CheckCollisionPointLine(Vector2 point, Vector2 p1, Vector2 p2, int threshold);  // Whether point is within threshold pixels of the segment from p1 to p2
bool CheckCollisionPointPoly(Vector2 point, ReadOnlySpan<Vector2> points);  // Whether point is inside the polygon whose corners are points, in order
bool CheckCollisionLines(Vector2 startPos1, Vector2 endPos1, Vector2 startPos2, Vector2 endPos2, out Vector2 collisionPoint);  // Whether two segments cross, with where in collisionPoint
bool CheckCollisionCircleLine(Vector2 center, float radius, Vector2 p1, Vector2 p2);  // Whether a circle touches the segment from p1 to p2

bool CheckCollisionSpheres(Vector3 center1, float radius1, Vector3 center2, float radius2);  // Whether two spheres overlap
bool CheckCollisionBoxes(BoundingBox box1, BoundingBox box2);  // Whether two boxes overlap
bool CheckCollisionBoxSphere(BoundingBox box, Vector3 center, float radius);  // Whether a box and a sphere overlap
RayCollision GetRayCollisionSphere(Ray ray, Vector3 center, float radius);  // Where a ray first meets a sphere, from outside or within it
RayCollision GetRayCollisionBox(Ray ray, BoundingBox box);  // Where a ray first meets a box, by the slab test, from outside or within it
RayCollision GetRayCollisionTriangle(Ray ray, Vector3 p1, Vector3 p2, Vector3 p3);  // Where a ray meets a triangle, from either side, by the Möller-Trumbore test
RayCollision GetRayCollisionQuad(Ray ray, Vector3 p1, Vector3 p2, Vector3 p3, Vector3 p4);  // Where a ray meets a quad of corners in order around its edge
RayCollision GetRayCollisionMesh(Ray ray, ModelMesh mesh, Matrix4x4 transform);  // Where a ray first meets a mesh placed by transform, testing each of its triangles
BoundingBox GetMeshBoundingBox(ModelMesh mesh);          // The box around a mesh's vertices, in its own space
```

## 3D shapes

<!-- written by build/cheatsheet.sh from E3D.Shapes3D.cs -->
```csharp
void DrawLine3D(Vector3 start, Vector3 end, Color color);  // Draws a line in 3D
void DrawTriangle3D(Vector3 v1, Vector3 v2, Vector3 v3, Color color);  // Draws a filled triangle in 3D
void DrawCube(Vector3 position, float width, float height, float length, Color color);  // Draws a box centered on position
void DrawCubeV(Vector3 position, Vector3 size, Color color);  // Draws a box centered on position
void DrawCubeWires(Vector3 position, float width, float height, float length, Color color);  // Draws a box's edges
void DrawCubeWiresV(Vector3 position, Vector3 size, Color color);  // Draws a box's edges
void DrawSphere(Vector3 center, float radius, Color color);  // Draws a sphere of 16 rings and 16 slices
void DrawSphereEx(Vector3 center, float radius, int rings, int slices, Color color);  // Draws a sphere with the given number of rings and slices
void DrawSphereWires(Vector3 center, float radius, int rings, int slices, Color color);  // Draws a sphere's rings and slices as lines, with a diagonal across each face between them, as raylib's does
void DrawPoint3D(Vector3 position, Color color);         // Draws a point, as raylib's does, a line from it a tenth of a unit along z, since a line of no length draws nothing
void DrawRay(Ray ray, Color color);                      // Draws a ray as a line from where it starts, a hundred units along it
void DrawCircle3D(Vector3 center, float radius, Vector3 rotationAxis, float rotationAngle, Color color);  // Draws a circle's outline on the XY plane, turned rotationAngle degrees about rotationAxis
void DrawTriangleStrip3D(ReadOnlySpan<Vector3> points, Color color);  // Draws a strip of triangles, each from three points in a row, every other one taken in the opposite order so that all face the way the first does, as raylib's are
void DrawCylinder(Vector3 position, float radiusTop, float radiusBottom, float height, int slices, Color color);  // Draws an upright cylinder, or a cone with a top radius of 0, its base centered on position, with slices sides
void DrawCylinderEx(Vector3 startPos, Vector3 endPos, float startRadius, float endRadius, int sides, Color color);  // Draws a cylinder or cone from startPos to endPos, with its caps
void DrawCylinderWires(Vector3 position, float radiusTop, float radiusBottom, float height, int slices, Color color);  // Draws an upright cylinder's or cone's edges as lines
void DrawCylinderWiresEx(Vector3 startPos, Vector3 endPos, float startRadius, float endRadius, int sides, Color color);  // Draws a cylinder's or cone's edges as lines, from startPos to endPos
void DrawCapsule(Vector3 startPos, Vector3 endPos, float radius, int rings, int slices, Color color);  // Draws a capsule, a cylinder from startPos to endPos with a half sphere on each end
void DrawCapsuleWires(Vector3 startPos, Vector3 endPos, float radius, int rings, int slices, Color color);  // Draws a capsule's edges as lines
void DrawPlane(Vector3 center, Vector2 size, Color color);  // Draws a flat rectangle on the XZ plane, centered on center
void DrawGrid(int slices, float spacing);                // Draws a grid on the XZ plane centered on the origin, slices cells across
```

## rlgl

raylib's layer under its shapes, the calls its examples make. Vertices go into the frame's draw
list, moved by the matrix stack, which moves every shape, text and model drawn inside a push too.

<!-- written by build/cheatsheet.sh from E3D.Rlgl.cs -->
```csharp
void rlMatrixMode(RlMatrixMode mode);                    // Has the matrix calls after this change mode's matrix, rlgl's modelview, which moves what is drawn, or its projection, as rlgl's rlMatrixMode does
void rlPushMatrix();                                     // Keeps the current transform, or the projection in its mode, which rlPopMatrix returns to, as rlgl's rlPushMatrix does
void rlPopMatrix();                                      // Returns to the transform, or the projection in its mode, the last rlPushMatrix kept
void rlTranslatef(float x, float y, float z);            // Moves what is drawn after by (x, y, z), within the transforms set before, as rlgl's rlTranslatef does
void rlRotatef(float angle, float x, float y, float z);  // Turns what is drawn after by angle degrees about the axis (x, y, z), within the transforms set before
void rlScalef(float x, float y, float z);                // Scales what is drawn after by (x, y, z), within the transforms set before
void rlLoadIdentity();                                   // Makes the modelview, the view with the transforms after it, or the projection in its mode, the identity, as rlgl's rlLoadIdentity does
void rlMultMatrixf(ReadOnlySpan<float> matf);            // Multiplies the matrix of the mode in by sixteen values, in raymath's order, as rlgl's rlMultMatrixf does, what is drawn after moved by them first
void rlSetMatrixProjection(Matrix4x4 proj);              // Sets the projection what is drawn after is seen through, as rlgl's rlSetMatrixProjection does, a projection as Matrix4x4.CreatePerspectiveOffCenter makes one, depth from 0 to 1
void rlEnableDepthTest();                                // Tests what is drawn after against the depth drawn before, as rlgl's rlEnableDepthTest does, in 2D as well as 3D
void rlDisableDepthTest();                               // Draws what follows over what is there whatever its depth, as rlgl's rlDisableDepthTest does
void rlEnableDepthMask();                                // Writes the depth of what is drawn after with the test on, as rlgl's rlEnableDepthMask does, and as the engine does until it is turned off
void rlDisableDepthMask();                               // Tests what is drawn after against the depth there without writing its own, as rlgl's rlDisableDepthMask does, for shapes, text, rlgl's vertices and models, so a sky drawn around the camera leaves what is drawn after it in front
void rlBegin(RlDrawMode mode);                           // Starts giving vertices one at a time, joined as mode says, until rlEnd
void rlEnd();                                            // Ends the vertices rlBegin started
void rlVertex2f(float x, float y);                       // Gives a vertex at (x, y), as rlgl's rlVertex2f does, for 2D
void rlVertex3f(float x, float y, float z);              // Gives a vertex at (x, y, z), as rlgl's rlVertex3f does
void rlTexCoord2f(float x, float y);                     // Sets the texture coordinate of the vertices given after, from 0 to 1 across the texture
void rlNormal3f(float x, float y, float z);              // Takes the normal of the vertices given after, as rlgl's rlNormal3f does, and keeps none, since the vertices of shapes drawn this way are not lit and carry no normal
void rlColor4ub(byte r, byte g, byte b, byte a);         // Sets the color of the vertices given after
void rlColor4f(float r, float g, float b, float a);      // Sets the color of the vertices given after, from four values from 0 to 1
void rlSetTexture(int id);                               // Sets the texture of the primitives given after, by the id of a Texture2D, or 0 for none, which draws them in their colors alone, as rlgl's rlSetTexture does
bool rlCheckRenderBatchLimit(int vertexCount);           // Whether giving vertexCount more vertices would draw what rlgl's batch held first, which is never, since the draw list grows to hold a frame
void rlEnableBackfaceCulling();                          // Leaves out the back faces of what is drawn after, or the front ones after rlSetCullFace, shapes and text as well as models, as rlgl's rlEnableBackfaceCulling does
void rlDisableBackfaceCulling();                         // Draws both faces of what is drawn after, models whatever their material says, as rlgl's rlDisableBackfaceCulling does
void rlSetCullFace(RlCullFace mode);                     // Sets which faces culling leaves out, the back ones unless set, as rlgl's rlSetCullFace does
void rlEnablePointMode();                                // Draws the models drawn after as a point at each corner of their triangles, as rlgl's rlEnablePointMode draws them, where the GPU's driver can, and filled where it cannot
void rlDisablePointMode();                               // Draws the models drawn after filled again
void rlEnableColorBlend();                               // Blends what is drawn after by the blend mode again, as rlgl's rlEnableColorBlend does
void rlDisableColorBlend();                              // Writes what is drawn after as it is, its alpha with it, shapes and models alike, as rlgl's rlDisableColorBlend does, so a texture of a G-buffer keeps in its alpha what a shader puts there
void rlSetBlendFactors(RlBlendFactor glSrcFactor, RlBlendFactor glDstFactor, RlBlendEquation glEquation);  // Sets the factors and the equation BlendMode.Custom combines by, for the color and its alpha alike, as rlgl's rlSetBlendFactors does
void rlSetBlendFactorsSeparate(RlBlendFactor glSrcRGB, RlBlendFactor glDstRGB, RlBlendFactor glSrcAlpha, RlBlendFactor glDstAlpha, RlBlendEquation glEqRGB, RlBlendEquation glEqAlpha);  // Sets the factors and the equations BlendMode.CustomSeparate combines by, the color's apart from the alpha's, as rlgl's rlSetBlendFactorsSeparate does
void rlSetBlendMode(BlendMode mode);                     // Lays what is drawn after over what is there by mode, as BeginBlendMode does, under rlgl's name
void rlDrawRenderBatchActive();                          // Draws what rlgl's batch holds before what follows, as rlgl's rlDrawRenderBatchActive does, which here has nothing to do, since the draw list keeps the shapes of each state apart and in order
```

## Images and textures

<!-- written by build/cheatsheet.sh from E3D.Textures.cs, E3D.PixelFormats.cs, E3D.Images.cs, E3D.ImageEffects.cs, E3D.ImageText.cs -->
```csharp
Image LoadImage(string fileName);                        // Loads an image file (PNG, JPEG, BMP, TGA, PSD, GIF, HDR) into memory
Image GenImageText(int width, int height, string text);  // Makes a grayscale image from text, each of its bytes a pixel as bright as the byte, row by row from the top, and black past its end, as raylib's GenImageText does
Image GenImageColor(int width, int height, Color color);  // Makes an image of one color
Image GenImageChecked(int width, int height, int checksX, int checksY, Color first, Color second);  // Makes a checkerboard of squares checksX by checksY pixels
Image GenImageGrid(int pixelsPerMeter);                  // Makes the image of one meter's cell of a grid, in its default colors, a light gray face with darker lines
Image GenImageGrid(int pixelsPerMeter, Color face, Color line, Color fine);  // Makes the image of one meter's cell of a grid, its edges a line and a finer line every tenth of a meter, to tile faces whose texture coordinates are in meters
Color GetImageColor(Image image, int x, int y);          // The color of one pixel
Color[] LoadImageColors(Image image);                    // Every pixel of an image as a color, row by row from the top left
void UnloadImage(Image image);                           // Does nothing, because an image is managed memory

Texture2D LoadTexture(string fileName);                  // Loads an image file into a texture
Texture2D LoadTextureFromImage(Image image);             // Uploads an image into a texture
Texture2D LoadTextureCubemap(Image image, CubemapLayout layout);  // A cube texture from an image of its six faces laid out as layout says, or as its shape shows with CubemapLayout.AutoDetect, as raylib's LoadTextureCubemap, for a shader's SamplerCube, set by SetShaderValueTexture
void UnloadTexture(Texture2D texture);                   // Frees a texture
bool IsTextureValid(Texture2D texture);                  // Whether texture is loaded
IntPtr GetImGuiTextureId(Texture2D texture);             // The id ImGui draws texture by, for ImGui.Image, ImGui.ImageButton and a draw list's AddImage, or none for a texture that is not loaded
bool UpdateTexture(Texture2D texture, Image image);      // Replaces a texture's pixels with an image of the same size
bool UpdateTextureRec(Texture2D texture, Rectangle rec, byte[] pixels);  // Replaces the pixels of a rectangle of a texture, the rest kept, as a minimap or a painted canvas changes a part at a time, from pixels four bytes a pixel, red, green, blue and alpha, rows from the top, the rectangle's size
void GenTextureMipmaps(ref Texture2D texture);           // Gives a texture mip levels, each half the size of the one before, down to one pixel, so it stays smooth instead of shimmering when drawn smaller than its size
void SetTextureFilter(Texture2D texture, TextureFilter filter);  // Sets how a texture is sampled between its pixels
void SetTextureWrap(Texture2D texture, TextureWrap wrap);  // Sets what a texture shows past its edges, where a texture coordinate leaves 0 to 1

void DrawTexture(Texture2D texture, int x, int y, Color tint);  // Draws a texture with its top left corner at (x, y)
void DrawTextureV(Texture2D texture, Vector2 position, Color tint);  // Draws a texture with its top left corner at position
void DrawTextureEx(Texture2D texture, Vector2 position, float rotation, float scale, Color tint);  // Draws a texture scaled, and rotated by rotation degrees around its top left corner
void DrawTextureRec(Texture2D texture, Rectangle source, Vector2 position, Color tint);  // Draws the part of a texture source covers
void DrawTexturePro(Texture2D texture, Rectangle source, Rectangle dest, Vector2 origin, float rotation, Color tint);  // Draws the part of a texture source covers into dest, rotated by rotation degrees around origin, which is relative to the top left of dest
void DrawTextureNPatch(Texture2D texture, NPatchInfo nPatchInfo, Rectangle dest, Vector2 origin, float rotation, Color tint);  // Draws a texture stretched into dest with its borders kept at their size, as a panel or button is drawn from a small image of one, rotated by rotation degrees around origin, which is relative to the top left of dest
void DrawBillboard(Camera3D camera, Texture2D texture, Vector3 position, float size, Color tint);  // Draws a texture in 3D facing camera, size world units high and as wide as the texture's shape makes it
void DrawBillboardRec(Camera3D camera, Texture2D texture, Rectangle source, Vector3 position, Vector2 size, Color tint);  // Draws the part of a texture source covers in 3D, centered on position, size world units across and up, turned toward camera about the world's up axis, as a tree or a sprite standing on the ground is drawn
void DrawBillboardPro(Camera3D camera, Texture2D texture, Rectangle source, Vector3 position, Vector3 up, Vector2 size, Vector2 origin, float rotation, Color tint);  // Draws the part of a texture source covers in 3D, size world units along the camera's right and along up, with origin, measured from its bottom left corner in the same units, at position, and turned rotation degrees about it in the billboard's plane, counterclockwise as the camera sees it

Image LoadImageRaw(string fileName, int width, int height, PixelFormat format, int headerSize);  // Reads an image of width by height pixels from a file of pixels alone, laid out as format says after headerSize bytes, as raylib's LoadImageRaw does
void ImageFormat(ref Image image, PixelFormat newFormat);  // Keeps of each pixel what newFormat keeps, as raylib's ImageFormat converts an image to it, gray for the gray formats, fewer levels of each channel for the formats of fewer bits, no alpha for those without it, and red alone for those of one channel
int GetPixelDataSize(int width, int height, PixelFormat format);  // The bytes width by height pixels take laid out in format, as a file of raw pixels holds them and raylib reckons them, a compressed format by the bits a pixel of its blocks comes to

Image GenImageGradientLinear(int width, int height, int direction, Color start, Color end);  // Makes an image that blends from start to end along a direction in degrees, where 0 runs top to bottom and 90 left to right, as raylib's does
Image GenImageGradientRadial(int width, int height, float density, Color inner, Color outer);  // Makes an image that blends from inner at its center to outer at its edge, holding the inner color for the first density part of the radius
Image GenImageWhiteNoise(int width, int height, float factor);  // Makes an image of white and black pixels, white with the chance factor
Image GenImagePerlinNoise(int width, int height, int offsetX, int offsetY, float scale);  // Makes an image of Perlin noise in grays, six octaves of it, at scale over the image's width and height and moved by the offsets, as raylib's is
Image GenImageCellular(int width, int height, int tileSize);  // Makes an image of cells in grays, one point in each square of tileSize pixels, each pixel darker the nearer it is to a point, as raylib's is
Image ImageCopy(Image image);                            // A copy of an image, with pixels of its own
Image ImageFromImage(Image image, Rectangle rec);        // A new image of the part of image that rec covers, clipped to the image
byte[] ExportImageToMemory(Image image, string fileType);  // An image encoded as a file's bytes, of fileType, which is ".png"
Image LoadImageAnim(string fileName, out int frames);    // Reads every frame of an animated GIF into one image, the frames stacked from the top, each as tall as the GIF, and says how many there are
Image LoadImageAnimFromMemory(string fileType, byte[] fileData, out int frames);  // Reads every frame of an animated GIF held in memory, as LoadImageAnim reads a file
Image LoadImageFromMemory(string fileType, byte[] fileData);  // Decodes an image held in memory, as a file's bytes, the type named by its extension as ".png"
Image LoadImageFromScreen();                             // The window as the last frame presented left it, at the size its content is drawn at, or, for the first call of a run, the window's size in the last color it was cleared to
Image LoadImageFromTexture(Texture2D texture);           // A texture's pixels read back from the GPU, a render texture's color among them, as the frames drawn so far left it, or an invalid image when it is not on the GPU yet
bool IsImageValid(Image image);                          // Whether an image holds pixels, as one loaded or made does
void ImageAlphaCrop(ref Image image, float threshold);   // Crops an image to the smallest rectangle holding every pixel with alpha above threshold, from 0 to 1
void ImageToPOT(ref Image image, Color fill);            // Grows an image's canvas to the next power of two each way, the new pixels fill, as an old GPU's textures had to be
Color[] LoadImagePalette(Image image, int maxPaletteSize);  // The different colors of an image, in the order they are first met row by row, up to maxPaletteSize of them
bool ExportImage(Image image, string fileName);          // Writes an image to a PNG file

void ImageCrop(ref Image image, Rectangle rec);          // Cuts an image down to the part rec covers
void ImageResize(ref Image image, int newWidth, int newHeight);  // Scales an image to a new size, blending neighboring pixels
void ImageResizeNN(ref Image image, int newWidth, int newHeight);  // Scales an image to a new size, taking the nearest pixel, which keeps pixel art sharp
void ImageResizeCanvas(ref Image image, int newWidth, int newHeight, int offsetX, int offsetY, Color fill);  // Changes an image's size without scaling it, placing it at an offset in the new size and filling the rest with fill
void ImageFlipVertical(ref Image image);                 // Turns an image upside down
void ImageFlipHorizontal(ref Image image);               // Mirrors an image left to right
void ImageRotateCW(ref Image image);                     // Turns an image a quarter turn clockwise
void ImageRotateCCW(ref Image image);                    // Turns an image a quarter turn counterclockwise

void ImageColorTint(ref Image image, Color color);       // Multiplies every pixel by a color
void ImageColorInvert(ref Image image);                  // Inverts every pixel's red, green and blue
void ImageColorGrayscale(ref Image image);               // Turns every pixel gray, by its perceived brightness
void ImageColorContrast(ref Image image, int contrast);  // Changes every pixel's contrast, from -100 (flat gray) to 100
void ImageColorBrightness(ref Image image, int brightness);  // Adds to every pixel's red, green and blue, from -255 to 255
void ImageColorReplace(ref Image image, Color color, Color replace);  // Replaces every pixel of exactly one color with another

void ImageClearBackground(ref Image image, Color color);  // Fills a whole image with one color
void ImageDrawPixel(ref Image image, int x, int y, Color color);  // Sets one pixel, when it is inside the image
void ImageDrawPixelV(ref Image image, Vector2 position, Color color);  // Sets one pixel, when it is inside the image
void ImageDrawLine(ref Image image, int startX, int startY, int endX, int endY, Color color);  // Draws a line one pixel wide between two points
void ImageDrawLineV(ref Image image, Vector2 start, Vector2 end, Color color);  // Draws a line one pixel wide between two points
void ImageDrawCircle(ref Image image, int centerX, int centerY, int radius, Color color);  // Fills a circle
void ImageDrawCircleLines(ref Image image, int centerX, int centerY, int radius, Color color);  // Draws a circle's outline one pixel wide
void ImageDrawRectangle(ref Image image, int x, int y, int width, int height, Color color);  // Fills a rectangle
void ImageDrawRectangleRec(ref Image image, Rectangle rec, Color color);  // Fills a rectangle
void ImageDrawRectangleLines(ref Image dst, int posX, int posY, int width, int height, Color color);  // Draws a rectangle's outline a pixel wide, inside its edge, as ImageDrawRectangleLinesEx draws one of any width

Image GenImageGradientSquare(int width, int height, float density, Color inner, Color outer);  // Makes a square gradient, inner at the center to outer at the edges, holding the inner color for the first density part
Image ImageFromChannel(Image image, int selectedChannel);  // A grayscale image of one channel of another, 0 red, 1 green, 2 blue and 3 alpha, opaque
void ImageAlphaClear(ref Image image, Color color, float threshold);  // Gives every pixel whose alpha is below threshold, from 0 to 1, the color color
void ImageAlphaMask(ref Image image, Image alphaMask);   // Sets each pixel's alpha from the brightness of the same pixel of a mask of the same size, white opaque and black clear
void ImageAlphaPremultiply(ref Image image);             // Multiplies each pixel's color by its alpha, as a texture blended by premultiplied alpha needs
Rectangle GetImageAlphaBorder(Image image, float threshold);  // The smallest rectangle holding every pixel whose alpha is above threshold, from 0 to 1, which trims a sprite's clear border, or an empty one when no pixel is
void ImageBlurGaussian(ref Image image, int blurSize);   // Blurs an image by a Gaussian of about blurSize pixels, across and then down
void ImageKernelConvolution(ref Image image, float[] kernel);  // Convolves an image's color with a square kernel, its rows from the top, each pixel the sum of its neighbors weighted by the kernel, its alpha kept, as an edge or sharpen filter does
void ImageRotate(ref Image image, int degrees);          // Turns an image by degrees clockwise, its canvas grown to hold all of it, clear where it was not, each pixel read between its neighbors
void ImageDrawLineEx(ref Image dst, Vector2 start, Vector2 end, int thick, Color color);  // Draws a line thick pixels wide between two points
void ImageDrawTriangle(ref Image dst, Vector2 v1, Vector2 v2, Vector2 v3, Color color);  // Fills a triangle, each pixel whose center it holds, blended over what is there by the color's alpha
void ImageDither(ref Image image, int rBpp, int gBpp, int bBpp, int aBpp);  // Reduces an image to so many bits for each channel, spreading each pixel's rounding to the pixels after it (Floyd and Steinberg), so few colors still read as smooth gradients, as art for an old console's palette is made
void ImageDrawLineStrip(ref Image dst, Vector2[] points, Color color);  // Draws lines within an image joining each point to the next
void ImageDrawTriangleGradient(ref Image dst, Vector2 v1, Vector2 v2, Vector2 v3, Color c1, Color c2, Color c3);  // Fills a triangle with a color at each corner, blended across it
void ImageDrawTriangleLines(ref Image dst, Vector2 v1, Vector2 v2, Vector2 v3, Color color);  // Draws a triangle's outline within an image
void ImageDrawTriangleFan(ref Image dst, Vector2[] points, Color color);  // Fills triangles fanning out from the first point through each pair of the rest
void ImageDrawTriangleStrip(ref Image dst, Vector2[] points, Color color);  // Fills a strip of triangles, each from three points in a row
void ImageDrawRectangleV(ref Image dst, Vector2 position, Vector2 size, Color color);  // Fills a rectangle at a position, of a size
void ImageDrawRectanglePro(ref Image dst, Rectangle rec, Vector2 origin, float rotation, Color color);  // Fills a rectangle turned rotation degrees around origin, which is relative to its top left
void ImageDrawRectangleLinesEx(ref Image dst, Rectangle rec, int thick, Color color);  // Draws a rectangle's outline thick pixels wide, inside its edge
void ImageDrawRectangleGradientEx(ref Image dst, Rectangle rec, Color topLeft, Color bottomLeft, Color bottomRight, Color topRight);  // Fills a rectangle with a color at each corner, blended across it, given top left, bottom left, bottom right and top right
void ImageDrawCircleLinesV(ref Image dst, Vector2 center, int radius, Color color);  // Draws a circle's outline within an image
void ImageDrawCircleGradient(ref Image dst, Vector2 center, float radius, Color inner, Color outer);  // Fills a circle blending from inner at its middle to outer at its edge
void ImageDrawImage(ref Image dst, Image src, int posX, int posY, Color tint);  // Draws a whole image within another with its top left at a pixel, multiplied by tint
void ImageDrawImageRec(ref Image dst, Image src, Rectangle srcRec, Vector2 position, Color tint);  // Draws a part of an image within another with its top left at a position
void ImageDrawImageEx(ref Image dst, Image src, Vector2 position, float rotation, float scale, Color tint);  // Draws a whole image within another, scaled and turned rotation degrees around its top left
void ImageDrawImagePro(ref Image dst, Image src, Rectangle srcRec, Rectangle dstRec, Vector2 origin, float rotation, Color tint);  // Draws a part of an image into a rectangle of another, turned rotation degrees around origin, which is relative to the rectangle's top left, each pixel read between its neighbors and blended over what is there, as DrawTexturePro draws
void ImageDrawTextPro(ref Image dst, Font font, string text, Vector2 position, Vector2 origin, float rotation, float fontSize, float spacing, Color tint);  // Draws text within an image, turned rotation degrees around origin, which is relative to the text's top left
void ImageDrawCircleV(ref Image dst, Vector2 center, int radius, Color color);  // Fills a circle around a point

void ImageDrawText(ref Image dst, string text, int x, int y, int fontSize, Color color);  // Draws text into an image in the default font, as DrawText draws it on the screen
void ImageDrawTextEx(ref Image dst, Font font, string text, Vector2 position, float fontSize, float spacing, Color tint);  // Draws text into an image in a font, as DrawTextEx draws it on the screen, each glyph scaled from its bake by the nearest pixel and blended by its coverage
Image ImageText(string text, int fontSize, Color color);  // A new image holding text in the default font, as large as the text, clear around it
Image ImageTextEx(Font font, string text, float fontSize, float spacing, Color tint);  // A new image holding text in a font, as large as the text, clear around it
```

An `Image` is RGBA bytes in memory. The `Image*` functions change the image passed by `ref`, as
raylib's take a pointer. Shapes replace the pixels they cover, alpha included, and an image drawn
into another by `ImageDrawImage` or its kin blends by the source's alpha. A changed image reaches the screen through `LoadTextureFromImage`
or `UpdateTexture`.

`LoadImageFromScreen` reads the frame before, inside a frame as well, since a frame is drawn on the
GPU as `EndDrawing` ends it. Frames are kept from its first call on, so that first call reads the
window's size in the last color it was cleared to. From outside, `./e3d command frame.read x y w h`
reads a rectangle of the next frame as its mean sRGB color and its largest step between neighboring
pixels, so a script asserts a picture without downloading it.

A file name is looked for as given, then beside the program, then under `source/` beside it. A file
that cannot be read gives an invalid image or texture and a warning in the log, and drawing an
invalid texture draws nothing. `CloseWindow` logs how many textures were still loaded.

## Shaders

<!-- written by build/cheatsheet.sh from E3D.Shaders.cs -->
```csharp
Shader LoadShader(string fileName);                      // Loads and compiles a Slang shader file
Shader LoadShaderFromMemory(string code, string name = "shader.slang");  // Compiles Slang source held in memory as a shader
bool IsShaderValid(Shader shader);                       // Whether shader is loaded
void SetShaderKeepsPositions(Shader shader, bool keepsPositions);  // Says whether a model shader's vertex stage keeps its mesh's positions where the instance places them, so a mesh drawn with it is left out of each pass that cannot see the mesh's box, as a mesh drawn with the model pass's own shader is
void UnloadShader(Shader shader);                        // Frees a shader
void BeginShaderMode(Shader shader);                     // Draws the following shapes, textures and text with shader until EndShaderMode
void EndShaderMode();                                    // Returns to the engine's own shader
int GetShaderLocation(Shader shader, string uniformName);  // The location of a uniform the shader declares at the top level, by name, for SetShaderValue, or -1 when it declares none of that name
void SetShaderValueTexture(Shader shader, int location, Texture2D texture);  // Sets a texture the shader samples, found by name with GetShaderLocation, as raylib's SetShaderValueTexture, for what is drawn after it
void SetShaderValueMatrix(Shader shader, int location, Matrix4x4 value);  // Sets a named uniform to a 4x4 matrix, as raylib's SetShaderValueMatrix
void SetShaderValue(Shader shader, int location, int value);  // Sets a named uniform to a whole number, or slot (0 to 3) to (value, 0, 0, 0)
void SetShaderValueV<T>(Shader shader, int location, ReadOnlySpan<T> values);  // Sets a named array uniform from values, as many as it holds, each a float, a vector, an int or a matrix, as raylib's SetShaderValueV sets several at once
void SetShaderValue(Shader shader, int slot, Vector4 value);  // Sets a named uniform found with GetShaderLocation, or slot slot (0 to 3), which an immediate shader reads as param(slot)
void SetShaderValue(Shader shader, int slot, float value);  // Sets slot slot to (value, 0, 0, 0)
void SetShaderValue(Shader shader, int slot, Vector2 value);  // Sets slot slot to (x, y, 0, 0)
void SetShaderValue(Shader shader, int slot, Vector3 value);  // Sets slot slot to (x, y, z, 0)
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

Inside `BeginShaderMode`, a shader applies to the immediate pass, to shapes, textures, text
and render textures drawn as textures. A model takes one through its material,
`model.Materials[0].Shader = shader;`, and such a shader imports `modelpass` instead, which gives
it `ModelVertexOutput` (position, normal, world position, uv), `baseColor(input)` and
`lit(color, input)`, the model pass's own lighting, and `ModelStreamsOutput` in its place adds the
mesh's `vertexColor` and `uv2` beside a `base`. A model shader with a vertex stage of its own
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

<!-- written by build/cheatsheet.sh from E3D.Compute.cs -->
```csharp
Shader LoadComputeShader(string fileName);               // Loads and compiles a Slang compute shader file
Shader LoadComputeShaderFromMemory(string code, string name = "compute.slang");  // Compiles Slang compute shader source held in memory
ShaderBuffer LoadShaderBuffer(int size);                 // Makes a storage buffer of size bytes, zeroed
ShaderBuffer LoadShaderBuffer<T>(ReadOnlySpan<T> data);  // Makes a storage buffer holding data
void UnloadShaderBuffer(ShaderBuffer buffer);            // Frees a storage buffer, once the dispatches using it have finished, and the meshes over it
bool IsShaderBufferValid(ShaderBuffer buffer);           // Whether buffer is loaded
void UpdateShaderBuffer<T>(ShaderBuffer buffer, ReadOnlySpan<T> data, int offset = 0);  // Writes data into a storage buffer, starting offset bytes in
void ReadShaderBuffer<T>(ShaderBuffer buffer, Span<T> destination, int offset = 0);  // Reads a storage buffer into destination, starting offset bytes in
ShaderReadback ReadShaderBufferAsync(ShaderBuffer buffer, int offset = 0, int size = 0);  // Asks for a storage buffer's bytes to be copied out after the dispatches before it, without waiting, to read a frame or two later
bool IsShaderReadbackReady(ShaderReadback readback);     // Whether a readback's bytes have been copied out, so GetShaderReadback reads them without waiting
void GetShaderReadback<T>(ShaderReadback readback, Span<T> destination);  // Copies a readback's bytes into destination, as many as it holds
void UnloadShaderReadback(ShaderReadback readback);      // Lets a readback go, its buffer back to the pool once the GPU has copied into it
void SetShaderValueBuffer(Shader shader, int location, ShaderBuffer buffer);  // Sets a storage buffer the shader uses, found by name with GetShaderLocation, for the dispatches and draws after it, as raylib's rlBindShaderBuffer
void ComputeShaderDispatch(Shader shader, int groupsX, int groupsY, int groupsZ);  // Runs a compute shader over groupsX by groupsY by groupsZ groups of the threads its numthreads names, with the uniforms and buffers set on it
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
texture's colors as well, through a stand-in copied into it where the GPU cannot store to its format. A shader that draws reads
the same buffer as a `StructuredBuffer`, set with `SetShaderValueBuffer`, so what a dispatch wrote
is drawn with no copy through the CPU. An immediate shader reads it in `BeginShaderMode`, and a
model shader drawn with `DrawMeshInstanced` picks each copy's values from it by `SV_InstanceID`.

## Models and meshes

<!-- written by build/cheatsheet.sh from E3D.Models.cs, E3D.MeshGen.cs, E3D.Meshes.cs, E3D.Environment.cs, E3D.DaySky.cs, E3D.Animation.cs -->
```csharp
ModelMesh GenMeshCube(float width, float height, float length);  // Makes a box centered on the origin, with a normal and texture coordinates per face
ModelMesh GenMeshSphere(float radius, int rings, int slices);  // Makes a sphere centered on the origin, its poles on the Z axis, as raylib's is
ModelMesh GenMeshPlane(float width, float length, int resX, int resZ);  // Makes a flat rectangle on the XZ plane, centered on the origin, facing up

Model LoadModel(string fileName);                        // Loads a model file through Assimp (glTF, FBX, OBJ, COLLADA and about forty more), with each material's base color and base color texture
Model LoadModelFromMesh(ModelMesh mesh);                 // Makes a model of one mesh with a white material
void UnloadModel(Model model);                           // Frees a model's meshes and the textures it loaded
bool IsModelValid(Model model);                          // Whether model has meshes that are loaded
BoundingBox GetModelBoundingBox(Model model);            // The box around every mesh of a model, in its own space before Model.Transform

void DrawModel(Model model, Vector3 position, float scale, Color tint);  // Draws a model at a position, scaled the same on every axis, with its colors multiplied by tint
void DrawModelEx(Model model, Vector3 position, Vector3 rotationAxis, float rotationAngle, Vector3 scale, Color tint);  // Draws a model at a position, rotated by rotationAngle degrees around rotationAxis, and scaled
void DrawModelWires(Model model, Vector3 position, float scale, Color tint);  // Draws a model's triangle edges in one color, at a position and scaled the same on every axis
void DrawModelWiresEx(Model model, Vector3 position, Vector3 rotationAxis, float rotationAngle, Vector3 scale, Color tint);  // Draws a model's triangle edges in one color, rotated (degrees) and scaled
void DrawModelPoints(Model model, Vector3 position, float scale, Color tint);  // Draws each of a model's vertices as a point, at a position and scaled the same on every axis
void DrawModelPointsEx(Model model, Vector3 position, Vector3 rotationAxis, float rotationAngle, Vector3 scale, Color tint);  // Draws a model as a point at each corner of its triangles, rotated (degrees) and scaled
ModelMaterial LoadMaterialDefault();                     // A white material with no maps, as a model made from a mesh starts with
ModelMaterial[] LoadMaterials(string fileName);          // The materials of a model file, without its meshes, as raylib's reads a .mtl or a glTF's materials
void UnloadMaterial(ModelMaterial material);             // Unloads a material's five maps and its shader, which UnloadModel leaves loaded for the materials that may share them
bool IsMaterialValid(ModelMaterial material);            // Whether a material's maps are loaded textures, or none, so it can be drawn with
void SetMaterialTexture(ref ModelMaterial material, MaterialMapIndex mapType, Texture2D texture);  // Sets one of a material's maps by raylib's name for it, the color for MaterialMapIndex.Albedo and the normal, metallic-roughness, emissive or occlusion map for the others it has
void SetModelMeshMaterial(Model model, int meshId, int materialId);  // Has one of a model's meshes drawn with one of its materials, by their indices
void SetSubsurfaceQuality(SubsurfaceQuality quality);    // Sets how finely the light under a surface, a material's ModelMaterial.SubsurfaceRadius, is spread over the window's frame, at half the window's size with nine taps each way for a slow GPU, at its size with nine, or with seventeen, as by default
void DrawMesh(ModelMesh mesh, ModelMaterial material, Matrix4x4 transform);  // Draws one mesh with a material and a model to world transform
void DrawMeshInstanced(ModelMesh mesh, ModelMaterial material, ReadOnlySpan<Matrix4x4> transforms);  // Draws one mesh many times, once at each model to world transform, as one instanced draw
void DrawBoundingBox(BoundingBox box, Color color);      // Draws a box's edges

ModelMesh GenMeshPoly(int sides, float radius);          // Makes a flat regular polygon on the XZ plane, centered on the origin, facing up
ModelMesh GenMeshCylinder(float radius, float height, int slices);  // Makes a cylinder standing on the XZ plane, from y 0 up to height, closed at both ends
ModelMesh GenMeshCone(float radius, float height, int slices);  // Makes a cone standing on the XZ plane, its base at y 0 and its tip at height
ModelMesh GenMeshHemiSphere(float radius, int rings, int slices);  // Makes the upper half of a sphere, its flat side on the XZ plane and open, its pole on the Z axis
ModelMesh GenMeshTorus(float radius, float size, int radSeg, int sides);  // Makes a torus standing on the XY plane, as raylib's does, a ring of radius size / 2 and a tube radius of that thick, so 0.25 makes a tube a quarter of the ring's radius
ModelMesh GenMeshKnot(float radius, float size, int radSeg, int sides);  // Makes a trefoil knot as raylib's does, par_shapes' knot, about 0.8 across from its center and a tube of radius / 10, scaled by size
ModelMesh GenMeshHeightmap(Image heightmap, Vector3 size);  // Makes terrain from an image, a grid with a vertex per pixel raised by the pixel's brightness, from the origin to size, where black is at y 0 and white at size.Y
ModelMesh GenMeshCubicmap(Image cubicmap, Vector3 cubeSize);  // Makes a maze from an image as raylib's does, a block of cubeSize for every white pixel, centered on the pixel's place, so pixel (x, y) stands at x by y times the size, and a floor and a roof over every black pixel
Mesh GetMeshComponent(ModelMesh mesh);                   // The triangles of a mesh as a Mesh component, three positions to a triangle with their normals and texture coordinates, so an entity draws a generated or loaded shape
bool ExportMesh(ModelMesh mesh, string fileName);        // Writes a mesh as a Wavefront OBJ file, its positions, texture coordinates, normals and triangles, as raylib's ExportMesh does, answering whether it was written

ModelMesh UploadMesh(ModelVertex[] vertices, uint[] indices);  // Uploads vertices and triangle indices as a mesh
ModelMesh UploadMesh(ModelVertex[] vertices, uint[] indices, Color[]? colors, Vector2[]? texcoords2);  // Uploads vertices and triangle indices as a mesh, with a color at each vertex and a second texture coordinate at each, either of which may be null, as raylib's mesh has its colors and texcoords2
ModelMesh UploadMesh(ModelVertex[] vertices, uint[] indices, Color[]? colors, Vector2[]? texcoords2, MeshStream? stream);  // Uploads vertices and triangle indices as a mesh, with its colors and second texture coordinates where given, and a stream of the program's own, a value each vertex carries that a vertex stage of the program's own reads as CUSTOM0
ModelMesh UploadMesh(Vector3[] positions, uint[] indices, MeshStream? stream, Color[]? colors);  // Uploads a mesh of positions alone, twelve bytes a vertex, with a stream of the program's own and a color at each vertex where given, for a mesh whose normal and texture coordinates its stream packs, as a voxel world's chunks are
void UpdateMeshVertices(ModelMesh mesh, ModelVertex[] vertices);  // Replaces a mesh's vertices with as many new ones, keeping its triangles, as raylib's UpdateMeshBuffer does for the vertex arrays
void UpdateMeshBuffer<T>(ModelMesh mesh, int index, ReadOnlySpan<T> data, int offset);  // Writes data into one of a mesh's arrays, starting offset bytes in, by raylib's index for it, 0 its positions, 1 its texture coordinates and 2 its normals, as floats, 3 its colors, four bytes each, and 5 its second texture coordinates, or at MeshStreamBuffer its stream of the program's own, in the stream's format
ModelMesh LoadMeshFromShaderBuffers(ShaderBuffer positions, ShaderBuffer stream, MeshStreamFormat format, ShaderBuffer count, int capacity);  // Makes a mesh of the vertices compute shaders write into storage buffers, drawn as many as a shader wrote into the first four bytes of count, at most capacity, with no copy through the CPU and no wait
void SetMeshBoundingBox(ModelMesh mesh, BoundingBox box);  // Sets the box in its own space a mesh over shader buffers is culled by, which GetMeshBoundingBox then gives, since the CPU knows nothing of its vertices
void UnloadMesh(ModelMesh mesh);                         // Frees a mesh

void SetEnvironmentMap(Image equirectangular, float intensity = 1);  // Lights models from all around by an equirectangular image of sRGB color, a sky photo or a painted gradient, which smooth and metal surfaces reflect and rough ones scatter
bool SetEnvironmentMap(string fileName, float intensity = 1);  // Lights models from all around by an equirectangular image file
void UnloadEnvironmentMap();                             // Stops lighting models by an environment map, and ends the day sky where it runs
void SetEnvironmentLight(float intensity, Color tint);   // Sets how much of the environment map's light reaches the scene, intensity of it times tint, apart from the sky the map draws, 1 and white unless set
(float Intensity, Color Tint) GetEnvironmentLight();     // The share and tint of the environment map's light on the scene as SetEnvironmentLight left them, 1 and white unless set
void DrawSkybox(Color tint);                             // Draws the environment map as the sky behind everything, through the camera of the BeginMode3D it is called inside
void DrawSkybox();                                       // Draws the environment map as the sky behind everything, untinted
void SetSkyHorizon(Color color, float blendDegrees);     // Draws the sky below the horizon in color, blended into the environment map's image over blendDegrees above it, so land a program fades into fog of that color meets the same color past the last thing drawn
(Color Color, float BlendDegrees) GetSkyHorizon();       // The color the sky is drawn in below the horizon and the degrees it blends over, as SetSkyHorizon or the day sky left them, or a clear color and 0 where none is set

void SetSkyTime(float hours);                            // Sets the day sky's time in hours from its first midnight, starting the day sky where it was not running, so the environment map, the light and the horizon follow the hour, 30 being six in the morning of the second day
float GetSkyTime();                                      // The day sky's time in hours from its first midnight, or 0 where none runs
void SetSkyDay(SkyDay day);                              // Sets how the day sky's day runs, the minutes it lasts, the lean of the sun's path and the light it turns, starting the day sky at midnight where it was not running
SkyDay GetSkyDay();                                      // How the day sky's day runs, as SetSkyDay left it, or the day it runs unless set

ModelAnimation[] LoadModelAnimations(string fileName);   // Loads every clip of a model file (glTF, FBX, COLLADA and the rest Assimp reads), each sampled into poses at AnimationFps frames a second
void UpdateModelAnimation(Model model, ModelAnimation animation, float frame);  // Poses model as animation has it at frame, counted round the clip's length, by moving each skinned mesh's vertices from their rest
void UpdateModelAnimationAt(Model model, ModelAnimation animation, float seconds);  // Poses model as animation has it seconds into the clip, counted round its length, between the two frames either side
void UpdateModelAnimationEx(Model model, ModelAnimation animA, float frameA, ModelAnimation animB, float frameB, float blend);  // Poses model between two clips at frames of each, blend of the way from the first to the second, as raylib's does by frame where UpdateModelAnimationBlend takes seconds
void UpdateModelAnimationBlend(Model model, ModelAnimation from, float fromSeconds, ModelAnimation to, float toSeconds, float weight);  // Poses model between two clips, from at fromSeconds and to at toSeconds, weight of the way from the first to the second, as a walk turning into a run
void UpdateModelAnimationLayer(Model model, ModelAnimation under, float underSeconds, ModelAnimation over, float overSeconds, string bone, float weight = 1);  // Poses model by under at underSeconds, with over at overSeconds playing on bone and every bone below it, weight of the way from the first clip to the second there, as a wave of the arm over a run, or a head turning while the rest walks
void SetModelMorphWeight(Model model, string target, float weight);  // Sets the weight of every morph target named target in the model's meshes, 0 for its shape at rest and 1 for the target's, as a face's smile or a ball's squash, and poses the model again as its bones were last posed
bool IsModelAnimationValid(Model model, ModelAnimation animation);  // Whether animation moves the bones model has, as many, each under the same parent and each by the same name where the clip names its bones
void UnloadModelAnimation(ModelAnimation animation);     // Lets go of a clip
void UnloadModelAnimations(ModelAnimation[] animations);  // Lets go of clips
```

A `Model` has `Meshes`, `Materials` and `MeshMaterial`, as raylib's does, and a `Transform`, and a
model with a skeleton has its `Skeleton`, its `Bones` and `BindPose`, which a `ModelAnimation` of
the same file poses keyframe by keyframe in its `KeyframePoses`. A `ModelMaterial` is a `Color` and
a `Texture`, so `model.Materials[0].Texture = texture;` textures a mesh, with `Metallic`,
`Roughness`, a `NormalMap` and its `NormalScale`, a `MetallicRoughnessMap` as glTF packs one, an
`Emissive` color with its `EmissiveIntensity` and `EmissiveMap`, and an `OcclusionMap` with its
`OcclusionStrength`, an `AlphaMode` (`Blend` by default, `Mask` below its `AlphaCutoff`, or
`Opaque`) and `DoubleSided` (false by default), both of which a glTF file sets, `CastsShadows` (true
by default), false for an emissive mesh that leaves no shadow, `BouncesLight` (true by default),
false for a mesh that moves in most frames, as an animal, which casts its shadow and is left out of
global illumination, and a `SubsurfaceRadius` in world units with
its `SubsurfaceColor`, how far each color travels under skin, wax or marble before it leaves, which
the window's frame spreads, and a `SubsurfaceThickness`, how thick its parts are at most for the
light from behind, as a glTF file's `KHR_materials_volume` says. Models are drawn unlit, as raylib
draws them, until the ECS holds `Light` entities, and draw through the camera `BeginMode3D` set.

An entity's `AnimatedModel` plays a clip, and an `AnimationMachine` beside it chooses which, by an
`AnimationStateMachine` of states and transitions read from a file (`AnimationStateMachine.Load`,
`Save`), on the parameters the game sets with `machine.Set("speed", 2f)` and
`machine.Trigger("jump")`.

## Modeling

<!-- written by build/cheatsheet.sh from E3D.Modeling.cs, E3D.Booleans.cs, E3D.Prototypes.cs, E3D.ModelingSlots.cs, E3D.ModelingUvs.cs, E3D.ModelingNames.cs, E3D.ModelingWindow.cs -->
```csharp
EditMesh LoadEditMesh();                                 // Makes an empty edit mesh, which primitives and operations then shape
void UnloadEditMesh(EditMesh mesh);                      // Frees an edit mesh
EditMesh LoadEditMesh(string fileName);                  // Makes an edit mesh from a recipe's file, a .model file of one operation a line, found beside the program or in the working directory
EditMesh LoadEditMeshFromMemory(string recipe);          // Makes an edit mesh from a recipe's text, one operation a line, its model 1 header first or left out
bool SaveEditMesh(EditMesh mesh, string fileName);       // Writes an edit mesh's recipe to a .model file, beside the program where the name is relative, and says whether it was written
bool IsEditMeshValid(EditMesh mesh);                     // Whether mesh names an edit mesh that is loaded
string GetEditMeshRecipe(EditMesh mesh);                 // The mesh's recipe as the text of its file, its model 1 header and then the operations that made it, a line each
void EditMeshRun(EditMesh mesh, string line);            // Runs one line of a recipe on the mesh, as though its operation were called, as box 8 3 6 or select +y
bool EditMeshUndo(EditMesh mesh);                        // Takes back the last operation of the mesh's recipe and says whether there was one
bool EditMeshRedo(EditMesh mesh);                        // Does again the operation last taken back and says whether there was one
EditMeshInfo GetEditMeshInfo(EditMesh mesh);             // The mesh's counts of vertices, edges and faces, its open edges, its selection, its volume, its bounds and its grid step
int GetEditMeshVersion(EditMesh mesh);                   // A count that moves on with every change to what the mesh builds into, its shape and its slots, by the program, the modeling window or the model commands, and not with its selection
Vector3 GetEditMeshVertex(EditMesh mesh, int vertex);    // Where a vertex of the mesh is, by its index from zero
int[] GetEditMeshFace(EditMesh mesh, int face);          // A face's vertices, by the face's index from zero, in order round it, counterclockwise seen from the side it faces
Vector3 GetEditMeshFaceNormal(EditMesh mesh, int face);  // The way a face faces, a length of one, or zero for a face with no area
string GetEditMeshFaceSlot(EditMesh mesh, int face);     // The name of the material slot a face is in
string[] GetEditMeshSlots(EditMesh mesh);                // The names of the mesh's material slots, the first of which, default, every new face takes
bool IsEditMeshFaceSelected(EditMesh mesh, int face);    // Whether a face of the mesh is selected
bool IsEditMeshVertexSelected(EditMesh mesh, int vertex);  // Whether a vertex of the mesh is selected

void EditMeshBox(EditMesh mesh, Vector3 size, Vector3 at = default, bool centered = false);  // Adds a box as wide along X, tall along Y and deep along Z as size, eight vertices and six faces
void EditMeshPlane(EditMesh mesh, float width, float depth, Vector3 at = default, bool centered = false);  // Adds a flat rectangle facing up, as wide along X and deep along Z as given, one face whose four edges are open
void EditMeshCylinder(EditMesh mesh, float radius, float height, int sides, Vector3 at = default, bool centered = false);  // Adds a cylinder standing along Y, its round side made of flat sides
void EditMeshCone(EditMesh mesh, float radius, float height, int sides, Vector3 at = default, bool centered = false);  // Adds a cone standing along Y on a base of flat sides, its point over the middle of the base
void EditMeshWedge(EditMesh mesh, Vector3 size, Vector3 at = default, bool centered = false);  // Adds a wedge, a box cut from one bottom edge to the opposite top edge, rising along Z from nothing to its full height
void EditMeshStairs(EditMesh mesh, int steps, float rise, float run, float width, Vector3 at = default, bool centered = false);  // Adds a flight of stairs rising along Z, each step rise high and run deep, closed underneath
void EditMeshArch(EditMesh mesh, float radius, float thickness, float depth, int sides, Vector3 at = default, bool centered = false);  // Adds an arch, half a ring standing on its two feet with its opening along Z, its curve made of flat sides
void EditMeshClear(EditMesh mesh);                       // Takes away every vertex, face and material slot but the first, and the selection with them

void EditMeshSelectAll(EditMesh mesh);                   // Selects every vertex and every face
void EditMeshSelectNone(EditMesh mesh);                  // Selects nothing
void EditMeshSelectInvert(EditMesh mesh);                // Selects what was not selected and leaves what was, faces with their corners
void EditMeshSelectFaces(EditMesh mesh, ReadOnlySpan<int> faces, SelectionMode mode = SelectionMode.Replace);  // Selects faces by their indices from zero
void EditMeshSelectFacing(EditMesh mesh, Vector3 direction, SelectionMode mode = SelectionMode.Replace);  // Selects the faces whose normals are nearest one way along an axis, so Vector3.UnitY selects the floors
void EditMeshSelectInside(EditMesh mesh, Vector3 corner, Vector3 opposite, SelectionMode mode = SelectionMode.Replace);  // Selects the vertices inside a box given by two opposite corners, and the faces all of whose corners are inside it
void EditMeshSelectSlot(EditMesh mesh, string slot, SelectionMode mode = SelectionMode.Replace);  // Selects the faces in a material slot, a name no slot has selecting none

void EditMeshMove(EditMesh mesh, Vector3 by);            // Moves the selected vertices by a distance along each axis, in meters
void EditMeshRotate(EditMesh mesh, Vector3 axis, float degrees, Vector3? pivot = null);  // Turns the selected vertices about an axis by a number of degrees, through a pivot or the middle of the box holding them
void EditMeshScale(EditMesh mesh, Vector3 factor, Vector3? pivot = null);  // Scales the selected vertices by a factor along each axis, about a pivot or the middle of the box holding them
void EditMeshGrid(EditMesh mesh, float step);            // Sets the mesh's grid step, a meter or a power of two of it from a sixteenth to sixty-four, which EditMeshSnap rounds to
void EditMeshSnap(EditMesh mesh);                        // Puts each selected vertex on the nearest point of the mesh's grid
void EditMeshMaterial(EditMesh mesh, string slot);       // Puts the selected faces in a material slot of that name, made where the mesh has none
void EditMeshShade(EditMesh mesh, bool smooth, float degrees = 30f);  // Smooths the selected faces into one another where two meet at under an angle in degrees, or draws them flat, so a cylinder's sides are round and its ends stay flat

void EditMeshExtrude(EditMesh mesh, float distance);     // Pushes the selected faces out along their normals by a distance, filling the sides, the faces moved staying selected
void EditMeshExtrude(EditMesh mesh, Vector3 by);         // Pushes the selected faces out by one vector, filling the sides, as a wall is pushed up from a floor's edge
void EditMeshInset(EditMesh mesh, float distance);       // Makes each selected face a smaller face inside it by a distance, with a band of faces round it, the smaller faces selected after
void EditMeshDelete(EditMesh mesh, bool fill = false);   // Takes away the selected faces, or where none is selected the selected vertices and every face using them
void EditMeshMerge(EditMesh mesh, float distance);       // Joins the selected vertices nearer one another than a distance into one, faces left with fewer than three corners taken away
void EditMeshFlip(EditMesh mesh);                        // Turns the selected faces to face the other way
void EditMeshTriangulate(EditMesh mesh);                 // Cuts each selected face of more than three corners into triangles, a concave face inside its outline, the triangles selected after
void EditMeshDuplicate(EditMesh mesh);                   // Copies the selected faces where they are, with vertices of their own, and selects the copy
bool EditMeshJoin(EditMesh mesh, string fileName);       // Adds another model, made from its recipe's file, to the mesh and selects it, its faces keeping their slots by name

void EditMeshBevel(EditMesh mesh, float width, int segments = 1);  // Cuts each selected edge back by a width into a band of faces, round across in a number of segments, and closes each corner with one face
void EditMeshLoopCut(EditMesh mesh, float fraction = 0.5f);  // Cuts round the ring of four-cornered faces each selected edge crosses, at a fraction along the edges the ring crosses
void EditMeshSubdivide(EditMesh mesh, int cuts = 1);     // Cuts each selected face into a grid, each of its edges into one more part than the count of cuts

void EditMeshBridge(EditMesh mesh);                      // Joins the loops round two regions of selected faces with a band of four-cornered faces, taking the selected faces away
void EditMeshFill(EditMesh mesh);                        // Closes each hole whose edges all have both ends selected with one face, facing the way the faces round it face
void EditMeshDissolve(EditMesh mesh);                    // Joins each region of selected faces into one face round it, the edges and vertices inside it taken away and its shape kept
void EditMeshDissolveEdges(EditMesh mesh);               // Takes away each selected edge that two faces run along, joining the faces on either side into one, its shape kept
void EditMeshKnife(EditMesh mesh, Vector3 normal, Vector3 point = default);  // Cuts the selected faces along the plane through point square to normal, each face it crosses split in two
void EditMeshMirror(EditMesh mesh, Vector3 axis, Vector3 pivot = default);  // Copies the selected faces mirrored across the plane through a pivot square to an axis, the vertices on the plane shared, and selects the copy
void EditMeshSpin(EditMesh mesh, Vector3 axis, float degrees, int steps, Vector3 center = default);  // Sweeps the selected faces, or the selected edges where no face is selected, round an axis through a point by an angle in a number of steps
void EditMeshSmooth(EditMesh mesh, float factor = 0.5f);  // Moves each selected vertex toward the mean of the vertices it shares an edge with, by a factor from zero to one

void EditMeshModifierMirror(EditMesh mesh, string axes, Vector3 pivot = default);  // Adds a modifier that mirrors the mesh across the planes through a pivot square to each axis named, as "x" or "xz"
void EditMeshModifierArray(EditMesh mesh, int count, Vector3 offset);  // Adds a modifier that repeats the mesh a number of times in all, each copy an offset further on
void EditMeshModifierArrayCurve(EditMesh mesh, ReadOnlySpan<Vector3> positions);  // Adds a modifier that repeats the mesh at each of a curve's positions, moved by the position's offset from the first, which is the mesh itself
void EditMeshModifierBevel(EditMesh mesh, float width, int segments = 1, float angle = 0);  // Adds a modifier that bevels every edge two faces run along, or where an angle is given those whose faces meet at more than it
void EditMeshModifierSubdivision(EditMesh mesh, int level);  // Adds a modifier that subdivides the mesh by Catmull and Clark's rule to a level, each level making every face four-cornered and rounding the shape
void EditMeshModifierSolidify(EditMesh mesh, float thickness);  // Adds a modifier that gives the mesh a thickness, out along its normals or in where the thickness is below zero, its open edges joined across it
void EditMeshModifierTriangulate(EditMesh mesh);         // Adds a modifier that cuts every face of more than three corners into triangles
void EditMeshModifierMove(EditMesh mesh, int from, int to);  // Moves a modifier from one place in the stack to another, by their indices from zero
void EditMeshModifierRemove(EditMesh mesh, int index);   // Takes a modifier out of the stack, by its index from zero
void EditMeshModifierApply(EditMesh mesh, int index);    // Does a modifier to the mesh itself, as though it were first in the stack, and takes it out of the stack
string[] GetEditMeshModifiers(EditMesh mesh);            // The mesh's modifiers in the order they apply, each as its words after modifier in the recipe
EditMeshInfo GetEditMeshModifiedInfo(EditMesh mesh);     // What the mesh builds into with its modifiers applied, its counts, volume and bounds, as GetEditMeshInfo reads the mesh itself

bool ExportEditMesh(EditMesh mesh, string fileName, bool embedBuffer = false);  // Writes what the mesh builds into, its modifiers applied, as a glTF 2.0 file, one primitive a material slot, and says whether it was written
bool EditMeshImport(EditMesh mesh, string fileName);     // Adds the meshes of a model file, as LoadModel reads it, to the mesh as faces, its triangles in one plane joined back into faces, and selects them

Model LoadModelFromEditMesh(EditMesh mesh);              // Builds the mesh into a model, one mesh for each material slot in the order of the slots, each drawn with its slot's look or the prototype material of its slot's name
void UpdateModelFromEditMesh(Model model, EditMesh mesh);  // Writes a model built by LoadModelFromEditMesh again from the mesh, its meshes in place, after the mesh changed

bool EditMeshUnion(EditMesh mesh, string fileOrPrimitive, Vector3 at = default);  // Joins another solid into the mesh where they overlap, a model's recipe file moved by at, or a solid primitive's line
bool EditMeshSubtract(EditMesh mesh, string fileOrPrimitive, Vector3 at = default);  // Takes another solid away from the mesh, a model's recipe file moved by at, or a solid primitive's line
bool EditMeshIntersect(EditMesh mesh, string fileOrPrimitive, Vector3 at = default);  // Keeps what the mesh and another solid both hold, a model's recipe file moved by at, or a solid primitive's line
void EditMeshCut(EditMesh mesh, string primitive);       // Takes a solid primitive, made where its line places it, away from the mesh, as "box 1 1 1 at 1.5 1 -0.25"
void EditMeshCutWindow(EditMesh mesh, float width, float height, float along, float sill);  // Cuts a window through the wall whose face was selected first, measured on every selected face in its plane, as deep as the wall
void EditMeshCutDoor(EditMesh mesh, float width, float height, float along);  // Cuts a door through the wall whose face was selected first, standing on the wall's bottom, as deep as the wall
void EditMeshRoom(EditMesh mesh, Vector3 size, float thickness, Vector3 at = default, bool centered = false);  // Adds a room, a box hollowed out by a thickness of wall, floor and ceiling, which windows and doors are cut through
void EditMeshRamp(EditMesh mesh, float rise, float run, float width, Vector3 at = default, bool centered = false);  // Adds a ramp rising along Z, a wedge given in the words of a flight of stairs, its rise, its run and its width
void EditMeshEpsilon(EditMesh mesh, float distance);     // Sets how near a point is to a plane to lie in it for the mesh's booleans, a hundredth of a millimeter until set, up to a centimeter

Color GetPrototypeColor(string name);                    // The color of the prototype material for a part of a level, by the name of the material slot its faces are in
ModelMaterial LoadMaterialPrototype(string name);        // The prototype material of a part of a level, the grid tinted by the color GetPrototypeColor gives its name
ModelMaterial LoadMaterialPrototype(Color color);        // A material of the grid of a meter's cells tinted by a color, rough and not metal, for blocking out a level

void EditMeshSlotAdd(EditMesh mesh, string slot);        // Adds a material slot no face is in yet, drawn with the prototype material of its name until it is given a look
void EditMeshSlotRename(EditMesh mesh, string slot, string name);  // Gives a material slot another name, its faces and its look kept
void EditMeshSlotRemove(EditMesh mesh, string slot);     // Takes a material slot away, its faces given the first slot, and the slots after it moving down by one
void EditMeshSlotColor(EditMesh mesh, string slot, Color color);  // Gives a material slot a color, which tints its texture, or tints the grid in its place where it has none, an alpha below 255 drawing it blended
void EditMeshSlotMap(EditMesh mesh, string slot, MaterialMapIndex map, string? fileName);  // Gives a material slot a texture or a map from its file, which its faces show by their texture coordinates, or takes it away with null
void EditMeshSlotEmissive(EditMesh mesh, string slot, Vector3 color);  // Gives a material slot an emissive color, in linear numbers that may pass one for a light that blooms past white
void EditMeshSlotMetallic(EditMesh mesh, string slot, float metallic);  // Sets how metal a material slot's faces are, from zero to one
void EditMeshSlotRoughness(EditMesh mesh, string slot, float roughness);  // Sets how rough a material slot's faces are, from zero, a mirror, to one
ModelMaterial LoadMaterialFromEditMeshSlot(EditMesh mesh, string slot);  // The material a slot of an edit mesh is drawn with, its look's, or the prototype material of its name where it has none

Vector2[] GetEditMeshFaceUvs(EditMesh mesh, int face);   // A face's texture coordinates, one for each of its corners in their order, its own or the world-aligned default
void EditMeshUvPlanar(EditMesh mesh, Vector3 facing);    // Gives the selected faces texture coordinates by a planar projection along a direction, in meters
void EditMeshUvBox(EditMesh mesh);                       // Gives the selected faces the world-aligned default as texture coordinates of their own, which then follow their corners
void EditMeshUvCylinder(EditMesh mesh, Vector3 axis, Vector3? center = null);  // Gives the selected faces texture coordinates by a cylindrical projection about an axis
void EditMeshUvSphere(EditMesh mesh, Vector3? center = null);  // Gives the selected faces texture coordinates by a spherical projection about a point
void EditMeshUvMove(EditMesh mesh, Vector2 offset);      // Moves the selected faces' texture coordinates, a whole number leaving a repeating texture where it was
void EditMeshUvScale(EditMesh mesh, Vector2 factor, bool each = false);  // Scales the selected faces' texture coordinates about their middle, two showing the picture twice as often, as a whole or each face about its own middle
void EditMeshUvRotate(EditMesh mesh, float degrees, bool each = false);  // Turns the selected faces' texture coordinates about their middle, from the first coordinate toward the second, as a whole or each face about its own middle
void EditMeshUvDensity(EditMesh mesh, float pixelsPerMeter, int texturePixels);  // Scales the selected faces' texture coordinates so a texture of a width shows so many of its pixels a meter
bool EditMeshUvDensity(EditMesh mesh, float pixelsPerMeter);  // Scales the selected faces' texture coordinates so the texture of the first selected face's slot shows so many of its pixels a meter, and says whether it had one to read the width of
void EditMeshUvReset(EditMesh mesh);                     // Puts the selected faces' texture coordinates back on the world-aligned default

void SetEditMeshName(EditMesh mesh, string name);        // Gives an edit mesh a name the console's model commands reach it by, so a program's level is shaped from the terminal
EditMesh GetEditMeshByName(string name);                 // The edit mesh of a name, given by SetEditMeshName or by the model commands that made it, or one that is not loaded where none has the name

bool DrawModelingWindow(EditMesh mesh, Vector3 position = default);  // Draws an ImGui window that shapes an edit mesh by hand, by buttons for its operations and by the mouse selecting and dragging its faces in the 3D view
RayCollision GetRayCollisionEditMesh(Ray ray, EditMesh mesh, Vector3 position, out int face);  // The face of an edit mesh a ray meets first, from either side, the mesh placed at position, with where it meets it
```

An `EditMesh` is a polygon mesh of faces of any count, shaped by primitives and by operations on
its selection, each of which writes its line to the mesh's recipe. `GetEditMeshInfo` reads its
counts, its selection, its volume and its bounds as an `EditMeshInfo`, whose `IsClosed` says
whether every edge is shared by two faces running along it in opposite directions. A selection
takes a `SelectionMode`, `Replace` unless it says `Add` or `Remove`. A saved model is its recipe, a
`.model` file of one operation a line starting with `model 1`, made again when it loads, and a
model built from the mesh has a mesh for each material slot, drawn with the prototype material of
the slot's name, the grid of a meter tinted by the color of a part of a level, until the slot is
given a look of a color, a texture and the maps of the engine's material, and which
`CreatePhysicsStaticModel` makes a collider of. A face's texture coordinates are its place in
meters until the `EditMeshUv` calls project, move, scale, turn or set them to a texel density, each
a line of the recipe as a slot's look is. The console's `model.*` commands run the recipe's
lines on a mesh by its name, as `./e3d command model.cut house window 1.2 1 4.5 1`, and
`model.show` draws it as it changes. `DrawModelingWindow` shapes one by hand in an ImGui window,
its faces picked by the pointer's ray.

## States

<!-- written by build/cheatsheet.sh from E3D.States.cs -->
```csharp
void AddState<TState>(TState initial);                   // Adds a state machine over an enum, starting at initial, as a game's menu, play and pause are
void AddSubState<TSub, TParent>(TParent whileIn, TSub initial);  // Adds a state machine that exists only while TParent is at whileIn, starting at initial each time the parent gets there, as a pause that exists only while playing
void AddComputedState<TComputed, TSource>(Func<TSource, TComputed?> compute);  // Adds a state machine whose value compute works out from TSource's each time it moves, with none where it gives null, as an in-game state that holds for several playing screens
TState GetState<TState>();                               // The value a state machine is in
void SetState<TState>(TState value);                     // Moves a state machine to value at the start of the next frame, running the exit systems of the value it leaves and the enter systems of the one it reaches
bool IsState<TState>(TState value);                      // Whether a state machine is at value, false for one with no value at present
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
`ecs.DespawnOnEnter(entity, Screen.Title)` takes it as the state enters the value, before its enter
systems, and `ecs.DespawnWhen<Screen>(entity, t => t.To is Screen.Paused)` at the first transition the
rule answers true for.

`ecs.Observe<Add<Health>>(on => ...)` runs the moment an entity gets a `Health`, `Remove<Health>`
once one leaves it, and `ecs.Trigger(new Hit(entity, 10))` sends an event of the game's own to its
observers, the entity's after the rest, and up its parents for an `IPropagatingEvent` until one
calls `on.Propagate(false)`. A window camera with `Picking` set tells the mesh entity under the
mouse what it does to it, as `Pointer<Over>`, `Out`, `Move`, `Press`, `Release`, `Click`,
`DragStart`, `Drag` and `DragEnd`, climbing its parents the same way. A system added with
`new SystemDescriptor(Score, "Score").After("Move")`, or `.Before`, or a list given to
`app.Chain(stage, ...)`, runs in that order within its stage, sorted as the app starts.

## Scenes

<!-- written by build/cheatsheet.sh from E3D.Scenes.cs -->
```csharp
IReadOnlyList<Entity> LoadScene(string fileName);        // Loads a scene file into the ECS, found as models and sounds are, beside the program or in the working directory, and returns the entities it spawned
void SaveScene(string fileName, IEnumerable<Entity>? entities = null);  // Saves every entity of the ECS that a scene did not spawn, or only entities, to a scene file
```

A scene file is JSON of entities and their components by name. The engine's components are saved,
and a program's own are when marked `[SceneComponent]`, as `[SceneComponent] public struct Crate;`,
and `[FormerName("Chest")]` on a component or a field reads a level saved under its old name.
`[DataVersion(2)]` numbers a component's layout, and its static
`JsonObject Migrate(int from, JsonObject value)` brings a level saved at an earlier one up to it.
A `Collider` and a `RigidBody` say what is solid, and the body is made when the scene loads, so
`ecs.GetReadOnly<PhysicsBody>(entity)` gives it to the physics functions. ARCHITECTURE.md describes
the format. Once a file is in, `ecs.Observe<Loaded<Door>>(on => ...)` is told each `Door` it
spawned, with every other entity of the file present, and `ecs.Observe<SceneLoaded>(on => ...)`
once after them, a save read back as any file. `SaveGame.Start(world, scenes)` begins a game,
`SaveGame.Save(world, path)` writes what play changed over its scenes, and `SaveGame.Load` reads the
scenes again with the save laid over them, an entity spawned in play saved where it is `Saved`.
`new Persistent<Settings>("settings.json", fallback)` keeps a `[SceneComponent]` value in a file
read over its default, written by `Save()` or at each `Set` with `saveOnChange: true`. A
`[DataAsset]` struct is kept in a file of its own by `DataAssets.Save("sword.json", value)`, which
gives the file an id at its first save, and a component's `DataRef<Weapon>` field names the file by
that id through any rename, `DataAssets.Load` and `DataAssets.Find` giving one by name or by id.

## Lights

<!-- written by build/cheatsheet.sh from E3D.Lights.cs, E3D.PostProcess.cs, E3D.ReflectionProbes.cs -->
```csharp
LightHandle CreateDirectionalLight(Vector3 direction, Color color, float intensity = 1, bool castsShadows = false);  // A light from far away along direction, as the sun's, which may cast the frame's shadow
LightHandle CreatePointLight(Vector3 position, Color color, float intensity = 1, float range = 0, bool castsShadows = false);  // A light shining every way from position, reaching range units, or every distance with 0, which may cast shadows all around it, as the twelve such lights that matter most to the camera's view do
LightHandle CreateSpotLight(Vector3 position, Vector3 direction, Color color, float intensity = 1, float innerAngle = 25, float outerAngle = 30, float range = 0, bool castsShadows = false);  // A light from position along direction, full inside innerAngle and gone past outerAngle degrees, which may cast a shadow, as the ten such lights that matter most to the camera's view do
void SetLightPosition(LightHandle light, Vector3 position);  // Moves a point or spot light
void SetLightDirection(LightHandle light, Vector3 direction);  // Turns a directional or spot light to shine along direction
void SetLightColor(LightHandle light, Color color, float intensity = 1);  // Sets a light's color and how bright it is
void SetLightCastsShadows(LightHandle light, bool castsShadows);  // Sets whether a light casts shadows, which the first directional light that does casts, and of spot and point lights the ten and the twelve that matter most to the camera's view
void SetShadowDistance(float distance);                  // Sets how far past the camera, in world units, the sun's shadows reach (150 by default), and how far a spot or point light with no range casts them
void SetShadowFill(bool enabled);                        // Sets whether the sun's shadows go on past the shadow distance to the camera's far plane through a fill cascade, which the cascades' count leaves out, on by default
void SetContactShadows(ContactShadows contactShadows);   // Sets where the sun's contact shadows through the scene's distance field darken its light, everywhere the field reaches (ContactShadows.On, the default), past the shadow map's reach alone, or nowhere
void SetShadowMapSize(int size);                         // Sets how many texels wide each tile of the shadow map is, the sun's cascades and the spot lights each having one, rounded to a power of two from 256 to 4096 (2048 by default)
void UnloadLight(LightHandle light);                     // Removes a light
void SetAmbientOcclusion(float intensity, float radius = 1);  // Darkens the light from all around where the surfaces near a point close it off, as where a floor meets a wall or under a crate, by intensity, looking for those surfaces within radius world units, or turns it off with an intensity of 0, which it is by default
void SetAmbientLight(Color color, float intensity);      // Sets the light from all around that keeps the side of a model away from every other light from going black, replacing the one set before, or removes it with an intensity of 0

void SetBloom(float intensity, float threshold = 1);     // Spreads light brighter than threshold into the pixels around it, as a bright light spreads through a lens, adding intensity of it to the frame, or turns bloom off with an intensity of 0, which it is by default
(float Intensity, float Threshold) GetBloom();           // The bloom's intensity and threshold as SetBloom left them, 0 and 1 unless set
void SetExposure(float exposure);                        // Multiplies the scene's light by exposure before it is brought under 1, so 2 brightens a dark scene and 0.5 dims a bright one, 1 being as it is and the default
float GetExposure();                                     // What the scene's light is multiplied by before its curve, as SetExposure left it, 1 unless set
void SetAutoExposure(bool enabled, float min = 0.25f, float max = 4, float speed = 2);  // Makes the exposure follow the scene, as an eye adapts, brightening a dark room and dimming a sunlit field, between min and max, or turns it off
(bool Enabled, float Min, float Max, float Speed) GetAutoExposure();  // Whether the exposure follows the scene, and its bounds and speed, as SetAutoExposure left them, off between 0.25 and 4 at a speed of 2 unless set
void SetTonemap(Tonemap curve);                          // Chooses the curve that brings the scene's light past 1 under it, the engine's own, which leaves colors under 0.9 as they are, Narkowicz's fit of ACES, a cut at 1, or one of the eight Bevy offers, drawn as Bevy draws them
Tonemap GetTonemap();                                    // The curve the scene's light past 1 is brought under by, Tonemap.Engine unless set
void SetTonemapCurve(float exposure, float white, float lower, float upper);  // Brings the scene's light under 1 by a curve set by four numbers, as the BSL shader draws it, and chooses that curve, Tonemap.Curve
(float Exposure, float White, float Lower, float Upper) GetTonemapCurve();  // The numbers of the curve SetTonemapCurve sets, as it left them, 1.4, 2, 1.2 and 1.5 unless set, whether or not that curve is the one chosen
void SetTonemapShader(Shader shader);                    // Draws the window's scene through shader's fragment stage in the curve's place, for a curve no other setting gives, or the chosen curve again with the default shader
Shader GetTonemapShader();                               // The shader SetTonemapShader draws the scene through in the curve's place, or the default shader for the curve
void SetColorGrading(float contrast, float saturation, Color tint);  // Grades the scene's color
(float Contrast, float Saturation, Color Tint) GetColorGrading();  // The contrast, saturation and tint SetColorGrading left, 1, 1 and white unless set
void SetVignette(float intensity, float radius = 0.5f);  // Darkens the scene toward its corners by intensity, 0 for none and 1 to black, from radius of the way from the middle to a corner
(float Intensity, float Radius) GetVignette();           // How much the corners darken and from how far out, as SetVignette left them, 0 and 0.5 unless set
void SetFxaa(bool enabled);                              // Turns FXAA on or off, which smooths the jagged edges multisampling leaves, those inside a surface, as a sharp highlight's or a texture's, and those of lines thinner than a pixel
bool IsFxaaEnabled();                                    // Whether FXAA smooths the frame's edges, as SetFxaa left it, off unless set
void SetDepthOfField(float focusDistance, float focusRange, float blur);  // Blurs what is nearer or farther than focusDistance from the camera, as a camera's lens does, growing over focusRange either side of it to blur of the picture's height across, or turns it off with a blur of 0
(float FocusDistance, float FocusRange, float Blur) GetDepthOfField();  // The depth of field's focus distance, range and blur, as SetDepthOfField left them, 10, 5 and 0, which is off, unless set
void SetMotionBlur(float amount);                        // Blurs the picture along the way the camera moved it since the frame before, by amount of that movement, 0.5 as a film camera's shutter does, or turns it off with 0
void SetMotionBlur(float amount, bool objects);          // Blurs the window's frame along the camera's movement since the frame before and, with objects, each mesh entity along its own, an amount of that movement, or turns it off with 0
(float Amount, bool Objects) GetMotionBlur();            // The share of the movement motion blur blurs along and whether mesh entities blur by their own, as SetMotionBlur left them, 0, which is off, and false unless set
void DrawPostProcessWindow();                            // Draws the effects over the frame in an ImGui window of its own, each shown as it is set and set at once as it is changed, so the picture is tuned while it is looked at

ReflectionProbeHandle CreateReflectionProbe(Vector3 position, Vector3 size, float intensity = 1);  // A box of size around position whose surfaces reflect what is around the box's middle, as a room reflects its own walls, where they would reflect the environment map
void SetReflectionProbeRefresh(ReflectionProbeHandle probe, float seconds);  // Captures a probe again every seconds after its last capture, so a door opening in its room or a thing moving through it is seen, or only on a change for 0, as a probe starts
void UpdateReflectionProbe(ReflectionProbeHandle probe);  // Captures a probe again, as its room is now drawn
bool IsReflectionProbeReady(ReflectionProbeHandle probe);  // Whether a probe's capture of where it is now, under the lights reaching it now, has finished both its passes, so the surfaces in its box reflect the room as it is drawn
void UnloadReflectionProbe(ReflectionProbeHandle probe);  // Removes a probe, and its box reflects the environment map again
```

With no lights, models are drawn unlit, their color and texture as they are, as raylib draws them.
Lights are `Light` entities in the ECS, so lights made here and light entities of a program's own
light the same models.

## Global illumination

<!-- written by build/cheatsheet.sh from E3D.GlobalIllumination.cs -->
```csharp
void SetSceneField(int cascades, float cellSize = 0.25f, int updateBudget = 1, int triangleBudget = 32768);  // Builds a distance field of the scene around the camera, in cascades cascades from 1 to 8, the finest's cells cellSize world units wide, or turns it off with 0 cascades, which it is by default
void SetGlobalIllumination(GlobalIllumination quality);  // Works out global illumination in the window's scene at quality, or none with GlobalIllumination.Off, which it is by default, the light a surface sends on lighting the surfaces around it, so a red wall tints the floor beside it and a room lit by the sun through a window is lit inside
void SetGlobalIlluminationScreenProbes(bool enabled);    // Sets whether global illumination stands a probe on the window's surfaces every few pixels, beside the cascades of probes through the scene, which it does until a program leaves them out
void DrawBounceWindow();                                 // Draws what global illumination holds in an ImGui window of its own, which a program shows while its light is worked on, with how much it traces and the memory that takes, a view of its probes drawn over the window, its parts each to be left out, and a path-traced reference of the view with the frame's error over each region against it
```

The light is worked out each frame and nothing is baked, so every light and mesh may move.

## Particles

<!-- written by build/cheatsheet.sh from E3D.Particles.cs -->
```csharp
ParticleEmitterHandle CreateParticleEmitter(Vector3 position, ParticleEmitter? emitter = null);  // Makes an emitter of particles at position, giving them off as emitter says, or as ParticleEmitter.Default's small white fountain does without one
void SetParticleEmitterPosition(ParticleEmitterHandle emitter, Vector3 position);  // Moves an emitter, where its next particles start, those already emitted going on as they were
void SetParticleEmitterRotation(ParticleEmitterHandle emitter, Quaternion rotation);  // Turns an emitter, which way its next particles start, and its particles too where they live in its space (ParticleEmitter.Local) or stand at its points
void SetParticleEmitter(ParticleEmitterHandle emitter, ParticleEmitter settings);  // Gives an emitter new settings, which its particles alive take from the next frame
ParticleEmitter GetParticleEmitter(ParticleEmitterHandle emitter);  // The settings an emitter has, or the default ones for an emitter that is gone
void SetParticleEmitterActive(ParticleEmitterHandle emitter, bool emitting);  // Turns an emitter's stream on or off, the particles alive living out their lives
void EmitParticles(ParticleEmitterHandle emitter, int count);  // Emits count particles at once in the next frame, as an explosion or a hit does
void UnloadParticleEmitter(ParticleEmitterHandle emitter);  // Removes an emitter and its particles
```

A `ParticleEmitter` holds the rate, the life, the velocity and the cone it is spread over, gravity,
drag, the size and color at birth and at death, how bright an unlit one is, whether lights light
it, whether it adds its light or is laid over by alpha, and a texture each particle is drawn as in
place of a round dot, whole or as a sheet of frames played through over its life, cut or blended
from one to the next, and whether they bounce off or end at the scene the window shows. Its particles
are stepped by a compute shader and drawn after the window's meshes through its camera, and into
a render texture through the camera of its `BeginMode3D`, and an entity with the component in the
ECS is drawn the same way.

## Physics

<!-- written by build/cheatsheet.sh from E3D.Physics.cs -->
```csharp
PhysicsBody CreatePhysicsBox(Vector3 position, Vector3 size, float mass = 1);  // A box of size that falls, collides and is pushed, centered at position
PhysicsBody CreatePhysicsSphere(Vector3 position, float radius, float mass = 1);  // A ball that falls, collides, rolls and is pushed
PhysicsBody CreatePhysicsStaticBox(Vector3 position, Vector3 size);  // A box that never moves, for floors, walls and level geometry
PhysicsBody CreatePhysicsCapsule(Vector3 position, float radius, float height, float mass = 1);  // An upright capsule height units from end to end that falls, collides and is pushed
PhysicsBody CreatePhysicsTrigger(Vector3 position, Vector3 size);  // A box that never moves and stops nothing, which reports what enters it as contacts that start and end, as a goal, a pickup or a door's sensor does
PhysicsBody CreatePhysicsStaticModel(Model model, Vector3 position, float scale = 1);  // A body that never moves, shaped as every triangle of model placed at position and scaled as DrawModel places it, for level geometry a box does not fit
PhysicsBody CreatePhysicsConvexHull(Model model, Vector3 position, float mass = 1, float scale = 1);  // A body shaped as the convex hull of model, the smallest shape without hollows that holds its vertices, scaled as DrawModel scales it, with the model's origin at position, which falls and is pushed with mass, as a rock, a barrel or a crate of the model's own shape

PhysicsJoint CreatePhysicsBallJoint(PhysicsBody a, PhysicsBody b, Vector3 point);  // Joins two bodies at a point in the world, each free to turn about it, as a ball in a socket
PhysicsJoint CreatePhysicsHingeJoint(PhysicsBody a, PhysicsBody b, Vector3 point, Vector3 axis);  // Joins two bodies at a point in the world, turning only around axis, as a door on its hinge
PhysicsJoint CreatePhysicsWeldJoint(PhysicsBody a, PhysicsBody b);  // Joins two bodies rigidly, as they are placed when it is made
PhysicsJoint CreatePhysicsDistanceJoint(PhysicsBody a, PhysicsBody b, Vector3 pointA, Vector3 pointB, float minimum, float maximum);  // Keeps a point on each body between two distances apart, as a rope does with a minimum of 0
PhysicsJoint CreatePhysicsSliderJoint(PhysicsBody a, PhysicsBody b, Vector3 axis);  // Joins two bodies so the second slides along axis against the first and neither turns, as a drawer, a sliding door or a lift on its frame, starting as they are placed
void SetPhysicsSliderLimits(PhysicsJoint slider, float minimum, float maximum);  // Keeps a slider between two distances along its axis from where it was made, as a drawer that stops out and in
void SetPhysicsSliderMotor(PhysicsJoint slider, float speed, float maximumForce);  // Drives a slider at a speed in units a second, toward its axis's tip for a positive one, with no more than a force, as a lift's winch
float GetPhysicsSliderPosition(PhysicsJoint slider);     // How far a slider's second body is along its axis from where it was made, as how high a lift has risen
void SetPhysicsHingeLimits(PhysicsJoint hinge, float minimumDegrees, float maximumDegrees);  // Keeps a hinge turned between two angles in degrees from where it was made, as a door that opens one way
void SetPhysicsHingeMotor(PhysicsJoint hinge, float degreesPerSecond, float maximumTorque);  // Turns a hinge at a speed in degrees a second, with no more than a torque, as a wheel's drive does
void SetPhysicsBallJointLimits(PhysicsJoint ball, Vector3 axis, float swingDegrees, float twistDegrees);  // Keeps a ball joint within a cone around axis, in the world, swung no more than swingDegrees and twisted no more than twistDegrees either way from how the bodies are turned now, as a shoulder or a link of a chain
void SetPhysicsDistanceJointRange(PhysicsJoint joint, float minimum, float maximum);  // Changes how far apart a distance joint keeps its points, as a winch reeling a rope in does a little each frame
void SetPhysicsCharacterStepHeight(PhysicsBody body, float height);  // The highest step a character climbs onto as it walks into it, its radius to begin with
bool SetPhysicsCharacterHeight(PhysicsBody body, float height);  // Makes a character a height with its feet where they are, as crouching and standing do
void DestroyPhysicsJoint(PhysicsJoint joint);            // Removes a joint
bool IsPhysicsJointValid(PhysicsJoint joint);            // Whether a joint exists, which it stops doing when it or one of its bodies is destroyed
float GetPhysicsJointForce(PhysicsJoint joint);          // The force a joint held its bodies together with over the last step, in newtons where mass is in kilograms, as a rope holding a box reads the box's weight
void SetPhysicsBodyMaterial(PhysicsBody body, float friction, float bounce);  // Gives a body a friction, 0 for ice and 1 for rubber, and a bounce, 0 for none and 1 for a ball that comes back as fast as it fell
void SetPhysicsBodyTrigger(PhysicsBody body, bool trigger);  // Makes a body a trigger, which reports what it touches and stops nothing, or a solid body again
PhysicsBody CreatePhysicsKinematicBox(Vector3 position, Vector3 size);  // A box moved only by the program, through SetPhysicsBodyPosition or SetPhysicsBodyVelocity, which pushes what it meets
PhysicsBody CreatePhysicsVehicle(Vector3 position, Vector3 size, float mass = 1000, Vehicle? settings = null);  // A vehicle, a box of size facing -Z, held up on raycast wheels as springs and driven by SetPhysicsVehicleInput, every step pushing it as settings says, a car of about 1000 kg unless set
void SetPhysicsVehicleInput(PhysicsBody vehicle, float throttle, float steer, bool brake = false);  // Drives a vehicle until told otherwise, with throttle and steering from -1 to 1, reverse and right being negative
void SetPhysicsVehicle(PhysicsBody vehicle, Vehicle settings);  // Changes how a vehicle rides and drives, as a tuning screen or a gear does
Vehicle GetPhysicsVehicle(PhysicsBody vehicle);          // How a vehicle rides and drives, to change one value with with
VehicleWheel[] GetPhysicsVehicleWheels(PhysicsBody vehicle);  // A vehicle's wheels as its last step left them, front first, for drawing them and for a skid's sound
PhysicsBody CreatePhysicsCharacter(Vector3 feet, float radius, float height, float mass = 80);  // A character controller's body with its feet at feet, an upright capsule walked by MovePhysicsCharacter that walls stop, that slides along them, rides over a low edge and holds still on a slope (PhysicsWorld.CreateCharacter says how)
void MovePhysicsCharacter(PhysicsBody body, Vector3 velocity);  // The velocity a character walks at along the ground, until it is given another, whose vertical part carries it up and down only while it flies
void JumpPhysicsCharacter(PhysicsBody body, float speed);  // Makes a character on the ground jump, leaving it upward at speed
void SetPhysicsCharacterFly(PhysicsBody body, bool fly);  // Makes a character fly, as a creative mode or a spectator does, or walk again
bool IsPhysicsCharacterGrounded(PhysicsBody body);       // Whether a character stands on ground it can walk on
void SetPhysicsCharacterMaxSlope(PhysicsBody body, float degrees);  // The steepest ground, in degrees, a character walks on, 45 to begin with
void DestroyPhysicsBody(PhysicsBody body);               // Removes a body from the simulation
bool IsPhysicsBodyValid(PhysicsBody body);               // Whether a body exists in the simulation

Matrix4x4 GetPhysicsBodyTransform(PhysicsBody body);     // Where a body is to be drawn this frame, as a model's Model.Transform: its pose blended between its last two steps, so it moves smoothly whatever the frame rate
Vector3 GetPhysicsBodyPosition(PhysicsBody body);        // Where a body is in the simulation, as of its last step
void SetPhysicsBodyPosition(PhysicsBody body, Vector3 position);  // Moves a body to a position at once, keeping its velocity
void PlacePhysicsBody(PhysicsBody body, Vector3 position, Quaternion rotation);  // Puts a body at a position and rotation at rest, as a respawn does
Vector3 GetPhysicsBodyVelocity(PhysicsBody body);        // How fast and which way a body moves, in units a second
void SetPhysicsBodyVelocity(PhysicsBody body, Vector3 velocity);  // Sets how fast and which way a body moves, waking it
void ApplyPhysicsImpulse(PhysicsBody body, Vector3 impulse);  // Pushes a body at its center, changing its velocity by the impulse over its mass
void ApplyPhysicsImpulseAt(PhysicsBody body, Vector3 impulse, Vector3 point);  // Pushes a body at a point in the world, which turns it as well as moving it unless the point is its center of mass, as a wheel's grip on a car or a hit on a crate's corner does
Quaternion GetPhysicsBodyRotation(PhysicsBody body);     // A body's rotation as of its last step
void SetPhysicsBodyRotation(PhysicsBody body, Quaternion rotation);  // Turns a body at once, about its position, as righting a car that rolled over does
Vector3 GetPhysicsBodyAngularVelocity(PhysicsBody body);  // How fast a body turns, about which axis, in radians a second
void SetPhysicsBodyAngularVelocity(PhysicsBody body, Vector3 velocity);  // Sets how fast a body turns, waking it
Vector3 GetPhysicsBodyPointVelocity(PhysicsBody body, Vector3 point);  // How fast a point of a body moves in the world, its velocity and its turn at that point, as a wheel's contact patch
void SetPhysicsPaused(bool paused);                      // Holds the simulation still, as a pause menu does, or lets it run again
bool IsPhysicsPaused();                                  // Whether the simulation is held still
void SetPhysicsGravity(Vector3 gravity);                 // Sets the acceleration every dynamic body falls by, (0, -9.81, 0) to begin with

PhysicsRayCollision GetRayCollisionPhysicsEx(Ray ray, float maxDistance, PhysicsBody ignore);  // The first body a ray meets other than ignore, as a ray cast from inside a car's body down to the ground under a wheel needs, of those ignore's layer collides with
PhysicsRayCollision GetRayCollisionPhysics(Ray ray, float maxDistance);  // The first body a ray meets within maxDistance, with where and at what face, its PhysicsRayCollision.Hit false for none
PhysicsRayCollision GetSphereCastPhysics(Ray ray, float radius, float maxDistance);  // The first body a ball of radius meets moving along a ray within maxDistance, as a thick shot or a camera pulled in from behind a wall, its PhysicsRayCollision.Hit false for none
PhysicsRayCollision GetSphereCastPhysicsEx(Ray ray, float radius, float maxDistance, PhysicsBody ignore);  // The same past one body, as a ball cast from a character's own capsule
PhysicsBody[] GetPhysicsBodiesInSphere(Vector3 center, float radius);  // Every body a sphere overlaps or touches, each once, as what an explosion reaches
void SetPhysicsBodyLayer(PhysicsBody body, int layer);   // Puts a body on one of 32 layers, 0 to 31, which decides what it collides with, as SetPhysicsLayersCollide says
void SetPhysicsBodyContinuous(PhysicsBody body, bool continuous);  // Sweeps a body over each step to find what it would meet within it, as a ball struck hard or a thrown crate needs, so it does not cross a thin wall within one step, or stops sweeping it
int GetPhysicsBodyLayer(PhysicsBody body);               // The layer a body is on
void SetPhysicsLayersCollide(int a, int b, bool collide);  // Whether bodies on layer a collide with bodies on layer b, both ways, as the player's shots pass through the player and the enemies through each other
float GetPhysicsContactImpulse(PhysicsBody a, PhysicsBody b);  // The push the last step gave two touching bodies along the normals of their contacts, in mass times units a second, or 0 for a pair not touching
IReadOnlyList<ContactStarted> GetPhysicsContacts();      // The pairs of bodies that started touching in this frame's steps
IReadOnlyList<ContactEnded> GetPhysicsContactsEnded();   // The pairs of bodies that stopped touching in this frame's steps, as a body leaving a trigger
bool IsPhysicsBodyHit(PhysicsBody body);                 // Whether a body started touching anything in this frame's steps

Ray GetScreenToWorldRay(Vector2 position, Camera3D camera);  // The ray from a camera through a point of the window, as a mouse click points into the scene
Ray GetScreenToWorldRayEx(Vector2 position, Camera3D camera, int width, int height);  // The ray from a camera through a point of a view width by height pixels
```

Bodies are BepuPhysics's, stepped at the fixed rate inside `BeginDrawing`, so a box is drawn by
setting a model's `Transform` to `GetPhysicsBodyTransform(body)` before `DrawModel`. The ECS reaches
the same world as `PhysicsWorld`, with contacts as `ContactStarted` and `ContactEnded` events, and
walks a character through a `CharacterController` component beside its `PhysicsBody`, flying it
where its `Fly` is set. `PhysicsWorld.Place` puts a body at rest where `SetPosition` keeps its
velocity.

## Audio

<!-- written by build/cheatsheet.sh from E3D.Audio.cs, E3D.Waves.cs, E3D.AudioStreams.cs -->
```csharp
void InitAudioDevice();                                  // Opens the audio device
void CloseAudioDevice();                                 // Stops every sound and music the flat API started
bool IsAudioDeviceReady();                               // Whether the audio device is open and has a backend that makes sound
int GetAudioDeviceSampleRate();                          // The rate the audio device plays at, in samples a second
int GetAudioDeviceChannels();                            // The channels the audio device plays, two on most, or 0 before it is open
void SetMasterVolume(float volume);                      // Sets the volume every sound is multiplied by, from 0 to 1
float GetMasterVolume();                                 // The volume every sound is multiplied by

Sound LoadSound(string fileName);                        // Loads a WAV, Ogg Vorbis, MP3 or FLAC file, decoded into memory
bool IsSoundValid(Sound sound);                          // Whether a sound has samples to play
void UnloadSound(Sound sound);                           // Stops a sound
Sound LoadSoundAlias(Sound source);                      // A second sound sharing source's samples and playing apart from it, with a volume, pitch and pan of its own, so one sound is heard over itself, as rapid shots are
void UnloadSoundAlias(Sound alias);                      // Stops an alias, leaving the sound it shares samples with as it is
void PlaySound(Sound sound);                             // Plays a sound from its start, stopping it first if it was playing
void StopSound(Sound sound);                             // Stops a sound
void PauseSound(Sound sound);                            // Pauses a playing sound
void ResumeSound(Sound sound);                           // Resumes a paused sound
bool IsSoundPlaying(Sound sound);                        // Whether a sound is playing
void SetSoundVolume(Sound sound, float volume);          // Sets a sound's volume, from 0 to 1, for this play and the next
void SetSoundPitch(Sound sound, float pitch);            // Sets a sound's pitch as a speed, where 1 is as recorded, for this play and the next
void SetSoundPan(Sound sound, float pan);                // Sets a sound's balance, as raylib's -1 (left) to 1 (right) with 0 in the middle, for this play and the next

Music LoadMusicStream(string fileName);                  // Opens a WAV, Ogg Vorbis, MP3, FLAC, XM or MOD file as music, which streams from the file as it plays
Music LoadMusicStreamFromMemory(string fileType, byte[] data);  // Opens a music file already in memory, by its type, as ".ogg", streamed from the bytes as it plays as one from a file is, as a game whose music comes from a pack file needs
void UnloadMusicStream(Music music);                     // Stops a piece of music and closes its file
bool IsMusicValid(Music music);                          // Whether a piece of music has samples to play
void PlayMusicStream(Music music);                       // Plays a piece of music from its start
void UpdateMusicStream(Music music);                     // Feeds a playing piece of music its next samples from the file
void StopMusicStream(Music music);                       // Stops a piece of music
void PauseMusicStream(Music music);                      // Pauses a piece of music where it is
void ResumeMusicStream(Music music);                     // Resumes a paused piece of music
void SeekMusicStream(Music music, float position);       // Moves a playing or paused piece of music to a time in seconds
bool IsMusicStreamPlaying(Music music);                  // Whether a piece of music is playing, which is started, not paused and not run out
void SetMusicVolume(Music music, float volume);          // Sets a piece of music's volume, from 0 to 1
void SetMusicPitch(Music music, float pitch);            // Sets a piece of music's pitch as a speed, where 1 is as recorded
void SetMusicPan(Music music, float pan);                // Sets a piece of music's balance, as raylib's -1 (left) to 1 (right) with 0 in the middle
float GetMusicTimeLength(Music music);                   // A piece of music's length in seconds
float GetMusicTimePlayed(Music music);                   // How far into the piece the music heard is, in seconds

Wave LoadWave(string fileName);                          // Reads a WAV, Ogg Vorbis, MP3 or FLAC file into a wave
Wave LoadWaveFromMemory(string fileType, byte[] fileData);  // Reads a file already in memory into a wave, by its type, as ".ogg"
bool IsWaveValid(Wave wave);                             // Whether a wave has samples
void UnloadWave(Wave wave);                              // Does nothing, because a wave is managed memory
Sound LoadSoundFromWave(Wave wave);                      // A sound of a wave's samples, which share its array, so the wave is left as it is afterward
Wave WaveCopy(Wave wave);                                // A wave of its own with a copy of another's samples
void WaveCrop(ref Wave wave, int initFrame, int finalFrame);  // Keeps the frames of a wave from initFrame up to, not including, finalFrame
void WaveFormat(ref Wave wave, int sampleRate, int sampleSize, int channels);  // Converts a wave to another rate and number of channels, its samples resampled between neighbors and its channels averaged into one or copied into two
float[] LoadWaveSamples(Wave wave);                      // A copy of a wave's samples, interleaved, from -1 to 1
void UnloadWaveSamples(float[] samples);                 // Does nothing, because the samples are managed memory
bool ExportWave(Wave wave, string fileName);             // Writes a wave to a WAV file of 16-bit samples, beside the program for a relative name

AudioStream LoadAudioStream(int sampleRate, int sampleSize, int channels);  // Makes a stream that plays samples the program gives it, at a rate and with one or two channels
bool IsAudioStreamValid(AudioStream stream);             // Whether a stream can be played, which it can until it is unloaded
void UnloadAudioStream(AudioStream stream);              // Stops a stream and lets its voice go
void UpdateAudioStream(AudioStream stream, ReadOnlySpan<float> samples);  // Queues samples after those a stream has yet to play, interleaved, from -1 to 1
void AttachAudioStreamProcessor(AudioStream stream, AudioCallback processor);  // Runs processor over every piece of samples the stream plays after this, after those attached before it, as raylib's AttachAudioStreamProcessor does, a piece of music's through its Music.Stream
void DetachAudioStreamProcessor(AudioStream stream, AudioCallback processor);  // Stops running processor over a stream's samples
void AttachAudioMixedProcessor(AudioCallback processor);  // Runs processor over every piece of mixed samples the device plays, after those attached before it, as raylib's AttachAudioMixedProcessor does
void DetachAudioMixedProcessor(AudioCallback processor);  // Stops running processor over the mixed samples
void UpdateAudioStream(AudioStream stream, ReadOnlySpan<short> samples);  // Queues 16-bit samples after those a stream has yet to play, interleaved
bool IsAudioStreamProcessed(AudioStream stream);         // Whether a stream has played enough of what it was given to take more, as a loop feeding it asks each frame
void PlayAudioStream(AudioStream stream);                // Plays a stream, from what it has been given
void PauseAudioStream(AudioStream stream);               // Pauses a stream, keeping what it has yet to play
void ResumeAudioStream(AudioStream stream);              // Resumes a paused stream
void StopAudioStream(AudioStream stream);                // Stops a stream and drops what it had yet to play
bool IsAudioStreamPlaying(AudioStream stream);           // Whether a stream is playing
void SetAudioStreamVolume(AudioStream stream, float volume);  // Sets a stream's volume, from 0 to 1
void SetAudioStreamPitch(AudioStream stream, float pitch);  // Sets a stream's pitch as a speed, where 1 plays it at its sample rate
void SetAudioStreamPan(AudioStream stream, float pan);   // Sets a stream's balance, as raylib's -1 (left) to 1 (right) with 0 in the middle
void SetAudioStreamBufferSizeDefault(int size);          // How many frames a stream made after this keeps waiting to be heard before it asks for more, 4096 to begin with
void SetAudioStreamCallback(AudioStream stream, AudioCallback? callback);  // Feeds a stream from a callback, which the end of each frame calls for as many samples as keep the stream fed while it plays
```

A sound is decoded whole when it loads. Music is read from its file half a second ahead of what
is heard, by `UpdateMusicStream`, so a program calls it every frame the music plays, as with
raylib. Pausing one sound or piece of music leaves the others playing.

## Text and fonts

<!-- written by build/cheatsheet.sh from E3D.Text.cs, E3D.Fonts.cs -->
```csharp
void DrawText(string text, int x, int y, int fontSize, Color color);  // Draws text with its top left corner at (x, y), fontSize pixels high, in the default font
int MeasureText(string text, int fontSize);              // The width in pixels DrawText would draw text at
void DrawFPS(int x, int y);                              // Draws the frame rate at (x, y)

Font GetFontDefault();                                   // The engine's default font, ProggyClean at 13 pixels, point filtered so it scales as pixels
Font GetFontDefault(int size);                           // The default font baked at size pixels, kept for later calls at the same size
Font LoadFont(string fileName);                          // Loads a TrueType or OpenType font at 32 pixels, a font drawn as an image, a PNG whose glyphs are separated by magenta from the space on, or a BMFont .fnt file with the images of its pages beside it, as raylib's LoadFont reads each
Font LoadFontFromImage(Image image, Color key, int firstChar);  // Makes a font from an image of its glyphs, as a pixel-art game draws one, each glyph a run of pixels on a row, separated from the next and from the rows above and below by key, the first glyph firstChar and each after the next character
Font LoadFontEx(string fileName, int fontSize);          // Loads a TrueType or OpenType font baked at fontSize pixels, with the Latin-1 characters
Font LoadFontEx(string fileName, int fontSize, int[] codepoints);  // Loads a TrueType or OpenType font baked at fontSize pixels, with exactly the characters in codepoints, as raylib's does
Font LoadFontEx(string fileName, int fontSize, int[]? codepoints, FontType type);  // Loads a TrueType or OpenType font baked at fontSize pixels as type, with the characters in codepoints, or the Latin-1 ones when it is null
int[] LoadCodepoints(string text);                       // The code points of text, one for each character in order, for LoadFontEx
void UnloadFont(Font font);                              // Frees a font's atlas, and those it was baked into at larger sizes
void DrawTextEx(Font font, string text, Vector2 position, float fontSize, float spacing, Color tint);  // Draws text with a font at position, its top left corner, fontSize pixels high, with spacing pixels between characters
void DrawTextPro(Font font, string text, Vector2 position, Vector2 origin, float rotation, float fontSize, float spacing, Color tint);  // Draws text with a font as DrawTextEx does, rotated by rotation degrees around origin, which is relative to the text's top left corner and lands at position
void SetTextLineSpacing(int spacing);                    // Sets the pixels between the tops of a text's lines to its size and this much more, for text drawn and measured after it, in place of each font's own line height
void SetFontLigatures(Font font, bool ligatures);        // Makes a font's standard ligatures or not in the text drawn and measured in it after this, as fi drawn as the one glyph the font has for both or as its two letters
void DrawTextCodepoint(Font font, int codepoint, Vector2 position, float fontSize, Color tint);  // Draws one character, by its code point, in a font
void DrawTextCodepoints(Font font, int[] codepoints, Vector2 position, float fontSize, float spacing, Color tint);  // Draws characters by their code points in a font, as DrawTextEx draws a string
Glyph? GetGlyphInfo(Font font, int codepoint);           // A character's glyph in a font, where it sits and how far it advances, the font's '?' where it lacks the character, as raylib's is, or null when it lacks both
Rectangle GetGlyphAtlasRec(Font font, int codepoint);    // Where a character's glyph lies in the font's atlas, in pixels, the '?' glyph's where the font lacks the character, or an empty rectangle when it lacks both
Font LoadFontFromMemory(string fileType, byte[] fileData, int fontSize, int[]? codepoints);  // Loads a TrueType or OpenType font from a file already in memory, by its type, as ".ttf", baked at fontSize pixels with the characters in codepoints, or the Latin-1 ones when it is null
bool IsFontValid(Font font);                             // Whether a font has an atlas and glyphs, as one loaded does
Vector2 MeasureTextCodepoints(Font font, int[] codepoints, float fontSize, float spacing);  // The size of a line of characters given as codepoints, as MeasureTextEx measures text
Vector2 MeasureTextEx(Font font, string text, float fontSize, float spacing);  // The width and height DrawTextEx would draw text at
```

Text is drawn in the draw list like any shape, so it keeps its place among shapes, reaches render
targets, and draws through `BeginMode3D` on the plane z = 0. `DrawText` bakes the default font at
the size it is drawn, so small text stays sharp, and draws a size below 10 at 10, as raylib's
does. A newline starts a new line. A line in a script written right to left, Hebrew or Arabic, is
drawn in the order it is read, its numbers left to right and Arabic's letters joined with their
marks put on them, and a line of none in the order it is stored, as raylib's is.

## Files

<!-- written by build/cheatsheet.sh from E3D.Files.cs -->
```csharp
string GetApplicationDirectory();                        // The folder the program runs from, ending in a separator, where its resources are
bool FileExists(string fileName);                        // Whether a file exists, beside the program or in the working directory
string? LoadFileText(string fileName);                   // A text file's contents, found beside the program or in the working directory, or null with a warning when there is none, as for a save not yet written, which FileExists asks first
bool SaveFileText(string fileName, string text);         // Writes text to a file, replacing what it held, making its folder when it has none
byte[]? LoadFileData(string fileName);                   // A file's bytes, found beside the program or in the working directory, or null with a warning when there is none
bool SaveFileData(string fileName, ReadOnlySpan<byte> data);  // Writes bytes to a file, replacing what it held, making its folder when it has none
uint ComputeCRC32(ReadOnlySpan<byte> data);              // The CRC-32 of data, as zlib, PNG and raylib reckon it
void TraceLog(LogLevel level, string text);              // Writes a line to the engine's log, under the category Program, as raylib's TraceLog does
void SetTraceLogCallback(Action<LogLevel, string>? callback);  // Hands every line that reaches the console's level to callback as well, with its level, as a game showing the log in its own window does, or stops with null
void SetTraceLogLevel(LogLevel level);                   // Sets the least level a line needs to reach the console, Info to begin with
void OpenURL(string url);                                // Opens a web address in the desktop's browser, as a credits screen's link does
void WaitTime(double seconds);                           // Holds the program for a number of seconds, as a loading screen's pause does
bool IsFileDropped();                                    // Whether files have been dropped on the window since the program last unloaded them
string[] LoadDroppedFiles();                             // The paths of the files dropped on the window, in the order they arrived
void UnloadDroppedFiles();                               // Forgets the dropped files, so IsFileDropped answers for the next drop
```

## Colors

`Color(byte r, byte g, byte b, byte a = 255)`, with `Fade(alpha)` and `ToVector4()`, and raylib's
named colors, `LightGray`, `Gray`, `DarkGray`, `Yellow`, `Gold`, `Orange`, `Pink`, `Red`, `Maroon`,
`Green`, `Lime`, `DarkGreen`, `SkyBlue`, `Blue`, `DarkBlue`, `Purple`, `Violet`, `DarkPurple`,
`Beige`, `Brown`, `DarkBrown`, `White`, `Black`, `Blank`, `Magenta`, `RayWhite`.

<!-- written by build/cheatsheet.sh from E3D.Colors.cs -->
```csharp
Color Fade(Color color, float alpha);                    // The color with its alpha set to alpha, from 0 to 1
Color ColorAlpha(Color color, float alpha);              // The color with its alpha set to alpha, from 0 to 1
int ColorToInt(Color color);                             // The color as a number, red in the high byte and alpha in the low, as 0xRRGGBBAA
Color GetColor(uint hexValue);                           // The color of a number written as 0xRRGGBBAA, as ColorToInt gives
Vector4 ColorNormalize(Color color);                     // The color as four floats from 0 to 1
Color ColorFromNormalized(Vector4 normalized);           // The color of four floats from 0 to 1
Vector3 ColorToHSV(Color color);                         // The color's hue in degrees from 0 to 360, and its saturation and value from 0 to 1
Color ColorFromHSV(float hue, float saturation, float value);  // The opaque color of a hue in degrees, and a saturation and value from 0 to 1
Color ColorTint(Color color, Color tint);                // The color multiplied by tint, channel by channel
Color ColorBrightness(Color color, float factor);        // The color darkened toward black for a factor below 0 or lightened toward white above it, from -1 to 1, its alpha kept
Color ColorContrast(Color color, float contrast);        // The color with its contrast lowered toward gray for a contrast below 0 or raised above it, from -1 to 1, its alpha kept
Color ColorAlphaBlend(Color dst, Color src, Color tint);  // src tinted by tint and laid over dst by its alpha, the color a pixel takes when one is drawn over the other
Color ColorLerp(Color color1, Color color2, float factor);  // The color factor of the way from color1 to color2, from 0 to 1
bool ColorIsEqual(Color col1, Color col2);               // Whether two colors are the same in all four channels
```

## Math

raymath's functions are C#'s own where `System.Numerics` has them, as `Vector2.Dot`, `Vector3.Cross`,
`Matrix4x4.CreateRotationX` and `Quaternion.Slerp`, and the operators for adding, scaling and
multiplying. A raymath `Matrix` is a `Matrix4x4`, composed in the same order. These are raymath's
with no such counterpart, under raymath's names, and `docs/compared-with-raylib.md` says which
name C# gives the rest.

<!-- written by build/cheatsheet.sh from E3D.RayMath.cs -->
```csharp
float Normalize(float value, float start, float end);    // Where value lies from start to end, 0 at the start and 1 at the end
float Remap(float value, float inputStart, float inputEnd, float outputStart, float outputEnd);  // A value carried from one range to another, at the same place in each
float Wrap(float value, float min, float max);           // A value wrapped into the range from min to max, as an angle is into a turn
bool FloatEquals(float x, float y);                      // Whether two floats are equal within a millionth of the larger, or of one where both are smaller

float Vector2CrossProduct(Vector2 v1, Vector2 v2);       // The 2D cross product, the z of the 3D one, positive where v2 turns clockwise on the screen from v1
float Vector2Angle(Vector2 v1, Vector2 v2);              // The signed angle from v1 to v2 in radians, positive clockwise on the screen, where Y is down
float Vector2LineAngle(Vector2 start, Vector2 end);      // The angle of the line from start to end in radians, counterclockwise on the screen from the right
Vector2 Vector2Rotate(Vector2 v, float angle);           // A vector turned by angle radians, clockwise on the screen, where Y is down
Vector2 Vector2MoveTowards(Vector2 v, Vector2 target, float maxDistance);  // A point moved toward target by at most maxDistance, and onto it where it is nearer
Vector2 Vector2ClampValue(Vector2 v, float min, float max);  // A vector with its length held between min and max, its direction kept
bool Vector2Equals(Vector2 p, Vector2 q);                // Whether two vectors are equal within a millionth in each component, as FloatEquals is
Vector2 Vector2Refract(Vector2 v, Vector2 n, float r);   // The direction a ray going along v takes through a surface of normal n, both of length one, where r is the ratio of the refractive index it leaves to the one it enters, or zero where it reflects whole

Vector3 Vector3Perpendicular(Vector3 v);                 // A vector at right angles to v, its cross product with the axis it is least along
float Vector3Angle(Vector3 v1, Vector3 v2);              // The angle between two vectors in radians, from 0 to π
Vector3 Vector3Project(Vector3 v1, Vector3 v2);          // The part of v1 along v2
Vector3 Vector3Reject(Vector3 v1, Vector3 v2);           // The part of v1 at right angles to v2
void Vector3OrthoNormalize(ref Vector3 v1, ref Vector3 v2);  // Makes v1 of length one and v2 of length one at right angles to it, in the plane the two were in (Gram-Schmidt)
Vector3 Vector3RotateByAxisAngle(Vector3 v, Vector3 axis, float angle);  // A vector turned angle radians about axis, counterclockwise looking down the axis
Vector3 Vector3MoveTowards(Vector3 v, Vector3 target, float maxDistance);  // A point moved toward target by at most maxDistance, and onto it where it is nearer
Vector3 Vector3CubicHermite(Vector3 v1, Vector3 tangent1, Vector3 v2, Vector3 tangent2, float amount);  // The cubic Hermite curve between two points with their tangents at amount from 0 to 1, as glTF interpolates
Vector3 Vector3Barycenter(Vector3 p, Vector3 a, Vector3 b, Vector3 c);  // The barycentric coordinates (u, v, w) of p in the triangle a, b, c, in whose plane it lies
Vector3 Vector3Unproject(Vector3 source, Matrix4x4 projection, Matrix4x4 view);  // A point in the space projection clips carried back through it and view into the world
Vector3 Vector3ClampValue(Vector3 v, float min, float max);  // A vector with its length held between min and max, its direction kept
bool Vector3Equals(Vector3 p, Vector3 q);                // Whether two vectors are equal within a millionth in each component, as FloatEquals is
Vector3 Vector3Refract(Vector3 v, Vector3 n, float r);   // The direction a ray going along v takes through a surface of normal n, both of length one, where r is the ratio of the refractive index it leaves to the one it enters, or zero where it reflects whole

Vector4 Vector4MoveTowards(Vector4 v, Vector4 target, float maxDistance);  // A point moved toward target by at most maxDistance, and onto it where it is nearer
bool Vector4Equals(Vector4 p, Vector4 q);                // Whether two vectors are equal within a millionth in each component, as FloatEquals is

float MatrixTrace(Matrix4x4 mat);                        // The sum of a matrix's diagonal
Matrix4x4 MatrixRotateXYZ(Vector3 angle);                // The rotation by the three angles of angle in radians, which turns a point about Z, then Y, then X, as CreateRotationZ(z) * CreateRotationY(y) * CreateRotationX(x) does
Matrix4x4 MatrixRotateZYX(Vector3 angle);                // The rotation by the three angles of angle in radians, which turns a point about X, then Y, then Z, as CreateRotationX(x) * CreateRotationY(y) * CreateRotationZ(z) does
Matrix4x4 MatrixCompose(Vector3 translation, Quaternion rotation, Vector3 scale);  // The transform that scales by scale, turns by rotation and then moves by translation

Quaternion QuaternionLerp(Quaternion q1, Quaternion q2, float amount);  // The quaternion a straight line of the way from q1 to q2, not made of length one, where Quaternion.Lerp is raymath's QuaternionNlerp
Quaternion QuaternionCubicHermiteSpline(Quaternion q1, Quaternion outTangent1, Quaternion q2, Quaternion inTangent2, float t);  // The cubic Hermite spline between two rotations with their tangents at t from 0 to 1, as glTF interpolates, of length one
Quaternion QuaternionFromVector3ToVector3(Vector3 from, Vector3 to);  // The shortest rotation turning the direction from to the direction to
void QuaternionToAxisAngle(Quaternion q, out Vector3 outAxis, out float outAngle);  // The axis a rotation turns about and the angle in radians it turns by, the axis X for a rotation of none
Quaternion QuaternionFromEuler(float pitch, float yaw, float roll);  // The rotation by pitch about X, yaw about Y and roll about Z in radians, composed as raymath composes them, which Quaternion.CreateFromYawPitchRoll does in another order
Vector3 QuaternionToEuler(Quaternion q);                 // A rotation's angles about X, Y and Z in radians, as QuaternionFromEuler takes them
Quaternion QuaternionTransform(Quaternion q, Matrix4x4 mat);  // A quaternion's four components carried through a matrix as a vector's are
bool QuaternionEquals(Quaternion p, Quaternion q);       // Whether two rotations are equal within a millionth, as a quaternion and its negative turn alike
```

## ImGui

Every `ImGui.*` call works between `BeginDrawing` and `EndDrawing`, and is not wrapped here. ImGui
is drawn over everything else. The keyboard moves through its widgets, and the first gamepad too
once `SetImGuiGamepadNavigation(true)` is called.
F1 hides the interface a program draws inside `BeginUi`, F2 writes a screenshot and F3 shows the
engine's debug overlay, keys `SetUiHiddenKey`, `SetScreenshotKey` and `SetDebugOverlayKey` move.
