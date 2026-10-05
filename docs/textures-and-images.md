# Textures and images

An `Image` is pixels in memory, which the program reads, generates and edits on the CPU. A
`Texture2D` is pixels on the GPU, which the frame draws. A picture usually starts as an image and
is uploaded once as a texture, and a file the program only draws can be loaded straight as one.

## Loading and drawing a texture

`LoadTexture` reads a PNG, JPEG, BMP, TGA, PSD, GIF or HDR file from beside the program. `DrawTexture` draws it with its top left corner at a pixel, and `UnloadTexture` frees it
when the program is done with it. From the `textures_basic` example:

```csharp
var logo = LoadTexture("resources/logo.png");
var checker = LoadTextureFromImage(GenImageChecked(64, 64, 8, 8, Color.DarkGray, Color.LightGray));
SetTextureFilter(checker, TextureFilter.Point);
// ...
DrawTextureEx(checker, new Vector2(20, 60), 0, 2, Color.White);
DrawTexturePro(logo, new Rectangle(0, 0, logo.Width, logo.Height), new Rectangle(690, 340, 128, 128),
    new Vector2(64, 64), rotation, Color.DarkBlue);
DrawTexturePro(logo, new Rectangle(0, 0, logo.Width / 2f, logo.Height), new Rectangle(170, 60, 64, 128),
    Vector2.Zero, 0, Color.Orange);
// ...
UnloadTexture(logo);
UnloadTexture(checker);
```

The color a texture is drawn with multiplies its pixels, so `Color.White` draws it as it is and
another color tints it. The four drawing calls go from simple to complete:

| Call | Draws |
|---|---|
| `DrawTexture(texture, x, y, tint)` | At a pixel, at its size |
| `DrawTextureEx(texture, position, rotation, scale, tint)` | Turned and scaled about its corner |
| `DrawTextureRec(texture, source, position, tint)` | Part of it, as a frame of a sprite sheet |
| `DrawTexturePro(texture, source, dest, origin, rotation, tint)` | Part of it into a rectangle, turned about an origin |

`DrawTexturePro` draws any part of a texture stretched into any rectangle, turned about a point
given in the rectangle's own pixels, which is how sprites from a sheet are drawn, turned about
their middle.

## Filtering and wrapping

A texture drawn larger or smaller than its size is filtered. `SetTextureFilter` picks how:

| Filter | Suits |
|---|---|
| `TextureFilter.Point` | Pixel art, every texel a sharp square |
| `TextureFilter.Bilinear` | Photographs and smooth art, blended between texels, in the nearest mip level |
| `TextureFilter.Trilinear` | The same, blended between mip levels too, so a texture drawn ever smaller shows no step |
| `TextureFilter.Anisotropic4x`, `8x` and `16x` | Textures seen at a slant, as a floor stretching away |

A texture drawn much smaller than its size, as a floor stretching away, breaks into noise unless it
has mip levels, smaller copies of itself the GPU reads instead. `GenTextureMipmaps` makes them,
and `Trilinear` blends between them, which the `textures_mipmaps` example shows beside a texture
without:

```csharp
var image = GenImageChecked(512, 512, 8, 8, Color.Black, Color.White);
var plain = LoadTextureFromImage(image);
var mipmapped = LoadTextureFromImage(image);
GenTextureMipmaps(ref mipmapped);
SetTextureFilter(mipmapped, TextureFilter.Trilinear);
```

`SetTextureWrap` sets what lies past a texture's edge when a model's texture coordinates reach
beyond it, repeated, clamped to the edge or mirrored.

## Generating and editing images

Images are made and changed on the CPU, where every pixel can be read, then uploaded once. The
`textures_image_drawing` example builds a picture from a gradient, shapes and another image:

```csharp
// An image is pixels in memory. It is edited on the CPU and uploaded once as a texture.
var image = GenImageGradientLinear(400, 300, 45, Color.DarkBlue, Color.SkyBlue);
ImageDrawRectangleLines(ref image, new Rectangle(0, 0, 400, 300), 4, Color.RayWhite);
ImageDrawCircle(ref image, 300, 90, 50, Color.Gold);
ImageDrawCircleLines(ref image, 300, 90, 60, Color.Orange);
ImageDrawRectangle(ref image, 40, 200, 140, 60, Color.Maroon);

// Another image, made smaller, turned and tinted, drawn into the first with its alpha.
var logo = LoadImage("resources/logo.png");
ImageResize(ref logo, 96, 96);
ImageRotateCW(ref logo);
ImageColorTint(ref logo, Color.RayWhite.Fade(0.85f));
ImageDraw(ref image, logo, new Rectangle(0, 0, logo.Width, logo.Height), new Rectangle(230, 170, 120, 120), Color.White);

var texture = LoadTextureFromImage(image);
```

An image function that changes the image takes it by `ref`, and one that makes a new image returns
it. The generators make gradients, checkerboards, noise, cellular patterns and plain colors, the
editors crop, resize, flip, turn and change colors, and `ImageDraw*` draws shapes and text into an
image as the frame draws them on the screen. `GetImageColor` reads a pixel, `LoadImageColors`
every pixel, and `ExportImage` writes an image to a PNG, or `ExportImageToMemory` to bytes.

Past those, images are blurred (`ImageBlurGaussian`), convolved with a kernel of the program's own
(`ImageKernelConvolution`), turned by any angle (`ImageRotate`), dithered down to a few bits a
channel (`ImageDither`), and trimmed to what is not clear (`GetImageAlphaBorder`). `ImageText`
makes an image of text, and `LoadImageAnim` reads every frame of an animated GIF into one image,
stacked from the top, which a sprite drawn frame by frame reads as a sheet.

A texture already loaded is changed with `UpdateTexture(texture, image)`, which uploads an image of
the same size again, as a picture drawn by the program every frame needs.

## Sprites and animation

A sprite sheet is one texture holding many frames, and an animation draws a different part of it
each frame:

```csharp
var frame = (int)(GetTime() * 12) % 6;   // six frames, twelve a second
var source = new Rectangle(frame * 32, 0, 32, 32);
DrawTexturePro(sheet, source, new Rectangle(x, y, 64, 64), new Vector2(32, 32), 0, Color.White);
```

A frame drawn mirrored has a negative width in its source rectangle. `DrawTextureNPatch` draws a
panel or button whose corners keep their size while its middle stretches.

## Textures in 3D

`DrawBillboard` draws a texture in the world, always facing the camera, as a tree far away or a
particle. A model's material holds the texture it is drawn with, set by
`model.Materials[0].Texture = texture`, and the [Drawing in 3D and cameras](drawing-3d-and-cameras.md)
page draws a scene into a texture with `BeginTextureMode`.

## Many sprites

Drawing a texture costs little, and the immediate pass draws every sprite of one texture in one
call. The `textures_bunnymark` example finds how many sprites a frame holds at 60 frames a second
on the machine it runs on, which is over a hundred thousand on a desktop GPU.

## See also

- Examples: [`textures_basic`](../3DEngine.Examples/Textures/TexturesBasic.cs),
  [`textures_image_drawing`](../3DEngine.Examples/Textures/TexturesImageDrawing.cs),
  [`textures_mipmaps`](../3DEngine.Examples/Textures/TexturesMipmaps.cs),
  [`textures_bunnymark`](../3DEngine.Examples/Benchmarks/TexturesBunnymark.cs)
- The cheatsheet's [Images and textures](../CHEATSHEET.md#images-and-textures)
- Previous: [Drawing in 3D and cameras](drawing-3d-and-cameras.md)
- Next: [Text and fonts](text-and-fonts.md)
