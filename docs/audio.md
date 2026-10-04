# Audio

Sound comes in two kinds, as in raylib. A `Sound` is a short effect read whole into memory, played
the moment a game calls for it. A `Music` is a long piece streamed from its file as it plays, so a
song of many minutes takes little memory.

## The audio device

`InitAudioDevice` opens the machine's audio output, and sounds are silent until it has.
`CloseAudioDevice` stops every sound and piece of music the program started, before `CloseWindow`.

```csharp
InitWindow(800, 450, "[audio] sound and music");
InitAudioDevice();
// ...
CloseAudioDevice();
CloseWindow();
```

`IsAudioDeviceReady` says whether the device opened with a backend that makes sound. On a machine
with no audio device, as a build server, the program runs on in silence rather than failing, so a
game need not check. `SetMasterVolume` sets a volume from 0 to 1 that every sound and piece of
music is multiplied by, which a game's settings screen changes.

## Sounds

`LoadSound` reads a WAV, Ogg Vorbis, MP3 or FLAC file and decodes it whole, so playing it later
costs nothing. `PlaySound` plays it from the start. From the `audio_sound` example:

```csharp
var coin = LoadSound("resources/coin.wav");
// ...
if (IsKeyPressed(Key.Space)) PlaySound(coin);
// ...
if (IsSoundPlaying(coin)) DrawCircle(460, 156, 12, Color.Gold);
// ...
UnloadSound(coin);
```

Playing a sound that is already playing starts it again from the start. A sound heard several
times over itself, as rapid gunfire, is loaded as many times as it overlaps.

Each sound has a volume from 0 to 1, a pitch where 1 is as recorded and 2 an octave up, and a pan
from 0 at the left to 1 at the right. They hold for the play under way and the plays after it:

```csharp
SetSoundVolume(step, 0.6f);
SetSoundPitch(step, 0.9f + GetRandomValue(0, 20) / 100f);   // each footstep a little different
SetSoundPan(step, 0.5f);
PlaySound(step);
```

`StopSound`, `PauseSound` and `ResumeSound` stop or hold one sound and leave the others playing.

## Music

`LoadMusicStream` opens a file of the same formats as music, read half a second ahead of what is
heard. `UpdateMusicStream` reads the next part from the file, so a program calls it every frame
the music plays, as raylib's does. From `audio_sound`:

```csharp
var drone = LoadMusicStream("resources/drone.ogg");
PlayMusicStream(drone);

var volume = 0.5f;
SetMusicVolume(drone, volume);
SetTargetFPS(60);

while (!WindowShouldClose())
{
    UpdateMusicStream(drone);

    if (IsKeyPressed(Key.Space)) PlaySound(coin);
    if (IsKeyPressed(Key.P))
    {
        if (IsMusicStreamPlaying(drone)) PauseMusicStream(drone);
        else ResumeMusicStream(drone);
    }
    if (IsKeyDown(Key.Up)) volume = Math.Min(1, volume + GetFrameTime());
    if (IsKeyDown(Key.Down)) volume = Math.Max(0, volume - GetFrameTime());
    SetMusicVolume(drone, volume);
    // ...
}
// ...
UnloadMusicStream(drone);
```

Music loops unless its `Looping` is set false, which suits a jingle at the end of
a level. `GetMusicTimeLength` and `GetMusicTimePlayed` give its length and how far into it the
music heard is, in seconds, which the example draws as a bar:

```csharp
// How far through the piece it is, and how loud.
DrawRectangle(20, 140, 400, 12, Color.LightGray);
DrawRectangle(20, 140, (int)(400 * GetMusicTimePlayed(drone) / GetMusicTimeLength(drone)), 12, Color.Maroon);
```

`SeekMusicStream` moves to a time, and music takes a volume, a pitch and a pan as a sound does.

## Sound in a 3D world

A game built on the ECS can place a sound in the world, so it is louder near the listener and
heard from its side. `PlaySpatialSound` on a behavior's context loads and plays a file at a
position, and returns an `AudioSource` the behavior keeps to move the sound later. The listener is
the entity with an `AudioListener` component, usually the camera:

```csharp
ctx.Ecs.Add(camera, AudioListener.Default);
// ...
var engine = ctx.PlaySpatialSound("resources/drone.ogg", truckPosition);
// ...
engine.SetPosition(truckPosition);
```

The [Behaviors and the ECS](behaviors-and-the-ecs.md) page covers behaviors and their context.

## See also

- Examples: [`audio_sound`](../3DEngine.Examples/Audio/AudioSound.cs)
- The cheatsheet's [Audio](../CHEATSHEET.md#audio)
- Previous: [Shaders and compute](shaders-and-compute.md)
- Next: [Input](input.md)
