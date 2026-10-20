# Moving a game from 5.1 to 6.0

6.0 answers to raylib's names where 5.1 had names of its own. Its keys, gamepad buttons and log
levels are named as raylib names them, a model's skeleton and a clip's keyframes are held as raylib
6 holds them, three functions 5.1 carried under older names are gone, each with the call that does
its work, and the class the flat API is called through is `E3D`, where 5.1's was `Engine3D`. This
page counts from 5.1.116, the 5.1 package these changes came after, and the packages of 5.1 made
since carry some of them already.

Each change is a row saying what a game wrote and what it writes. A build of a game of 5.1 against
6.0 fails at each name a row starts with, so the compiler's errors lead here a line at a time, and
nothing else a game calls has changed. Six changes still compile and do something else, and they
come first.

## Six that still compile

A capsule takes its rings before its slices, as raylib 6's does. Both are numbers, so a call
written for 5.1 compiles and draws its capsule with the two swapped.

| A game wrote | It writes |
|---|---|
| `DrawCapsule(start, end, radius, slices, rings, color)` | `DrawCapsule(start, end, radius, rings, slices, color)` |
| `DrawCapsuleWires(start, end, radius, slices, rings, color)` | `DrawCapsuleWires(start, end, radius, rings, slices, color)` |

The log levels are numbered as raylib numbers them, from `All` below every level to `None` above
them, so `SetTraceLogLevel(LogLevel.None)` keeps every line from the console, as raylib's
`LOG_NONE` does. A game that names its levels changes only `Critical`, below. One that kept a level
as its number, in a settings file or a cast, adds one to it, since each level's number is one
higher.

| Level | Its number in 5.1 | In 6.0 |
|---|---|---|
| `LogLevel.All` | | 0 |
| `LogLevel.Trace` | 0 | 1 |
| `LogLevel.Debug` | 1 | 2 |
| `LogLevel.Info` | 2 | 3 |
| `LogLevel.Warning` | 3 | 4 |
| `LogLevel.Error` | 4 | 5 |
| `LogLevel.Critical`, now `LogLevel.Fatal` | 5 | 6 |
| `LogLevel.None` | | 7 |

A model shader of the program's own returns its color sRGB-encoded with bloom or another effect
over the frame on, as it does with every effect off and in a render texture, where 5.1 read it as
linear light while an effect was on. A shader that returns what `lit`, `unlit` or `toDisplay`
gives, as the guide's do, draws as it did. One that returned a linear color of its own for an
effect, light past white to bloom, returns `toDisplay(light)` in its place.

`Tonemap.Reinhard` is Bevy's Reinhard, each channel over one plus itself, where 5.1's took the
color over one plus its luminance, which is `Tonemap.ReinhardLuminance` now. A gray is drawn the
same by both, and a game that chose Reinhard for how its colors kept their hue chooses
`Tonemap.ReinhardLuminance`.

`GetPhysicsContactImpulse` gives the push of the whole step at any number of substeps, where 5.1's
gave the push of the step's last substep alone. A game at the default of one substep reads what it
did, and one that set `PhysicsSettings.SubstepCount` above 1 reads that many times more, its weight
times the step, as at one substep.

`CreatePhysicsConvexHull` and `CreatePhysicsStaticModel` shape the body from the model's meshes
times the scale given, where 5.1's took the model's `Transform` in as well. A program sets the
`Transform` to a body's to draw it, and a body made from the model after stood where that one was
drawn. A model whose `Transform` held a scale of its own gives it as the scale, and one whose
`Transform` held a turn or a move of its own gives it to the mesh, through `UpdateMeshVertices`
or the file it is loaded from.

## The flat API's class

The class a game calls the flat API through is `E3D`, where 5.1 named it `Engine3D`. The namespace
is `Engine` as before and the package `3DEngine`, so a game changes the class's name where it writes
it and nothing else.

| A game wrote | It writes |
|---|---|
| `using static Engine.Engine3D;` | `using static Engine.E3D;` |
| `Engine3D.UseApp(app)` | `E3D.UseApp(app)`, as every call written through the class |

## Functions

| A game wrote | It writes |
|---|---|
| `GetMouseRay(GetMousePosition(), camera)` | `GetScreenToWorldRay(GetMousePosition(), camera)`, the same ray |
| `GetSplinePointBezierQuad(p1, c2, p3, t)` | `GetSplinePointBezierQuadratic(p1, c2, p3, t)` |
| `ImageDraw(ref dst, src, srcRec, dstRec, tint)` | `ImageDrawImageRec(ref dst, src, srcRec, new Vector2(dstRec.X, dstRec.Y), tint)` where the two rectangles are one size, which draws the same pixels, or `ImageDrawImagePro(ref dst, src, srcRec, dstRec, Vector2.Zero, 0, tint)` to scale, which blends neighboring pixels where `ImageDraw` took the nearest |
| `ImageColorContrast(ref image, 40f)` | `ImageColorContrast(ref image, 40)`, a whole number from -100 to 100 as before |
| `ImageDrawRectangleLines(ref image, rec, thick, color)` | `ImageDrawRectangleLinesEx(ref image, rec, thick, color)`, since `ImageDrawRectangleLines` takes `x, y, width, height, color` and draws a line a pixel wide, as raylib's does |

Each call a row ends with builds on 5.1.116 as well, so a game can make these changes before it
moves to 6.0.

## Keys

Each key keeps its number, so a key a game saved as one reads as the same key.

| A game wrote | It writes |
|---|---|
| `Key.Alpha0` | `Key.Zero` |
| `Key.Alpha1` | `Key.One` |
| `Key.Alpha2` | `Key.Two` |
| `Key.Alpha3` | `Key.Three` |
| `Key.Alpha4` | `Key.Four` |
| `Key.Alpha5` | `Key.Five` |
| `Key.Alpha6` | `Key.Six` |
| `Key.Alpha7` | `Key.Seven` |
| `Key.Alpha8` | `Key.Eight` |
| `Key.Alpha9` | `Key.Nine` |
| `Key.Equals` | `Key.Equal` |
| `Key.KpEquals` | `Key.KpEqual` |
| `Key.KpMinus` | `Key.KpSubtract` |
| `Key.KpPlus` | `Key.KpAdd` |
| `Key.LAlt` | `Key.LeftAlt` |
| `Key.LCtrl` | `Key.LeftControl` |
| `Key.LGUI` | `Key.LeftSuper` |
| `Key.LShift` | `Key.LeftShift` |
| `Key.RAlt` | `Key.RightAlt` |
| `Key.RCtrl` | `Key.RightControl` |
| `Key.RGUI` | `Key.RightSuper` |
| `Key.RShift` | `Key.RightShift` |
| `Key.NumLockClear` | `Key.NumLock` |
| `Key.Application` | `Key.KbMenu` |
| `Key.ACBack` | `Key.Back`, the back button of a phone |
| `Key.Unknown` | `Key.Null`, no key, which `GetKeyPressed` gives when none was pressed |

## Gamepad buttons

A button is named for where it sits, as raylib names it, and keeps its number.

| A game wrote | It writes |
|---|---|
| `GamepadButton.South` | `GamepadButton.RightFaceDown`, A or cross |
| `GamepadButton.East` | `GamepadButton.RightFaceRight`, B or circle |
| `GamepadButton.West` | `GamepadButton.RightFaceLeft`, X or square |
| `GamepadButton.North` | `GamepadButton.RightFaceUp`, Y or triangle |
| `GamepadButton.DpadUp` | `GamepadButton.LeftFaceUp` |
| `GamepadButton.DpadDown` | `GamepadButton.LeftFaceDown` |
| `GamepadButton.DpadLeft` | `GamepadButton.LeftFaceLeft` |
| `GamepadButton.DpadRight` | `GamepadButton.LeftFaceRight` |
| `GamepadButton.LeftShoulder` | `GamepadButton.LeftTrigger1` |
| `GamepadButton.RightShoulder` | `GamepadButton.RightTrigger1` |
| `GamepadButton.LeftStick` | `GamepadButton.LeftThumb`, the left stick pressed |
| `GamepadButton.RightStick` | `GamepadButton.RightThumb`, the right stick pressed |
| `GamepadButton.Back` | `GamepadButton.MiddleLeft`, back or select |
| `GamepadButton.Guide` | `GamepadButton.Middle`, guide or home |
| `GamepadButton.Start` | `GamepadButton.MiddleRight`, start |

## Logging

| A game wrote | It writes |
|---|---|
| `logger.Critical(message)` on an `ILogger` or a `Logger`, as `ILogger.Critical` and `Logger.Critical` | `logger.Fatal(message)`, `ILogger.Fatal` and `Logger.Fatal` |
| A class of its own implementing `ILogger`, with `Critical` | The same class with `Fatal` in its place |

## Models and clips

A model's bones are its skeleton's, and a clip's poses are its keyframes, as raylib 6 holds them.

| A game wrote | It writes |
|---|---|
| `model.Bones` (`Model.Bones`) | `model.Skeleton.Bones`, `Model.Skeleton` being a `ModelSkeleton` |
| `model.BindPose` (`Model.BindPose`) | `model.Skeleton.BindPose` |
| `model.Bones.Length` | `model.Skeleton.BoneCount` |
| `clip.FramePoses` (`ModelAnimation.FramePoses`) | `clip.KeyframePoses` (`ModelAnimation.KeyframePoses`) |
| `clip.FrameMorphWeights` (`ModelAnimation.FrameMorphWeights`) | `clip.KeyframeMorphWeights` (`ModelAnimation.KeyframeMorphWeights`) |
| `clip.FrameCount` (`ModelAnimation.FrameCount`) | `clip.KeyframeCount` (`ModelAnimation.KeyframeCount`) |

## Added

Nothing a game of 5.1 writes changes for these. They are the functions and types 6.0 has that 5.1
lacked, grouped by what they do, and what calls 5.1 had do now that they did not, each with the
guide that shows it.

- **The scene as a distance field.** `SetSceneField` builds a distance field of the meshes that
  cast shadows, in cascades around the camera on the GPU, which ambient occlusion reads beside the
  window's depth, the sun casts soft contact shadows through and a particle emitter that collides
  meets off the screen. `Config.SceneField` takes a `SceneFieldConfig` of the same cascades, cell
  size and budget for an app made from a `Config`, as
  [The scene as a distance field](materials-light-and-shadows.md#the-scene-as-a-distance-field)
  shows.
- **Global illumination.** `SetGlobalIllumination` with `GlobalIllumination.Low`, `Medium` or
  `High` traces the indirect light between surfaces each frame through that field, nothing
  baked, so every light and mesh may move, and `GlobalIllumination.Off`, the default, leaves it
  out. `Config.GlobalIllumination` sets it for an app made from a `Config`, as
  [Global illumination](global-illumination.md) shows with each quality's
  cost. `DrawBounceWindow` shows its probes, leaves its parts out one at a time and measures the
  frame against a path-traced reference, in an ImGui window of its own.
- **Glossy surfaces reflect.** Where global illumination is on, a surface with a roughness under 0.5
  traces its reflection through the window's depth and on through the field, and at `High` on a GPU
  that traces rays itself, where the field misses, against the meshes' own triangles. Render
  textures and reflection probes' captures take indirect light, in the same guide.
- **Text shaped.** A line in a script written right to left is drawn in the order it is read,
  Arabic's letters join, with their marks put on them, their pairs kerned and Nastaliq's letters
  joined along the word by the font's own tables, and a letter and the mark after it are drawn as
  the one character Unicode has for both, as
  [Text read right to left](text-and-fonts.md#text-read-right-to-left) shows.
- **Color fonts.** A color emoji font draws its emoji in color, held as pictures, as Apple's
  bitmaps, as colored layers or as COLR version 1's gradients, and joins families, flags, skin
  tones and keycaps into their pictures. Characters past U+FFFF are drawn from a font's TrueType or
  CFF outlines, and in a distance field font. A font file the atlas cannot read is refused with the
  reason rather than stopping the program, and a font collection is read as its first font, as
  [Characters past Latin-1](text-and-fonts.md#characters-past-latin-1) shows.
- **Text from a file where raylib's lies.** Text in a font loaded from a file advances by whole
  pixels on raylib's baseline, so a line may sit a little apart from where 5.1 drew it.
- **ImGui's viewports.** An ImGui window dragged outside the game's window gets a window of its
  own, drawn in the same frame, once a program sets `ImGuiConfigFlags.ViewportsEnable`, on X11,
  Windows and macOS, as [ImGui in the same frame](window-and-frame.md#imgui-in-the-same-frame)
  shows.
- **VR.** `LoadVrStereoConfig`, `BeginVrStereoMode`, `EndVrStereoMode` and `UnloadVrStereoConfig`,
  with `VrDeviceInfo` and `VrStereoConfig`, draw a frame for each eye of a headset, as raylib's do
  and [Drawing into a texture](drawing-3d-and-cameras.md#drawing-into-a-texture) shows.
- **States.** `DespawnOnEnter<TState>` and `DespawnWhen<TState>`, and `EcsWorld.DespawnOnEnter`
  and `EcsWorld.DespawnWhen`, which add them, despawn an entity when a state is entered or when a
  rule of its transitions holds, as [States](states.md) shows.
- **Sound.** `Sound.FrameCount` and `Music.FrameCount`, how many frames of samples each holds, and
  `BehaviorSounds.PlaySpatialSound`, a sound placed in the world from a behavior's context, as
  [Audio](audio.md) shows. A run that shows no window plays to SDL's dummy driver and makes no
  sound, which `Config.AudibleWithoutWindow` turns off.
- **Textures and the window.** `TextureWrap.MirrorClamp`, and the window flags
  `ConfigFlags.InterlacedHint` and `ConfigFlags.WindowMousePassthrough`.
- **Models.** A vertex stage of a program's own is fed each input by its semantic, so it takes them
  in any order with no locations of its own, as
  [Moving vertices](shaders-and-compute.md#moving-vertices) shows. An entity's `AnimatedModel`
  plays in an app a program built itself, and a body made from a model the GPU posed takes its
  pose, as [Bodies that fall](physics.md#bodies-that-fall) shows.
- **A vertex stream of the program's own.** `UploadMesh` takes a `MeshStream` after a mesh's
  colors and second texture coordinates, its values one a vertex in one of the five
  `MeshStreamFormat`s, which a vertex stage of the program's own reads as `CUSTOM0`, and
  `UpdateMeshBuffer` writes it in place at `MeshStreamBuffer`. `UploadMesh` takes a mesh's
  positions alone with its stream, twelve bytes a vertex, for a stream that packs the normal and
  texture coordinate, as
  [A vertex stream of the program's own](shaders-and-compute.md#a-vertex-stream-of-the-programs-own)
  shows.
- **A shader that keeps its positions.** `SetShaderKeepsPositions` says a model shader's vertex
  stage keeps its mesh's positions, so a mesh drawn with it is left out of each pass that cannot
  see the mesh's box, the camera's and each shadow cascade's among them, where it was drawn whole
  in every pass, as [Moving vertices](shaders-and-compute.md#moving-vertices) shows.
- **A mesh global illumination leaves out.** `ModelMaterial.BouncesLight`, true by default,
  set false for a mesh that moves in most frames, as a game's animal, which casts its shadows and
  is lit as before while the scene's field and global illumination leave it out, as
  [The scene as a distance field](materials-light-and-shadows.md#the-scene-as-a-distance-field)
  shows.
- **Effects read and tuned.** `GetTonemap`, `GetExposure`, `GetAutoExposure`, `GetColorGrading`,
  `GetVignette`, `IsFxaaEnabled`, `GetDepthOfField`, `GetMotionBlur` and `GetBloom` read what each
  effect's setter left, and `DrawPostProcessWindow` shows and sets every one in an ImGui window, as
  [Effects over the frame](materials-light-and-shadows.md#effects-over-the-frame) shows.
- **An animated entity's body.** `Collider.Mesh` and `Collider.ConvexHull` on an entity an
  `AnimatedModel` draws shape its body from the model as the clip last posed it, where they were
  refused with a warning, as
  [Models and animation](models-and-animation.md#animation) shows.
- **Shadows.** The spot and point lights given shadows are ranked for every camera the frame draws
  meshes through, so a render texture shadows the lights it sees, as
  [Shadows](materials-light-and-shadows.md#shadows) shows.
- **Light under the surface.** `ModelMaterial.SubsurfaceRadius` and `ModelMaterial.SubsurfaceColor`,
  and a mesh entity's `Material.SubsurfaceRadius` and `Material.SubsurfaceColor`, spread the light a
  surface scatters under it, as skin, wax and marble do, over the window's frame, and a thin part
  lit from behind shows the light that comes through it, at the `SubsurfaceQuality`, `Low`,
  `Medium` or `High`, that `SetSubsurfaceQuality` sets, as
  [Light under the surface](materials-light-and-shadows.md#light-under-the-surface) shows.
- **A part's own thickness.** `ModelMaterial.SubsurfaceThickness` and a mesh entity's
  `Material.SubsurfaceThickness` say how thick a scattering material's parts are at most, which a
  glTF file's `KHR_materials_volume` thickness sets, so a leaf drawn as one sheet lets the light
  from behind through, and a lamp the scene's field cannot measure toward does too.
- **Bevy's tonemappers.** `SetTonemap` takes the eight Bevy offers, `Tonemap.None`,
  `Tonemap.Reinhard`, `Tonemap.ReinhardLuminance`, `Tonemap.AcesFitted`, `Tonemap.AgX`,
  `Tonemap.SomewhatBoring`, `Tonemap.TonyMcMapface` and `Tonemap.BlenderFilmic`, drawn as Bevy
  draws them, AgX, Tony McMapface and Blender's filmic from Bevy's own tables, as
  [Effects over the frame](materials-light-and-shadows.md#effects-over-the-frame) shows with what a
  table costs.
- **Particles that turn with their emitter, and particles placed.** `ParticleEmitter.Local` keeps
  an emitter's particles in its entity's space, so they move and turn with its transform and its
  parent's, `ParticleEmitter.Points` places particles of the program's own at points in that space,
  standing until the array no longer holds them, and `SetParticleEmitterRotation` turns an emitter
  made through the flat API, as [Particles](drawing-3d-and-cameras.md#particles) shows.
- **The sun's contact shadows chosen.** `SetContactShadows` with `ContactShadows.On`, the default,
  `ContactShadows.PastShadowMap` or `ContactShadows.Off` says where the sun's contact shadows through
  the scene's distance field darken its light, so a shadow the map holds may keep its hard edge, as
  [The scene as a distance field](materials-light-and-shadows.md#the-scene-as-a-distance-field)
  shows.
- **A program's textures in ImGui.** `GetImGuiTextureId` gives a texture's id for `ImGui.Image`,
  `ImGui.ImageButton` and a draw list's `AddImage`, which drew a part of the font atlas before, as
  [ImGui in the same frame](window-and-frame.md#imgui-in-the-same-frame) shows.
- **A joint's force.** `GetPhysicsJointForce` says how hard a joint held its bodies together over
  the last step, the force alone in newtons, so a rope or a bridge breaks past a load the program
  chose, as [Joints](physics.md#joints) shows.
- **The audio device's format.** `GetAudioDeviceSampleRate` and `GetAudioDeviceChannels` say the
  rate and the channels a mixed processor's samples come in, so a program saves what the device
  plays as a wave of the right speed, as [Sound the program makes](audio.md#sound-the-program-makes)
  shows. A backend of the program's own gives them as `IAudioBackend.DeviceSampleRate` and
  `IAudioBackend.DeviceChannels`, which are 0 until it says otherwise.
