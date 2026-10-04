# Cheatsheet

Every public function of the flat API, one line each, grouped as raylib groups its own. A program
imports them with `using static Engine.Engine3D;`. [DESIGN.md](DESIGN.md) has the rules they follow.

## Window and timing

```csharp
void SetConfigFlags(ConfigFlags flags);                    // Ask the next window for vsync, fullscreen, no border, topmost, maximized, hidden or 4x MSAA
void SetConfigSamples(int samples);                       // Samples a pixel of the next window, 1 for none, 4 unless asked
void InitWindow(int width, int height, string title);    // Open a window and build the app behind it
void CloseWindow();                                      // Run Cleanup, close the window and free what the app holds
bool WindowShouldClose();                                // Process events; true once the window or the exit key asks to close
bool IsWindowReady();                                    // Whether a window is open
void SetExitKey(Key key);                                // Key that closes the window (Escape by default, Key.Unknown for none)
void SetWindowTitle(string title);                       // Set the window's title
int GetScreenWidth();                                    // Window width
int GetScreenHeight();                                   // Window height
bool IsWindowResized();                                  // Whether its size changed this frame
bool IsWindowFullscreen();                               // Whether it is fullscreen
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
void SetWindowPosition(int x, int y);                    // Move on the desktop
Vector2 GetWindowPosition();                             // Top left corner on the desktop

int GetMonitorCount();                                   // Connected monitors
int GetCurrentMonitor();                                 // The monitor the window is on
int GetMonitorWidth(int monitor);                        // Its width in its current mode
int GetMonitorHeight(int monitor);                       // Its height in its current mode
int GetMonitorRefreshRate(int monitor);                  // Its refresh rate in hertz
string GetMonitorName(int monitor);                      // Its name
void SetClipboardText(string text);                      // Put text on the clipboard
string GetClipboardText();                               // The text on the clipboard
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

RenderTexture2D LoadRenderTexture(int width, int height); // An image drawing can be sent to
void UnloadRenderTexture(RenderTexture2D target);        // Free it
bool IsRenderTextureValid(RenderTexture2D target);       // Whether it is loaded
void BeginTextureMode(RenderTexture2D target);           // Draw into the image until EndTextureMode
void EndTextureMode();                                   // Return to the window
```

Inside texture mode, `ClearBackground` clears the target, 2D drawing is in its pixels and cameras
use its shape. `target.Texture` draws like any texture. A target is drawn before the window in a
frame that sends anything to it, and keeps its picture in frames that do not.

`Camera3D` holds `Position`, `Target`, `Up`, `FovY` (degrees) and `Projection` (`Perspective` or
`Orthographic`). `CameraMode.Free` moves with W, A, S, D, Q and E, turns while the right mouse
button is dragged, and goes faster with Shift. `Orbital` circles the target. `FirstPerson` turns
with the mouse and walks along the ground with W, A, S and D, and `ThirdPerson` does the same
around its target, which the program draws as the player. Both are meant with `DisableCursor`.

## Input

```csharp
bool IsKeyPressed(Key key);                              // Key went down this frame
bool IsKeyDown(Key key);                                 // Key is held
bool IsKeyReleased(Key key);                             // Key came up this frame
bool IsKeyUp(Key key);                                   // Key is not held
Key GetKeyPressed();                                     // Next key pressed this frame, Unknown when none is left
int GetCharPressed();                                    // Next character typed this frame (a code point), 0 when none is left

bool IsMouseButtonPressed(MouseButton button);           // Button went down this frame
bool IsMouseButtonDown(MouseButton button);              // Button is held
bool IsMouseButtonReleased(MouseButton button);          // Button came up this frame
bool IsMouseButtonUp(MouseButton button);                // Button is not held
Vector2 GetMousePosition();                              // Pointer position in the window
int GetMouseX();                                         // Pointer x
int GetMouseY();                                         // Pointer y
Vector2 GetMouseDelta();                                 // How far the pointer moved this frame
float GetMouseWheelMove();                               // How far the wheel turned this frame
void ShowCursor();                                       // Show the cursor
void HideCursor();                                       // Hide the cursor
bool IsCursorHidden();                                   // Whether it is hidden
void DisableCursor();                                    // Hide the cursor and hold it, for mouse look
void EnableCursor();                                     // Release and show it

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
Image GenImageChecked(int width, int height, int checksX, int checksY, Color first, Color second); // A checkerboard of checksX by checksY pixel squares
Image GenImageGradientLinear(int width, int height, int direction, Color start, Color end); // A blend along a direction (0 top to bottom, 90 left to right)
Image GenImageGradientRadial(int width, int height, float density, Color inner, Color outer); // A blend from the center outward
Image GenImageWhiteNoise(int width, int height, float factor);                         // White pixels with the chance factor, the rest black
Image ImageCopy(Image image);                                                          // A copy with pixels of its own
Image ImageFromImage(Image image, Rectangle rec);                                      // A new image of part of one
Color GetImageColor(Image image, int x, int y);                                        // One pixel's color
bool ExportImage(Image image, string fileName);                                        // Write a PNG file
void UnloadImage(Image image);                                                         // Nothing (images are managed memory)

void ImageCrop(ref Image image, Rectangle rec);                                        // Keep part of an image
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
void ImageDrawCircle(ref Image image, int centerX, int centerY, int radius, Color color);      // A filled circle
void ImageDrawCircleLines(ref Image image, int centerX, int centerY, int radius, Color color); // A circle's outline
void ImageDrawRectangle(ref Image image, int x, int y, int width, int height, Color color);    // A filled rectangle
void ImageDrawRectangleRec(ref Image image, Rectangle rec, Color color);               // A filled rectangle
void ImageDrawRectangleLines(ref Image image, Rectangle rec, int thick, Color color);  // A rectangle's outline
void ImageDraw(ref Image destination, Image source, Rectangle sourceRec, Rectangle destinationRec, Color tint); // Part of an image into another, blended

Texture2D LoadTexture(string fileName);                                                // Read an image file into a texture
Texture2D LoadTextureFromImage(Image image);                                           // Upload an image into a texture
void UnloadTexture(Texture2D texture);                                                 // Free a texture
bool IsTextureValid(Texture2D texture);                                                // Whether a texture is loaded
bool UpdateTexture(Texture2D texture, Image image);                                    // Replace a texture's pixels with an image of the same size
void SetTextureFilter(Texture2D texture, TextureFilter filter);                        // Point or Bilinear (the default)
void GenTextureMipmaps(ref Texture2D texture);                                         // Make mip levels on the GPU, so it stays smooth drawn small

void DrawTexture(Texture2D texture, int x, int y, Color tint);                                         // Texture at a position
void DrawTextureV(Texture2D texture, Vector2 position, Color tint);                                    // Texture at a position
void DrawTextureEx(Texture2D texture, Vector2 position, float rotation, float scale, Color tint);       // Rotated (degrees) and scaled
void DrawTextureRec(Texture2D texture, Rectangle source, Vector2 position, Color tint);                // Part of a texture
void DrawTexturePro(Texture2D texture, Rectangle source, Rectangle dest, Vector2 origin, float rotation, Color tint); // Part of a texture into a rectangle, rotated around origin
void DrawBillboard(Camera3D camera, Texture2D texture, Vector3 position, float size, Color tint);       // Texture in 3D, facing the camera
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
```

A shader imports the engine's module, which gives it `VertexOutput` (position, uv, color),
`boundTexture` and `param(slot)`:

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
`lit(color, input)`, the model pass's own lighting. A model shader's uniforms are set by name:

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
ModelMesh GenMeshPoly(int sides, float radius);                             // A flat regular polygon facing up
ModelMesh GenMeshHemiSphere(float radius, int rings, int slices);          // The upper half of a sphere, closed
ModelMesh GenMeshCylinder(float radius, float height, int slices);         // A closed cylinder standing on y 0
ModelMesh GenMeshCone(float radius, float height, int slices);             // A cone standing on y 0
ModelMesh GenMeshTorus(float radius, float size, int radSeg, int sides);   // A ring of radius, a tube of size, lying flat
ModelMesh GenMeshKnot(float radius, float size, int radSeg, int sides);    // A trefoil knot as a tube of size
ModelMesh GenMeshHeightmap(Image heightmap, Vector3 size);                 // Terrain raised by each pixel's brightness
ModelMesh GenMeshCubicmap(Image cubicmap, Vector3 cubeSize);               // A maze, walls where pixels are white
ModelMesh UploadMesh(ModelVertex[] vertices, uint[] indices);              // A mesh of the program's own triangles
void UpdateMeshVertices(ModelMesh mesh, ModelVertex[] vertices);           // Replace a mesh's vertices, keeping its triangles
void UnloadMesh(ModelMesh mesh);                                           // Free a mesh

void SetEnvironmentMap(Image equirectangular, float intensity = 1);         // Light models from all around by a sky image, which smooth and metal surfaces reflect
bool SetEnvironmentMap(string fileName, float intensity = 1);              // The same from a file, a Radiance .hdr keeping light past white
void UnloadEnvironmentMap();                                               // Back to the fixed light, or the light entities alone

ModelAnimation[] LoadModelAnimations(string fileName);                     // Every clip of a model file, sampled at AnimationFps (60) frames a second
void UpdateModelAnimation(Model model, ModelAnimation anim, int frame);    // Pose a model's skinned meshes at a frame of a clip
bool IsModelAnimationValid(Model model, ModelAnimation anim);              // Whether a clip moves the bones a model has
void UnloadModelAnimations(ModelAnimation[] animations);                   // Let clips go

void DrawModel(Model model, Vector3 position, float scale, Color tint);                                               // A model
void DrawModelEx(Model model, Vector3 position, Vector3 rotationAxis, float rotationAngle, Vector3 scale, Color tint); // Rotated (degrees) and scaled
void DrawModelWires(Model model, Vector3 position, float scale, Color tint);                                          // A model's triangle edges
void DrawModelWiresEx(Model model, Vector3 position, Vector3 rotationAxis, float rotationAngle, Vector3 scale, Color tint); // Rotated and scaled
void DrawMesh(ModelMesh mesh, ModelMaterial material, Matrix4x4 transform);                                           // One mesh at a transform
void DrawBoundingBox(BoundingBox box, Color color);                                                                   // A box's edges
```

A `Model` has `Meshes`, `Materials` and `MeshMaterial`, as raylib's does, and a `Transform`, and
a model with a skeleton has `Bones` and `BindPose`, which a `ModelAnimation` of the same file
poses frame by frame. A
`ModelMaterial` is a `Color` and a `Texture`, so `model.Materials[0].Texture = texture;` textures a
mesh, with `Metallic`, `Roughness`, a `NormalMap` and its `NormalScale`, a `MetallicRoughnessMap`
as glTF packs one, an `Emissive` color with its `EmissiveIntensity` and `EmissiveMap`, and an
`OcclusionMap` with its `OcclusionStrength`. Models are lit by one fixed light from above, unless
the ECS holds `Light` entities, and draw through the camera `BeginMode3D` set.

## Scenes

```csharp
IReadOnlyList<int> LoadScene(string fileName);                       // Spawn a scene file's entities into the ECS, found beside the program
void SaveScene(string fileName, IEnumerable<int>? entities = null);  // Write the ECS's entities, or some, to a scene file
```

A scene file is JSON of entities and their components by name. The engine's components are saved,
and a program's own are when marked `[SceneComponent]`, as `[SceneComponent] public struct Wall {
public Vector3 Size; }`. ARCHITECTURE.md describes the format.

## Physics

```csharp
PhysicsBody CreatePhysicsBox(Vector3 position, Vector3 size, float mass = 1);   // A box that falls, collides and is pushed
PhysicsBody CreatePhysicsSphere(Vector3 position, float radius, float mass = 1); // A ball
PhysicsBody CreatePhysicsStaticBox(Vector3 position, Vector3 size);              // A box that never moves, for floors and walls
PhysicsBody CreatePhysicsKinematicBox(Vector3 position, Vector3 size);           // A box only the program moves, which pushes what it meets
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
void SetPhysicsGravity(Vector3 gravity);                                         // What every body falls by
void SetPhysicsPaused(bool paused);                                              // Hold the simulation still, or let it run
bool IsPhysicsPaused();                                                          // Whether it is held still

bool GetRayCollisionPhysics(Ray ray, float maxDistance, out RaycastHit hit);     // The first body a ray meets
IReadOnlyList<ContactStarted> GetPhysicsContacts();                              // Pairs that started touching this frame
bool IsPhysicsBodyHit(PhysicsBody body);                                         // Whether a body started touching anything this frame
Ray GetScreenToWorldRay(Vector2 position, Camera3D camera);                      // The ray through a point of the window
Ray GetScreenToWorldRayEx(Vector2 position, Camera3D camera, int width, int height); // The same for a view of a given size
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

Music LoadMusicStream(string fileName);                   // Open a WAV or Ogg Vorbis file as music, streamed as it plays
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
float GetMusicTimeLength(Music music);                    // Length in seconds
float GetMusicTimePlayed(Music music);                    // How far into it the music heard is, in seconds
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
Font LoadFont(string fileName);                                                             // A TrueType or OpenType font, baked at 32 pixels
Font LoadFontEx(string fileName, int fontSize);                                             // Baked at a size, with the Latin-1 characters
Font LoadFontEx(string fileName, int fontSize, int[] codepoints);                           // Baked with exactly these characters (Greek, Cyrillic, ...)
int[] LoadCodepoints(string text);                                                          // The distinct characters of a text, for LoadFontEx
void UnloadFont(Font font);                                                                 // Free its atlas
void DrawTextEx(Font font, string text, Vector2 position, float fontSize, float spacing, Color tint); // Text in a font
Vector2 MeasureTextEx(Font font, string text, float fontSize, float spacing);               // Its width and height
```

Text is drawn in the draw list like any shape, so it keeps its place among shapes, reaches render
targets, and draws through `BeginMode3D` on the plane z = 0. `DrawText` bakes the default font at
the size it is drawn, so small text stays sharp. A newline starts a new line.

## Colors

`Color(byte r, byte g, byte b, byte a = 255)`, with `Fade(alpha)` and `ToVector4()`, and raylib's
named colors: `LightGray`, `Gray`, `DarkGray`, `Yellow`, `Gold`, `Orange`, `Pink`, `Red`, `Maroon`,
`Green`, `Lime`, `DarkGreen`, `SkyBlue`, `Blue`, `DarkBlue`, `Purple`, `Violet`, `DarkPurple`,
`Beige`, `Brown`, `DarkBrown`, `White`, `Black`, `Blank`, `Magenta`, `RayWhite`.

## ImGui

Every `ImGui.*` call works between `BeginDrawing` and `EndDrawing`, and is not wrapped here. ImGui
is drawn over everything else. F2 shows the engine's performance window.
