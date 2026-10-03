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
void TakeScreenshot(string fileName);                    // Write the frame being drawn to a PNG once it is presented

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

bool IsGamepadAvailable(int gamepad);                                  // Whether a pad is connected at that index
string GetGamepadName(int gamepad);                                    // Its name
bool IsGamepadButtonPressed(int gamepad, GamepadButton button);        // Button went down this frame
bool IsGamepadButtonDown(int gamepad, GamepadButton button);           // Button is held
bool IsGamepadButtonReleased(int gamepad, GamepadButton button);       // Button came up this frame
bool IsGamepadButtonUp(int gamepad, GamepadButton button);             // Button is not held
float GetGamepadAxisMovement(int gamepad, GamepadAxis axis);           // Stick -1 to 1, trigger 0 to 1
int GetGamepadAxisCount(int gamepad);                                  // Six for a connected pad
void SetGamepadVibration(int gamepad, float left, float right, float seconds); // Rumble
```

Pads are indexed in the order they connected. Buttons are named by position (`South`, `East`,
`West`, `North`, `DpadUp`, `LeftShoulder`, `Start`, ...) rather than by the letter printed on them.

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

## Images and textures

```csharp
Image LoadImage(string fileName);                                                      // Read PNG, JPEG, BMP, TGA, PSD, GIF or HDR into memory
Image GenImageColor(int width, int height, Color color);                               // An image of one color
Image GenImageChecked(int width, int height, int checksX, int checksY, Color first, Color second); // A checkerboard
Color GetImageColor(Image image, int x, int y);                                        // One pixel's color
void UnloadImage(Image image);                                                         // Nothing (images are managed memory)

Texture2D LoadTexture(string fileName);                                                // Read an image file into a texture
Texture2D LoadTextureFromImage(Image image);                                           // Upload an image into a texture
void UnloadTexture(Texture2D texture);                                                 // Free a texture
bool IsTextureValid(Texture2D texture);                                                // Whether a texture is loaded
bool UpdateTexture(Texture2D texture, Image image);                                    // Replace a texture's pixels with an image of the same size
void SetTextureFilter(Texture2D texture, TextureFilter filter);                        // Point or Bilinear (the default)

void DrawTexture(Texture2D texture, int x, int y, Color tint);                                         // Texture at a position
void DrawTextureV(Texture2D texture, Vector2 position, Color tint);                                    // Texture at a position
void DrawTextureEx(Texture2D texture, Vector2 position, float rotation, float scale, Color tint);       // Rotated (degrees) and scaled
void DrawTextureRec(Texture2D texture, Rectangle source, Vector2 position, Color tint);                // Part of a texture
void DrawTexturePro(Texture2D texture, Rectangle source, Rectangle dest, Vector2 origin, float rotation, Color tint); // Part of a texture into a rectangle, rotated around origin
void DrawBillboard(Camera3D camera, Texture2D texture, Vector3 position, float size, Color tint);       // Texture in 3D, facing the camera
```

A file name is looked for as given, then beside the program, then under `source/` beside it. A file
that cannot be read gives an invalid image or texture and a warning in the log, and drawing an
invalid texture draws nothing. `CloseWindow` logs how many textures were still loaded.

## Models and meshes

```csharp
Model LoadModel(string fileName);                                          // Read a model through Assimp (glTF, FBX, OBJ, ...) with its base colors and textures
Model LoadModelFromMesh(ModelMesh mesh);                                   // A model of one mesh with a white material
void UnloadModel(Model model);                                             // Free a model's meshes and the textures it loaded
bool IsModelValid(Model model);                                            // Whether a model's meshes are loaded
BoundingBox GetModelBoundingBox(Model model);                              // The box around a model

ModelMesh GenMeshCube(float width, float height, float length);            // A box
ModelMesh GenMeshSphere(float radius, int rings, int slices);              // A sphere
ModelMesh GenMeshPlane(float width, float length, int resX, int resZ);     // A flat rectangle facing up
ModelMesh UploadMesh(ModelVertex[] vertices, uint[] indices);              // A mesh of the program's own triangles
void UnloadMesh(ModelMesh mesh);                                           // Free a mesh

void DrawModel(Model model, Vector3 position, float scale, Color tint);                                               // A model
void DrawModelEx(Model model, Vector3 position, Vector3 rotationAxis, float rotationAngle, Vector3 scale, Color tint); // Rotated (degrees) and scaled
void DrawMesh(ModelMesh mesh, ModelMaterial material, Matrix4x4 transform);                                           // One mesh at a transform
void DrawBoundingBox(BoundingBox box, Color color);                                                                   // A box's edges
```

A `Model` has `Meshes`, `Materials` and `MeshMaterial`, as raylib's does, and a `Transform`. A
`ModelMaterial` is a `Color` and a `Texture`, so `model.Materials[0].Texture = texture;` textures a
mesh. Models are lit by one fixed light from above, and draw through the camera `BeginMode3D` set.

## Audio

```csharp
void InitAudioDevice();                                   // Open the audio device (sounds are silent until then)
void CloseAudioDevice();                                  // Stop every sound and music the flat API started
bool IsAudioDeviceReady();                                // Whether the device is open with a backend that makes sound
void SetMasterVolume(float volume);                       // Volume every sound is multiplied by (0 to 1)
float GetMasterVolume();                                  // That volume

Sound LoadSound(string fileName);                         // Read a WAV or Ogg Vorbis file into memory
bool IsSoundValid(Sound sound);                           // Whether a sound has samples
void UnloadSound(Sound sound);                            // Stop a sound
void PlaySound(Sound sound);                              // Play from the start, restarting it if it was playing
void StopSound(Sound sound);                              // Stop
void PauseSound(Sound sound);                             // Pause
void ResumeSound(Sound sound);                            // Resume
bool IsSoundPlaying(Sound sound);                         // Whether it is playing
void SetSoundVolume(Sound sound, float volume);           // Volume (0 to 1), now and for the next play
void SetSoundPitch(Sound sound, float pitch);             // Speed, where 1 is as recorded

Music LoadMusicStream(string fileName);                   // Read a WAV or Ogg Vorbis file as music
void UnloadMusicStream(Music music);                      // Stop music
bool IsMusicValid(Music music);                           // Whether music has samples
void PlayMusicStream(Music music);                        // Play from the start, looping unless music.Looping is false
void UpdateMusicStream(Music music);                      // Nothing (music is decoded whole)
void StopMusicStream(Music music);                        // Stop
void PauseMusicStream(Music music);                       // Pause
void ResumeMusicStream(Music music);                      // Resume
bool IsMusicStreamPlaying(Music music);                   // Whether it is playing
void SetMusicVolume(Music music, float volume);           // Volume (0 to 1)
void SetMusicPitch(Music music, float pitch);             // Speed, where 1 is as recorded
float GetMusicTimeLength(Music music);                    // Length in seconds
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
