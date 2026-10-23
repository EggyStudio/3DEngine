# Review archive

What REVIEW.md no longer carries, moved here at 19:30 on 2026-10-23 and after: the items of its Now
list that are done or dropped and the verdicts that are settled, each under its number as it stood,
for 3DEngine. REVIEW.md holds the current work, this file the record; nothing here is read before a
batch.

## Items done

5. **The first shares recorded from the workflow's own device.** The examples job's first green run
   puts every pair measured for the first time into notices, which the public listing of the job's
   annotations gives; those shares go into `3DEngine.Examples/measured-ci.tsv` as the device's own,
   so the run after holds every pair to them and a share can only fall. They came with the examples
   job of `71050c8f`, job 114279020552, every pair but `audio_sound_multi`, whose raylib program
   drew no frame there, from 0.0 for `models_tesseract_view` to 14.4% for `audio_amp_envelope`, and
   go into the file with the next batch, the one `none` recorded as the page has it. Done at
   `1fa88e8b`, 212 shares in `3DEngine.Examples/measured-ci.tsv`.

9. **A color stream written in place reaches the bounce, done at `feec07a2`.** The field paints a
   mesh's vertex colors as it stamps it and the ray scene packs them into its corners, both keyed by
   the mesh's vertices, so colors written in place by `UpdateMeshBuffer`, which the model pass draws
   at once, leave the bounce and the reference on the colors of the mesh's last upload: in the
   game's white room relit by its light levels, the frame's floor read 0.099 and its far wall 0.035
   for 2,100 frames, and 0.130 and 0.072 once each section uploaded anew with the same blocks and
   light, the reference's wall 0.002 and 0.043 with whole sections black in its picture, so a lamp
   placed in a dark room bounces off walls as dark as they were until a block changes in their
   section (ASKS.md, 2026-10-11). A write to a color stream marks the mesh's colors stale, and in
   the frame after the field repaints the colors of the cells it holds for the mesh without
   rebuilding their distances and the ray scene rewrites its corners' values, so a relight costs
   less than an upload, which would have the field build the mesh again. Measured on the game's
   room, the floor and the wall after a relight against the reference at eight bounces, and a relit
   section's cost against an upload's; a test writes a colored slab's colors in place and holds the
   bounce to the reference within the colored slab's 1%. Taken after item 2's parts f and c and
   before item 8, since a wrong bounce weighs more than its 0.6 ms. Done at `feec07a2`: the
   recolored meshes repainted in the built cascades from the words each build keeps, the ray scene's
   corners rewritten, the red bounce within 2% of the reference.

10. **A post-processing window in the overlay, which a game opens as it opens its performance window
    (Decision 28), done at `c54d64eb`.** The owner asked on 2026-10-11 for a tab of the ImGui
    overlay that opens a window of the tonemapping and coloring settings, as the voxel game's Voxel
    window opens its profile window. The engine's post-processing is set and not read:
    `SetExposure`, `SetAutoExposure`, `SetTonemap`, `SetColorGrading`, `SetVignette`, `SetFxaa`,
    `SetDepthOfField`, `SetMotionBlur` and `SetBloom` write `FrameEffects`, which is internal, so a
    game cannot show the values it has. The engine gains a reading of each setting beside its setter
    and a window of its own, drawn on a call from a game's ImGui frame as its other windows are,
    that shows and sets every one: the tonemapper by name from the `Tonemap` choices (Decision 17),
    the exposure and the automatic exposure's bounds and speed, the contrast, the saturation and the
    tint, the vignette and its radius, FXAA, the depth of field's distance, range and blur, the
    motion blur and its per-object kind, and the bloom's intensity and threshold, each a slider, a
    check or a choice that writes its setter at once, so the owner tunes the picture while looking
    at it. A test reads each setting back after its setter and draws the window once in a headless
    frame. The game's session is asked to open the window from its Voxel window as it opens the
    profile window, once the engine has it. Taken after item 2's parts g, c and f and before item 9,
    since the owner asked for it and it is small; the owner's eye judges the look (Decision 27).
    Done at `c54d64eb`: nine readings beside their setters and the window, with its test.

11. **The README's examples as one scrolling row a folder, the games' text under them gone, done at
    `706a1499`'s look, `4805676c` reverted at `aa9fe773`, the games' captions at `e28064f7`
    (Decision 29).** The owner asked on 2026-10-11 at 15:30. The README's Examples section holds a
    table of two columns and thirty-four rows, pictures of picked examples and games mixed, and
    under it eighty lines of prose and a block that explain the games. It becomes one table a folder
    of `3DEngine.Examples`, in the folders' order as the repository lists them, Audio, Benchmarks,
    Core, Ecs, Gui, Models, Physics, Scenes, Shaders, Shapes, Text and Textures, each a single row
    holding every example of the folder that has a capture under `.github/assets/examples`, the
    picture linking the program and the run name under it as the rows do today, with a heading line
    naming the folder before each, and a last table of the same shape for `games`; a single row
    wider than the page scrolls sideways on GitHub, which is the point, so no row is wrapped. The
    prose and the block between the table and the Guide heading that explain the games go, their
    facts living in the games' own READMEs and in the games table of EXAMPLES.md where they are not
    already. The README's walk and the examples' table check read the file as before, so each is run
    on the result, and the captures' links keep their raw form. Taken as a batch of its own after
    h's next commit, since it is small and the owner asked; The owner asked at 15:52 that the
    pictures be one size, where they differ today: every picture in the rows carries the same width
    and height on its tag, and the captures that are not 800 by 450, sixteen of 268, among them a
    square and a four by three, are taken again at that size where the program's window allows or
    padded to it by the capture script, so the tag distorts nothing; the examples' table check and
    the captures' comparison read the result. Done at `ee47849e`: twelve rows of 257 examples and a
    games row of thirteen, every tag 400 by 225, the sixteen captures of other sizes fitted to 800
    by 450 by the WebP script, the games' notes moved into each game's own README. The owner looked
    at the pushed page at 16:04 and the pictures still read as different sizes. GitHub renders every
    tag at one box, its own style giving each the full width of its cell, an automatic height and
    the tag's aspect, so the box is uniform; what differs is the picture inside it, since a capture
    of another shape was fitted inside sixteen by nine with black bars, which read as a smaller
    picture. The fit fills instead: the WebP script scales a capture of another shape so its shorter
    side fills the box and cuts the rest around the middle, or the example's window is opened at
    sixteen by nine where the cut would lose something that matters, so every picture fills its box
    edge to edge; the sixteen captures and the games' thirteen are fitted again so, and the page is
    read after the push. Done at `60f2999d`: the WebP script fills its size and cuts around the
    middle or from a named side, all 268 captures at 800 by 450 and filling their boxes. The owner
    looked again at 16:12: each picture is as wide as the name under it, a short name's tiny and a
    long name's large. GitHub squeezes a table to the page's width before it lets it scroll, and a
    cell shrinks to the one thing in it that cannot wrap, the name, the picture shrinking with it
    under the page's own rule of a picture no wider than its cell. Each cell takes a width of its
    own, `width="400"` on every `td`, which the page's sanitizer keeps as it keeps a picture's, so
    no column goes below it and the row keeps its full width and scrolls; a header row of
    non-breaking spaces would do the same and is the fallback should the attribute be stripped,
    which the rendered page says after the push. Done at `623bdc47`: every cell 400 wide in an HTML
    table a folder, the rendered page read after the push. The owner looked at the pushed page at
    16:20 and the pictures still differ. The rendered page keeps the cell's width, read here, so the
    squeeze is the browser's table layout: a cell's width is a preference, and when a table is wider
    than the page the browser shrinks each cell to its narrowest unbreakable content, the name, the
    picture with it, GitHub's table being a block capped at the page's width that scrolls only past
    that minimum. So the minimum comes from content: the run name under each picture, in its code
    tag, is padded with non-breaking spaces, the character itself and not the entity, to fifty-two
    characters, which the code font draws at some 426 pixels, past the picture's 400 on every
    platform's monospace, so no cell can shrink below its picture and the name reads as a caption
    bar as wide as it; the cell's width stays as the preference. The rendered page is read after the
    push. Done at `54c0e669`: every name padded to 52 characters, the README at 237 lines of prose,
    the rendered page read after the push. The owner looked at the pushed page at 16:27 and it
    works, and asked for the pictures a bit smaller: every tag 320 by 180 in place of 400 by 225,
    the cell's width 320 with it, and each name padded to 42 characters, which the code font draws
    at some 344 pixels, past the picture, the captures themselves unchanged at 800 by 450. Done at
    `3051fab8`: 320 by 180, cells of 320 and names of 42. The owner looked at the pushed pages at
    16:30: the look is right, and on a phone the caption under each picture is a tenth to a fifth
    wider than the picture, on a desktop a little wider. The caption's padded text sets the cell's
    width while the picture fills no more than its own size, so on a font that draws text wider, as
    a phone's does, the text overshoots the picture. The caption sets the width on purpose and the
    picture fills it exactly: each picture's tag and cell take a nominal width past the caption's
    widest rendering, so the cell is the caption's width on every device and the picture, no wider
    than its cell by the page's rule, fills it edge to edge; and the caption shrinks to the small
    tag, `<small><code>`, which the page keeps, since the padding cannot go below the longest name
    and the font is the other knob: the names padded to 28 characters in a small code caption, and a
    name longer than that, three of 268, broken at an underscore with a `<wbr>` tag, which the page
    keeps, so no piece exceeds 28 and its last piece is padded to 28, which the owner chose at 16:34
    over padding every name to the longest, the names being raylib's and not renamed; the code font
    draws 28 at some 204 to 210 pixels, every tag 220 by 124 and every cell 220, so the pictures
    come out about 205 wide, like BevyCSharp's as the owner asked, and the same width as their
    captions, a long name wrapping to a second caption line only where the table is squeezed; the
    rendered page read after the push on a desktop and a phone. `706a1499` carries the first form,
    32 and 260, and `4805676c` the change of 16:34, 28 and 220 with seven names broken by a `<wbr>`
    tag, and the owner looked at both on a phone at 16:38 and prefers the first, its pictures
    filling up to the caption's width where the second's fell short of it, so `4805676c` is reverted
    by a commit in the form and the gallery stands at `706a1499`'s look. Reverted at `aa9fe773`, the
    README byte for byte `706a1499`'s. The owner saw at 17:02 that the games' captions, `games/`
    before each name, break onto a second line, since a browser may break a line after a slash; each
    game's caption is the game's name alone, padded to 32 as the examples' are, the picture's link
    unchanged. Done at `e28064f7`, the thirteen names alone at 32. the owner's eye judges the look
    (Decision 27).

12. **The e3d skill left the `.claude` folder, done at `7a69c936` (Decision 30).** The owner asked
    on 2026-10-11 at 15:50 that `.claude/skills/e3d-cli/SKILL.md` go to the root or elsewhere, the
    `.claude` folder and long subfolders holding nothing else being unwanted. The document becomes
    `.github/E3D.md`, beside the other documents a session reads, its skill header, the name, the
    description and the allowed tools, dropped since it means nothing outside that folder, its body
    as it is; AGENTS.md's paragraph on controlling the engine from the command line and its
    documents table name the new place, which the owner's ask is the word for, as do BUILDING.md,
    STYLE.md's line on the skills, the README's line on the commands, the two links in
    `docs/driving-with-e3d.md` and the workflow's path filters, which drop `.claude`; the folder
    goes with the move, and `.claude/` joins `.gitignore` so the tool's own lock file there never
    enters the tree. A batch of its own, taken when the owner types the ask in the working session,
    which changes AGENTS.md, the ignore file and the tool's own folder only on the owner's word
    given there and has asked the owner for it, a right the reviewing session holds. Done at
    `7a69c936` on the owner's word typed there: `.github/E3D.md`, every reference moved, the folder
    gone and ignored.

13. **`games/Pusher` is gone, done at `ffa3ec48` (Decision 31).** The owner asked on 2026-10-11 at
    17:18, finding it ugly and of no use. The folder goes with its capture under
    `.github/assets/examples` and its cell in the README's games row, and every place that names it
    follows: the test workflow, where Pusher is the game built and played from the package,
    published native and drawn, and played once more in a window on the runner's desktop, the three
    roles passing to the lightest of the 3D games by the time `build/play-game.sh` takes it,
    measured once across them, and the games' loops dropping it; `build/soak.sh`,
    `build/drive-game.sh` and the soak check's test, whose reading named for it takes another game's
    name; BUILDING.md's two paragraphs on a game built and shipped this way and the shipping guide's
    worked example, which name the replacement; TODO.md's two lines; the raylib comparison's size
    line, which gives the replacement's; the materials guide's timing line and RENDERING.md's table
    row, which go or give the replacement's; and Hopper's README, which no longer calls it a
    counterpart. SHARED.md's row on a game published native and played by CI is the reviewing
    session's and names the replacement once the commit does. The suite, the document and norm tests
    and the README walk on the result, and the examples' table check. Done at `ffa3ec48`: Slide in
    the three roles, the lightest 3D game by measure, every place that named Pusher following, and
    SHARED.md's rows naming Slide.

14. **The development machine's GPU is named nowhere a user reads (Decision 32), done at
    `e910f4f4`.** The owner asked on 2026-10-11 at 17:26 that the mention of their machine, the RTX
    4070, leave the code and comments a user sees. Seventeen files outside `.github` name it, the
    shaders' comments, the rendering tests' comments and the guides among them, each naming it where
    a measurement was taken; the measurement stays and the device becomes a plain phrase, the
    development machine's GPU or a laptop GPU of its class, so a reader knows the class of device
    and not the owner's. The documents under `.github`, RENDERING.md, ASKS.md and this file, keep
    the name since their measurements are read against one device, unless the owner says otherwise.
    Done at `e910f4f4` for the seventeen files; whether the documents under `.github` follow is
    asked of the owner at 23:59.

15. **`Engine3D` becomes `E3D` (Decision 32), done at `35322cfe`.** The owner asked on 2026-10-11 at
    17:26 that the flat API's class be renamed, with the namespaces and the collections that carry
    its name. The static class `Engine3D` in the namespace `Engine` becomes `E3D`, its partial files
    under `3DEngine/Api` named for it, the 63 test collections named for it, and every `using
    static` and every mention in the guides, the cheat sheet, the templates, the games, the examples
    and the README follow, 716 mentions in 486 files; the namespace `Engine` stays unless the owner
    says, since `E3D.E3D` reads badly, and the package and the projects keep their names as the
    owner's (Decision 2). A rename of the public surface, so the version is the owner's to number
    (Decision 5). A batch of its own, moving names alone, taken when no other batch is in flight.
    The rename touches thirteen files of `3DEngine.Game` by their `using static` line, which the
    game's session owns and the solution needs changed in the same commit, so the working session
    changes those lines and nothing else of the game's in the rename's commit, once the game's
    session has committed or set aside its edits to those files and said so. Done at `35322cfe` so,
    the old name left on the upgrading page and in its test alone; AGENTS.md's two mentions wait on
    the owner's word.

16. **The README's guide table opens with the cheat sheet's row, done at `ffa3ec48` (Decision 32).**
    The owner asked on 2026-10-11 at 17:26 for the row BevyCSharp's README has first in its guide
    table, the cheat sheet, every public function of the library on one line, grouped as the pages
    are, linking `CHEATSHEET.md` at the root. One line, with item 13's commit. The working tree
    holds the row at the table's head and the older cheat sheet row at its foot, so the one at the
    foot goes with it. Done at `ffa3ec48`.

18. **The examples' counts under the Examples heading, and a count on each folder's heading
    (Decision 34), done at `043b3af1`.** The owner asked on 2026-10-11 at 19:35, as of BevyCSharp.
    Under the Examples heading, after its paragraph and before the gallery, a line says the counts
    the examples table's header holds, of raylib's 222 examples how many are written here and the
    one that does not apply, and beside it how many examples the engine has of its own past raylib's
    and how many games; and each folder's heading in the gallery carries the count of pictures in
    its row, as `### Core (50)`, the games' heading its thirteen, the form the working session finds
    reads best and the owner's eye judges. Written by whatever writes the gallery today, so the
    table check and the README walk read the result, the prose count kept. Done at `043b3af1`: the
    line of 221 written and one not applying beside 34 of the engine's own and 12 games, each
    heading its row's pictures, 255 in all, the script writing both and its check failing where
    stale.

19. **A push plays one game, and the pack run plays them all (Decision 35), done at `7798d5e5` and
    `958a3eb3`.** The owner chose on 2026-10-11 at 19:50, asked whether every push must play the
    thirteen games for an hour and offered one game or a benchmark instead, and a nightly schedule
    declined. The examples job on a push keeps the examples' table check, builds every game from the
    package, which catches what a consumer of the package would hit in a few minutes, and plays and
    soaks Slide alone, the lightest game and already the one the test workflow builds, publishes and
    plays in a window; the captures, the measure against raylib's programs, the thirteen games
    played, the whole soak, the resize and the README's walk move to the pack run, which the owner
    starts, so a release still proves everything and a push costs minutes rather than an hour and a
    half. Verdict 30's soak of Manor and Verdict 49's measure are then read on the pack run's page,
    and 5.2's pack (Decision 19) reads its own run green. The workflow's script and norm tests read
    the result, and BUILDING.md's paragraph on the jobs says which run does what. A workflow batch
    of its own after the third build. Done at `7798d5e5`: the examples job a workflow of its own,
    called with whole false by a push and true by the pack, the thirty steps found by the step
    script given the workflow's file. The pack waits on the examples job since `958a3eb3`, as
    Decision 19 wants.

20. **A vertex stream of the game's own beside the mesh's vertex, and a vertex of positions alone
    (Decisions 36 and 37), done at `958a3eb3` and `3ec1fdb3`.** The owner put
    `.ref/Voxel-Sandbox-main` in the tree on 2026-10-11 at 20:05, a game on the 3.1.0 package that
    fed its shader one packed float a vertex through an input layout it set itself and unpacked it
    in the shader, and asked for the same in both engines; the plan was approved at 20:40 and
    refined at 22:55, a position every mesh's and the packed data beside it, its full text kept by
    the reviewing session, this item being what it asks of the engine. First stage, one commit: a
    mesh may carry one stream of the game's own beside its vertices, in one of five formats, `UInt`,
    `UInt2`, `Float`, `Float4` and `UNorm8x4`, the types `MeshStream` with `From<T>` and `SizeOf`
    and `MeshStreamFormat` named as BevyCSharp's are, given to `UploadMesh` with the colors and
    second texture coordinates and written in place by `UpdateMeshBuffer` at index 9, a constant
    naming it, which writes the stream's buffer through a ring as the vertices are written and never
    uploads the mesh again; a shader of the program's own reads it as `CUSTOM0`, placed by its
    semantic as the others are, at binding 5 of the model pass's pipelines since the subsurface pass
    holds binding 4, the stream bound by a call of its own, the pipeline keyed by the format; a
    shader that reads it drawn with a mesh without it reads zero from a sixteen-byte buffer at
    stride 0 as the default color and coordinates are, zeros of the shader's own class, integer or
    float, which the reflection of the stage's inputs then says, since Vulkan wants the attribute's
    class to be the input's; the stream is kept beside the mesh's streams in the store and not
    inside them, since a mesh with streams carries both default buffers and one with the game's
    stream alone must not pay for those. A `uint` and not a `float` is the packing type, which the
    guide says and why. The guide says why the packed bytes are a vertex stream and not a storage
    buffer read by the vertex id, which the owner asked at 23:15: a stream travels with the mesh,
    uploaded once with its positions and indices and read by every draw of it with nothing else
    bound, where a buffer is bound to the shader or the material, so thousands of chunks would each
    need a binding a draw or one giant buffer with an offset a chunk; a shader's buffers are bound
    per shader and a mesh's stream per mesh, a draw with a shader of its own being a batch of its
    own here already; the GPU fetches the attribute by the vertex index as it fetches the position,
    where a buffer read needs the vertex id by hand and an offset an instance when instanced; the
    fetch is about as fast either way, so the gain is the binding model; and a buffer stays the
    right tool for what is not per vertex, a palette or atlas table every chunk shares, per-chunk
    constants, or what a compute pass also reads. Tests: the store refusing a wrong count and
    queueing the in-place write alone, the write of index 9 changing one value of four bytes, the
    reflection naming `CUSTOM0` with its class, and one Vulkan fact of two planes through one
    shader, the left with the stream and the right without, drawn at 64 by 32 on lavapipe. The
    example `shaders_packed_vertices` in the Shaders folder, a grid of cubes whose four bytes a
    vertex hold a palette index and a shade, captured and in the README's row and count. Second
    stage, one commit: a mesh uploaded from positions alone, `UploadMesh(Vector3[] positions, uint[]
    indices, MeshStream? stream, Color[]? colors)`, 12 bytes a vertex at binding 0 with no normal
    and no texture coordinate of its own, since a shader that unpacks four bytes has both in them;
    the store keeps a `ModelVertex[]` of the positions with zero normals and coordinates for the
    light field, the glow lights, the bounce reference, picking and the bodies, which then work
    unchanged, and uploads the positions alone as the vertex buffer; every pipeline that binds a
    mesh's vertex gets a variant with binding 0 at stride 12 and the normal and coordinate read as
    zero from a stride-0 default at binding 6, the model pass's own and custom pipelines, the solid
    and masked shadow pipelines and the velocity pass's, picked by the mesh at draw, the subsurface
    pass leaving such a mesh out since it shades with the engine's normal; `UpdateMeshBuffer` at
    index 0 writes its positions through the ring and indices 1 and 2 warn, a skin is refused and
    `UpdateMeshVertices` warns once; the engine's own shader lights such a mesh by a zero normal, so
    it wants a shader of the program's own, said in the guide and warned once where drawn with the
    engine's own, its shadow right either way. Tests: the store keeping the zero-normal array and
    uploading 12 bytes a vertex, and a Vulkan fact of a quad of positions alone with a `UInt` stream
    over a floor under one light, the quad's pixel the packed color and the floor darker under it
    than beside it, so the positions-only shadow pipeline is proven; the reference frames unchanged;
    RENDERING.md's bindings table gains binding 6. The example becomes the reference's chunk, a hill
    of voxels as positions and four bytes a vertex, the normal, the coordinate and the tile unpacked
    in the shader, with its shadow and a line of text giving the bytes a vertex. Then the voxel game
    takes the stream as its first user, its own item once this lands, 20 bytes a vertex where 44.
    Kept out and said: keying the field's readers on positions rather than the vertex array, a later
    change of five files. Done at `958a3eb3` and `3ec1fdb3` as written; the voxel game's adoption is
    the `Review:` line under its entry in ASKS.md.

21. **The colors of a mesh of positions alone serve the light field alone and reach no buffer, done
    at `86fba7ad`.** The game's measure at `b275f738`, in its entry in ASKS.md: a section of
    positions and eight packed bytes uploads 72% of the vertex bytes it did where 45% was expected,
    since the colors given for the field's tint, which must go with the upload until item 9, give
    the mesh on the device a colors buffer of four bytes a vertex and a zero second texture
    coordinate of eight, 32 bytes a vertex in four buffers. A mesh uploaded from its positions is
    drawn by a shader that unpacks its color from its stream, so its colors are the CPU's alone:
    `UploadMesh(positions, indices, stream, colors)` keeps them where the field, the glow lights and
    the bounce reference read them and gives `GpuMeshes` no colors and no second texture coordinates
    for such a mesh; a shader of the program's own that reads `COLOR0` or `TEXCOORD1` on it reads
    the defaults, white and zero, at stride 0 as a mesh without streams does, said in the guide's
    positions-alone paragraph and in the overload's remark. A store test that a positions-only
    upload with colors keeps them for the readers and queues no stream buffers, and the Vulkan fact
    of stage 2 unchanged. The game then measures its land again, 20 bytes a vertex and about 40 MB
    expected where 63. Done at `86fba7ad`; the game measures again.

23. **A mesh casts the sun's shadow and stays out of the light field, and a moving mesh's stamp
    costs less; done at `6a36b2b5` and `089ac771`.** The game's entry of 2026-10-12 in ASKS.md: its
    mobs, four to six pieces each drawn with `DrawMeshInstanced` at matrices that change as they
    walk, casting shadows, are stamped by the field as boxes in every frame they move, 17 walking
    mobs taking `cpu.scene_field` from 0.8 ms to 2.9 and `gpu.scene_field` from nothing to 1.0, 76
    taking them to 4.8 and 3.9 with the 256 boxes of `SceneFieldPlan.MaxShapes` filled and the
    farthest pieces left out, 16 to 28 microseconds of the CPU and 13 to 15 of the GPU a walking
    piece a frame, where 76 standing cost 1.1 and nothing. First, the flag: `CastsShadows` on a
    material decides both the shadow and the field, so a material gains a second switch,
    `BouncesLight`, true by default, which keeps every scene as it is, and a mesh drawn with it
    false casts its shadows and is left out of the field, the glow lights and the bounce reference,
    said in the materials guide beside `CastsShadows` with the mob as its case, a test that such a
    mesh casts a shadow and stamps no box, and the game's measure again with its mobs so, which
    should read the standing cost; the game's own color is then lost to the bounce for mobs, which
    is the right trade for a crowd. Second, the cost of a walking mesh, read in a reproduction of
    the engine's at 03:15, a land of 400 sections and 200,000 triangles with mobs of five pieces
    drawn instanced in a Debug build: the stamp itself costs the GPU half a microsecond a box, 0.06
    ms for 85 boxes and 0.12 for 256, under the 3 asked already; the GPU's 13 to 15 microseconds a
    box in the game are cascade rebuilds, since a piece that stands eight frames settles into the
    still field and marks its cascades dirty and marks them again when it walks on, so mobs that
    walk 90 frames and stand 45 rebuild a cascade every second frame with 17 and every frame with
    76, 3.6 and 6.2 ms; and the CPU's 8 microseconds a box at 76 is 0.91 ms of the plan's order,
    which once any mesh moves leaves the read-by-place path and rebuilds a dictionary keyed by every
    drawn instance, the land's included, 0.60 ms of the shapes, 0.29 of the bricks and 0.28 of the
    buffers. So the remedy is two things and no cheaper stamp: the read-by-place path keeps going
    when a slot's mesh is the same and only its matrix moved, which takes the order to near nothing,
    and the shapes' loop is trimmed toward 5 microseconds a box; and a mesh whose instances were
    seen moving settles into the still field only after a longer stillness, its length chosen by
    measure and said in RENDERING.md, so a pausing mob rebuilds no cascade, as a still instance
    leaving while its vertices are drawn or its slot moved rebuilds none, a settle costing a few
    milliseconds of GPU against half a microsecond a frame for its box. Measured in the reproduction
    and then by the game in its village with 17 and 76 mobs with `BouncesLight` on, 76 walking and
    pausing to cost the field under 0.5 ms of GPU and under 2 of CPU. Grouping a mob's pieces as one
    box is not needed. Done at `6a36b2b5` and `089ac771`, the game measuring its village with the
    bounce on after.

24. **The glow lights' shadow past a box's edge is a step where a penumbra belongs; done at
    `2a14ab13`.** The owner's picture of 2026-10-11 at 13:50 showed a zigzag seam on the voxel
    game's wall where the lamps' light falls past a pillar, and the game's pictures drawn again at
    `6f4c98bb` show it unchanged by part h's work since: across the seam the frame steps 26% of the
    light in 20 pixels where the path-traced reference rises smoothly over 170, the frame 0.79 to
    0.88 of the reference on the dark side and 0.84 to 0.98 on the lit, the seam's middle rows
    within 2 pixels of a line and teeth of 11 to 26 pixels at its top and foot, where the march
    passes the pillar's ends. The cause is the glow lights' march: a ray from the surface to the
    halves of a lamp's faces either reaches or is stopped, so the shadow a box throws is hard at the
    march's resolution and toothed where its steps cross the box's ends, and the wall's bounce from
    the lamp, which is most of its light there, takes that edge. The remedy is the distance field's
    own penumbra: along the march the least of the field's distance over the distance traveled gives
    how far the ray passes from the occluder's edge, and the lamp's light is scaled by it from zero
    at a grazing pass to one past a cone's width, the cone set by the lamp's face's size over its
    distance as a soft shadow is, so the shadow falls off over the width the reference shows and the
    teeth, which are the step's, go with the step; measured on the game's seam row by row against
    the reference, the step's 20 pixels to become a falloff within a quarter of the reference's
    width and the teeth under 3 pixels, the glowstone room and the bounce rooms' 27 cases read for
    what moves, the march's GPU time before and after, and the owner's eye on the picture drawn
    again. Done at `2a14ab13`, by a cause other than the cone's: the march's way out ended at the
    step that left the surface, so a way grazing an edge counted half a cell under it; ended where
    it left, the rise in 20 pixels is 5 to 7% where 22 to 24 and no teeth remain.

25. **The macOS captures move to the pack run (Decision 38); done at `9a3e2e2a`.** The macOS
    examples job of `test.yml`, every example captured on the macOS runner's GPU through MoltenVK
    and checked to draw without a validation error, runs on every push and is the longest job by
    far, 53 to 56 minutes where the suites take 17 to 38, and a push's Linux examples job captures
    every example on lavapipe already. It moves to where the soak, the storm and the measure against
    raylib run, the pack run alone: the job leaves `test.yml` for `examples.yml` behind
    `inputs.whole`, with its runner, its caches, MoltenVK and its artifact as they are, the pack job
    still needing the examples workflow, and the workflows' head comments and BUILDING.md saying
    what a push runs and what a pack run adds. In the engine's session's batch with Verdicts 54 and
    55.

26. **Two workflows an engine, `build.yml` on a push and `pack.yml` by hand (Decision 40), committed
    at `534a1c2b`, done on 2026-10-18 with the pack run of `4d5db9df` whole green after the push run
    of `683e6e9d`.** `test.yml` and `examples.yml` fold into `build.yml`, which has `push`,
    `pull_request` and `workflow_call` with one input, `pack`: the jobs `linux`, `windows`, `macos`,
    `digest`, `examples` (needs `linux`, 90 minutes, the push half of today's examples job,
    `step.py` given `--workflow .github/workflows/build.yml`), and pack-only `captures` (the Linux
    captures and the raylib measure, 150), `captures-macos` (today's `macos-examples`, 160) and
    `games` (the twelve games played, soaked and stormed from the package and the README walked,
    150); the Windows and macOS jobs keep Slide played offscreen, native and in a window, the README
    walk and the first game on every push, and play the other eleven in the pack run alone behind
    `if: ${{ inputs.pack }}`, Decision 35 on every system, their limits `${{ inputs.pack && 180 ||
    100 }}` and `${{ inputs.pack && 120 || 75 }}`. `pack.yml` keeps `workflow_dispatch` with
    `publish` and runs `build` (uses `build.yml` with `pack: true`) then `pack` (needs `build`, 30
    minutes, today's pack job whole, the version read in it). Both files: `permissions: contents:
    read`, concurrency `build-${{ github.ref }}` cancelling in progress and `pack-${{ github.ref }}`
    not, the paths filter as today, a head comment saying what a push runs and what the pack run
    adds, a time limit on every job with its reason (N 6.8). Every line naming the old files
    changes: `build/step.py:6-7`, `ScriptTests.cs:99, 103` (`build.yml`, so `captures-macos` joins N
    6.6's scan), `.github/BUILDING.md:25, 81-82, 182-191, 315-316, 335-341, 354-359`,
    `.github/TODO.md:352`, `docs/shipping-a-game.md:56-57`, `docs/compared-with-raylib.md:60`,
    `README.md:256-258`. Checked by `ScriptTests` and `NormTests`, a push run green with the three
    pack-only jobs skipped and the Windows job near 55 minutes, and a pack run green with its
    package, which are Decision 39's gate (Decision 42). The norm's and SHARED.md's lines are the
    reviewer's and ride with the commit. Done on 2026-10-18, the push run of `683e6e9d` whole green
    on 2026-10-17 and the pack run of `4d5db9df` whole green at 14:14 with its package, Decision
    42's gate.

27. **The voxel game leaves for its own repository (Decision 41), done at `b40a788d`.** One commit:
    `3DEngine.Game/` and `3DEngine.Game.Tests/` removed, `3DEngine.slnx:12-13` with them,
    `AGENTS.md:93-94` (the `games` row covers every game; the owner's word under N 7.4 is Decision
    41), `docs/global-illumination.md:42, 130` and `docs/materials-light-and-shadows.md:390` citing
    a voxel game built on the engine, Voxel-Sandbox, with the numbers as measured,
    `.github/RENDERING.md:2140` saying where the game lives; the tests' comments, `.gitignore:83`
    and the asks' captures stay. Checked by the strict build, `NormTests` and `grep -rn
    "3DEngine\.Game"` finding only the channel's files. The gate was met on 2026-10-18 (Decision 42)
    and the game's build has been green in its repository since 2026-10-17, so the item is open from
    14:19. Done at `b40a788d` on 2026-10-18, the two projects, the solution's lines and AGENTS.md's
    rows gone, the guides and RENDERING.md citing the game's repository; item 28 is open from 15:05.

28. **A tiny sample, `games/Ore`, a voxel landscape, a character and the light that bounces, done at
    `4c6630a4`.** A game like the others (`Ore.csproj` and `nuget.config` as Wick's, a `Program.cs`
    of some 150 lines, `OreCommands.cs` with `ore.status`, `ore.autopilot`, `ore.warp` and
    `ore.night`, a README in the games' form): a 48 by 48 landscape from an inline value noise,
    heights 1 to 12, grass, dirt, stone and ore, its faces against the air meshed into positions,
    normals and vertex colors of a plain mesh, since global illumination reads a mesh's colors, the
    ore faces in a second mesh with `Emissive` and `EmissiveIntensity`, the ground a static physics
    model, a first-person character through `CreatePhysicsCharacter`, `MovePhysicsCharacter` and
    `JumpPhysicsCharacter` with mouse look as Manor's, `SetSceneField(4, 0.5f, 2)` and
    `SetGlobalIllumination(GlobalIllumination.High)` with G stepping the quality as Wick does,
    `DrawBounceWindow` while B is held, a sun and N for night. It joins the lists of `build.yml`
    (the build-all, soak and storm lists and the Windows and macOS play loops), gets a
    `build/drive-game.sh` case (a walk, a jump, a capture), a `build/soak.sh` case, a capture step
    in the `games` job and `.github/assets/examples/ore.webp` through `build/examples-table.py`,
    thirteen games where twelve, BUILDING.md's count and the guide's see-also. The name is the
    owner's, Ore unless told otherwise. Checked by `NormTests`, `build/drive-game.sh Ore`, the soak
    and the storm, and the pack run playing it on three systems. Done at `4c6630a4` on 2026-10-18,
    with one change said, bare stone from height 9 up so the hills show ore; its last check is the
    pack run playing thirteen games on three systems. The last check is met on 2026-10-18, Ore
    played from the package on Linux by the games job of `32ce6f4e`'s pack run, on Windows by that
    run's suite and on macOS by `1398ed24`'s pack run's suite.

29. **The register: STYLE.md's rule and the sweep (Decision 43), done at `683e6e9d`, the figures
    Decision 43 adds at `7b2486a5`.** STYLE.md gains a section, Register: prose uses the technical
    and generic term, the engine's own where it has one, and no figure of speech or casual
    paraphrase, with the pairs the owner named and their replacements: the light that bounces
    becomes global illumination, radiance cascades where the technique is meant and indirect light
    where the effect is; gives off light and glowing become emissive; driving a running program
    becomes controlling or scripting it through the command line, and a drive a scripted run or a
    course. The colon joins it: STYLE.md's rule, which allows a colon before a list, tightens so
    that a running sentence holds none, the sentence recast or split where one appended an
    elaboration or a list, as in "makes the program the README opens with: a window with a turning
    cube"; a colon stands before a bulleted list, a code block or a command, and as a label in a
    table, and STYLE.md's check of colons joining clauses reads prose lines of Markdown as well as
    comments. The sweep covers README.md, `docs/`, AGENTS.md, every Markdown under `.github`, the
    comments of the code, the scripts and the workflows, and the sentences a program prints; 72
    files hold the first pair some 170 times, 20 documents the drive words some 80 times, and a
    rough count finds a colon inside a sentence on some 230 prose lines of the documents and 375
    comment lines of the library and the examples. `docs/driving-with-e3d.md` becomes
    `docs/command-line.md`, titled for the command line, its links following, which
    `DocumentLinkTests` holds; job and script names stay as identifiers (`build/drive-game.sh`, the
    captures named `-driven`) with their comments reworded, since their renames reach workflows and
    tests. The REVIEW.md, NORM.md and SHARED.md lines are the reviewer's and are swept already.
    Checked by `NormTests`, `DocumentLinkTests` and `git grep -i` for the pairs finding the
    channel's history alone. One commit. The game's repository did this first at its `b87e386`, and
    its STYLE.md's Register section and colon rule, with their checks, are the text to take, with
    the engine's examples in place of the game's. Two things it found of the engine's:
    `DrawBounceWindow` titles its window "Light that bounces", which becomes "Global illumination",
    and the API logger's warnings put the exception after a colon, which becomes a full stop. Open
    again on 2026-10-17 at 20:34 for the figures Decision 43 adds, a likeness in place of a
    description and an idiom in place of a term. In the manner of raylib stands in
    `3DEngine/Api/E3D.cs:4`, `.github/DESIGN.md:116` and `3DEngine.Examples/Program.cs:3`; out of
    the box in `3DEngine/Assets/Textures/TexturesPlugin.cs:61`; under the hood in
    `3DEngine/Assets/Models/ModelLoadExtensions.cs:47`; in the wild in
    `3DEngine/Assets/Models/Assimp/AssimpModelReader.Meshes.cs:224`; a great deal of in
    `3DEngine.Examples/Shaders/ShadersMandelbrotSet.cs:87`; a handful of in
    `.github/ARCHITECTURE.md:245`, `.github/TODO.md:227` and `build.yml:1197`; and whatever a sweep
    for such figures finds beside them, STYLE.md's Register section carrying the rule with the
    owner's example. `AGENTS.md:93` and the game's files leave with item 27, and the reviewer's line
    in NORM.md is reworded here. Done at `7b2486a5`, the ten places reworded and what the sweep
    found beside them, on the fly, for free, first-class, the -style likenesses and the figurative
    wire up, the README's opening and the package descriptions saying what the engine does and
    naming raylib for its function names and rlgl's batching alone, two AGENTS.md lines the owner
    approved in the working session, and a word in its literal sense kept, wires drawn, curves end
    to end and motion back and forth.

30. **The radiance cascades look as good as they can (Decision 44), the week's work, one part a
    commit.** The owner said on 2026-10-18 at 22:25 that the light the cascades give does not yet
    look as good as it can, and made it the week's focus; the owner's eye of 2026-10-10 and
    2026-10-11 (Decisions 27 and 28) named what was wrong then and items 22 to 24 mended three
    things since. First the reading, in a commit of its own: the examples that show the cascades and
    `games/Ore` captured on the development machine's GPU and on lavapipe at the frames their
    captures take, each thing that looks worse than it should named here as a part of this item with
    its picture, what it should look like and where in the cascades it comes from, the dark
    terminator rings, the spokes, the leaks, the noise of a wide penumbra and whatever else the
    pictures show, and the reviewer orders the parts with the owner's eye. Then each part is mended
    on its own, measured before and after and held by a test as Decision 27's were, the figures in
    WHOLE or the tests' comments (N 3.5), the pictures taken again where they change. The voxel game
    takes the mends through the package, so a pack run follows the parts that change the light. Read
    at `4fa51a9c` (RENDERING.md, "What the light looks like, read on 2026-10-18"), six parts, here
    in the reviewer's order, the most seen first and the parts of one mechanism together, the
    owner's eye to reorder: (a) Ore's ground beside the ore, the world probes giving it nothing
    since the first cascade's probe stands a unit above the ground and the block's light reaches it
    level or from below, the floor's upward face reading 0 against the reference's 0.028, so the
    screen probes alone light it and their filter blends the tiles into patches, the cliffs losing
    their orange alike; (f) the lamp outside leaking through the thin walls' corner, the world
    probes taking its light through walls thinner than their spacing, which the screen probes hide
    but at a tile's edge, a tick 3.25 times the wall; (b) and (c) the screen probes' placement, each
    tile's probe at the tile's middle up to 8 pixels off a corner lifting it from 0.60 to 0.76, and
    their rays toward the open side taking the first cascade along their way, the block's front 65
    percent over; (d) and (e) the world probes' blend, a dark patch at the moved wall's foot falling
    where the reference rises and patches of 0.84 to 1.14 across the far wall. Each part is mended
    on its own, one commit, measured before and after with `gi.compare` against the reference at the
    frame named and held by a test on that measure, the pictures of RENDERING.md's passage taken
    again as they change; the four things seen that are not the cascades' stay as read, the
    terminator's crescent being Decision 27's subsurface part. Toward (a), read at `29eb2eca`:
    `GlowLights.Carries` (`GlowLights.cs:57`) carries an emitter only where its box's longest side
    is within twice the first cascade's spacing, so Ore's one landscape-wide ore mesh is never
    carried and the ground beside each ore is left to the screen probes' sixteen rays a tile. Part
    (a) done at `de92a62b` on 2026-10-19, a mesh of many emitters cut into its parts and carried as
    glow lights by the faces each shows, the ground beside Ore's ore within 2 percent of the
    reference where it read 0.03 to 1.74 of it in patches, held by two glow light tests, the limit
    of 64 lit emitters said; parts (f), (b) and (c), (d) and (e) follow. Part (f) done at `f40133c0`
    on 2026-10-19, a pixel with no screen probe on a surface like its own reading the world's probes
    it sees through the field, the tick from 3.25 to 1.02 times the wall beside where the reference
    has 1.10, held by a bounce room test, at 0.07 to 0.09 ms of the scene pass; the corner's band
    past the screen probes still reads 1.41 against 0.96 since the world probes' own rays take the
    lamp through walls thinner than a cell, a remainder noted in the reading for the parts to come;
    parts (b) and (c), (d) and (e) follow. Read toward (b) and (c) on 2026-10-19 at 01:52, before a
    mend: the two causes the reading named explain a part of each. With the screen probes' light
    from beyond zeroed the block's front reads 0.151, so the rays meeting nothing in reach carry
    0.056 of the 0.082 excess and the hits the rest, and reading the light past the first bounce at
    the hits through the traced read changed nothing at 0.2 ms, reverted; the first cascade's probe
    in the angle of the floor and the block reads 58 percent over in its own rays' hits and its
    faces, the room's other first-cascade probes 14 under to 18 over and the second cascade's above
    the block 2 over, so the excess sits where probes read each other's light past the first bounce
    in a concave angle and the rays from beyond carry it to the block. At the corner, from the red
    wall out a hundred pixels of the back wall against the far wall, the frame runs 0.65 to 1.02,
    the frame without screen probes 0.49 to 1.02 and the reference 0.44 to 1.03, so the screen
    probes darken the corner less than the world probes across a band ten tiles wide, which a tile's
    middle 8 pixels off cannot account for, and neither the filter, the history nor 64 rays a probe
    in place of 16 moves it, a bias and not noise. The next step is an instrument in a commit of its
    own, `gi.screenprobe <x> <y>`, giving for the screen probe at a pixel each of its 16 rays'
    light, met on the screen, in the field or from beyond, against the path-traced light along the
    same ray, so the rays and the term at fault are named before a mend is chosen; then (b) and (c)
    in one commit where one cause serves both, measured and held as before, and where the cause is
    the world probes' light past the first bounce in concave angles, (d) and (e) are read for the
    same before their own mend. The instrument came at `d8b01672`, `gi.screenprobe` reading a screen
    probe's sixteen rays against the reference along each way and over its cell, and named two terms
    at fault in both (b) and (c), the light from beyond read around the ray's start and not its end,
    and the light past the first bounce at hits in a concave angle at about twice the reference,
    which is the mechanism (d) and (e) are read for. The mend of (b) and (c) reads beyond at the
    ray's end, from the probes around that point weighed by how far their rays reached toward it,
    then measures what the concave-angle term leaves, with (d) and (e) read for the same before
    their own mend, separate commits where the mends differ. Read on 2026-10-19 at 03:06, before a
    commit: the beyond term mended in the tree, the read taken around where the ray ended from the
    probes whose reach covers a point the ray passed, the screen march handing the field the point
    where it went behind the depth and the last point on the picture where it leaves it, which the
    reflections share, takes the block front's rays past their reach from 97 percent over to 3 and
    the far wall of the carried lamp's room from 0.84 of the reference to 1.00, that room's frame
    from 0.944 to 0.989 and the Cornell box's from 1.064 to 1.033, so part (e)'s level is mended by
    it and its patches are not, the wall's blocks spanning 0.85 to 1.12 of their mean where they
    spanned 0.94 to 1.12 before, a bright patch mid-wall and a dark band along its right edge, since
    the rays past their reach carry the world probes' blend from where they end and the wall
    inherits the grid's variation, and the floor at the moved wall's foot (d) moves from 0.171 to
    0.192 against 0.213, nearer and unmended; the faces of (b) and (c) do not move yet, the block's
    front at 1.65 with the pocket between floor and block carrying the concave-angle excess, its
    first cascade probe 85 percent over in its upward face against a one-bounce reference where its
    neighbors read 4, and the corner at 0.77 against 0.60, where the second cascade's nearest probe
    stands over the tall block with 182 of its 233 rays met before their interval began, which no
    visibility weighting recovers, and its screen hits 88 over by the concave term. A third term is
    the sampling, a probe's sixteen ways fixed by its normal and the filter blending tiles of the
    same ways, the reference along those ways against the whole hemisphere reading 22 percent over
    at (c), 26 under at (b), 21 under on (d)'s floor and 46 over on (e)'s wall. The reviewer's call
    is one commit a mechanism: the beyond term's commit at once, held by its test of the rays past
    their reach within 15 percent of their cells at the block front and the far wall, the corner
    left out of it with the reason in the comment, the figures above in the message and in
    RENDERING.md with part (e)'s level said mended and its patches not, its picture retaken, and
    (b)'s and (c)'s faces said unmoved, the slab test's tint bound widened from 2 to 3 percent at
    1.212 against 1.240 with the reason, the change reading each ray past its reach as the reference
    does along its way while the upper wall's probe's sixteen fixed ways miss the slab's red their
    cells hold, which is the sampling term (N 3.5); then the pocket's term, the light past the first
    bounce at hits in a concave angle, for (c) and (b)'s hits; then the corner's world probe over
    the tall block; then the sampling term, read with the instruments before its mend. The beyond
    term done at `33018b8b` on 2026-10-19, the block front's stray rays from 97 percent over to 3,
    (e)'s level from 0.84 to 1.00 with its patches left, both frames nearer their references, held
    by a bounce room test, the faces of (b) and (c) unmoved as said; next the pocket's term with
    `gi.probe` extended to place a face's excess in its own hits or the merge from the cascade
    above. The instrument extended at `ad5d3692` reads the pocket as the sampling term in the world
    probes and not a concave-angle effect, one way of 64 carrying three quarters of the probe's
    excess with the panel's hot spot standing for its whole texel every frame. Three mends were
    offered, each texel's ray turned within its texel from frame to frame on a stratified pattern
    with a history of the probes' light held by hysteresis and reset where a cascade moves, which
    removes the bias at the cost of a history volume a cascade and some flicker as the eye moves;
    256 rays for the first cascade as the two above have, which reduces the bias to a quarter at
    four times the first cascade's trace; or several rays a texel each frame averaged, no history
    and no flicker at a cost in rays. The reviewer's order is to measure the 256 rays first, one
    constant, the instruments saying how much of the pocket and of the faces it takes and the
    illumination pass's time and Ore's frame on lavapipe saying its cost, and then to mend with the
    turned rays and the history, which removes the bias rather than dividing it, the hysteresis
    measured so a lamp brought in reaches its light within a few frames and a still view keeps its
    calm, the several rays a texel kept as the fallback where the flicker is unacceptable; the 256
    rays stay only where they alone bring the faces within the test's bound at a cost the runner
    bears. Measured on 2026-10-19 at 04:09 and reverted, the first cascade at 256 rays took the
    block's front from 1.65 to 1.40 of the reference and the pocket probe's upward face from 93
    percent over to 27, left the corner at 0.77, moved the Cornell frame from 1.033 to 1.029 and the
    carried lamp's room from 0.989 to 0.979, and cost the illumination pass 1.29 to 1.38 ms in the
    box and 145 to 165 ms of Ore's 249 ms frame on lavapipe, a quarter of the bias left for a fifth
    more of the pass, so it is not kept and its figures go in the mend's text. A plain hysteresis
    would fail the two tests that hold the indirect light's response to a lamp brought in or put out
    within two frames, which a history blending a fifth of each frame once lagged by seven. The mend
    goes as a variant the reviewer approves: each texel's ray cycles through fixed sub-positions of
    its texel, one a frame, four by four for the first cascade's texels and two by two for the
    coarser cascades', each probe offset in the cycle; the merge keeps the last light seen at each
    sub-position and writes their mean, so the bias falls to a grid as many times finer at one ray a
    texel and a still view is constant by construction; a change is judged at the same sub-position
    against what it held a cycle before, by a share and an absolute floor to be measured, and resets
    the texel to the new light, so a lamp brought in or put out answers in one frame and the two
    tests stand; the reach the visibility weights read takes the same slots; memory is the cycle's
    count times the merged volume, some 4 MB a cascade. Its weak case, a lamp moving every frame
    resetting the texels near it to one sub-position's light while it moves, is measured with the
    carried lamp, and the mend is held by a test of the pocket's face and the two faces at their
    frames, the still view's change from frame to frame and the two-frame tests kept, the thresholds
    and the costs in its comment. Built and read on 2026-10-19 at 04:41, uncommitted, the
    sub-position variant takes the pocket probe's upward face from 93 percent over to 15 and its own
    rays' hits from 59 over to 6 under, the block's front from 1.65 to 1.41 and the corner from 0.77
    to 0.75, the two-frame tests standing, at four times the world probes' memory, 6 to 25 MB in the
    Cornell box at High; the 15 percent left on every face is the corner's term from the cascade
    above. Its flaw is the fill window: a change is judged only at a sub-position seen since the
    last reset, so in the sixteen frames after one a change landing on an unseen sub-position goes
    unseen, the emissive box's walls appearing at frame 7 reading 0.063 where 1.0 is read without
    it, the share logic then overshooting by 64 percent at frame 10 and settling by 17 where a test
    reads at 11, and judging an unseen sub-position against the reset's sample would reset every
    penumbra and edge texel each frame; a grazing floor view reads 0.188 against its bound of 0.15
    besides. Three ways were offered, a change signal from the CPU comparing this frame's lights,
    sun, sky, glow lights and rebuilt field regions with the last and resetting the texels within
    reach of what changed, a slower reset, which fails the two-frame tests, and several rays a texel
    each frame with no history. The reviewer chooses the rays, two by two a texel every frame,
    option 3: it quarters the bias as the 256 rays did, the block's front at 1.40 beside the slots'
    1.41 with the corner's term left for its own mend either way, and it has no history, no fill
    window, no flicker as the eye moves and no noise around a carried lamp, no signal to keep from
    the CPU and no memory grown fourfold, at a cost measured, 1.29 to 1.38 ms of the pass on the GPU
    and 145 to 165 ms of Ore's on lavapipe; the slots stay out of the tree, their figures kept in
    RENDERING.md as the way to the rest of the bias if the eye asks for it after the corner's term
    is mended, four by four rays being too dear for lavapipe. The grazing floor view's 0.188 is
    placed before the commit. The sampling mend done at `9b37e326` on 2026-10-19, four rays a
    direction in the first cascade taken together, the pocket probe's faces within 30 percent of the
    reference where its upward face read 93 over, held by a test, the block's front from 1.65 to
    1.39 and the corner unmoved at 0.77, at a tenth of a millisecond more of the pass on the GPU;
    the figures said of Ore's pass on lavapipe came from timestamps that claim more than the frame
    and are not trusted, the box's pass there going 87 to 100 ms and Ore's frame 249 ms either way.
    The corner's mend done at `b4543ab5` on 2026-10-19, screen rays past the first cascade sent the
    full 2.4 as those within it, the corner's screen probe from 86 percent over its hemisphere to 5
    and the corner from 0.77 of the reference to 0.68 against 0.60, held by a test, at 0.04 ms of
    the pass; what remains at (b) is the field's color blending the white wall into the red near the
    back wall and the light past the first bounce in the concave corner, said in RENDERING.md. The
    sampling term read on 2026-10-19 with the screen probe instrument at fourteen probes, the
    reference along a probe's sixteen fixed ways 39 percent under to 149 over the hemisphere, worst
    on the Cornell floor, the probes' own light 33 under to 46 over, often the other way since light
    past their reach is a texel's average, every floor probe sharing one set of ways since the frame
    comes from the normal, so the filter blends like with like and averages none of it away. Four
    mends tried, the ways turned per frame among sixteen places, which makes a still view shimmer by
    2 to 4 percent of its pixels' value a frame; turned per tile over four by four places, and over
    two by two, calm, the Cornell front 1.39 to 1.20 and the corner to 0.64, and at the bounce
    rooms' 160 by 90 views 2 and 6, the thin walls and the strip, move against their references by
    up to 0.04, five tests off by up to 0.024 and the wick frame apart in 3.2 percent of pixels; and
    thirty-six ways where sixteen, calm, the front 1.22, the pass 1.43 to 2.43 ms. Each gains in the
    Cornell box and each takes the far wall of the carried lamp's room from 0.99 of the reference to
    0.89 or 0.92, where the fixed ways' overread had covered the screen hits landing at 1.3 on the
    thin green wall's dark side while the reference sees the lit side behind it, a thickness fault
    of the screen hits. The reviewer's choice is no mend of the sampling term, since a term whose
    bias covers another fault's cannot be measured until that fault is mended, and widening five
    tests' bounds for a view moved by a mend is not N 3.5's measurement. The readings and the four
    mends' figures go into RENDERING.md in one commit with no shader change, and the order goes on
    to the screen hits behind thin walls, (e)'s level, then (b)'s color blend and concave term,
    (c)'s light past the first bounce and (d), the sampling term measured again after them with the
    tiled ways and the thirty-six as its candidates, recorded at `0d17cecc` the same day with no
    shader change. The screen's hits behind thin walls mended at `9c44401f` the same day, a ray
    meeting the surface only where the depth does not leap across the halving's step, the far wall's
    probe from 51 percent under the reference along its ways to 2 over, the wall's level to 1.06
    with the sixteen ways' overread uncovered, (d)'s foot from 0.187 to 0.201 against 0.213, the
    Cornell box and the bounce rooms' views as they were, held by a test, the march in
    `screenmarch.slang` under N 1.3. (b)'s blend read on 2026-10-19 with each hit's color in place
    of its light in the screen probe's report, the corner probe's red-wall hits within 0.22 of the
    back wall given a green of 0.15 to 0.16 where the red wall's is 0.012, 0.08 further out and the
    wall's own at 0.4, the field's normal right there; those hits read with the wall's own color
    would fall from about 0.56 to 0.34 against the reference's 0.25 over their cells, so the blend
    is most of their excess and the light past the first bounce the rest. The cause is in the field,
    each cell painted with its one nearest triangle's color, and a cell on the corner's diagonal as
    far from the red wall as from the white holding whichever won, which the trilinear read over 0.3
    cells takes in and no distance test can leave out, the diagonal cells passing every one; the
    paint pass knows each cell's nearest face direction in eight bits of its `uint`, and the resolve
    pass writes color alone. The reviewer's choice is the mechanism's own mend, the resolve pass
    writing each cell's face direction as one byte a cell, about 0.8 MB at High, and the hit
    shading, field and screen hits and the world probes' alike, blending only the cells whose face
    looks the hit's way, eight loads a hit where one filtered read was, falling back to the filtered
    read where no cell faces the hit's way; the reflections' read of the frame before is not taken
    for the diffuse light, since screen probes feeding back through the frame and specular
    highlights entering the diffuse light are a change of method that needs a measured reason of its
    own, read after the blend's mend if the concave term still shows. One commit, measured at the
    corner probe's red-wall hits, the corner over 50 pixels in, the Cornell frame, the carried
    lamp's room and a world probe near a corner, with the pass's cost at both qualities and the
    image's memory, held by a test. That mend done at `a52b125b` the same day, the faces in two
    single-byte images, the hit shading keeping cells whose face is within 60 degrees of the hit's
    normal with the filtered read as its fallback, the corner probe's red-wall hits from up to 0.105
    of green to 0.007 and from 24 to 139 percent over the reference across their ways to 12 to 31,
    the corner over 50 pixels in 0.66 against 0.60, the pass within 0.02 ms, held by a test; what
    remains at (b) is the light past the first bounce in the concave corner, the hits' red 1.36 to
    1.40 against 1.07 to 1.11 across their ways. Read and recorded at `12ef6f13` the same day with
    no shader change, the light past the first bounce at those hits 40 to 50 percent over the
    reference and the whole excess, the probes' own light within 7 to 9 percent along the wall's
    normal, the excess from reading probes 1.3 to 1.9 units out in more open light at a point in the
    corner where two fifths less arrives, which a blend of probes 2.4 apart cannot draw. The
    reviewer's choice is to measure the occlusion factor first, on the light past the first bounce
    alone, as the ratio of the hit's openness to the openness where the probes stand, read from the
    field's distances along a few cones, so an open hit beside open probes changes nothing, with any
    constant in it measured against the reference and said (N 3.5); it is judged with the screen
    probes on, as the frame has them, at the corner, the pocket (c), the wall's foot (d), which
    reads under the reference and must not fall, the carried lamp's far wall and the open middle of
    both rooms, which must not move, with the pass's cost at both qualities; the probes' weighting
    by the hemisphere they share is not taken, since these probes see the hit and their light is
    right where they stand, and a finer cascade is the last resort by its cost. The red wall's
    under-read of 0.86 to 0.95 with the screen probes on is placed in the same reading before the
    factor is judged, and was at 10:29, on the upper wall the sampling term, three red-wall screen
    probes reading 12 and 25 percent under their hemispheres on the upper wall and 35 over on the
    lower while each reads about 28 percent over the reference along its sixteen ways, the ways
    missing the bright directions toward the lit ceiling, so the upper wall's hits stand in the open
    room, the factor must read near one there and the upper wall's frame figure must not fall.
    Measured and not kept, recorded at `75d7af37` on 2026-10-19 with no shader change: the factor
    mends the corner's hits and the block's front and moves the open middle, the foot and the upper
    wall in every variant, and it reads how near a surface is and not how bright, a face lit by the
    red wall losing nine tenths of its light beside it, so it is rejected. Its reading found that
    the corner's first 12 pixels read 1.00 to 1.02 of the reference and the wall 50 pixels in 0.93,
    so (b)'s frame figure is that wall's under-read, the sampling term's, and that the frame's open
    middle is right by two errors cancelling, the world probes' excess of 9 to 16 percent at their
    own hits against what the sixteen fixed ways miss. Neither can be judged in the frame while the
    other stands, and (c) and (d) are figures of the frame, so the order changes: the sampling term
    comes next, its two candidates, the ways turned for each tile and the thirty-six ways, measured
    at this head with the distance to the path-traced reference as the judge at 800 by 450 by
    `gi.compare` and at 160 by 90 by the bounce rooms' views, a candidate out where it widens any
    view's distance at either size, the test's measured differences pinned again only where a view's
    distance shrinks and with the per-view figures as the reason (N 3.5), the thirty-six ways' cost
    said at both qualities, and if both fail the figures recorded and the choice made again. After
    it the light past the first bounce at the world probes' own hits, read by `gi.probe` against the
    reference independent of the frame, which is (c)'s term and what remains of (b)'s hits, then
    (d). Measured and both out, recorded at `3904d0f0` the same day with no shader change: each
    widens four views' distance to their references at both sizes, the thin walls, the strip, the
    grazing floor and the window, where the walls that read over with the fixed ways read further
    over, the sixteen ways' under-read covering the world probes' excess at their own hits on those
    walls, while the Cornell box, the corridor and the carried lamp's room narrow and the far wall
    comes to 0.93 and 0.94 with its blocks within 0.87 to 1.07 and 0.94 to 1.04 of their mean; both
    are better samplers held back by that excess. Next the light past the first bounce at the world
    probes' own hits, read by `gi.probe` against the reference independent of the frame, the term
    named by what carries it before a mend is proposed. Named at `e298f4c4` the same day with no
    shader change: near the walls the world probes' first bounce reads at or under the reference and
    the light past it 14 to 39 percent over, a gain of 1.08 to 1.27 a bounce in the irradiance a
    probe's hit reads from the probes around it, compounding through the probes' own hits; and apart
    from it the merge reads over at the first bounce in the pocket and around the carried strip
    through the cascade above, by up to 2,521 percent along those ways. The gain is named by where
    it shows and not yet by its cause, which is one of three, the constant that turns the faces into
    irradiance and irradiance into the hit's light, the probes read standing in more light than
    reaches the hit on its wall, or a face holding the wall's own outgoing light read back into it,
    so one more reading comes first, at one hit on the thin wall, one on a grazing wall and one on
    the red wall, the irradiance the hit reads against the reference's irradiance at that point from
    one bounce and the same read 0.3 off the wall, and the constant alone in a closed white room
    under the sky at one bounce, which tells the three apart; then the mend of the cause found, one
    commit, measured at the seventeen faces and the frame as before; then the merge's first-bounce
    excess at the pocket and the strip as its own term; then the sampling term's two candidates
    again, then (c) and (d). Read at 11:58 on 2026-10-19 with a scratch test that stays out of the
    tree, recomputing a hit's read on the CPU and tracing the reference's one-bounce irradiance at
    the hit, 0.3 off it and at each probe's own place: the constant holds, a white floor under a
    uniform sky reading 1.00 of the reference at the floor and off it and the probe's faces 1 to 2
    percent over; the wall's own light is ruled out, the read against the probes' own places 0.97 to
    1.07 on the red and the white walls alike and the red wall's gradient the smaller; and the cause
    is the gradient, the probes right where they stand and standing where 11 to 64 percent more
    light arrives than at the hit, 1.13 at the thin wall whose probes are 0.7 to 1.2 off, 1.15 at
    the Cornell back wall's middle and 1.56 by its corner with the second cascade's planes 1.1
    behind the wall and 1.3 before it, 1.63 at the grazing walls, 1.08 under the window's ceiling,
    the thin room at 1.08 a bounce and the Cornell box at 1.27 differing in that alone. The lean of
    0.5 recomputed at 0.1 and 0 brings the grazing walls to 1.07 and 0.86 and raises the window's
    ceiling and the back wall off it, and moves the Cornell walls not at all, since no probe stands
    near them; the facing weight dropped takes 3 to 10 percent off everywhere. The reviewer's choice
    is the probes placed against the walls, a probe whose grid spot lies behind a wall within its
    spacing moved to the wall's front so every wall has a plane of probes beside it, the move rule
    widened from its 0.45, measured together with the lean toward none, each kept by what the
    measurement says; a finer spacing near surfaces stays the last resort. Conditions: the blend
    weighs a moved probe by where it stands or the move is held within what the grid's weights bear,
    said which and why; the facing rule holds for a moved probe; a still view's frame-to-frame
    change and the carried lamp's moved wall are read for a probe that crosses a wall as it moves;
    and it is judged at the seventeen faces by bounce, the frame's figures, the bounce rooms' views
    at both sizes against their references by the standing rule, the screen probes' corner hits,
    which read the same probes, and the pass's cost at both qualities, with a test holding the gain
    at the faces. The thin room's low point, whose probe sits in a pocket and reads 0.23 the other
    way, is the pocket's term, (c), after it. Measured by 13:08, against the head at both sizes, at
    seventeen faces by bounce, at the wall hits by the recompute and in still views: the probes
    crossing the walls toward the eye with tent weights by where each stands works mechanically and
    is out, widening the grazing floor by up to 0.034 at both sizes, the strip, the corridor and the
    window, moving the gain little, 1.14 to 1.27 to 1.09 to 1.19, and reading the Cornell corner's
    hits worse, 1.22 to 1.60 and 1.56 to 1.70, since probes brought within 0.3 of a concave corner's
    walls stand where the reference itself is 1.6 to 1.8 of the light at the hit; the lean at none
    takes the gain to 1.05 to 1.17 in the Cornell box, 1.00 at the grazing walls, 1.05 under the
    window and 1.03 to 1.06 at the strip, narrows the window by up to 0.066, the strip by up to
    0.045 and the thin room, lifts the wall's foot from 0.200 to 0.219 against 0.215, and widens the
    grazing floor at Low alone by 0.08, because the blend toward the ambient light past the last
    cascade takes its point by the same lean and at Low the grazing room's far wall sits in the last
    cascade's band, taking the closed room's absent ambient light. Under measurement the lean at
    none with that blend keeping a half-spacing lean as a constant of its own, with and without the
    crossing, and the moved wall's frames across its restamping. Measured and recorded at `69c7e8cc`
    the same day with no shader change: the refinement narrows or holds every view but the grazing
    floor at Low, which widens from 0.092 to 0.140, and the carried lamp's room at Low by 0.007,
    lifts the foot to 0.219 and 0.209 against 0.215, which is (d), and takes the gain to 1.00 to
    1.08 everywhere but the Cornell corner's 1.17 and the pocket's 1.12, the merge's; the grazing
    room reads 9 percent under its reference at Low before any mend, its wall in the last of Low's
    two cascades with no cascade above to lend light, and the gain covered that. Two errors cancel
    in that room at Low as they did in the open middle, so by the standing principle Low's shortfall
    is read and mended first as a term of its own, the light the last cascade's rays and the blend
    toward the ambient give a closed room beyond their reach, judged at Low by the rule on the
    grazing room, the carried lamp's room, the Cornell box and the thin room and with High unmoved;
    then the refinement is measured again, expected to pass the rule once the room stands on its
    own, one commit each. The crossing is out for good, the corner's gradient being steeper within
    0.3 of the walls than a spacing out. After them the merge's first-bounce excess at the pocket
    and the strip, the sampling term's two candidates, (c) and (d). Read by 13:49 with a scratch
    switch holding the device's rays at every quality for the two instruments alone, out of the
    tree: the last cascade's reach is not the term, Low's last cascade reaching 2.4 to 9.6 and every
    way meeting a surface within it in that room, and the ambient blend fixed at one moving the room
    one point; of the seventeen points between Low's 9 under and High's 8 over, about six lie at the
    first bounce in the screen probes, Low's reading 9 to 23 percent under along their ways through
    their field hits and their reads beyond reach alike with a tile of 16 pixels where High's is 8,
    and High's the same probes 1 to 33 over, and eleven lie past the first bounce, where the world
    probes read within about 8 percent of the reference at Low and 12 to 37 over at High, so High's
    gain covers the room's first-bounce shortfall and part of Low's, and the refinement at Low
    lowers a part that is near right; the Cornell box, the thin room and the carried lamp's room
    read alike at both qualities. The reviewer's order: the screen probes' first bounce at Low
    first, a term of its own with a one-bounce reference, named by what carries it, the march's step
    or reach at the wider tile, the hits' direct light or the read beyond reach from Low's two
    cascades, and mended in one commit judged at Low on the four rooms with High unmoved; then the
    refinement measured again, with a reading of why Low's world probes carry little of the gain
    High's carry, so the lean's part at each quality is known before it is set to none for both,
    since a mend that lowers a near-right term at one quality is not yet the gradient's mend. Named
    by 14:06: not the tile, which moves the frame at one bounce by a point at either quality, not
    the hits' direct light, within 3 percent in every arrangement, but the read beyond reach, Low's
    second cascade sending one ray through each of its 64 texels, the sampling term four rays a
    texel mended in the first cascade at `9b37e326`, since 16 texels a side there make Low read as
    High. The mend measured sends four rays a texel where a cascade has 8 a side and one where 16,
    every probe 256, High unchanged by construction and to the last digit, Low's frame at one bounce
    from 5 under to the reference, and at every bounce Low's grazing room nearer its reference by
    0.018 and 0.037 at the two sizes, the other three rooms within 0.004, and the window wider by
    0.041 and 0.054 at Low and 0.040 at Medium, the strip by 0.013 and the grazing floor at Medium
    by 0.025, each widened view reading as High reads it region by region, the window's distance
    0.303 against High's 0.311 and the strip's 0.150 against 0.149, Low's one-ray sampling having
    covered High's own excess on the walls the gain lifts; at 0.05 ms on Low and 0.08 on Medium. The
    reviewer takes it, since a view that widens to High's own figure for its room uncovers a fault
    that stands at High already and is next in the order, which is the covered-fault principle and
    not a widening the rule is against; the test's bounds at Low and Medium are pinned at the new
    figures with the reason per view that each equals High's for that room (N 3.5), RENDERING.md
    takes the reading and the figures, the scratch switches stay out, one commit. Done at `a27fbdd0`
    the same day, the test's Low and Medium figures pinned with both figures per view. The lean's
    part is read there: the probes stand alike at both qualities and hold the reference at their
    places at High, 7 to 15 percent under at Low where their near-level rays end at the last
    cascade's reach with nothing past, so the half-spacing lean reads toward the brighter far plane
    and none reads the wall-plane probe, which at Low carries the missing light beyond as well.
    Measured on `a27fbdd0` and recorded at `50afce3c` the same day with no shader change: the
    refinement passes its check, the window and the strip narrowing at every quality, the foot at
    the reference, the gain 1.00 to 1.17, no flicker across the moved wall's restamping, and the
    grazing floor at Low still wider, its room 12 percent under, which by the rule names Low's own
    term, what a last cascade's ray that ends in mid-air takes in a closed room larger than its
    reach, the far wall 11.8 across where the reach is 9.6 and nothing past it at Low where High's
    third cascade lends the light. The reviewer's choice is that no ray of the last cascade ends in
    mid-air inside the field: its interval ends at the field's edge and not at a fixed reach, a ray
    going on until it meets a surface or leaves the field, where it reads the sky as any ray leaving
    the field does, so the last cascade hands over to nothing because there is nothing left to hand
    to; not a third cascade at Low, which changes what Low is, and not the same cascade's probes
    read at the ray's end, which is a feedback with a gain of its own. Judged at Low and Medium by
    the rule at both sizes on the four rooms, the grazing room's probes' faces at the first bounce
    against High's, High's rooms unmoved, and the cost at every quality in the Cornell box, the
    carried lamp's room and Ore's landscape, whose last cascade's rays leave the field, since the
    march is longer only where the room is; held by a test, one commit. Then the refinement once
    more, expected to hold the grazing floor at Low, its own commit; then the merge's first-bounce
    excess at the pocket and the strip, the sampling term's two candidates, (c) and (d). Done at
    `14ae5a02` the same day, the last interval's end the field's edge, held by a cascade edge test,
    Medium and High unmoved, Low's grazing room from 4 percent under to 7 over against High's 8 with
    its view at High's own figure, the one widening, under the covered-fault principle, at 0.03 to
    0.15 ms where rooms are larger than the old reach. Measured on `14ae5a02` by 16:02: the
    refinement holds the grazing floor at Low, 0.104 to 0.075 and 0.086 to 0.046, narrows or holds
    every view at 160 by 90 at every quality and every view at 800 by 450 but the carried lamp's
    room, wider by 0.002 at High and at Low there while narrower by as much at the other size, its
    red and back walls nearer and its far wall further over, the level 1.061 to 1.075, which is the
    fixed ways' overread the lean's lower read there offset; the foot at the reference, the gain
    1.00 to 1.17, still views calm and the moved wall settling over two frames. The reviewer takes
    it under the covered-fault principle, the widening lying in the far wall's own term, the
    sampling term's, which stands in the order with the level of 1.075 as its figure to mend, and
    the room narrowing at the test's size; the measure's repeatability on the head read twice is
    said beside the figures, so a difference of 0.002 is known for what it is. Done at `257006c7`
    the same day, the reads at the surface and the lean kept for the ambient blend alone with its
    reasons in the remark, the bounds pinned with both figures per view, the measure shown to repeat
    exactly, and the wall's foot at 0.214 and 0.219 against 0.215, which is (d) done. Read by 16:47
    with a scratch test at High, the light the merge lent along each way marked as taken from the
    cascade above against the reference from the probe's place, and each probe above with its own
    light along the way against the reference from its own place: two causes, neither of the three
    named. Around the strip the excess is the probe's own rays and not the merge, one texel of each
    of the two probes carrying 3.7 and 3.5 against 0.41 and 0.17, three of its four rays meeting
    nothing and one meeting the strip itself 3.3 out, where the hit is lit by the strip's own glow
    light from no distance at about 14.8 and stands for a quarter of the texel, which the reference
    leaves out since a carried emitter's emission does not light the surface it is carried for; it
    read as the merge's because the texel's mark said most of its rays met nothing, and the probes
    above hold their places' light right along those ways. At the pocket it is the merge's parallax,
    one way lending 0.650 against 0.250 where all four rays met nothing within reach, the probe
    above that carries most of its weight, 1.28 away and higher, right where it stands and meeting
    the ceiling 1.6 from the lamp along that way where the pocket's same way meets it 2.7 from the
    lamp, where the reference is a quarter as bright, the probes above lending their view from where
    they stand in place of the lower probe's against a near and strongly varying bright spot. The
    reviewer's order: the strip's term first, a hit on a mesh carried as a glow light taking none of
    that light's own light, one commit held by a test, judged at the strip's two probes by bounce,
    the strip's view at both sizes and the glow tests; then the sampling term's two candidates
    again, since their term stands in every frame and its patches are the most seen, with the far
    wall's 1.075 as their figure; then the pocket's parallax, read for its mend's form and cost
    first, the lower probe's rays carried to where each probe above's interval begins so the merged
    ray is one ray, or what else fits, since it is a term of the merge's method and not a fault of a
    line; then (c). Read further by 17:32: the strip's term is a leak through a thin face and not a
    light lighting its own surface, a probe's ray passing through the strip, 0.06 thick and held
    about its middle by the field, and meeting the wall 0.02 behind it, which the strip lights from
    almost no distance, where the reference's path stops at the strip and leaves its emission out; a
    margin on the glow light and a test of the hit's normal left the excess standing. The mend
    measured, a light whose box the ray passed through within a cell before the hit giving the hit
    none of its light by a slab test cast back along the ray, takes the texel's own light from 3.70
    and 3.49 to none and the two probes' faces at the first bounce from 209 and 204 percent over to
    5 and 9, moves no view outside the strip's room, and widens the strip's view at every quality by
    0.026 to 0.044, the room reading 14 percent under where it read as its reference, its walls 12
    to 18 under and its floor 18, the two probes 18 and 24 under past the first bounce where they
    read 12 and 6 over; a first form leaving out any light within half a cell behind the hit dropped
    the strip's halo as well and is out. So the leak's excess covered an under-read of the room's
    light past the first bounce that stands at every quality and is named by where it shows and not
    by its cause. The reviewer's ruling is to record the mend and read the under-read first, by the
    principle as first set, the covered fault mended before the one that covers it is judged, since
    the widening lands at no figure already known and the room would read 14 percent under for
    anyone until the under-read's turn; the leak's mend stays in the tree and is judged again after
    it. The reading: the strip room's light past the first bounce at its probes' own hits by bounce
    and by where the hits stand, against the reference, with the leak's mend in, so the under-read
    is named by what carries it, the strip's light carried as glow lights at the probes' hits, the
    probes' reads at the surface in that room, or the merge. Named by 17:37: the merge's part is too
    small to count, the ways from the cascade above holding at most 0.17 of a face's weight and 0.01
    to 0.08 of light against faces of 0.5 to 0.7; at the probes' own hits the light past the first
    bounce reads 18 to 24 percent under on the faces the walls take, and the first bounce there, the
    strip's light at the hits, is uneven, 19 under on one face, 5 to 18 over on most and 47 to 133
    over toward the front wall and the back; and at hits on the room's surfaces, read as the code
    reads them with the probes holding one bounce, the floor's middle reads 0.71 of the reference
    with the probes' own light at 0.64, the ceiling 0.87 with 0.85, the back wall 0.75 to 0.85 with
    0.95 and a gradient of 0.79 to 0.88 running the other way, the probes standing where 12 to 21
    percent less of the strip's light arrives than at the wall beside it, and the front wall and the
    floor by the back 1.52 and 1.69 with the probes over and the gradient over. So two terms carry
    it, the strip's light carried as glow lights as the probes' rays gather it from the surfaces it
    lights, 36 percent under in what the floor's probes gather from above and 15 at the ceiling's,
    over toward the front wall and the floor by the back, and the reads at the surface beside the
    strip where the gradient reverses; the floor's middle and the back wall carry most of the room's
    14. The reviewer's order: the glow light's light at a hit first, a first-bounce term with a
    one-bounce reference, read at hits on the ceiling, the floor, the front and the back walls at
    their distances and angles to the thin emitter against the reference, so the light a glow light
    gives a surface is named by its form, its falloff with distance, its angle to the thin face or
    its box's extent, and mended in one commit judged at those hits, the two probes' first bounce,
    the strip's view and the other glow rooms; then the leak's mend judged again by the rule; then
    the reversed gradient beside the strip, which is the gradient's term in the other direction and
    goes with the sampling term's candidates and the parallax as the merge's and the read's terms;
    then (c). Named by 17:45 with screen probes in the strip's room at one bounce against the
    strip's exact unhidden direct light in closed form: none of the three forms, the falloff moving
    at most 4 percent at the front wall, the angle nothing within four half diagonals where each
    face's light is exact, and the box cut into points past that within 1.6 percent; what carries it
    is the visibility march's cone, which runs from the hit to the box's middle as wide as the box's
    mean half side, 0.35 for the strip, the same in every direction, so from directly above or below
    the strip, whose middle lies 0.05 from the wall it is mounted on, the march runs along that wall
    within a cell and reads it as a blocker, about 0.6 seen, though the wall stands behind the strip
    and never between; the hits read the strip's light 28 percent under at the floor, 30 at the
    ceiling, 12 and 24 at the side walls and 38 to 39 directly above and below. The mend under
    judgment, one mechanism in the march, is a cone whose width toward a surface a step passes is
    the box's own half-width across the way toward that surface, its support along the field's
    normal at the step, 0.03 toward the strip's wall and near what it was for a cube, the march to
    the faces' halves keeping its own cone; the floor from 28 under to the reference, the ceiling
    from 30 under to 10, the side walls 10 and 21 under, a few hits in the ceiling's back corners 35
    under remaining, to be read as their own figure. The leak's mend is set aside out of the tree
    while this one is judged by the rule. Judged on `33fd0804` by 19:51, the scripts recovered and
    the ten references at 800 by 450 traced anew at 2,048 paths: the cone's mend repeats its figures
    at the room's hits, moves no view but the strip's, and widens the strip's by 0.003 to 0.005 at
    both sizes at every quality, High among them, the back wall and the floor nearer and the side
    walls and the ceiling further over, the two probes' first bounce at the leak's texel unchanged
    at 209 and 204 percent over and their light past the first bounce a point or two higher, the
    Cornell box unmoved but the pocket's panel-lit faces; so the widening is the leak's excess no
    longer offset by the hits' under-read, the room having read level at the head because the leak's
    excess at the probes' first bounce covered the hits' 28 to 39 under. Both mends together widen
    the strip's view by 0.019 to 0.036, the room 12 to 13 percent under, the probes' first bounce 6
    and 10 over and their light past the first bounce 17 to 23 under, so the leak's mend uncovers
    the room's light past the first bounce as before and the cone takes about a point of it. The
    cone costs 0.23 ms at High in the Cornell box, 1.44 to 1.67, and 0.08 in the strip's room, the
    field's gradient's six reads a step about 0.13 of it and the narrower cone's longer marches the
    rest at Low; forward differences, three reads where six, would change its figures and be judged
    again. The reviewer's ruling: both mends stay in the tree uncommitted, and the strip room's
    light past the first bounce is read first with both in, so neither covers it, and named by its
    cause, the reads at the surface beside the strip where the gradient reverses, what remains of
    the probes' gathering of the strip's light at their own hits after the cone, or another; if a
    line's fault, it is mended first and the two mends follow by the rule; if the gradient's, a term
    of the method, the leak and the cone are taken together under the covered-fault principle, the
    strip's test pinned at the new figures with the reason naming that term, which then stands in
    the order with the sampling term's candidates and the parallax. The cone's commit, when it
    comes, measures forward differences against the six reads first and takes the three where the
    hits' figures hold within their bound, the cost said either way. Named and recorded at
    `132ac219` the same day with no shader change: neither of the two, but the band of light the
    strip puts on the wall it is mounted on, narrower than a quarter texel, which the probes' four
    rays at their fixed quarter-middles pass over, so the floor's probes gather the band's light at
    a tenth of the reference; a term of the method, in the sampling term's family, shown by the
    strip set half a unit off its wall reading within 2 percent of a fresh reference with any
    shaders. So the leak and the cone are taken together under the covered-fault principle as the
    ruling said: the cone first in forward differences, three reads where six, which hold the hits
    within two points of the six reads' figures at about the same cost, its test holding the
    ceiling's hits over the strip against the closed form; then the leak with its remark on the
    reference leaving a thin emitter's emission out; the strip's test pinned at the new figures,
    0.235, 0.246 and 0.245 where 0.216, 0.213 and 0.209, the reason naming the band with both
    figures; the band in the order beside the sampling term's candidates and the parallax, the side
    walls' 7 to 8 percent under noted as their own. The cone done at `fb1d1f95` the same day in
    forward differences, held by a test at the ceiling over the strip, at 0.15 to 0.19 ms on High's
    pass in the Cornell box. The owner saw three things at 21:16 on 2026-10-19, playing the voxel
    game on `5.2.92` with the day's work in it, and they are the item's parts (g), (h) and (i), each
    read in the game first, with the game's coder giving the world, the place, the hour and the view
    where each shows, and then mended as one mechanism a commit as the parts before: (g) with the
    screen probes on the indirect light looks like depth fighting, a flicker read frame to frame in
    a still view at the 99th part of its pixels and by tile, the screen probes' jittered places, the
    depth leap test of `9c44401f`, the thickness of the march or the filter's blend across tiles
    among the candidates; (h) light leaks a little, read where it shows against the field's cells
    there, a thin wall held about its middle, a probe inside a solid, the merge's parallax or the
    band's family among the candidates; and (i) when a block is broken the indirect light goes blank
    for a frame and shows again with the field's next build, read in the frames around the break,
    whether the field is cleared before its new build is ready or the probes read a field that is
    not yet stamped, mended so the light of the frame before stands until the new field is read. At
    21:19 the owner added two from the bounce rooms example, read the same way: (j) in the carried
    lamp's room with the light in its third state and the green wall in its second, green blooms
    over the wall's edge into what lies beyond it, read at the edge's pixels against the reference,
    the filter's blend across a depth or normal edge, the field's color blend at the wall's end
    after `a52b125b`, or a screen ray's thickness at the edge among the candidates; and (k) in the
    ninth room the corners carry a light border, which the owner sees in many places and from the
    screen probes too, read at a corner's pixels frame by frame and tile by tile against the
    reference, the filter blending tiles across the corner's two normals, the probes read at the
    surface on both walls at once, or the hits' facing rule among the candidates, (b)'s corner
    reading of the day beside it. The order: the leak's commit, then (i), (g), (k), (j) and (h), two
    read as one where they share a cause, then Verdict 72's reading and mend, then item 35, then the
    sampling term's candidates with the band beside them, the parallax and (c). The leak's mend done
    at `fa607cb3` the same day, the strip's test pinned at 0.234, 0.245 and 0.244 with the band as
    the reason, the two probes' first bounce at 7 and 10 percent over where 211 and 206, the
    emissive blocks' room at High moved by 0.0004 where a probe's rays cross a neighbor's box. (i)
    done at `03e0bd36` the same day, a mesh that replaced a kept one in place not stamped as its
    boxes while a cascade the budget left for later still holds the one it replaced, the pair of
    blank frames gone in the game, held by a test. (g) read by 22:08 in the game at the birch trunk:
    five screen probes on the trunk read 4 to 55 percent under their hemispheres where the
    hemisphere's light along the trunk varies little, each tracking the reference along its own
    sixteen ways within 24 to 30 percent, so the fault is in which ways there are and not in what a
    ray brings, one way meeting sunlit grass deciding a probe's green and the ways beyond reach the
    blue, and neighboring probes casting the same ways err alike, so the patches stand still and
    span many tiles, which the filter's blend cannot even out; the jittered places would move them,
    the depth leap and the march's thickness touch one to six rays a probe at 0.005 of light at
    most, and the filter is what fails to hide them. So (g) is the sampling term, the screen probes'
    sixteen fixed ways, and goes with its two candidates, the ways turned per tile and the
    thirty-six, measured as one with the band; and since the gain that held the candidates back is
    mended and this term is what the owner sees most, the candidates move up. The order: (k), (j)
    and (h) read, then the sampling term's candidates with (g), the band and whatever of the three
    shares the cause, measured by the rule and committed where one holds, then Verdict 72's reading
    and mend, item 35, the parallax and (c). (k) done at `7fe22abc` the same day, the glow pass's
    blend with the frame before taking only texels whose normal lies within 60 degrees of the
    point's, the corner's line of dots gone, held by a test, every view holding or narrowing; it was
    the glow pass's and none of the three candidates. The order: items 38 and 37, then (j) and (h)
    read, item 36, the sampling term's candidates with (g) and the band, Verdict 72, item 35, the
    parallax and (c). (j) and (h) read by 23:36, each a fault of a line with its own cause and
    neither the sampling term's. (j): in the carried lamp's room with the lamp in its third state
    and the green wall in its second, green spreads past the wall's edge onto the back wall and the
    floor, green over red 0.934 where the reference has 0.736 and 1.351 where 0.987, gone with the
    screen probes off and standing with the history, the glow and the merge off, so it is the screen
    probes' own rays, the filter spreading it; a back-wall probe's ray past the wall's end is called
    met on the screen at 3.02 and shaded as the wall's lit face, 3.26 in green where the reference
    along the way holds 0.22, since the march takes a point within two steps behind the depth as met
    unless the depth leaps, and a ray passing behind the wall's lit face crosses no leap, so it is
    met from behind on a face whose normal points its way, the third candidate; a turn-away after
    the march takes the green down and then takes the world probes' light beyond reach, the back
    wall reading 32 percent under where 6, so the mend belongs in the march, a face met from behind
    counting as passing behind the depth, the ray going on and the field taking it from where it
    last stood in front. (h): in the game's case at night, a warm patch on the grass outside a stone
    wall against whose inside a glowstone stands, the probe whose grid spot is the corner where the
    wall's inner face, the floor's top and the glowstone meet was moved along the field's normal
    into the stone and kept there since the field read outside, holding light at 2,450 percent over
    its reference on one face and not marked as holding nothing as the probes inside the ground are,
    so the grass outside blends it in through a block of stone, the candidate of a probe inside a
    solid, and the owner's faint smudge with the screen probes on is likely this probe through the
    light beyond their reach; the mend is in the placing, a moved spot judged by the rays that leave
    it or a probe kept out of a solid at a corner. The reviewer's order: (j)'s and (h)'s mends
    first, one commit each by the rule with a test, since each is a fault of a line the owner sees;
    then item 36, the sampling term's candidates with (g) and the band, Verdict 72, item 35, the
    parallax and (c). (j) done at `1e89515c` on 2026-10-20, a face met from behind passed as the
    depth is, the green over the edge from 0.973 to 0.829 against 0.736 on the back wall, held by a
    test, three views at 160 by 90 wider within their bounds and the grazing ceiling's four rays met
    on their own ceiling uncovered as the sampling term's. (h) done at `a7c2da8b` on 2026-10-20, a
    moved probe walled in where more than a quarter of its rays meet faces from behind, the warm
    patch on the grass from 5.8 times the reference to 1.42 and the owner's smudge with it, held by
    a test; the game's mesh leaving out the stone's faces against a lamp is the game's item 15. Item
    36 done at `089761fb`. The sampling term's two candidates measured again on `089761fb` by 01:49
    on 2026-10-20, and neither holds by the rule: both widen the thin walls, the window's room and
    the grazing floor at both sizes, by 0.013 to 0.032 at 160 by 90 and 0.005 to 0.026 at 800 by
    450, while the strip's room, the carried lamp's room, the Cornell box and the corridor narrow;
    where they widen, walls that read over with the fixed ways read further over, the thin room's 12
    to 15 percent and the window room's 10 to 19, and the two candidates agree within a point or two
    there. On the trunk the fixed ways read the frame 39 to 2 percent under around the five probes,
    the tiled ways 11 to 26 over and the thirty-six 13 to 54 over, the still patches turned to tile
    noise the filter evens out while the level moves with them; the three disagree in a fixed order,
    the thirty-six putting more ways near the horizon and the tiled ways moving off the texels'
    middles, so the light a ray brings depends on its way and reads over the nearer it lies to the
    surface's plane, which the fixed ways at their texels' middles under-read and so covered; no
    single part of a probe carries the excess, and the ceiling probes' rays met on their own ceiling
    were one such case, taken by (j). So the covered fault is in what the rays bring by their way,
    likely the grazing ways, and not in the sampling. The reviewer's order: Verdict 74's mend first,
    then the rays' error against the reference over their cells read by elevation on the trunk's
    probes and the window room's ceiling and walls with the tiled ways in, so the term is named by
    its cause, the screen march's thickness at a grazing way, the field's read at a grazing hit or
    the facing rule among the candidates; then its mend first if it is a fault of a line, or the
    tiled ways taken under the covered-fault principle with the rooms' and the trunk's figures
    pinned and the reason naming the term if it is the method's; the candidates held meanwhile.
    Verdict 74 mended at `b03017f2`. The rays' error read by 02:23 with the tiled ways in, each
    probe's error split into what the rays read along their ways against the reference along the
    same and how the ways sample, the reference along them against the whole hemisphere: in the
    window room the rays read 26 to 28 percent over and the fixed ways sampled 15 under, a net of
    nothing, where the tiled ways sample 2 under and the net is 12 over; on the trunk the rays read
    33 to 35 over and the fixed ways sampled 41 under, a net of 25 under, where the tiled sample 5
    under and the net is 16 over; so the tiled ways mend the sampling term with (g) in it at no
    cost, the same sixteen rays, and the rays' over-read of about 30 percent, which the
    under-sampling covered, shows. By where the rays are met, the light beyond reach carries the
    most, 17 to 52 percent over, the field hits 7 to 27 and the screen hits 8 to 12 in the window
    room; by angle from the normal the rays near the surface's plane read no excess, so the march's
    thickness, the field's read at a grazing hit and the facing rule are not it; and the window
    room's world probes read 5 percent over where their own rays met a surface and 17 over on every
    way merged with the cascades above, one face taking 0.141 from the cascade above against 0.051,
    a coarser probe standing elsewhere seeing along those ways the sunlit floor or the window past
    what the way itself meets, which is the merge's parallax, already in the order; the thin room's
    probes read 9 to 12 over from their own rays alone, the lamp's light through walls thinner than
    a cell, (f)'s term. So the reviewer takes the tiled ways under the covered-fault principle, the
    rooms' and the trunk's figures pinned with the reason naming the parallax and the thin walls as
    the terms uncovered, the view that moves past its bound pinned anew, the still frames read again
    and RENDERING.md taking the figures; then Verdict 72, item 35, the parallax, (c). Next the tiled
    ways' commit. Done at `4bc29a78` on 2026-10-20, the tiled ways as the head says, with (g) in
    them; the parallax and (c) stand in the order after item 35. The parallax's mend read for its
    form and cost by 03:35 on 2026-10-20, two forms measured as scratch on the head. The first, a
    parallax read, a term of the merge's method, takes the reach each probe above already writes,
    the distance its ray met a surface at along each of its 64 ways, and reads that probe toward the
    point on this probe's ray nearest where its own ray met a surface, in place of along the same
    way, its texels sampled there folded across the octahedron's edges, the read unchanged where its
    ray met nothing or the turn is nil, at the cost of one binding in the merge and the upper
    interval's end in the push, no trace. The second, the bilinear fix as a texel's own sight,
    counts a probe above for a texel only where its interval's start along that way can be seen from
    this ray's point as far out, eight short field marches a texel in place of one sight test a
    probe. At the pocket's probe at High, the part from the cascade above, the upward face reads
    0.274 on the head, 0.163 with the parallax read and 0.218 with the sight where the reference is
    0.101, and the face toward minus x 0.129, 0.063 and 0.099 where 0.049; every way merged, 16 over
    on the head and with the parallax read and 12 with the sight, the faces' totals moving a few
    points either way with their parts within the interval 8 to 24 over on their own rays. The cost
    over two rounds with frames unlimited, the Cornell box at 1.97 to 1.99 ms at High and 1.74 to
    1.76 at Low and the window's room at 1.15 to 1.16 and 0.96 on the head, is within 0.03 ms for
    the parallax read and 0.01 to 0.04 more in the window's room, and 0.24 more at High and 0.04 at
    Low in the box and 0.27 to 0.29 and 0.07 to 0.08 in the room for the sight. By the rule against
    the head, the parallax read narrows the window's room by 0.013 to 0.019 at the small size and
    0.025 and 0.015 at the large, the grazing floor by 0.008 to 0.015 and 0.008 and 0.013, the
    strip's room by 0.005 to 0.009 and 0.007 and 0.016, the carried lamp's room by up to 0.004 and
    0.005 and 0.003, the Cornell box within 0.001, and widens the thin walls alone, 0.004 to 0.005
    at the small size and 0.003 and 0.005 at the large, their regions reading as before at High, 11
    over, and a point further over at Low, the walls thinner than a cell, (f)'s term, which the
    tiled ways uncovered and which stands in the order. So the reviewer takes the parallax read
    under the covered-fault principle, the thin walls' widening pinned with the reason naming that
    term, the remark in the merge shader in place of the bilinear fix's sentence, a test at the
    pocket probe's part from the cascade above, the room bounds held or narrowed and the thin walls'
    pinned anew where they move, RENDERING.md with these figures, the suite and lavapipe, one
    commit; the sight is not taken, a tenth of the illumination's time for a worse read. Then (c),
    and after it the thin walls' term read for its mend's form and cost as the parallax was. The
    parallax read done at `b35bcb8f` on 2026-10-20, as the head says. (c) read first by 04:02 at the
    small block's front in the Cornell box, view 1 of the bounce rooms at 800 by 450 at High, the
    front reading 1.37 of the reference at both qualities with the fixed ways, 1.18 and 1.13 with
    the tiled and 1.20 and 1.10 with the parallax read. Twelve screen probes on the front with the
    tiled ways read their hemisphere 15 over, the rays bringing 26 over their cells and the ways
    sampling within 2; by where met, against the reference along each, the screen hits are 44 rays
    and 42 percent of the light at 43 over, the rays beyond reach 120 at 32 over and the field's 28
    at 17 over. Four of them read once at one bounce, the second round off against the reference at
    one bounce, and once in full: at one bounce they read 29 to 39 under along their ways, their
    screen hits 31 to 44 under along their own ways and their field hits 19 to 59, each hit's light
    its own alone, and in full 19 to 31 over, so the light past the first bounce at the probes reads
    several times the reference's, 0.64 against 0.17 and 0.60 against 0.18 in luminance at two
    probes, covering a first bounce that reads a third under. A hit's own light in the probe hit's
    shading is its color times the lamps' direct light less what the field hides plus the two
    brightest glow lights, and the reference counts a carried glow light, the box's panel among the
    four carried there, in the hit's direct light; so the shortfall at one bounce lies in the point
    lamp's shadow as the field gives it, in the panel's glow light at the hit, or both. Next the
    coder reads those against the closed form at the hits on the floor before the block and on the
    block's sides, as the strip's light was read, the lamp's direct light with its shadow and the
    panel's light by its rectangle, then the light past the first bounce at the same hits. (c) read
    and named by 04:06 on 2026-10-20, with the parallax read in, at view 1 at High. The hits' own
    light is right, and the earlier read of a first bounce a third under was against a reference
    holding one more bounce, since the reference at b bounces traces b plus 1 segments; against the
    reference at no bounce and the closed form, the hits' own light matches, the model's falloff
    0.2295 in green at a floor point against the reference's 0.231, and the engine's light with the
    panel by Lambert's rectangle 0.308 against 0.306 and 0.200 against 0.206 where both are in
    sight. The light past the first bounce at the floor hits the block's front probes meet reads 3.4
    to 3.6 times the reference within about 0.3 of the block's front, 0.517 against 0.146, 0.447
    against 0.131 and 0.375 against 0.104 in green, 1.7 times a little further, 28 over clear of it
    and under by the green wall, 0.222 against 0.418, the hits' full light 73 to 79 over before the
    block and 16 to 37 further off. The world probes around those hits read their own places near
    their references, the pocket's 16 over upward and 25 downward, its neighbors 2 to 12 over, and
    the one moved out of the block 5 under; the floor hit takes their light from 0.6 above the
    floor, where the block hides little, and not the light that reaches the floor in the block's
    lee, where the reference's light past the first bounce is a quarter of theirs. So (c)'s term is
    the probes' spacing against near occlusion, a term of the method, named with two candidates for
    a form and cost reading as the parallax's, an occlusion at each probe hit from a few short field
    marches scaling the light it takes from the probes, or the probes' blend at a hit weighted
    toward those the hit's own hemisphere sees. The reviewer orders (c)'s candidates read first
    while its figures are warm, then the thin walls' term read, the commits following the readings
    under the rule. (c)'s two candidates read by 04:51 on 2026-10-20 by the rule at both sizes, 160
    by 90 with the bounds at zero and 800 by 450 against the references, and none passes. The
    block's front against the reference at High and Low reads 1.198 and 1.099 on the head, the back
    wall 1.033, the floor 1.000, the whole box 1.011 and the illumination 1.98 ms at High; the blend
    toward the probes a hit sees changes nothing at the front, 1.21 at High for 2.24 ms, since the
    floor hits before the block see the same probes either way, so it is out; the occlusion forms,
    which scale the indirect light at the screen probes' hits alone, bring the front to 1.099 and
    1.015 with four short marches a spacing out for 2.45 ms, 1.145 and 1.062 half a spacing out for
    2.27, 1.143 and 1.054 by the ratio of the hit's open share to the blended probes' by their reach
    for 2.62, and 1.019 and 0.953 by the field distances along the same four ways for 2.12, but
    every one widens the corridor at both sizes by 0.007 to 0.027, the strip by 0.002 to 0.048 and
    the carried lamp's room by 0.002 to 0.045, the march over a spacing and the field distances
    widen the Cornell box by 0.006 to 0.018, and the half-spacing march and the ratio hold the box
    at the large size and widen it by 0.002 to 0.004 at the small at Low and Medium, while all
    narrow the thin walls' view by 0.019 to 0.079 and the window's by 0.006 to 0.031; these are the
    two faults the record of 2026-10-19 named for the openness ratio, a near surface counted as
    shade though it lights the hit, darkening rooms that read under, and the occlusion covering the
    thin walls' term by darkening the hits beside a thin wall that take the outside lamp's light. So
    (c) stands as named, the probes' spacing against near occlusion, with no mend that holds the
    rule. The thin walls' term has a likely cause, to be read on the GPU: the hidden lamp light's
    march starts 1.5 cells off the hit and its see-through skips its first two cells, so a floor or
    ceiling hit within about a tenth of a unit of a thin wall's inner face starts the march past the
    wall and takes the outside lamp's full light; the coder reads the thin room's world probes on
    the head and with the march starting half a cell out, keeping the two cells it stops short of
    the lamp, then the rule and the cost as for the parallax. The thin walls' term read again by
    04:58 on 2026-10-20, and its naming changes; the near-start candidate is out. The thin room's
    world probes do not take the outside lamp's light: five of them, by the right wall, its foot,
    mid-room and the far wall, read the same to the digit with the outside lamp at zero, 0.267
    against 0.242, 10 over, while the probe outside the wall goes from 7.67 to 0, and at one bounce
    their own rays match the reference at none, 0.074 against 0.075 and 0.100 against 0.100, so
    their 10 over is the inside lamp's light past the first bounce, the gain a bounce read before,
    even over every face and way and as large by the far wall; the earlier naming, the world probes'
    own rays taking the lamp's light through the thin walls, was wrong, and the hidden lamp light's
    march start changes nothing at them. The frame does take it at the thin wall's edges: at view 2
    at High at 800 by 450, where the reference is identical with the lamp on and off, the engine's
    thin wall facing minus x reads 15.1 over with the lamp on and 7.2 off, every other region moving
    0.5 to 3 points and the whole frame from 10.9 over to 8.0; on the wall in blocks of 20 pixels
    the lamp gives 1 to 2 percent of the reference in the middle, 50 to 62 along the foot, 20 to 30
    along the ceiling and 15 to 22 at the corner with the front wall. A screen probe on the wall by
    the front corner gains 6.6 percent of its light from the lamp, its rays meeting surfaces within
    about 0.4 of the thin wall each gaining it, 0.338 on against 0.206 off where the reference reads
    0.183, their own light the same on and off, so all of it past the first bounce; the screen
    probes' hits read the world probes through the bounced read weighed by the sight test with a
    slack of half a spacing, 0.6, and the probes 0.55 beyond the 0.1 wall reach its outer face, the
    slack passing them as seeing the hit, so their faces, 7.67 lit by the lamp, light the hits on
    this side, while the world probes' own hits march to their probes and do not take it, the model
    pass's tick at the corner having been the same fault in pixels, mended by (f). So the term is
    named anew: screen probes' hits beside a wall thinner than half the probes' spacing take the
    light of the probes beyond it through the reach's slack. Two candidates, read next by the rule
    and the cost: (a) the sight test's slack held to a cell or two in place of half a spacing, at no
    cost, which every reader of the bounced read takes, the screen probes' hits, the model pass and
    its reflections; (b) the screen probes' hits through the seen read's marches, (c)'s seen
    variant, 0.26 ms more at High in the Cornell box with no change at the block's front. A doubt
    raised beside it, that the readings labelled the panel alone had set the sun's light to zero and
    not the lamp's, was withdrawn by 05:00 on a second look: those readings ran in the Cornell box
    program, whose only light is the lamp as entity 1 and which has no sun, the sun being entity 1
    in the bounce rooms alone, so the panel-alone figures and RENDERING.md's line stand. The thin
    walls' candidates read by 05:18 on 2026-10-20 by the rule at both sizes with the cost and the
    reflections, (a) the sight test's slack at one cell, 0.125 of the spacing, and at two, 0.25,
    where half a spacing stands, and (b) the screen probes' hits through the seen read's marches.
    Against the head at 800 by 450 at High and Low, one cell narrows the thin walls by 0.017 and
    0.015, two cells by 0.014 and 0.012 and the marches by 0.022 and 0.019, and widens the corridor
    by 0.015 and 0.016 for either slack and 0.038 and 0.039 for the marches, the Cornell box by
    0.002 and 0.001 and the grazing room by 0.003 and 0.000, the small size reading alike and the
    other five views holding to the third digit; so by the rule alone each is out, the corridor
    widening most. The widening is the same leak taken away, running the other way: the thin wall's
    inner face reads 1.153 of the reference on the head and 1.072 with the outside lamp off, 1.098
    with one cell, 1.109 with two and 1.085 with the marches, the room's other surfaces holding at
    1.08 to 1.10, the gain past the first bounce; the corridor's walls read 67 to 70 under the
    reference on the head and 78 to 79 under with one cell, 90 to 92 with the marches, so the slack
    had lent them the light of sunlit probes beyond their walls, 0.3 thick, less than half the
    spacing, covering an under-read of the corridor's own not yet named by cause; the Cornell box's
    back wall goes from 1.033 to 1.043 and its red wall from 1.001 to 1.008, and the grazing room's
    wall facing minus x from 1.094 to 1.122, where the slack had lent dark probes from behind the
    walls, covering part of the gain. The cost of one cell is the head to the hundredth, the Cornell
    box 1.94 ms at High and 1.71 at Low and the window's room 1.15 and 0.96, and the marches 0.25 to
    0.42 ms more; the reflections scene and the reflection probe model, against references traced
    there, move by 0.01 and 0.09 percent of their light with one cell, 0.3072 to 0.3072 and 0.1330
    to 0.1336, and 0.0 and 0.06 with two, the marches giving 0.3075 and 0.1316, the model pass's own
    pixels in those frames. The reviewer takes (a) at one cell under the covered-fault principle,
    since what it takes away is light lent through solid walls, a mechanism known to be wrong at no
    cost with the reflections unmoved, unlike (c)'s candidates, which added a mechanism of their
    own; the corridor's own under-read is named as a term in the order, 67 to 70 under on the head,
    to be read for its cause next, before any other mend, and the Cornell box's and the grazing
    room's rises are the gain, already in the order. The commit in the usual form, the remark at the
    sight test naming the slack's borrowed light with the thin wall's and the corridor's figures,
    the thin walls' bounds narrowed and the corridor's and the grazing room's pinned with the
    reason, a test at the thin wall's inner face against the lamp off, RENDERING.md with these
    figures, the suite and lavapipe. (b) is not taken, more cost for less on the thin wall and the
    corridor uncovered twice as far. The thin walls' term mended at `4369898a` on 2026-10-20 with
    the slack at one cell, as the head says, the corridor's and the grazing floor's bounds pinned
    with the reason; a further way the lamp's light reaches the corner by the front wall through the
    model pass's read of the probes, 2.85 times the wall beside it with the screen's probes left
    out, is noted and not named. Next the corridor's under-read, 78 to 79 under at 800 by 450 with
    the mend, read for its cause by bounce and by where the light is met, before any mend. The
    corridor's under-read read and named by 05:51 on 2026-10-20 at view 3 on `4369898a` at High,
    nothing changed. The corridor, 2 wide in walls 0.3 thick, holds no probe of the second cascade
    or above, so the light along it past the first cascade's interval, from the sunlit floor at its
    open end 6 or more units off, reaches no probe inside it. Every first-cascade probe in the
    corridor reads nothing, four of them 0.000 by their own rays and merged at every bounce where
    the reference reads 0.011 to 0.027 merged and 0.005 to 0.015 by the rays' own reach, the sun met
    along the corridor at no bounce past the hits; the second cascade's spots, 2.4 apart, fall at z
    1.2 either side, inside the walls, and each is moved to stand a tenth of the spacing off the
    face nearer it, the outer one, to 1.54, outside the corridor where the sun lights it, 0.206 by
    its own rays against 0.200, and the third cascade's nearest stand outside too, so the merge's
    marches to the probes above see none of them through the walls and a first-cascade way that
    meets nothing within its interval takes nothing; the screen's probes take nothing either, one on
    a wall reading 0.000 against 0.008 along its sixteen ways, its hits' own light holding no sun
    this deep and the indirect light they read the first cascade's, which is nothing; before
    `4369898a` the slack had lent the walls' hits the sunlit first-cascade probes outside at z 1.8,
    67 to 70 under where 78 to 79 without it. So the term is the coarse cascades against a space
    narrower than their spacing, the probe's move out of a wall picking the nearer face whichever
    side the room lies, and a corridor 2 wide having no spot of a 2.4 grid inside it. Three forms
    for a reading as the others: (i) a probe whose spot lies inside a wall moved to the side with
    room for it, where the field shows space beyond the farther face within its most move of 0.45 of
    the spacing, at z 1.2 a move of 0.2 inward plus the clearance to 0.76, inside the corridor, at
    no cost in the frame, only the trace's choice, rooms whose walls sit on the grid's spots moving;
    (ii) a first-cascade way that meets nothing within its interval, where every probe above it is
    hidden, traced on through the field to what it meets and shaded with that hit's own light, the
    far light's first bounce, costing only where no probe above is seen; (iii) the screen's probes'
    rays past their reach the same, marching on where the world's probes around their end hold
    nothing. The reviewer orders (i) read first, since it mends the cause where a spot can be moved
    inside at no cost in the frame, by the rule at both sizes with the rooms whose walls sit on the
    grid's spots watched, and then (ii) and (iii) together as one reading, the far light's first
    bounce where the cascades cannot stand, by the rule and the cost, since a corridor narrower than
    the first cascade's spacing would still have no probe. The corridor's three forms read by 06:19
    on 2026-10-20 by the rule at both sizes and by cost, nothing committed. (i), a probe whose spot
    lies inside a wall going through its farther face where the next spot that way stands in no open
    space and the next on the nearer side does, moves the second cascade's probes at x 80.4 and 82.8
    inside to z 0.74 either side, those at 75.6 and 78 staying at 1.54, and the ones moved inside
    read nothing, 214 of their 252 ways meeting a wall before their interval begins and the rest
    reaching the third cascade outside, so it widens the corridor by 0.002 to 0.006 at both sizes,
    its walls going to 81 to 82 under where 78 to 79, every other view holding to the third digit;
    out by the rule. (ii), a world probe's ray that meets nothing in its interval tracing on through
    the field where its probe sees none of the eight above, with eight marches a workgroup as the
    merge does, and (iii), a screen probe's ray past its reach tracing on where the world's probes
    around its end hold nothing, the hit shaded as a probe's hit is either way, each narrow the
    corridor alone with every other view holding to the third digit, (ii) by 0.005 at the large size
    and 0.004 to 0.005 at the small, (iii) by 0.025 and 0.023 and 0.019 to 0.027, and both together
    by 0.009 and 0.007 and 0.002 to 0.008, less than (iii) alone, since (ii)'s small light at the
    probes stops (iii)'s march, which goes on only where the probes hold nothing; the corridor's
    regions at 800 by 450 at High go from walls 79 and 78 under, ceiling 69 and floor 66 to walls 66
    and 68, ceiling 52 and floor 54 with (iii), and to 77 and 75, 65 and 64 with (ii), the
    mid-corridor world probe 86 under where 100. The cost at High and Low on the development
    machine's GPU with frames unlimited, the corridor 1.27 to 1.29 and 0.99 ms on the head, the
    strip's room 1.44 to 1.47 and 1.30 to 1.32, the emissive block's room 1.90 to 1.93 and 1.72 to
    1.74, the Cornell box 1.95 and 1.72 and the window's room 1.15 and 0.97: (iii) takes the
    corridor to 1.50 and 1.22 to 1.26, the dark rooms the same, the box 0.01 more and the window's
    room 0.04 more at High, costing where it acts; (ii) costs 0.02 to 0.06 everywhere. The reviewer
    takes (iii) alone as the mend, (ii) not taken and (i) out, with its open risk measured first: a
    dark scene under a black sky, such as the ore sample at night, where a ray toward the sky whose
    probes hold nothing marches to the field's edge every frame, which the bounce rooms do not show;
    the coder measures the sample by night from a scratch copy of its build before the commit, and
    if the night's cost is more than a fraction of the day's the march takes a bound, a most length
    or a budget a frame, with the figures in the remark; then the commit in the usual form. What
    stays under in the corridor is the light past the first bounce, the world probes there 86 to 100
    under, so the screen probes' hits read nothing from them, a term that stands in the order under
    the gain. The ore sample by night measured by 06:54 on 2026-10-20 from a scratch copy of its
    build on the tree's engine and the head's shaders, frames unlimited: unbounded, (iii) adds 0.13
    to 0.19 ms by day and by night alike at High and Low on 3.3 to 3.9 ms, the night costing no more
    than the day, so no bound is forced; held to four times the rays' reach, 9.6 units there, it
    adds 0.05 to 0.14, the corridor keeping nearly all of its gain, narrowing by 0.017 to 0.023 at
    both sizes where unbounded by 0.019 to 0.027, every other view holding to the third digit, so
    the coder takes the bound, half the cost for the same mend, and the reviewer agrees; a corridor
    test reads the wall's screen probe at 0.13 of the reference along its ways with it where 0.00
    without, bounded above 0.06. The commit follows. (iii)'s checks by 07:19 on 2026-10-20 found a
    fault the rule's rooms do not hold: the cascade edge test of a face before the eye staying dark
    where the eye stands nearest the last cascade's side failed in the suite and on lavapipe, a
    closed stone room with walls 1 thick, the field's cells 0.25 doubling to 2, the pillar's face
    reading 0.029 of the open floor's light where the bound is 0.002, everything else passing, 1,745
    and 88. Three forms read against it: twice the reach for a bound still fails at 0.029; the hit's
    own light alone passes but loses the corridor's whole gain, walls 79 and 77 under where 67 and
    69, since the far hits' light comes mostly through the probes there; and the march held to the
    cascade its ray starts in and the next coarser one passes and keeps the corridor's gain whole,
    walls 67 and 69 under, ceiling and floor 55, at the same cost, 1.48 ms in the corridor. So the
    leak was the march into the coarser cascades, where walls 1 thick in cells of 2 let it through
    to the lit ground outside; the form is a trace held within those two cascades beside the field
    trace, its remark giving the cascade edge figure, and the rule at both sizes and the ore sample
    by day and night run again on that code before the full checks and the commit. The reviewer
    agrees, and asks the cascade edge figure pinned in the test as it stands. The corridor's term
    mended at `e6594bc4` on 2026-10-20 with form (iii) held to its cascade and the next coarser, as
    the head says. Next the light past the first bounce at probes that hold nothing, read for its
    cause. The light past the first bounce at probes that hold nothing read by 07:49 on 2026-10-20
    on `e6594bc4` at view 3 at High, nothing changed. Four screen probes about 13 units into the
    corridor, on the two walls, the floor and the ceiling, read 0.000 along their sixteen ways from
    the reference at no bounce, 0.001 to 0.002 over the whole hemisphere toward the sunlit patch at
    the open end alone, so the reference's light there is all past the first bounce, 0.004 to 0.012
    along the ways; the engine reads 0.000 at every one with the second round off, so all its light
    there is past the first bounce too, what the hits take from the world's probes, and against the
    reference along their ways the walls read 87 and 90 under, the floor 48 and the ceiling 27, the
    rays that meet the corridor on the screen within reach reading nothing against 0.001 to 0.003
    and those met in the field, most marched on by (iii), 36 to 78 under. The cause: a hit's light
    past the first bounce comes from the world's probes around it, and the corridor's first-cascade
    probes hold nothing at any bounce, their own rays meeting only its walls, which no lamp or sun
    reaches this deep and whose light past the first bounce comes from the same probes, while the
    merge lets nothing into that loop since no coarser probe stands inside; so the far light's first
    bounce never reaches the world's probes, and every bounce after it is missing at every hit the
    screen's probes meet near them, (iii) giving the far hits the far light's first bounce through
    the probes near the open end and the corridor's own probes nothing. The form to read, (ii)
    beside (iii): (ii)'s trace held within its cascade and the next coarser one as (iii)'s is, and
    (iii)'s march set off by the same finding as (ii)'s, where the probes around a ray's end see no
    probe above, which the merge finds each frame and would write into the probes' state for the
    screen's pass to read the frame after, in place of being set off by the light, so (ii)'s small
    light no longer stops it; and why (ii) reached only 14 percent of the reference at the
    mid-corridor probe, the sunlit patch subtending a few of its 256 rays where the reference's
    light there is mostly the walls near the open end that the patch lights, which (ii)'s hits take
    from probes near the open end. The reviewer orders that form read by the rule at both sizes and
    the cost as before, with the frame's lag of the finding read too, a probe's state from the frame
    before where the camera or the world moves. That form read by 08:02 on 2026-10-20 and out by the
    rule, nothing changed. The merge marks every texel of a first-cascade probe that sees none of
    the probes above around it, its alpha two more, safe since the readers test that alpha against
    0.25 alone and no cascade's merge reads the first's; (ii) traces such a probe's ray on within
    its cascade and the next; (iii) marches on where the probes around a ray's end are mostly marked
    or hold nothing; the finding has no lag, since the screen's pass is recorded after the merge in
    the same frame. Against `e6594bc4` the corridor moves by 0.001 and 0.000 at the large size and
    0.003 narrower, 0.001 wider and 0.000 at the small, Medium widening, every other view holding to
    the third digit, the corridor's walls as on the head and its ceiling and floor 52 and 54 under
    where 55, for 0.01 to 0.02 ms more, the still frames as on the head. Why (ii) reaches 14 percent
    at the mid-corridor probe: its face toward the open end takes the far light at 0.020 against
    0.125, 85 under, and its other five faces 87 to 93 under, their ways meeting the near walls
    within the interval at 0.001 a way where the reference reads 0.010 to 0.017; the ways that go on
    meet the sunlit patch, which gives its own light, and the walls and floor near the open end,
    which no sun reaches and which take their light past the first bounce from second-cascade probes
    that all stand outside the corridor behind its walls, so they bring nothing past their first
    bounce where the reference carries the patch's bounce; and the walls and floor deep in read only
    the probes' faces along their normals, which see the near walls, so the light along the corridor
    in the open end's face reaches them only where a surface faces it, at the closed end. So the
    light past the first bounce at probes that hold nothing stands, named as two terms of the
    method: the coarse cascades' absence inside a space narrower than their spacing, and the faces'
    read along a surface's normal of light that runs along it. Both stand in the order with the
    gain; none has a mend within the rule today.

31. **An example shows raylib's picture and none of the engine's making, the gamepad's controller
    first (Decision 44), done at `29eb2eca`.** The owner said on 2026-10-18 at 22:25 that the
    gamepad example shows a controller of the engine's own making where raylib's example shows its
    picture, and asked for the right one, and for the other examples to be checked alike.
    `CoreInputGamepad.cs:25-26` loads raylib's `ps3.png` and `xbox.png`, which
    `build/fetch-raylib-resources.sh` fetches for a capture and a build copies beside the program,
    so what the owner saw is read first, the example run as the owner runs it, with a controller and
    from the package, and the picture it shows found, a stand-in drawn where the file is missing, a
    placeholder texture, or the capture's frame without a controller; then the example shows
    raylib's pictures wherever it runs, shipped under the examples' resources with raylib's license
    for them noted where the other fetched files' is, or fetched where the example is built, as the
    file's license allows; and every example is read for a picture of the engine's making standing
    where raylib's resource belongs, each replaced by the resource raylib's example loads, listed
    here. One commit, the captures taken again where they change. Done at `29eb2eca` on 2026-10-18,
    the two pictures kept under CC0 with the notice, SDL3's PlayStation names read, every other pad
    on the Xbox picture, the capture taken again, and the sweep finding no other stand-in.

32. **Particles that live in their emitter's space and stand where the program puts them, for the
    game's stars (ASKS.md, 2026-10-18), done at `f5035acd`.** The voxel game's stars are fixed
    points on a sky that turns with the hour, asked for as a particle effect parented to the sun's
    and the moon's places (its item 6), and the package's particles cannot be that: `Particles.Run`
    (`3DEngine/Rendering/Particles/Particles.cs:52`) takes the translation of the emitter's world
    matrix alone, so its rotation and its parent's turn reach no particle, each starts at a random
    point within the radius and is stepped on the GPU in world space, keeping its place in the world
    as the emitter turns. Two things. An emitter may keep its particles in its entity's space, so
    they move and turn with its transform and its parent's, the simulation's positions local and the
    world matrix applied where they are drawn; and a program may place particles at positions it
    gives in that space, living until it removes them, beside the random birth within a radius. Both
    reach the flat API and the entity API as the emitter's other settings do, an example shows a
    field of fixed points turning with a parent, a test holds a particle's place through a parent's
    turn, and the particles' guide says both; the game moves its stars to them when a package brings
    them, and `ASKS.md`'s entry carries the item's number. After item 31 and before item 30's parts,
    being a day's work that the game's item waits on, the owner's eye welcome on the order. One
    commit. Done at `f5035acd` on 2026-10-18, `Local` and `Points` on the emitter with the
    rotation's call in the flat API, two tests, an example of a turning sky and the guides; the game
    takes it from the next package.

33. **A setting turns the sun's contact shadows off, or leaves them out where the shadow map holds
    the point (ASKS.md, 2026-10-18), done at `a2df7bb4`.** The voxel game draws its sun's shadows
    stepped along the blocks' pixels (its item 7), and the sun's light at a point is the shadow
    map's share times the contact shadow's (`modelpass.slang:583`), the contact shadow traced toward
    the sun through the scene's distance field in the occlusion pass at half the window's size, out
    to 32 cells of the finest cascade, blurred with the occlusion and read by the fragment's place
    in the window (`modelpass.slang:293`, `ao.slang`), on wherever the field is built and the sun
    casts shadows (`AmbientOcclusionRenderer.ContactShadows`), and off only with the field, which
    global illumination needs; measured on a pillar one block across under a low sun, it darkens the
    grass 0.4 to 0.7 of a block past the hard edge. One thing. A setting of the lighting, reached
    from the flat API and the entity API as the shadow distance is, turns the sun's contact shadows
    off, or leaves them out where the shadow map holds the point and keeps them past its reach, with
    the reason in the guide; a test holds a hard edge with them off and the field on; the game turns
    them off when a package brings it. The far cascade's texel at the game's distance, 1.27 of a
    block's pixel where the near ones are 0.11 and 0.38, is the lesser thing the entry notes and no
    item. After item 32. One commit. Done at `a2df7bb4` on 2026-10-18, `SetContactShadows` with On,
    PastShadowMap and Off, a test holding a hard edge with the field on and the figures in its
    comment, the guides following; the game takes it from the next package.

34. **An ImGui draw command binds the texture it names, so a window draws a program's `Texture2D`
    (ASKS.md, 2026-10-18), done at `c6914ab3`.** The voxel game draws its items' icons once into an
    atlas (its item 9), which its hotbar draws as a texture and its inventory window, drawn with
    ImGui, cannot: `ImGuiRenderNode.Run` binds the font atlas's set once before its loop over the
    draw commands (`3DEngine/Gui/Vulkan/ImGuiRenderNode.cs:172`) and draws every command with it,
    the command's texture id read by none, so an image of a program's texture draws a part of the
    font atlas; the game draws the icons' pixels as 11,087 rectangles meanwhile, half a millisecond
    a frame with the window open. One thing. A `Texture2D` gives an ImGui texture id, through the
    flat API and the entity API as the fonts are reached, and the ImGui pass binds a descriptor set
    for each command's texture as ImGui's own Vulkan backend does, the font atlas's where a command
    names none, with a test drawing a program's texture in an offscreen window and reading its
    pixels, and the overlay guide saying it. After item 33. One commit. Done at `c6914ab3`,
    `GetImGuiTextureId` and the pass binding each command's texture's set, a test reading the drawn
    texture beside the atlas's text, the guides following; the game takes it from the next package.

35. **The README's centered parts are markdown inside one centering block (Decision 45), and
    nuget.org draws them.** The heading, the description and the banners at the README's head stand
    as markdown inside one `<div align="center">` with blank lines inside it, no `<h1>`, `<p>` or
    `<img>` of their own, and every other HTML line of the README is read for whether markdown says
    it, the tables of pictures staying HTML unless thumbnails at the shown width make a markdown
    table render alike on GitHub and nuget.org, measured first; checked on GitHub's rendering and on
    nuget.org's page of the next version uploaded, the gallery's script writing the same. One
    commit, after the cone's and the leak's and before the sampling term's candidates, with the
    game's README left as it is, since no package carries it. Done at `9fd38dd9` on 2026-10-20, as
    the head says.

36. **A draw through a program's own shader is culled by its mesh's box where the program says the
    vertex stage keeps the positions (ASKS.md, Voxel-Sandbox's fourth entry).** Read in the game at
    {now} on 2026-10-19: its terrain draws through its own shader, whose vertex stage keeps the
    mesh's positions, and the model pass gives a batch with a program's own shader no box to cull by
    (`ModelRenderer.Instances.cs:108`, `batch.Custom < 0`), so every pass draws it whole
    (`ModelRenderer.Culling.cs:104, 115`), the nearest shadow cascade, which holds the first 7.7 of
    the 96 blocks shadowed, drawing 563 sections against the camera's 627 at 8 columns of 16 and
    2,021 against 2,251 at 16, so the game's frame on the development machine's GPU holds 60 a
    second to 8 columns and falls to 21 at 16, the shadows at 23.5 ms and the main pass at 12.9
    where the illumination stays near 2, and the game holds its render distance at 8. One commit: a
    program may say of a shader that its vertex stage keeps the positions, so a batch drawn with it
    takes its mesh's box and is culled by it in every pass as the engine's own are, the default as
    it is since a shader may move vertices, said in the remark and the shaders guide; held by a test
    of a batch with such a shader culled from a cascade it lies outside of; measured in the game,
    the draws per pass and the frame at 8 and 16 columns before and after. Done at `089761fb` on
    2026-10-20, `E3D.SetShaderKeepsPositions`, the game's frame at 16 columns from 42 ms to 26 to 30
    with its terrain shaders marked, held by a test; the game writes its line when a package carries
    it.

37. **A package's release notes are the commits since the package last published (Decision 47).**
    The pack run's notes are the commits since `build/version.txt` last changed, the whole minor
    each time, 89 lines at `5.2.92`. The notes take the latest version of the package the feed
    lists, from nuget.org's index of the package and that version's nuspec, whose repository commit
    names where that package was built, and are the commits from that commit to the head, newest
    first, each commit's sentence a line as before; where the feed answers nothing, lists no version
    of the minor, or names a commit the checkout does not have, the notes are the minor's as before
    and the summary says why; the pack job's step says which commit the notes start from. Held by a
    test of the script on a scratch history with a feed answer given to it, and read on the next
    package's page. One commit, with item 36 and before Verdict 72's reading. Done at `c9e1a5ac` on
    2026-10-19, 15 lines where the minor's 105 against the feed's `5.2.92`, and widened the same
    day: a commit whose every file lies under `.github/` is left out, since the package carries
    nothing from there, in a small commit before (j). The rule is in at `8e3ff67a`, the notes at 5
    lines of the engine's alone. Proven on `5.2.155`, listed on 2026-10-20 at 08:48 with 18 lines,
    the engine's commits since `33fd0804` and none of the review's. Proven again on `5.2.190`,
    listed on 2026-10-20 by 21:03 with 33 lines, the engine's commits since `9f9180b7`.

38. **The pack run publishes through nuget.org's trusted publishing and no stored key (Decision
    48).** The pack job gains the identity token's write permission beside its read of the contents,
    a step with the feed's login action, `NuGet/login@v1`, given the nuget.org user name the policy
    belongs to, `EggyStudio` unless the owner says another, which answers a short-lived key, and the
    push step takes that key where it took the secret, the input's description and the head comment
    saying the run publishes through the trusted publisher policy and needs no secret, BUILDING.md's
    publishing section saying the same with the policy's values, workflow `pack.yml`, no environment
    and the package pattern; the step with the secret goes. Checked by a pack run with publish on,
    whose push the feed accepts, the first the owner starts after the policy stands. One commit, the
    pattern `3DEngine*`; at 22:54 the owner asked whether the pack could upload by itself, so this
    comes first, right after the reading in hand, with item 37, and items 36, Verdict 72 and the
    rest after. Done at `4af3cdde` on 2026-10-19; the first pack run with publish on is its check.
    Proven on the pack run of `9f9180b7` on 2026-10-20 at 08:38, the login and the push through the
    policy passing and `5.2.155` pushed. Proven again on the pack run of `b3922179` on 2026-10-20 at
    20:53, `5.2.190` pushed.

39. **RENDERING.md names the terms of the global illumination's method that stand unmended, with
    their figures.** The week's readings under item 30 ended on 2026-10-20 with the rule's rooms at
    their bounds and five terms named by cause whose mends widen a view or cost more than they give:
    the gain past the first bounce, 8 to 16 over at the world probes; the probes' spacing against
    near occlusion, the small block's front at 1.20 of its reference; the coarse cascades' absence
    inside a space narrower than their spacing, the corridor's walls 67 to 69 under; the faces' read
    along a surface's normal of light that runs along it; and the further way the lamp's light
    reaches a corner through the model pass's read of the probes, 2.85 times the wall beside it with
    the screen's probes off. The readings that found them stand in REVIEW.md alone, and a reader of
    the engine has no page saying where the method stops. So RENDERING.md's global illumination part
    gains a section naming each term, its cause in a sentence, the room and figure that show it, the
    forms read against it and why each is out, and the rule they were read by; a sentence in the
    global illumination guide points to it. One commit, documents alone, carrying the review's
    files. Done at `82dd08f2` on 2026-10-20, as the head says.

40. **A program reads a joint's force, `GetPhysicsJointForce`.** Found by the rope-and-bridge puzzle
    on 2026-10-20 at 10:25, before its commit: its breaking rule, a joint broken where its force
    passes a bound so a weak bridge breaks where it is weak, cannot be built on what the flat API
    gives, since the API reads a contact's impulse and no joint's, and the one measure in reach, how
    far a joint's two anchor points stand apart under the load, follows the load and not the design,
    0.034, 0.056 and 0.092 m at 150, 400 and 800 kg for a truss of 17 planks and 27 joints against
    0.026, 0.057 and 0.098 for a bare road of 4 planks and 5 joints, a chain of planks on ball
    joints carrying a load in tension as a rope does, so a bound on the give breaks both bridges or
    neither. The physics library keeps each constraint's accumulated impulse and reads its magnitude
    by the constraint's handle, which over the fixed step is the joint's force in newtons. So the
    engine gains the call, the impulse magnitude of the joint's own constraint over the last step,
    its limit and motor apart, costing nothing until called, with its cheat sheet line, the surface
    listing, the upgrading guide's added entry and the physics guide's paragraph on joints, and a
    test of a box hanging on a distance joint reading its weight within a percent. One commit; then
    the puzzle breaks a joint past a force measured against the truss and the road, and reads with
    the force in hand why the load fell through the far cliff after crossing at 400 and 800 kg, a
    plank's end pinned at the cliff's edge and pressed into it the first suspect, said as a finding
    if it is the engine's. Done at `fdcfd435` on 2026-10-20, as the head says. A finding by 11:15
    the same day, before the publish: the read on a weld depends on the order its two bodies were
    given, and for welds and hinges it adds newton-meters to newtons. A 10 kg box, 98.1 N, on a
    kinematic anchor over 120 steps at rest reads 98.1 as a weld with the anchor first and 138.7
    with the box first, 404.5 with the box 4 m out, since the library's weld holds the offset of its
    second body's center and its linear impulse acts there, so with the box second the moment falls
    on the kinematic side and reads nothing, the linear part 98.1 in both orders and the angular
    part alone moving; a weld arm of two boxes reads 219.5 at the root, 196.4 of force with an
    angular 98 where the root's real moment is 294, so the angular part is not the moment at the
    joint either; a hinge on a vertical axis with the box 2 m out reads 219.4, the force 98.1 at the
    hinge's point and the moment 196.2, defined at the point but in mixed units; a stack of three
    welded boxes reads 294.3, 196.2 and 98.1 from the bottom, compression alone. The mend, one
    commit before the publish: the call returns the force alone, the magnitude of the constraint's
    linear part, all of a ball joint, the distance joint's scalar, a hinge's first three, a weld's
    last three and a slider's two, read from the accumulated impulses by the constraint's type, so
    it is newtons for every kind and a weld reads the same in either order; the remarks, the physics
    guide's paragraph and the upgrading entry say force alone; a test welds a box beside an anchor
    in both orders and reads its weight within a percent, beside the hanging rope. A torque read
    would be a call of its own with a defined point, if a game asks. Span's hinges hold forces in
    the bridge's plane, so its numbers stand, read again once the change is in; the siege game's
    breaking rule builds on it. The force-alone fix done at `d03d0e53` on 2026-10-20, as the head
    says; Span's numbers stand.

41. **The contact impulse read is scaled as the joint's force is, so both are the force over the
    step at any substep count.** Found on 2026-10-20 with item 40's fix: `GetPhysicsContactImpulse`
    returns what the solver keeps, the last substep's impulse, and its summary says the impulse
    divided by the step is the force, which holds at the default of one substep and is short by the
    substep count above it, where the joint's force read scales by the substep since `d03d0e53`. No
    game calls it yet, by the coverage reading under item 7, so the value may change: the read takes
    the same scaling as the joint's, its remarks say what it is over the step and at more substeps
    than one, the upgrading guide's entry says the value moved for substep counts above one, and a
    test reads a resting box's contact as its weight within a percent at one substep and at four,
    beside the joint's. One commit, before the siege game, so the package the day's pack run makes
    carries both reads alike. Done at `df03842b` on 2026-10-20, as the head says, the read an
    impulse over the whole step, its name kept.

42. **A convex hull and a static model are shaped from the mesh and the scale alone, the model's
    transform left out.** Found by the siege game on 2026-10-20 at 11:58: `CreatePhysicsConvexHull`
    and `CreatePhysicsStaticModel` bake the model's transform into the shape, and the documented way
    to draw a body sets that transform to the body's each frame, so a hull made from a model after
    it has been drawn is built where the last body drawn with it stood and the new body is placed
    there too; the game's second stone, a hull from the stone model after the first stone had been
    drawn at the castle, stood about 30 m from the cup, and welded to the arm across that distance
    the arm spun at 645, 4,293 and 73,838 radians a second within ten frames with the sling reading
    640 kN and then 21 MN, where the same game on spheres is stable through repeated throws and a
    scratch test swinging the arm three times with welded spheres is clean; the castle's columns and
    roof would meet it on every rebuild, since their models' transforms are set each frame to draw.
    The physics guide's own paragraph on hulls sets a cone's transform to its body's, and every game
    draws bodies so; the boxes example makes its cones before the first frame and never meets it.
    The mend, one commit: both calls shape the body from the mesh's vertices times the scale alone,
    posed for a skinned model as before, with the transform left out, their remarks saying the
    transform is not read since a program sets it to a body's to draw; the upgrading guide gets an
    entry among those that still compile, a model whose transform held a scale or turn of its own
    giving it through the scale or the mesh; tests make a hull and a static model from a model whose
    transform was set to a pose 30 m away and read them standing where they were asked and shaped as
    the mesh. Before the siege game's commit, which then takes hulls for its stones. Done at
    `b4b798d4` on 2026-10-20, as the head says.

43. **A program reads the audio device's sample rate and channels, so a mixed processor knows the
    samples it gets.** Found by Tones on 2026-10-20: the mixed processor's remarks say its samples
    are interleaved in the device's channels, two on most, and the callback is a span of floats
    alone, where the flat API has no call for the device's rate or channels; the SDL backend asks
    for 48,000 Hz and two channels but keeps what the device gives, which the postmix gets, 48,000
    and 2 on the development machine and otherwise on a 44.1 kHz or surround device, so a program
    cannot save what the device plays as a wave of the right speed, and Tones records at the
    stream's stage, where it knows the rate, using the mixed processor for a level alone; raylib's
    own callback is given the frame count on a device fixed at two channels. So the engine gains
    `GetAudioDeviceSampleRate` and `GetAudioDeviceChannels`, read from what the backend kept, zero
    before the device is open, with the mixed processor's remarks naming them and the audio guide's
    paragraph on processors saying how a program saves what the device plays, the cheat sheet line,
    the surface listing and the upgrading guide's added entry, and a test reading the dummy driver's
    format as the backend kept it. One commit. Done at `006ff3ba` on 2026-10-20, as the head says.

44. **Latin text is kerned and its standard ligatures made, in drawing and in measuring alike.**
    From TODO.md's text entry, read on 2026-10-20 at 13:44 as the next with work left, the entries
    before it being descriptions, left out for a reason or answered by the author's own publish: the
    engine shapes emoji and Arabic and orders right to left, and draws the rest a character at a
    time, so a Latin font's ligatures and kerning and Devanagari's shaping are not made. The
    smallest useful step is the kern feature's pair adjustments from the positioning table with the
    kern table as a fallback, and the standard ligatures from the substitution table, for Latin runs
    in fonts loaded from files, since every game draws Latin text and a kerned pair such as AV or To
    shows at once; applied in the run's drawing and in both measuring calls so layout stays true,
    with a way to turn ligatures off where a program needs a character a glyph, as raylib's text has
    none of this; tests on a font with a known kern pair and an fi ligature compared against the
    font's own tables, a reference frame and the text guide's paragraph. One commit unless it grows.
    Devanagari's shaping, the reph, the matras and the conjuncts, is a larger second step left for a
    game that asks. Done at `4789ebde` on 2026-10-20, as the head says. One thing sent back: the CI
    measure of the Unicode ranges example was raised by hand to the machine's 11.4, where a CI
    measure is recorded from the captures job's run; the line goes back to its 9.1 in a commit of
    its own, and the figure is recorded from the next pack run's captures job, which measures
    against raylib and says the share in its summary without failing, so the file says what a run
    measured. The sprite fonts example's stale capture is taken again with it, documents and
    captures alone. The line left the CI measure file at `3b46c543`, to be recorded from the run,
    the sprite fonts capture taken again with it. The captures job of the pack run of `2cc516fc`
    measured it at 10.7 percent on 2026-10-20, the line's figure.

45. **A renamed or reshaped type still reads its old scene files, from BevyCSharp's former names and
    data versions.** TODO.md's next entry with work left, read on 2026-10-20 at 14:19 under Scenes,
    the entries between text and it being descriptions, limits kept on purpose or the method's
    limits under item 39: an older file is read by keeping the fields it has, with no migration, so
    a field renamed or a component split leaves the old file's value behind. SHARED.md's row says
    BevyCSharp has it and this engine is to consider, and its model is read from the other checkout:
    a former-name attribute on a type or a field, repeatable, a name alone meaning the type's own
    namespace and a dotted name a full one, a renamed struct field taking the paths under it along
    and a name another type has since taken going to that type; a data-version attribute on a type
    with a static migration from a version and the file's object, which the generator finds by name
    and its registration calls, a load running it from the file's version, a file recording its
    version beside the fields once past 1, and a versioned type with no migration a generator
    warning; and a file from a later version read as far as the fields match, not migrated, and
    saved back at its later version with the values this build did not read. The port: the registry
    answering by each former name; the generated reader trying the former names where a field's own
    is missing; the generated codec writing the version past 1 and calling the migration on load,
    with a diagnostic for a versioned type with no migration and its code fix beside the generator's
    others; tests of a component renamed, a field renamed, a split migrated across two versions and
    a file from a later version kept; the scene guide's paragraph, the cheat sheet where it lists
    attributes, the surface listing and the upgrading guide's added entry; and a Shared line under
    Replies, the row then saying both have it. Two commits, the renames and then the versions. The
    renames done at `2cc516fc` on 2026-10-20, as the head says; the versions next. The versions done
    at `00c820ce` and TODO.md's section at `120b5ee9` on 2026-10-20, as the head says; the item is
    done.

46. **A run that shows no window makes no sound, its audio going to a device of silence while every
    sound runs.** SHARED.md's row, marked to take with SDL's dummy driver as the fallback already,
    taken on 2026-10-20 as the next with TODO.md through: an offscreen or hidden run opens the audio
    on SDL's dummy driver by choice and not by fallback, so a runner or a test machine with no
    device or a device another process holds plays every sound, stream and music through the same
    code at the same rate with nothing heard, the mixed processor fed as on a real device; the
    choice said in the audio guide and in E3D.md beside the offscreen flag, with a test that a
    hidden run reports the device ready, the dummy rate and channels, and a sound's playing state
    moving as on a device. Read first how the backend chooses today, since the games' drives on the
    runners already run with sound, and where the dummy driver is already the choice the item is a
    sentence in the guide and the ledger's row, not a commit. One commit at most. Done at `748779fc`
    on 2026-10-20, the choice already made and the line and the test added, as the head says.

47. **The cheat sheet is written by a tool from each call's documentation, so the two cannot
    differ.** SHARED.md's row, where BevyCSharp writes its sheet from the built library through
    `build/cheatsheet` and this engine writes its sheet by hand and checks it by name, taken on
    2026-10-20: a script under `build/` reads the flat API's XML documentation, the summary of each
    public static, and writes CHEATSHEET.md in its present shape, section by section as the surface
    listing orders them, with the hand-written parts kept where the sheet carries prose no summary
    holds, marked so the script keeps them; the cheat sheet test then checks the file against the
    script's output and fails where a hand edit or a new call stands unwritten, as BevyCSharp's
    does; BUILDING.md says how it is written again. Read BevyCSharp's script for the model and never
    edit it. One commit, the sheet rewritten by the script in it with its differences from the
    hand-kept text read and said. Done at `09a50fea` on 2026-10-20, as the head says. One thing
    back: a sheet is read at a glance, and 308 of its lines run past 160 characters where 37 did, so
    the writer wraps a comment past the sheet's width of 120 onto continuation comment lines at the
    same column, the summaries untouched, the test reading the wrapped form; one small commit, with
    the sheet rewritten. The parameter references that repeat their noun are the summaries' own and
    stay. The wrap done at `b371b65c` on 2026-10-20, as the head says.

48. **The suite runs once with the schedule's order shuffled by a seed, so a test that leaned on an
    order nothing asked for is found.** SHARED.md's row, where BevyCSharp has it since `379f59a4`, a
    seed in the environment shuffling every schedule of the app and its render world, said in
    BUILDING.md, and this engine is to consider, taken on 2026-10-20 as the next with TODO.md
    through: the engine's schedule, where it orders systems of one stage by no asked order, takes a
    seed from the environment and shuffles those systems' order once at the app's start, the seed
    logged, so a test or a game that leaned on an order the stage did not promise fails under it;
    the suite's script runs the whole suite once more with a seed where a flag asks, and the
    workflow's Linux suite runs that pass with the day's seed, its failures naming the seed to run
    again; BUILDING.md says it, with the flag and the seed. BevyCSharp's `379f59a4` is the model,
    read and never edited. Read first how the stage orders systems today and whether an asked order,
    before or after another, is kept under the shuffle, which it must be. One commit. Done at
    `6a289059` on 2026-10-20, as the head says, the ledger's row saying both engines have it.

49. **An observer is told when a component is added to or removed from an entity, or when an event
    of the game's own is sent to it.** SHARED.md's row, where BevyCSharp has Bevy's observers and
    this engine is to consider, taken on 2026-10-20 as the next with TODO.md through: today a game
    that wants to act when a component arrives or leaves polls for it each frame or keeps its own
    list, as the siege and ragdoll games did with their own bookkeeping. The engine gains a
    registration of an observer on a component type for its adding and its removal, called with the
    entity and the component at the stage where the change is applied, and an event type of the
    game's own sent to an entity, with observers of that event type called in the order registered
    and able to stop it, modeled on BevyCSharp's as it reads Bevy's, read and never edited; the
    behaviors guide's paragraph, the cheat sheet through its script, the surface listing and the
    upgrading guide's added entry; tests of an observer told once on adding, once on removal, of an
    entity's despawn removing every component, of a game's event sent and stopped, and of the cost
    when no observer is registered, which must be none. Read first how the ECS applies an add or a
    removal today and where a hook fits, and say the form and its cost before the commit. One
    commit, two if the events stand apart. The form said by 16:49 on 2026-10-20 and taken: on the
    world, an observer of every event of a type or of one entity's, returning a handle whose
    disposal stops it, and a trigger that does nothing when the world has no observers; the event's
    carrier holding the event by reference so a change reaches the observers after it, the entity it
    has reached or none, the world and a propagation switch; an entity event interface and a
    propagating one; adding and removal events carrying the entity and the value, the first firing
    when an entity gets a component it lacked through add, update, commands as applied or a batch
    spawn and not on a replacement, the second once the component has gone through remove or despawn
    with its value, a despawn taking the handle before the id is freed and running the observers
    after so they read the value and not a live entity, the entity's own observers then let go; a
    trigger running the type's observers then the entity's in the order added, a propagating event
    repeating at each parent until stopped or the root, the stop ending the climb and not the
    entity's remaining observers; observers running inside the call on its thread with an exception
    logged and going no further; a store's own add and remove through the generated path and the
    move of a recompiled script's components not observed. The hook sits in the world's add, update,
    remove, despawn and bulk spawns and not in the store, since a store holding the observers would
    hold the world and the static store cache would keep an undisposed world alive, each store
    keeping two counts the event types raise and lower, the registry made with the first observer
    and a test checking an undisposed world with observers is still collected. The cost over 200,000
    entities in nanoseconds a call: no observer at all the same as before within noise, 20, 19 and
    36 against 25, 16.5 and 34 for add, remove and despawn, with no allocation over a thousand of
    each; observers of other components only the same for add and remove and 13 more for a despawn;
    an observer of the component changed 200, 177 and 322, boxing the event, the carrier, a copy of
    the list and the call. One commit, the component events and the game's sharing the registry. The
    cheat sheet gets a hand sentence in its ECS prose naming the observers, since its lists hold the
    flat API alone. Done at `ec39331c` on 2026-10-20, as the head says.

50. **A game observes what a pointer does to an entity as events on that entity, climbing its
    parents until stopped.** SHARED.md's row, where BevyCSharp has Bevy's pointer events in their
    seventeen kinds and this engine is to consider, where a raylib program asks the ray collision
    calls itself, taken on 2026-10-20 as the next after item 49, whose propagating events are the
    carrier: a program that opts in, by a picking setting on a camera, has the engine cast the
    pointer's ray each frame against the models and shapes it draws with their boxes or meshes, as
    the editor's picking does, and trigger on the entity hit the events of the pointer's moving over
    it, leaving it, pressing, releasing, clicking and dragging with the button, the pointer's place
    and the hit's point and normal, each propagating to the entity's parents until stopped; nothing
    is cast where no camera opts in, so a program that asks the ray collision calls itself pays
    nothing. Read first how the editor picks today and what the drawn entities keep that a cast can
    read, and say the form, the kinds and the cost before the commit; the behaviors guide's
    paragraph after the observers, the cheat sheet's hand sentence, the surface listing and the
    upgrading guide's entry; tests of each kind on a box under a camera, of the climb to a parent
    and its stop, and of the cost with no camera opting in, which must be none. One commit. The form
    said by 17:11 on 2026-10-20 and taken: this engine has no editor, so the model is BevyCSharp's
    pointer event with its kinds and its camera's picking setting; the opt-in is a picking flag on
    the camera, off by default, read on the camera that draws the window alone, the first without a
    target, a render texture's camera not picking since the pointer is not over its texture; a
    pointer event carrying the entity, the pointer's place and the kind, propagating up the parents
    until stopped so a model's root hears a click on any of its meshes, the kind a record struct
    with Bevy's name, over, out, move with its delta, press, release and click with the button, drag
    start with the hit as the button went down, drag with its distance and delta and drag end with
    its distance, a hit carrying the world point, a normal facing the camera of length one and the
    distance in world units, eight of Bevy's seventeen left out, enter, leave, the drag enter, over,
    leave and drop, scroll and cancel, the mouse the only pointer; a resource the ECS plugin inserts
    with a main-thread system before the game's update, since an observer may change anything,
    casting the ray from the camera through the mouse's place against the window's size as the
    renderer sizes the camera, each mesh entity tested by the sphere around its positions kept in a
    weak table keyed by the array, scaled by the matrix's largest scale, one the ray reaches tested
    by its box in its own space and then its triangles with the world distance kept, the nearest
    triangle winning, a skinned mesh met unposed, the global transform of the frame before, which is
    what the player sees; Bevy's rules for the order, out to the old entity before over to the new,
    move over the same entity, press to the entity under the pointer, drag start and drag to the
    entity pressed each frame the pointer moves with the button held wherever it is, release to the
    entity under the pointer, click where that is the entity pressed and drag end to the entity
    pressed after a drag; the pointer over an ImGui window picking nothing. The cost with no camera
    asking 0.3 microseconds a frame reading the camera store alone with no allocation, a flat
    program with no camera returning at the count, and with a camera asking 45 microseconds for
    1,000 cube entities and 400 for 10,000, about 40 ns an entity, the last-array shortcut having
    taken 10,000 from 483; the flat ray calls untouched. Four tests on a cube under a camera: the
    kinds in order with a click, a right-button drag through out and a drag end with no release or
    click off the box, the climb to the parent and its stop, and no camera asking casting and
    allocating nothing until the flag is set; a live hidden run told the events on the spinning cube
    and the triangle with no error. The types one to a file under the rendering area's picking
    folder, needing no AGENTS.md row. Done at `e510f268` on 2026-10-20, as the head says.

51. **An animation state machine as a data asset, states each a clip and how it plays, transitions
    tried in order on a parameter.** SHARED.md's row, where BevyCSharp has it since `667ca9ac` with
    a scene naming the machine, and this engine is to consider, its models playing one clip through
    the update call and a game writing the machine itself, as Tumble did with its idle, walk and run
    by speed and its blends, taken on 2026-10-20 after item 50: a data asset of states, each naming
    a clip, whether it loops and its speed, and of transitions, each from a state or any to a state,
    tried in order on a condition, a parameter's value against a bound, a flag, a trigger used up as
    taken or the clip's end, with a fade time; a component holding the machine's parameters and its
    current state, run on a model by a system through the blending call the engine has so the fade
    and the clip's end are unchanged, the parameters set by the game's code or a behavior; the asset
    loaded from a file in the scenes' format and saved by the same, a scene naming it on an entity;
    Tumble's machine read against it and left as it is, since a game writes its own where it likes,
    with a sentence in the game's comment. Read first BevyCSharp's `667ca9ac` for the model, never
    edited, and how this engine's blend and layer calls compose, and say the form and cost before
    the commit; the animation guide's section with a block, the cheat sheet's hand sentence, the
    surface listing and the upgrading guide's entry; tests of a transition on a value, a flag and a
    trigger used up, the clip's end, the fade, a file saved and read back, and the cost of a machine
    with no transition due, which must be a read of its parameters alone. One commit. The form said
    by 17:37 on 2026-10-20 and taken: the data under the assets' animation folder one type a file, a
    machine with a start, its states each a name, a clip, whether it loops and its speed, and its
    transitions each from a state or any to a state on a condition, finished, a flag's truth or
    falsehood, a value above or below a bound or a trigger, with a fade; a file found as models are
    found, saved beside the program for a relative name, in the scenes' format with a format name
    and a version and the condition by its name, written without reflection, a bad read a data
    exception and a missing file a file exception; a scene component holding the file's path or a
    machine made in code, which the codec leaves out, the current state the machine sets, a
    parameters holder shared by copies, and calls to set a value or a flag, fire a trigger and read
    a parameter, a scene holding the path and the state so a level saved mid-run restarts in its
    state and the parameters set again by the game; the runner internal to the models' rendering,
    stepped before the draws' play for each entity with both components and only when the world
    counts any machine, a machine compiled once by its path or through a weak table for one made in
    code into each state's transitions in the order tried, entering its start or the saved state
    once the model has clips, entering a state setting the model's clip, speed, time and blend so
    the draws' blend on a change of clip is the fade, a looping state finished once its time comes
    round, a state played once held at its last frame with the speed at zero in the frame the time
    would pass it and finished the frame after so the held pose is drawn once, a trigger used up
    when its transition is taken, and a machine whose names do not hold together or whose file
    cannot be read logging an error once and running nothing. The cost of a step with no transition
    due 82.5 ns with three transitions and no allocation, one closure's 88 bytes a step taken out on
    the way, and a world with no machine paying one count read a frame. Eight tests on the arm
    model's two clips, a value above and below with the fade read in the blend and a fade of zero
    cutting, a flag each way, a trigger used up, a play-once clip from any state holding its last
    frame then finishing, a loop finishing as it comes round, a file saved and read back identical
    and run from its start with a scene holding the path and state, a bad machine logging once and
    the cost; a live hidden run on the robot went from idle to walk by speed, to a jump by trigger
    with its blend and back to idle and walk after the clip's end with no error. The animation
    guide's section, the cheat sheet's hand sentence, the upgrading guide's entry and 42 lines of
    the surface listing follow, and Tumble's robot keeps its own choice with a remark saying why,
    its walk and run blended by speed, its aim a layer and its rise from the ragdoll's pose, none of
    which a machine of whole clips holds. Done at `414e6f77` on 2026-10-20, as the head says.

52. **A message after a load, so a game builds once what a file does not hold.** SHARED.md's row,
    where BevyCSharp has it since `3ab5b22` and this engine is to consider with saves, taken on
    2026-10-20 after item 51 and on item 49's events: a scene file holds components and no meshes
    built from data, physics bodies or sounds, so a game builds those after a load, and today it
    polls or keeps a flag; the engine triggers an event on each entity a scene file spawned once the
    whole file is in, so an observer of that event on a component builds what the file does not hold
    for its entity with every other entity of the file already present, and a world-level event
    after the file naming it, for what a game builds once for the whole; the same after a saved game
    is read, since a save is a scene; the scene guide's paragraph after the loading section with a
    block, the cheat sheet's hand sentence, the surface listing and the upgrading guide's entry;
    tests of the entity event reaching every spawned entity after the last is in, of the file's
    event once, of a save read, and of the cost with no observer, which must be none. Read first
    BevyCSharp's `3ab5b22` for the model, never edited, and where the scene's spawn ends today, and
    say the form before the commit. One commit. The form said by 17:49 on 2026-10-20 and taken, the
    typed one: every load ends in the scene file's read, which spawns every entity first and then
    sets parents and reads components entity by entity, so the adding event fires mid-file; a typed
    loaded event carrying the entity and the component is triggered at the end of the read for each
    component of each entity the file spawned, in file order, once the whole file is in, counted per
    store as the adding and removal events are so nothing is made where no one watches, and a
    scene-loaded world event with the path and the entities is triggered once after them, the path
    empty for a read of text; a save read is a scene read, so the same events fire with the save's
    path; in the scene load the events come before the engine's own bodies are built from the
    colliders and bodies the file holds, which the documents say. The untyped alternative, one event
    an entity filtered by component, was not taken, since the typed one hands the observer the
    component, reaches only entities with it and costs nothing for components no one watches. The
    cost with no observer one read of a world counter and one registry check, no list recorded and
    nothing triggered, a test showing a read allocating the same with an unrelated observer as with
    none. The tests and numbers said by 17:55 and taken: the telling returns at once unless the
    world watches loads, a count of loaded observers kept beside each store's count or any observer
    of the scene event, and otherwise takes the codecs whose component is watched and tells entity
    by entity in the file's order, each entity's components in the registry's order, then the scene
    event; the read tells at its end, the load passes its path, and a scene reference reads without
    telling and tells after it has placed the copy under its entity and given back the shared
    meshes. Three tests: a file of three entities, a child among them and a camera, each loaded
    transform seeing all three named with the parent set and the camera in, in the file's order,
    then the scene event naming all; a world saved and read into another telling the hero and the
    save's path; and reads with no observer and with an observer of an unrelated event allocating
    the same bytes, a loaded observer then told three times. The numbers: the telling with no
    observer a count read at the stopwatch's resolution, and a file of a thousand transforms read in
    3,197 microseconds with no observer and 3,604 with one, about 0.4 microseconds an event told; a
    live reload of the level told both lights, the three model references and the scene with no
    error. The scene guide's paragraph after saving and loading with a block, the cheat sheet's
    sentence, the upgrading guide's entry and 23 lines of the surface listing follow. Done at
    `43ace92a` on 2026-10-20, as the head says.

53. **A saved game laid over the scenes it started from, and a setting read over its default.**
    SHARED.md's row, where BevyCSharp has a save game and a persistent setting, a file read over its
    default since `0f0f461` so a field a later version adds keeps its default, and this engine is to
    consider, taken on 2026-10-20 after item 52: today a save is a whole scene file written again,
    so a saved game carries every entity of the level it started from, and a game that keeps a few
    values across runs, a best lap, a score or a setting, writes its own file as the games do,
    Rally, Pinball and Tumble each with their own. The engine gains a saved game that holds what
    changed since the scenes it started from, the entities added, removed and changed by component
    against the loaded files, read by loading those scenes again and laying the save over them, so a
    level's own edits after the save still show where the save did not touch them, with the save
    naming its scenes and the loaded event firing once over the whole as item 52 has it; and a
    persistent value of a type the scene codec can write, read from a file beside the program over
    its default so a field a later version adds keeps its default and written back on change or on
    demand, for a game's settings and records. Read first BevyCSharp's save game and persistent
    setting for the model, never edited, and how this engine's scene file tells an entity from one
    run to the next, which a save over scenes needs; say the form before the commit; the scene
    guide's section with a block, the cheat sheet's hand sentence, the surface listing and the
    upgrading guide's entry; tests of a save over a scene with an entity added, one removed and one
    changed, of a level edited after the save, of a persistent value read over its default with a
    field added, and of the cost of a save against a whole scene written. Two commits, the save over
    scenes and then the persistent value, and a later item moving a game's records to it. The first
    commit's form said by 18:15 on 2026-10-20 and taken: an entity is told from one run to the next
    by the scene file's id, given at first save and read back, a scene reference's copy dropping it,
    so the key is the scene's index and the id; a static save game with a format name and version, a
    start naming the scenes, a save to a path returning the entities written and throwing with no
    game started, a load, and the scenes of the game, the game in progress a private resource of the
    world holding the scenes, each scene entity's handle by scene and id and each one's components
    as loaded, written compactly by the codecs so they compare as text; a marker component on an
    entity spawned in play to be written whole, keeping the game's own camera and interface from
    being spawned twice; the file holding the scenes and the entries, a changed component written
    whole, a taken-off one as null, a removed entity as such and a marked one whole with its name
    and parent; the comparison per component and not per field, so a changed component comes back
    with every field as play left it; the save written beside the old file and moved over it; the
    load refusing a file that is no save before anything is despawned, ending the game in progress,
    loading the scenes with item 52's telling held back, spawning the saved entities first so
    references resolve, laying the entries and then telling once over the whole with the save's
    path; entities placed by a scene or model reference left out as the scene's write leaves them.
    Six tests, an entity added, one removed and one changed with a camera taken off, the file
    holding only those, a load into the game it was made in ending that game first with no doubles,
    a level edited after the save showing its edits where the save did not touch them, the telling
    once over the whole, the cost of one change in 200 entities under a twentieth of the whole
    scene, and a save with no game throwing and a file that is no save refused with the level
    standing. The numbers: 200 entities with one change saved in 663 microseconds and 291 bytes
    against the whole scene's 1,105 and 43,339, loaded in 1,208; 2,000 entities saved in 6,281 and
    291 bytes against 11,244 and 438,739, loaded in 11,835, the save's time growing with the
    entities since each component is written to compare; a live run on the level saved two entries
    in 456 bytes and loaded back right. Item 52's allocation test takes the least of five reads a
    condition, since a test beside it registers codecs from an assembly it compiled and the
    registry's next lookup rebuilt its index in the middle of a measured read, the stated cost
    unchanged. The scene guide's section with a block, the cheat sheet's sentence, ARCHITECTURE.md's
    sentence, the upgrading guide's entry and eight lines of the surface listing follow. The save
    over scenes done at `a150c4c1` on 2026-10-20. The persistent value's form said by 18:27 and
    taken: a sealed class over a type made from a file name, a fallback and whether to save on
    change, a relative name beside the program, throwing where the type has no scene codec, which it
    finds among the registered codecs, with the path, the value, the problem, a changed flag and
    set, update, save, revert and reset as BevyCSharp's; the file the object of the type's fields by
    name as a scene file holds one component, written and read by the type's own codec through two
    internal calls that take a value apart from any entity against a detached world, so no
    reflection and the type a scene writes is the type it keeps; the read laying the file's object
    over the fallback written by the codec field by field and reading the result back, so a field
    the file lacks keeps the fallback's value and not the codec's own start, and a data version in
    the file bringing an older one up as a scene's would; a missing file giving the fallback, one
    that cannot be read giving it with the reason in the problem and a warning, left unwritten until
    a save; a save written beside the file and moved over it, and a set saving where asked. Four
    tests on the transform, an earlier file holding the position alone read over a fallback scaled 2
    keeping the fallback's scale, nothing written until asked and then on demand and on change with
    revert and reset, a broken file giving the fallback with its problem and the file left as it
    was, and a vector refused for want of a codec; a live run wrote a field of view and read it over
    a camera's fallback. The scene guide's section with a settings block, the cheat sheet's
    sentence, the upgrading guide's entry and eleven lines of the surface listing follow. The
    persistent value done at `8e31620c` on 2026-10-20; the item is done, the ledger's row saying
    both engines have it.

54. **An order among the systems of one stage, one before or after another or a chain, kept under
    the shuffle.** SHARED.md's row, where this engine is to consider and a system runs after every
    earlier one it conflicts with, taken on 2026-10-20 after item 53: a stage's systems run in the
    order added, which ARCHITECTURE.md says, so a plugin's system and a game's that must follow it
    lean on the order of their adding, which no one asked for and item 48's shuffle moves since
    `6a289059`; the engine gains an asked order, a system added before or after another by name or a
    chain of names, kept by the batches as a conflict's order is kept and by the shuffle, a cycle
    refused with the names at the app's start; the behaviors guide's paragraph on stages, the cheat
    sheet's hand sentence, ARCHITECTURE.md's schedule sentence, the surface listing and the
    upgrading guide's entry; tests of a before, an after and a chain holding under forty seeds
    beside item 48's, a cycle refused by name, and the cost of a stage with no asked order, which
    must be none. Read first how a system is named and found in the schedule today and BevyCSharp's
    form for the model, never edited, and say the form before the commit. One commit. The form said
    by 19:11 on 2026-10-20 and taken, as BevyCSharp's: a system is named by its descriptor, the name
    given or the delegate's type and method, a generated behavior's by its name, stage and method,
    and nothing finds one by name today but the removal by source; the descriptor gains after and
    before by name, fluent, with the lists read back, and the app a chain over a stage ordering each
    after the one before by name, so systems sharing a name are ordered together; an internal
    ordering of the schedule runs at the app's startup before the shuffle, leaving a stage where no
    system asks as it is after one look over its list, otherwise resolving every name among the
    stage's systems, after or before every system of the name and never itself, sorting stably by
    the earliest added of those whose predecessors are all placed so the add order holds wherever
    nothing was asked, each system keeping a predecessors set, null where no order touches it; a
    name the stage lacks refuses the start naming the system, the stage and the name, and a cycle
    refuses it naming the round; the batches treat a predecessor in an earlier batch as a conflict
    with an asked-order note and the shuffle keeps it; a system added after the start runs where it
    is added, said in the documents, and a plain system orders against a behavior's by its name. The
    cost with no order asked one look at each system at start and one null read a pair in the
    batches beside the conflict check, nothing allocated and the list untouched, held by a test.
    Four tests, an after, a before and a backward chain holding in the sorted list, through the
    parallel batches and under forty seeds, a chain run after startup in its order, the round and
    the unknown name refused with their messages, and a stage with no order left as it was. The
    behaviors guide's paragraph and block, the cheat sheet's sentence, ARCHITECTURE.md's schedule
    paragraph in place of its sentence that there is no ordering, BUILDING.md's shuffle paragraph,
    the upgrading guide's entry and five lines of the surface listing follow. Done at `02d89f18` on
    2026-10-20, as the head says, the ledger's row saying both engines have it.

55. **A character flies at its move with gravity held off, what it meets still stopping it, and a
    dynamic body is put at a point at rest by one call.** SHARED.md's row, where BevyCSharp has a
    fly setting and a place call since `aa5c9e0` and this engine is to consider for the flight,
    having a set-position call for the body that keeps its velocity, taken on 2026-10-20 after item
    54: a character controller gains a flight the game turns on and off, under which gravity is held
    off and its move carries it along its facing's up as well, what it meets still stopping it as on
    the ground, so a creative mode or a spectator flies through a world without falling and without
    passing through walls; and the physics API gains a call placing a dynamic body at a point and
    orientation at rest, its velocities zeroed and its sleep state woken, beside the set-position
    call that keeps the velocity, with the difference said in both remarks. The physics guide's
    paragraphs on characters and bodies, the cheat sheet through its script, the surface listing and
    the upgrading guide's entry; tests of a flying character holding its height with no ground under
    it, stopped by a wall and a ceiling, landing when the flight ends, and of a body placed at rest
    staying there where set-position would have carried its velocity on. One commit. Read first how
    the character controller moves today and BevyCSharp's form, never edited, and say the form
    before the commit. The form said by 19:39 on 2026-10-20 and taken: the character controller
    gains a fly flag copied to the body by the drive through an internal call, the character keeping
    the whole velocity asked for with the walk taking its horizontal part as before, a flying
    character's planned velocity the one asked for less the step's gravity as BevyCSharp's is,
    hovering when asked for nothing, carried up and down by its vertical velocity with the body
    upright so its up is the world's, and walls, floors and ceilings stopping it through its
    contacts as they stop a walk, no jump or step climb in flight and the grounded state still found
    so a game reads it to land, with a flat call through the controller where the entity has one; a
    public place on the physics world beside the public set-position and a flat place call beside
    the set-position one, setting the rotation and then the origin's position by the body's offset,
    zeroing both velocities, waking the body, updating its bounds and dropping the pose kept for
    blending so it is not blended into, a character keeping its upright rotation and walking on at
    the velocity last asked for and a static body moved and turned, both pairs' remarks saying the
    difference, set-position keeping the velocity so a body carried through a portal goes on and
    place stopping it as a respawn does, the world's set-position gaining the summary its bare
    inheritance lacked. Tests: a flying character over nothing rising 2 units in a second and
    holding within 0.01 not grounded, one flown into a wall stopped at its height, one flown through
    its controller's flag stopped under a ceiling, and both landing grounded when the flight ends;
    two spinning boxes thrown on ice, the placed one at zero velocities holding within 0.01 with its
    turn kept and the set one sliding on, the placed one then sleeping, placed up, awake at once and
    falling; each test failing at its first check with the flight branch or the zeroing taken out.
    The physics guide's sentences and a paragraph with a compiled block, the cheat sheet's three
    lines through its script and a sentence in its prose, four lines of the surface listing and the
    upgrading guide's entry follow. Done at `ac77ef83` on 2026-10-20, as the head says, the ledger's
    row saying both engines have it.

56. **Data in files of its own, referred to by an id that survives a rename.** SHARED.md's row,
    where BevyCSharp has a data asset attribute and a typed data reference, and this engine is to
    consider, taken on 2026-10-20 after item 55: a game's own data, a weapon's numbers, a level's
    rules or an enemy's kind, lives today in a scene component or in code, and a scene names a file
    by its path, so a renamed file breaks every scene that names it; the engine gains a data asset,
    a type marked as one whose scene codec writes it to a file of its own in the scenes' format with
    an id given at first save as the scene file's ids are, and a typed reference a component holds
    by that id, resolved at load from a table of the ids to paths that a rename keeps, with the file
    found as models are found, the reference reading as none where the file is missing with a
    warning naming the id; the animation machine's file of item 51 and the persistent value of item
    53 read against it, the machine's path field kept and the id offered beside it; the scene
    guide's section with a block, the cheat sheet's hand sentence, the surface listing and the
    upgrading guide's entry; tests of a data file saved with an id and read back, a reference
    resolved by id after the file is renamed, a missing file read as none with its warning, and the
    cost of a scene with no reference, which must be none. Read first BevyCSharp's data asset and
    reference for the model, never edited, and how the id table is kept and written there, and say
    the form before the commit. One commit. The form said by 20:16 on 2026-10-20 and taken: the id
    lives inside the data file as an entity's lives inside a scene file, twelve hex characters given
    at the first save and kept by every later one, so no sidecars and no shipped index where
    BevyCSharp keeps both; the table of ids to full paths in memory in a static holder, every data
    file read or written adding its entry, and an id missed adding one by one look through the
    folder beside the program, recursive, and through each folder a data file was read from or
    written to in this run, reading only the head of each JSON file and stopping at the first other
    key, so a file renamed by hand is found again since its id goes with it, a missed id remembered
    with its problem at one look and one warning a run; a data asset attribute on a struct taken by
    the scene component generator beside the scene component's, the same codec, field rules, former
    names and versions, registered with the data assets, a type marked both registered with both,
    the file carrying the format, the version, the id, the type's full name and the fields as a
    scene file holds a component, a later version read as far as known with a warning and a save
    laying the written fields over the file's own; a typed reference as a readonly struct holding
    the id and a shared holder per id and type, with the id, whether none, the value, the path, the
    problem and a try, the value the file's shared by every reference and replaced by a save, the
    type's start value where none, the id kept where the file is missing so a scene saved again
    writes it back; the generator writing a reference field or an array of them by the context's id
    and reading by its data look, which resolves at load and passes the scene's asset server so a
    handle in a data file loads; a missing file or one of another type reading as none with one
    warning naming the id; the static holder's save to a name or to a reference, load by name found
    as models are found and none with a warning never an exception, find by id and the id of a file,
    an unregistered type throwing as the persistent value does; the machine's file a kind the holder
    knows by its own format, gaining a top-level id with its save through the holder and the
    component a reference field beside its path; the persistent value taking a data asset type too,
    its file a bare object with no id, the remarks saying a persistent value is the player's read by
    name and a data asset the game's referred to by id. The tests as listed, and the cost of a scene
    with no reference the same allocation before and after the table is built with the holder's
    lookups at zero. The reviewer asks one sentence more in the look's remark, the count of files
    the look read where it was more than a few hundred, said in the warning, so a large folder's
    cost is seen. The tests and numbers said by 21:24 and taken, with three changes to the form: the
    look covers the folder of the scene or data file that names the id, added only when a reference
    resolves, since a fresh run's table is empty and a level loaded from beside its data could not
    otherwise find a renamed file; the write context gains nothing, the generator writing a
    reference as its id, since a new nullable member had flipped the class's nullable context and
    the surface writer rendered two unchanged members otherwise, the read context keeping its file
    non-nullable for the same reason; and the id-of-file call is dropped, the reference's id giving
    it; a look reading more than 500 files warns with the count and the folders. Five tests: a file
    saved with its twelve-hex id and read back by name after the table forgets it, its fields
    folded, a second save keeping the id and the old reference seeing the new value; a file written
    by hand lacking a field keeping the default's value and given its id, then found by it; a
    machine saved, referred to through the component's file in a saved scene, moved into a subfolder
    and renamed with the table forgetting it, the scene read finding it by id at the new path, and
    the same id asked for as another type reading as none naming the type; a missing file reading as
    none with the id kept, the problem naming it, the default's value, the warning in the log and
    the scene written again holding the id; and the cost, the least allocation of five reads of a
    scene with a null reference the same before and after the table holds files, the asked count not
    moving. The generator's probe takes a data asset, the every-field-kind test the reference and an
    array of them, the bad-file rows a machine file with its id and missing, empty and random files
    reading as none, and the machine's saved file carries its id after the version. Breaking the
    look fails the rename test and counting a null id fails the cost test. The strict build is
    clean, the suite gave 1,822 plain and shuffled, five more, 26 blocks of three guides built on a
    fresh pack, and a check program on that pack saved and loaded in two processes with the data
    file moved and found by id through the level's folder; the test host's folder holds seven JSON
    files, so its look reads seven heads. Done at `b1265b6a` on 2026-10-20, as the head says, the
    ledger's row saying both engines have it.

57. **A feature-test program puts every feature on one map, a capsule character on test courses, an
    admin panel and a console, with a crash log beside the executable.** SHARED.md's row, where
    BevyCSharp has its feature test in parts, the hub and its zones, the gallery, the player, the
    course, the panel and the console, the crash log and the portable build, and this engine is to
    consider, taken on 2026-10-20 after item 56 as the last substantial row: the games each show a
    few areas, and the coverage reading under item 7 found most of the surface shown by none, so a
    program in the repository beside the games, built on the package as they are, puts every feature
    area on one map in zones a capsule character walks between, a zone each for drawing, models and
    animation, lights and shadows, global illumination, physics with a course, audio, text,
    particles, scenes and saves, the interface and the effects, each zone exercising its area's
    calls with a status the console reads; an admin panel in ImGui toggling the features and reading
    the frame's costs; the console as the games' commands through e3d; a crash log written beside
    the executable on an unhandled exception; and a scripted run walking every zone and reading its
    status, in the games job, with the soak and storm lists taking it. Read first BevyCSharp's
    feature test for the model, never edited, and say the parts in order with the first part's shape
    before its commit, the hub and the character with two zones as the first; each part a commit,
    wired in as the games are, with its README and BUILDING.md's paragraph; the portable build for
    testers waits on the owner's word, since the games ship through the pack run already. The parts
    said by 21:28 on 2026-10-20 and taken, BevyCSharp's feature test read for the model: the program
    lives under the games' folder beside them, built on the package by the same project shape as
    Summit, its commands prefixed as the feature's and the player's, the engine's own crash log
    landing beside the executable already and the program's own crash file coming with part 2. Six
    parts, each a commit with its README section, BUILDING.md's paragraph grown and its zones in the
    scripted run: the hub, the capsule character and two zones, drawing and the physics course, with
    the status and teleport commands and the whole wiring; the admin panel in ImGui toggling every
    zone's features and reading the frame's costs, with the crash log and a command that throws to
    prove it; models and animation with a skinned walker on a state machine, morph targets and
    instancing, and lights and shadows of each kind with cascades and probes; global illumination
    with a room at each quality, particles of each kind with collision against the field, and the
    effects; audio with positional sound, music, a stream and a recording read back, and text with
    fonts, shaping, right to left, color emoji and measuring; and scenes and saves with a scene
    round trip, a saved game, a persistent value and a data reference found after a rename, and the
    interface with an ImGui window, 2D HUD widgets, states and screens. Part 1's shape: a ground of
    one static box, a hub plaza with a turning cube and a signpost per zone with its name drawn
    above, roads out to each zone on a ring of 40 m; a capsule character of radius 0.35 and height
    1.8 with a third-person orbit camera, walking relative to the camera, sprinting, jumping,
    crouching by the height, flying by item 55's call and put back at the zone's start by its place
    call, a fall below minus 20 putting it back too; a zone base with name, centre, start, facing
    and spots and its build, update, draw, screen draw and status, the zone found by position; the
    drawing zone every 3D shape call on plinths and an easel showing a render texture 2D shapes and
    text are drawn into each frame, its status reading the texture back at known pixels so the
    easel's ok is a fact; the course zone stations along x each with a trigger at its end, a ramp,
    stairs, a gap, a moving platform, crates and a low tunnel, its status naming each station
    reached; the commands for the status of all zones or one, a teleport to a zone and spot, the
    player's place and state, a put and a fly switch; a HUD of the zone's name and status, the frame
    rate and the keys. The wiring: the drive script's case walking from the hub to the drawing zone
    on its road by held keys with the zone asserted, the easel's ok asserted and each course station
    run from its start by keys; the games job's step building and walking it from the package with a
    capture; the three per-system loops, the soak list with a turn teleporting through the zones and
    the storm list; BUILDING.md's paragraph and the README. It stays out of the README's gallery and
    the table script's games, a test program and not a game to show, as the coder proposed; the
    portable build waits on the owner. Part 1's tests and numbers said by 21:46 and taken: the
    program's files as set out, the drive script's case with its ask passing all its arguments where
    a two-word command had reached the client as one, a soak turn through the hub, the drawing zone,
    the crates and the platform with a status read each, the workflow's loops, soak and storm lists
    and the games job's step with a capture in the drawing zone as an artifact only, and
    BUILDING.md's section before the shipping one. The drive on the development machine's GPU under
    the validation layer passes in 50 seconds, the hub's cube turned with two signposts and roads
    named on the screen, the walk from the hub reaching the drawing zone grounded, the drawing line
    of 23 calls a frame with the easel's ten of ten shapes found at their pixels in six reads, each
    course station run from its spot by keys and reached with four of four crates moved, and no
    validation error; on lavapipe in 1 minute 20 seconds; the negatives hold by hand, the gap
    unjumped, the tunnel walked standing and the platform walked off each stopping short of reached;
    the easel found whole on its first read, so the read-back and the drawing share their
    coordinates. The soak of 120 seconds offscreen gave 42 turns and 13 readings with the check
    passing and nothing growing, the storm captured after it with no validation error, the norm and
    document tests pass and the workflow and scripts parse. Part 1 done at `7d7ecba6` on 2026-10-20,
    as the head says. Part 2's shape, tests and numbers said by 22:00 and taken: an ImGui window on
    a key that the keyboard, a gamepad and the mouse move through, the player deaf to the keys while
    it holds them, showing the frame's costs by their profile names, the frame rate, the engine's
    profile window and the last crash file's name, the engine's switches for the sun's shadows,
    ambient occlusion, FXAA and the frame cap, the player's fly box, and each zone's status, toggles
    and buttons to its start and spots; toggles with a one-word key and a label for the hub's cube,
    the drawing zone's shapes and easel and the course's platform and triggers, each zone's status
    saying when one is off; a crash file written beside the executable on an unhandled exception
    with the exception whole, the time and frames into the run, the player's line, each zone's last
    status, the frame's costs, the system and the log's last forty lines, all from what the last
    frame published and never from the engine mid-frame, the next run naming the newest file in its
    log and on the panel, the engine's own crash log still written beside it; commands for the
    toggles, a toggle, the panel, the costs and a crash from the main loop after the next frame. The
    drive's case after the course turns the easel and the cube off and on in the statuses, reads the
    shadows' GPU cost before and 240 frames after the sun's shadows go off, under a tenth of on or
    zero where the device times nothing, and opens the panel and reads it as drawn; the games job's
    step captures the drawing zone with the panel open, asks for the crash, waits for the file and
    reads its lines, waits for the session's end at exit 134 since the file is written while the
    process still aborts, and reopens to read the log naming the file; the soak shows the panel
    every fourth turn. The whole step run locally from the workflow's text on lavapipe passes in 1
    minute 45 seconds, the shadows' cost 7.636 ms on and 0.194 off and on the machine's GPU 0.058
    and 0.000, the crash file saying 5.4 seconds and 94 frames into the run with the exception's
    line; the soak's 39 turns and 13 readings pass the check with nothing growing, the storm
    captures with no validation error, the workflow and scripts parse, and the README and
    BUILDING.md gain the paragraphs. Part 2 done at `8609089a` on 2026-10-20, as the head says. Part
    3's shape, tests and numbers said by 22:15 and taken: the models zone with every generated mesh
    and the heightmap and cubic map from generated images on plinths, the OBJ torus with its
    material and texture, the arm posed at a time, the morph model twice by clip and by weight, 400
    cubes instanced in one draw and Tumble's robot as an entity with an animated model and a
    machine, the machine of idle, walk and run on speed written at each start through the data
    assets keeping its id and named through the component's file reference so items 51 and 56 run in
    it, the models linked from the examples' and Tumble's resources and not copied; its status
    drawing the arm alone into a 96 by 96 texture every half second and reading it back, the arm
    bending when its outline's count ranges by a tenth of its most, the machine running once its
    state has been each of the three, then the meshes' vertices and the instanced count, with
    toggles for playing and instancing. The lights zone with a grey floor, a red point lamp casting
    shadows round a pillar, a blue spot turning and casting shadows, a block the sun shadows, an
    emissive sphere, six balls from rough to polished with every other one metal and a three-walled
    room with a reflection probe and a polished ball; its status drawing the zone from 22 m over it
    into a 128 by 128 texture each second while the player is in it, since the sun's cascades follow
    the window's camera, finding each patch by the world-to-screen call and averaging nine pixels,
    the lamp's patch red against the open floor, the spot's blue, the block's shadow more than 45
    darker than the floor beside it and the sphere's top glowing, each failing part named and the
    probe's readiness said, with toggles for the lamps and the shadows. The drive's case runs the
    models zone for 600 frames asserting the arm bends and the machine runs, the lights zone for 200
    asserting lights ok, then the lamps off asserting no red and no blue and on again; the shadow
    measure turns the lamps' shadows off with the sun's, since with the sun's alone off the lamps
    kept 9.3 of 10.5 ms; the soak cycles six places. The whole step on lavapipe under the layer
    passes in 2 minutes 56 seconds, the arm's outline 594 to 764 pixels, the machine through its
    three states, the lamp's patch 216, 154, 148 against the floor's 195, 189, 181, the spot's 167,
    175, 242, the shadow 49, 54, 67 and the sphere 255, 200, 85, the same on the GPU, the lamps off
    reading no red and no blue, the shadows 10.123 ms on and 0.181 off; the soak's 40 turns and 13
    readings pass with nothing growing, the storm has no validation error, and the README's map and
    BUILDING.md cover both zones. Part 3 done at `086d9a00` on 2026-10-20, as the head says. Part
    4's shape, tests and numbers said by 22:49 and taken: an illumination zone, a white room open
    toward the hub with a red wall and a green one each by its own material, a lintel, a white lamp
    toward the green wall, an emissive panel and a pillar, global illumination set to Low only while
    the player is in it from the 121st frame after arriving and off with the field cleared on
    leaving since it costs every frame, the window read at the 120th frame off and the 420th on at
    four places along the floor by each wall, the light bouncing when the red wall's side grows
    redder by its red over its green and blue than the green wall's side does by more than 0.03,
    since turned on it takes the sky's blue out of both sides alike and a one-sided read was too
    thin at 480 by 270, measured 0.062 on lavapipe and 0.059 on the GPU and 0.085 at 1280 by 720; a
    particles zone of six emitters over a slab, the default fountain, an additive fire, alpha smoke,
    sparks bouncing, lit particles and a burst, the particles alone drawn every half second into a
    160 by 90 texture through an empty 3D mode and read back with each emitter's stretch counted, ok
    when each is above 5, measured 38, 53, 158, 31, 37 and 107; an effects zone with a grey board, a
    red patch and an emissive bar, a knot through the toon shader and a billboard of the plasma
    texture, the four shaders linked from the examples' resources, each of none, bloom, vignette,
    grading at zero saturation, exposure at 2, depth of field, motion blur and the AgX curve shown
    alone for a second on arriving with the window read at each phase's end, ok when bloom lifts the
    glow by more than 15 summed levels, the vignette darkens the edge by 15, the red is graded to
    within 10 of grey and the exposure lifts the board by 30, each failure named, measured 133
    against 117, 33 against 64, the red to 27 and the board 32 to 44; the shaders checked by the
    life shader stepping a grid read back as a blinker of three cells turning, the plasma shader's
    colors counted and the grayscale shader drawing red into a target read grey, measured 161 turns,
    51 colors and 94; the screen read kept only from a first call that a shared keep makes on
    arriving at a zone that reads the window, so a run that does not visit them pays for no copy.
    The drive asserts the bounce after 440 frames, the particles after 150 and the effects with the
    shaders after 500; the soak cycles nine places. The whole step on lavapipe under the layer
    passes in 5 minutes 2 seconds against the drive's eight and the GPU in 1 minute 43 with the same
    readings; the soak's 36 turns and 13 readings pass with nothing growing, the storm has no
    validation error, and the README's map and BUILDING.md cover the three zones. Part 4 done at
    `c6face9f` on 2026-10-20, as the head says. Part 5's shape, tests and numbers said by 23:05 and
    taken: an audio zone where the drone plays alone for two seconds on arriving as a pannable
    looping sound, panned by the program toward the speaker each frame and its volume set by
    distance, a mixed processor summing each channel's energy from the half second on and the side
    heard when the left is above 1.2 times the right, then the music, a sine from a stream callback
    and a coin every two seconds panned by the player's side, everything stopping on leaving, and at
    start a half second of a tone written as a wave beside the program and read back with its frames
    counted and its peak measured, ok when the wave comes back as 24,000 frames peaking at 0.5, the
    music played past a second, the stream asked for samples and the side heard; a text zone on the
    outer ring drawing a page every second into a texture on a billboard, Lato baked with the asked
    code points, Hebrew in the tests' right-to-left font, a color emoji in the tests' layered font,
    the default font, a word with and without ligatures and an SDF Lato turned, the two test fonts
    linked from the tests as the models are, ok when the line's lit columns stand within six pixels
    of the measure, the Hebrew letters' bars rise left to right so the first is drawn at the right,
    the emoji's red and blue pixels count past ten with the red's mean left of the blue's, and the
    default font's line is lit. The drive runs the audio zone for 300 frames and the text zone for
    130; the soak cycles eleven places. The whole step on lavapipe under the layer passes in 5
    minutes 17 seconds, the device at 48,000 Hz in two channels on the dummy driver, the wave 24,000
    frames at 0.500, the music 2.9 s, the stream asked 82 times, the drone panned minus 0.46 with
    the mix 4.11 left and 2.48 right, Lato measured 299 and drawn 298, the word 56 with ligatures
    and 57 without; on the GPU the drone 2.88 and 1.74; the soak's 35 turns and 13 readings pass
    with nothing growing and the storm has no validation error. A finding of the engine's is item
    58. Part 5 done at `afbe81cf` on 2026-10-20, as the head says. Done at `c2b8c0e9` on 2026-10-21
    with its sixth part, the six parts at `7d7ecba6`, `8609089a`, `086d9a00`, `c6face9f`, `afbe81cf`
    and `c2b8c0e9`, as the heads said; the portable build waits on the owner.

58. **A sound placed by its position is heard from its side and fainter with distance, as the audio
    guide says.** Found by the feature test's audio zone on 2026-10-20: the audio server pans and
    attenuates a spatial voice only through a spatial processor, and nothing installs one, the
    setter having no caller and the interface no implementation in the engine, the backend's comment
    naming Steam Audio for the slot, which is not shipped, so a sound played with a position,
    through the behavior context's spatial play or the server's play with a position, plays from the
    middle at full volume, where the guide's section on sound in a 3D world says such a sound is
    louder near the listener and heard from its side; measured in the zone, the drone with a
    position to the camera's left gave the mix 14.28 left and 14.56 right, and alone at a pan of
    minus one 2.76 and 0.00. The mend is one thing: the engine installs a spatial processor of its
    own by default, the pan from the listener's across vector against the direction to the voice and
    the volume falling from a near distance to a far one the voice or the server sets, with the slot
    kept for a richer one and the setter replacing the default; the guide's sentence then true and
    saying the model, the cheat sheet through its script where a call is added, the surface listing
    and the upgrading guide's entry; a test playing a sound to the listener's left and reading the
    mix's sides through a mixed processor as the zone does, and one at twice the far distance
    reading near silence; the feature test's audio zone then plays its drone through the spatial
    play and keeps its check. One commit, after part 5. The form said by 23:14 on 2026-10-20 and
    taken: an internal distance processor in the platform's audio folder, the pan the unit direction
    from the listener to the voice against the listener's across vector, clamped, minus one at the
    left and zero straight ahead, behind or at the listener, the volume one within the near distance
    and zero at or past the far, between them the inverse of the distance brought to zero at the
    far, and where the voice has an orientation a directivity from the context's dipole, the
    attenuation the distance's times the directivity; the audio server starting with it, the setter
    kept internal and replacing it with null taking it away as its summary says, the server gaining
    near and far distances with defaults of 1 and 50 and the voice's parameters gaining their own,
    zero meaning the server's, the tick filling the context from them; the comments that said none
    is built in, that the backend ignores the pan and that named Steam Audio for the slot put right;
    the audio guide's section gaining the model and the upgrading guide its entry, no flat call
    added so the cheat sheet stands, eight lines of the surface listing. Tests: a unit test through
    the recording backend at four placements with their pans and volumes, a new device test playing
    a looping tone on the dummy driver and reading the mix's sides, the right under a hundredth of
    the left at three to the left and the mix at twice the far distance under a thousandth of the
    one near, two existing tests changed for the default and the near distance; a finding on the
    way, the new class beside the backend tests ending the test host with a heap fault since those
    set the process's audio driver and open and close backends, both classes put in one collection
    without parallelization, clean four times, an app opening one backend unaffected. The feature
    test's audio zone plays its drone with a position through the server with the listener set from
    the camera each frame and keeps its check, reading 2.31 left and 1.40 right alone where the same
    placement had read 14.28 and 14.56. Done at `f78b8c58` on 2026-10-21, as the head says.

67. **The first gamepad moves through ImGui only when the program asks.** Found by the feature
    test's interface zone on 2026-10-21: the engine feeds the first pad to ImGui's navigation, so
    the pad's lower face button presses the focused ImGui button as well as reaching the game, and
    the zone's drive had to click the button before pressing the pad; a game that plays by the pad
    with a tool window open has its button taken by the window. Dear ImGui's own default is
    navigation by the keyboard and the mouse alone, and rlImGui leaves the pad out unless asked, so
    the engine does the same: a call turning the pad's navigation on for a program that wants its
    windows driven from a couch, off by default, named in the ImGui guide section and the cheat
    sheet through its script, with the surface listing and the upgrading entry; a test that a pad's
    press reaches the game and not ImGui by default and both when the call is made. The interface
    zone then presses the pad without the click first, keeping the click as a step of its own. One
    commit, before item 59. Done at `b47d20d6` on 2026-10-21, as the head says.

68. **A shader buffer is read back without holding the program.** Asked by the voxel game on
    2026-10-21 for its generation on the GPU (ASKS.md, the entry of that day): `ReadShaderBuffer`
    waits for the dispatches submitted and holds the program until the GPU has caught up, which its
    remark says a game reading every frame pays for each time, so a game that generates a world's
    columns in compute and places blocks from the result on the CPU stalls on every column. The
    engine gains a readback asked for and collected later: `ReadShaderBufferAsync(buffer, offset,
    size)` records a copy into a staging buffer of its own after the dispatches submitted and
    answers a `ShaderReadback` handle with a fence of the submission;
    `IsShaderReadbackReady(readback)` answers without waiting; `GetShaderReadback(readback, span)`
    copies the bytes out once ready, or waits where asked early; `UnloadShaderReadback` frees the
    staging buffer; the staging buffers pooled by size. With the cheat sheet lines through the
    script, the surface listing, the compute guide's section saying when a game reads with a wait
    and when without, and the upgrading entry. Tests: a dispatch writing a sequence read back the
    frame after without a wait, the ready flag false on the frame of the ask under the null device's
    fences and true after, the bytes right, and a readback asked each frame for a hundred frames
    leaking no buffer. The game's entry of 00:33 reads a dispatch writing 128 KB at 0.60 to 0.83 ms
    and its read back at once at 1.08 to 1.71 ms on the main thread, on a frame of 16.9 ms with 4.8
    ms of wait, so a read takes up to a third of what a standing frame has left, and its flights at
    12 columns put the frame at 21.4 to 28.8 ms. One commit, after item 67 and before item 59, since
    the game waits on it. Done at `dc487681` on 2026-10-21, as the head says.

69. **A mesh drawn from buffers a compute shader wrote, with a count the shader writes.** Asked by
    the voxel game on 2026-10-21 for its meshing on the GPU (ASKS.md, the entry of that day): a mesh
    reaches the screen only by `UploadMesh` or `UpdateMeshBuffer` from arrays on the CPU, so a
    mesher in compute would write a buffer, read it back with the wait of item 68's fault and upload
    it again. The engine gains a mesh over shader buffers: `LoadMeshFromShaderBuffers(positions,
    stream, count, capacity)`, a mesh whose positions and stream of the program's own are the given
    shader buffers, with no indices, drawn with an indirect draw whose vertex count the shader
    writes into the count buffer's first four bytes, up to the capacity, so a dispatch and a draw
    share the buffers with no copy and no wait, the buffers' writes made visible to the vertex stage
    by a barrier the frame records; the mesh drawn by `DrawMesh` and the model pass as any other,
    culled by a box the program sets with `SetMeshBoundingBox`, read by the global illumination's
    data as a mesh of positions and the stream's colors where the format says so, and its physics
    left to the program. With the cheat sheet lines, the surface listing, the compute guide's
    section with a compute shader writing a quad and the frame drawing it, an example
    `shaders_compute_mesh` with its capture, and the upgrading entry. Tests: a shader writing two
    triangles and a count of 6 drawn and read from the frame's pixels on the device, a count of 0
    drawing nothing, a count past the capacity clamped, and the null device recording the indirect
    draw. The game's entry of 00:33 reads the mesher's 4 ms budget through `UploadMesh` and
    `UpdateMeshBuffer`, the update at 10.2 to 14.0 ms of a 21.4 to 28.8 ms frame flying at 12
    columns, and the render graph at 6.7 to 9.2 ms there against 5.3 to 5.5 standing. One commit,
    after item 68 and before item 59, since the game waits on it. First part done at `50b3aa6f` on
    2026-10-21, the mesh drawn everywhere with its tests and the example, a mesh with no box drawn
    whole, as the head says; the field's read follows. The field's read changed at 04:59 before it
    was written and taken: reserving each counted mesh's capacity in the corner pool would cost 48
    bytes a triangle of capacity, 190 MB for 500 voxel sections of 24,000 vertices mostly not there,
    so the field instead asks a readback of a counted mesh's four bytes of count when its version
    changes, with no wait, and once it is ready a frame or two on the mesh enters the plan at that
    version with its count known on the CPU, pooled with exactly its triangles, one small dispatch a
    mesh converting its positions into the pool's corners with the stream's colors where the format
    says so, run once a version when the mesh is pooled and not each frame, the splat shader
    unchanged; a mesh rewritten every frame stamped as its box as a moving mesh is; the count of
    conversion dispatches and their GPU time when the pool is made again with 500 sections read and
    given beside the reserved form's memory. The second part's reading at 05:07: the red wall's side
    at 212.8 red against 97.6 with a count of 0, the 500 sections pooled in one frame with 500
    conversion dispatches at 2.6 to 3.4 ms once, the pool 48 MB where reserving the capacities would
    hold 384 MB as the pool rounds up; the builds of 512,000 triangles in that frame at 77 to 81 ms
    are item 74. Done at `50b3aa6f` and `ee6cf5ee` on 2026-10-21, the mesh drawn everywhere, then
    the field's read, as the heads say.

70. **An animated capture of a run, frames taken at an interval and written as one file, and one
    made from captures on disk (Decision 53).** The owner asked at about 00:29 on 2026-10-21 for
    captures made into animations from several pictures with a time between them, a second or 250
    ms, as tooling to show things in the documentation and in devlogs. The engine gains a recorder:
    `record <path> <length> [every <step>] [scale <s>]` beside `shot` in the catalog, taking frames
    from the frame being drawn at a step given in milliseconds or frames (`250ms`, `15f`), for a
    length in seconds or frames or a count of pictures (`5s`, `8`), scaled down by a factor where
    asked, each frame kept in memory as it is taken and the file written when the length is reached
    or `record.stop` is sent, with the frames' own times as the delays, so a step of 1 s plays at 1
    s and `record ... play <ms>` plays faster or slower; the formats by the path's extension, `.gif`
    by an encoder of the engine's own in managed code, a palette of 256 colors by median cut over
    all the frames with ordered dithering, the LZW of the format and a loop forever, and `.png` as
    an animated PNG, the frames as the format's frame chunks over the engine's PNG writer, lossless,
    which GitHub's pages play as they play a GIF; no library is taken, the dependency table allowing
    none for it and, in the flat API as raylib's examples record with Ctrl and F12,
    `StartRecording(fileName, intervalMs)` and `StopRecording()` with their cheat sheet lines
    through the script and the surface listing; and a step of its own in the e3d client, `./e3d
    record <path> <length> [--every <step>]`, which sends the command and waits for the file. For
    captures on disk, a script `build/animate.py <out> <pictures...> --delay <ms> [--loop n]
    [--scale s]` assembles PNG or WebP captures into a GIF, an animated PNG or an animated WebP by
    their extension, with Pillow where it is present and the engine's own writers where it is not,
    so a devlog's animation is made from the captures the pack run already takes; the guide's
    section on captures shows both, with one animation of a game in the README's gallery as its
    first use. Tests: a recording of eight frames at 250 ms on the null device whose file parses as
    a GIF with eight frames and the delays right, the same as an animated PNG read back by the
    engine's reader frame by frame, a palette of a known picture within a distance of its colors,
    and the script run over three captures in the suite. Two commits, the recorder with its formats
    and the command, then the client's step and the script. One or two days, after item 69 and
    before item 59. Done at `31444e1e` and `0f281481` on 2026-10-21, the recorder with its writers,
    the commands and the calls, then the client's step, the script and Ore's day to night in the
    README, as the heads say.

73. **F2 takes a screenshot in every program, to a screenshots folder beside it (Decision 56).** The
    owner wrote shortly before 03:42 on 2026-10-21 that, as in Minecraft, a screenshot is to be
    taken in the game with F2, the same for BevyCSharp, the captures put in the root or a subfolder.
    The engine gains a built-in key: F2 writes the frame being drawn as a PNG into a `screenshots`
    folder beside the program, made on first use, the file named by the time as the source names its
    own, `2026-10-21_03.45.12.png` and a count where two fall in one second, the second file `_2`
    and the third `_3` as the source's own code counts, read by the game's coder, through
    `TakeScreenshot`; a line in the log says the path; `SetScreenshotKey(KeyboardKey key)` moves it
    or turns it off with `KeyboardKey.Null`, and a program that reads F2 itself still sees the
    press. The cheat sheet line through the script, the surface listing, the window guide's
    paragraph beside `TakeScreenshot` and the upgrading entry. Tests: a press through the input
    writing a file of the frame's size into the folder on the null device, two in one second given
    two names, the key moved and turned off. One commit, after item 70. F2 is the engine's own key
    for its performance window today, so the window moves to F3, the debug screen's key in the game
    the owner names, with `SetProfileWindowKey(KeyboardKey key)` to match the screenshot's call, the
    move said in the upgrading entry and the cheat sheet's ImGui prose; a program whose own F3 does
    something, the voxel game among them, moves or turns the window's key off through the call,
    which its item 40 does. Done at `eba9c35a` on 2026-10-21, as the head says, the window's key
    read by a `[RunIf]` condition.

79. **A masked surface drawn through a program's own shader is cut out of the shadow maps by its
    texture.** Asked by the voxel game on 2026-10-21 at 07:19 (ASKS.md, its last entry): with its
    plants' mask material casting shadows, each plant's shadow is its two crossed walls' whole
    quads, the same after the material takes the atlas and the vertex stage gives each corner its
    tile's coordinate, so the shadow pass draws a masked surface through a program's own shader with
    its own depth stage and no cut, where the model pass discards by the alpha cutoff. The mend: the
    shadow pass, the sun's cascades and the spot and point lights' alike, cuts a mask material's
    surface by its texture's alpha at the cutoff for a draw through a program's own shader, by the
    program's own fragment stage where the program gives one for the shadow or by the material's
    texture sampled at the coordinate the program's vertex stage gives, the engine's own masked
    materials already cut the same way confirmed; the shadow guide's paragraph, the upgrading entry,
    and the game's entry's capture as the reading. Tests: a masked quad's shadow on a floor read
    narrower than the quad on the device, through the engine's material and through a program's
    shader, and the null device recording the shadow draw with the texture bound. One commit, small,
    after item 73. Form approved at 08:25: the loader keeps a fragment entry named `shadowMain`
    apart from the program's fragment stage, its bindings merged into the program's set; a masked
    batch through a program with a vertex stage of its own or a `shadowMain` is drawn into every
    shadow map and into the depth the ambient occlusion and the screen's probes read, with the
    program's vertex stage given the light's view-projection in the camera's place and the program's
    `shadowMain` where it has one, else shadow.slang's cut at the coordinate the stage gives, the
    batch's own set, streams, CUSTOM0 feed and zeros bound as the model pass binds them, a pipeline
    a shader and its streams kept until the shader unloads; solid shadows and programs with neither
    stage drawn as before, so a voxel world's chunks pay nothing new. The guide says the stage runs
    with the light in the camera's place, so a stage reading the camera's position casts the shadow
    of the surface as the light sees it. The null device makes no pipelines, so without a GPU the
    loader's `shadowMain` and the draw list's masked draw with its texture are read, the shadow
    draws on the device alone; the game's capture comes from its own item 55 once the package
    carries this, no capture of the game from here. Done at `3e83c817` on 2026-10-21, as the head
    says; the game's capture comes with its item 55 once the package carries this.

80. **A tonemap curve a program sets by its numbers, or a tonemap through a program's own shader.**
    Asked by the voxel game on 2026-10-21 at 07:38 for its item 58, matching the modpack's shader
    whose settings the owner set: `SetTonemap` offers the engine's curve, ACES and Bevy's eight,
    none of them the shader's curve, which multiplies by an exposure, divides by one plus the color
    to a white curve's power to its inverse root, raises to a power from a lower curve in the darks
    to an upper in the lights mixed by the root of the color, takes the gamma and sets the
    saturation about the channels' mean. The mend: a parametric tonemap, `SetTonemapCurve(float
    exposure, float white, float lower, float upper)` in that form, as a tonemap beside the others,
    and `SetTonemapShader(Shader shader)` running a program's own fragment stage over the frame in
    the tonemap's place for a curve the parameters cannot say, the pass's input and output as the
    post guide names them; the cheat sheet lines through the script, the surface listing, the
    guide's paragraph and the upgrading entry. Tests: the parametric curve's output at a few levels
    from 0.05 to 4 against the formula on the null device, and a program's shader tonemapping a
    frame to a known color on the device. One commit, small, after item 79; its Review line under
    the game's entry when the entry is written. Form approved at 09:31: `Tonemap.Curve` at the end
    of the enum, the four numbers in FrameEffects with `GetTonemapCurve()` reading them back, the
    shader's defaults 1.40, 2.0, 1.20 and 1.50 when the curve is chosen alone, the exposure clamped
    at 0 and the white and the two curves at 0.01, the curve in composite.slang to its gamma of
    1/2.2 and the pass returning that value decoded so its encoding gives the curve's display value
    exactly, the modpack's saturation being `SetColorGrading`'s in linear light as the guide says;
    `SetTonemapShader(shader)` drawing a program's fragment stage in the composite's place over the
    engine's fullscreen triangle, a vertex stage of the program's ignored with one warning, the
    program importing a built-in `frame` module that declares the pass's inputs in set 1 and gives
    `frameLight(uv)` and `frameShown(color, uv)` as the input and the output, composite.slang
    importing the same module so the two cannot drift, FXAA through the same choice; the post
    window's combo and sliders, the bloom example's T key; the null device reading the numbers and a
    reference of the formula at the ask's seven levels within 0.001, the device the window's pixel
    within 2 of 255 at each and a program's shader's color read exactly. Done at `d3473746` on
    2026-10-21, as the head says.

## Verdicts settled

Verdicts 1 to 55 are settled, the last two, the drawing leak test's resident bound and the Windows
suite's time, on the runs of `82b23f8c` on 2026-10-12, the leak test green on four Windows suites
and the suite's loss at its limit traced to a shader cache never saved after a failed job, mended at
`9a3e2e2a`; their numbers are not given again. Verdicts 56 to 62 are settled on the runs of
`fc5fdc97`, `534a1c2b`, `2a02ba3e` and `683e6e9d` on 2026-10-17, and 63 and 65 on the runs of
`704952c5`, 66 on the pack run of `a41cc801`, 67 on the pack run of `5a2d7df3`, 69 on the runs of
`1398ed24` and `5a2d7df3`, 64 and 68 on the runs of `5a2d7df3` and `a12206af` on 2026-10-18, and 70
on the runs of `a12206af` and `ad5d3692` on 2026-10-19, said under each. Verdict 71 is settled on
the runs of `33fd0804` and `fa607cb3` on 2026-10-19, said under it. Verdict 73 is settled on the
runs of `80625742` and `193c68b0` on 2026-10-20, said under it. Verdict 74 is settled on the pack
runs of `193c68b0`, `448aa3fc` and `9f9180b7` on 2026-10-20, said under it. Verdict 72 is settled on
the runs of `9f9180b7` and `82dd08f2` on 2026-10-20, said under it. Verdict 78 is settled on the
pack run of `b19f09dc` on 2026-10-20, said under it. Verdict 76 is settled on the pack runs of
`2cc516fc`, `b19f09dc` and `b3922179` on 2026-10-20, said under it. Verdict 75 is settled on the
runs of `b3922179`, `ac77ef83`, `086d9a00` and `afbe81cf` on 2026-10-20, said under it. Verdict 77
is settled on the games jobs of the pack runs of `b3922179` and `f78b8c58` on 2026-10-20 and
2026-10-21 and the games job before them, said under it. Verdicts 79, 80 and 81 are settled on the
pack run of `b5db84f3` on 2026-10-21, said under each. Verdict 82 is settled on the Windows suites
of the push of `ec87f64b` and its pack run on 2026-10-21, said under it. Verdict 83 is open, mended
at `36cae0c2`.

56. **The pack run's examples job fails in the strategy game's step on a wait without a timeout.**
    On the pack run of `82b23f8c`, 2026-10-12, the step built Tactics from the package, played its
    opening and, after the order at tile 6 5, asked `frames.wait 300` with no `--timeout`, so the
    client gave up at its 30 seconds while the game, on lavapipe under the validation layer, was
    still running the 300 frames, and the step ended with exit code 6; Tactics.log holds no line at
    a warning or worse, so the game was well and the client was impatient. Manor's and the rally's
    steps give their long waits `--timeout 600`, and line 330 of `examples.yml` is the one wait of
    100 frames or more without it. One thing: `--timeout 600` on that wait, and the pack run after
    the push. Settled when the pack run's examples job passes the step. Settled on the pack run of
    `fc5fdc97`, 2026-10-17, the examples job passing the strategy game's step.

57. **Slide's process ends in its startup on the push's examples job, with nothing logged.** On the
    push of `82b23f8c`, the puzzle games' step opened Slide offscreen and asked `frames.wait 10`,
    and the client found the session unreachable, the process having ended after accepting the
    connection; its log ends at the renderer's resize complete, 1.09 seconds in, three offscreen
    images of 480 by 270 made, no exception and no warning, so the process died outside managed code
    or was killed. The pack run's examples job failed before the puzzle step, so the step has run
    once on this code. On the push of `cc473437` the Windows job's games' step found Slide's session
    gone at `input.touch`, the process still running, so it had stopped serving, its log ending at
    the same resize complete 1.27 seconds in; and on the push of `0c63cab5` the macOS job's step
    found a swipe up or down sliding no tile, the board unchanged, the log ending there too. Three
    systems, three shapes, one game, and the puzzle step passed on `fca652a1`'s push between them,
    so it is a race in Slide's own startup or in the scripted touch and not a system's. Slide is the
    flat API alone, `InitWindow` then a 128-pixel font, four sounds and the best score's file loaded
    before its loop, so the CLI server serves while no frame runs yet, and the step's first command
    meets a game that has not begun drawing. Read and mended at `b3895bd4`, two of the three: the
    macOS swipe was the script's drag read as a plain drag on a device under five frames a second,
    the game's gestures timed on its wall clock, and the script sets a sixtieth a frame before its
    gestures; the Windows session gone was a read of a session file while the game replaced it,
    which Windows refuses, answered as no session, and the read tries again up to four times ten
    milliseconds apart; the Linux exit is not reproduced, and the script asks the runtime for a dump
    off Windows so the next death names its thread. Settled when the puzzle step passes on two
    pushes and no Linux death comes, or the death's dump names its cause and that is mended. One
    push's puzzle step green of two, `ddb40469`'s, with no Linux death. The pack run's Windows job
    played every game green too. Settled on the push of `fc5fdc97`, 2026-10-17, the second puzzle
    step green with no Linux death.

58. **The macOS suite of the pack run is lost to a crash in MoltenVK's pipeline creation.** On the
    pack run of `cc473437`, 2026-10-13, the suite crashed after 1 minute 38 seconds with 1,392
    passed, a bus error on a runtime thread inside `vkCreateGraphicsPipelines`, reached from the
    model renderer's pipeline for the bloom's scene draw during `SceneFieldTests`' particles
    bouncing off a wall behind the camera, the dump written and read; the script's rerun by
    namespace passed every test, 1,640 with 25 skipped in 15 minutes, and the macOS suite passed on
    the push of the same code an hour earlier and on the four runs before. So the crash is the
    driver's, Metal compiling a pipeline under MoltenVK on the runner, seen once, and the job fails
    as a lost suite should. Read on the next macOS runs: a second crash in the same call names which
    pipeline and what the suite held, 1,202 MB here; one crash stays a note. Settled at two green
    macOS suites. One green macOS suite of two, the push of `ddb40469`. Settled on the push of
    `fc5fdc97`, 2026-10-17, the second green macOS suite, the pack run's a third.

59. **The drawing leak test's resident baseline is read before the heap census of the same app, so
    the census' footprint counts as growth.** The Windows suite failed the test's resident bound on
    the pack run of `9a3e2e2a` and on both runs of `b3895bd4`, having passed it on the five runs
    between, and the series the message carries since `9a3e2e2a` is the same each time: 10 apps 303
    to 312 MB, 20 apps 235 to 247, 30 apps 300 to 313, then a rise of some 20 MB to the hundredth,
    322 to 335. The twentieth app's figure is the low one because the resident memory is read before
    the collections, and the census of the twentieth app, `dotnet-gcdump` attached to the process,
    comes after the read and leaves its own footprint of some 60 MB in the working set for the rest
    of the run, so the difference of the last against the twentieth holds the census and the creep
    together, 75 to 88 against a bound of 50, and whether it passes depends on how much the census
    happens to cost on that runner. The heap is flat at 38 MB and no device object outlives its app,
    so no leak is in it. One thing: the baseline is the resident memory read after the census, the
    thirtieth app's or the twentieth's read again once the census is taken, said in the test's
    comment, and the message says both figures; the bound stays at 50 for the creep alone, which the
    series puts at 20 to 35 MB over eighty apps, and a creep near the bound is then a reading of its
    own. Settled at three green Windows suites in a row. Mended at `ddb40469`, the baseline read
    after the census. One green Windows suite of three, the push of `ddb40469`, the test passing
    with the baseline after the census. Two green Windows suites of three, the pack run of
    `ddb40469` the second. Settled on the push of `fc5fdc97`, 2026-10-17, the third green Windows
    suite in a row, the pack run's a fourth.

60. **Summit's reference frame was captured gray on macOS.** On the pack run of `b3895bd4`,
    2026-10-13, `ReferenceFrameTests`' frame of Summit differed from its reference in 77% of its
    pixels, gray from top to bottom, (83, 91, 79) in the bottom third against the reference's lit
    (186, 237, 161), every pixel of the lower two thirds apart, and the same test passed on the push
    an hour before and on every run since the reference came. The test loads the level and captures
    after a fixed settle of ten frames, with no `ready` of its own where the probe's capture waits
    on `IsReflectionProbeReady`, so a runner slower that minute captured the island before its
    meshes, its field or its light were ready to draw. One thing: the capture waits on the scene's
    readiness, every mesh uploaded, every pipeline made and the field settled, through a predicate
    the engine already answers for its pictures, with the settle after it, and the same for every
    reference frame captured after a fixed settle alone; the failing frame, kept under the results'
    reference-failures, is read where it was uploaded. Settled at three green macOS suites in a row.
    Mended at `ddb40469`, every reference frame waiting on `E3D.IsSceneLoaded` and its own ready
    before its settle. One green macOS suite of three, the push of `ddb40469`. Two green macOS
    suites of three in a row, the push and the pack run of `fc5fdc97` on 2026-10-17; the pack run of
    `ddb40469` between them was lost at Verdict 61's test, its reference frames unread. Settled on
    the push of `534a1c2b`, 2026-10-17, the third green macOS suite in a row.

61. **The script test's grow mode races its own two limits on a slow runner.** On the pack run of
    `ddb40469`, 2026-10-16, the macOS suite failed
    `TestScriptTests.A_Lost_Process_Is_Said_First_And_The_Suite_Runs_Again_In_Parts` in its grow
    mode: the test runs `build/test.py` on a stand-in that grows, with `--timeout-minutes 0.1` and
    `--memory-mb 200`, and expects the page to say the suite ended at its memory limit, and the page
    said it ended at its time limit after 6 seconds holding 196 MB, so the stand-in on that runner
    grew to within 4 MB of the memory limit when the six seconds ran out; the hang mode beside it
    passed, as did the other 1,641 tests, and the test has passed on every run before. One thing:
    the two limits are set so the memory limit comes first on any runner, the stand-in growing in
    larger steps or the test's time limit at a minute, and the test's comment says the race. Settled
    at two green macOS suites. Mended at `fc5fdc97`, the grow mode given a minute and the stand-in
    growing in random blocks. Settled on the runs of `fc5fdc97`, 2026-10-17, the push's and the pack
    run's macOS suites green.

62. **A graphics pipeline is made with an empty entry point name, and a lamp's light leaks into a
    closed room.** On the pack run of `534a1c2b`, 2026-10-17, the Linux suite failed one test of the
    1,684 run, the `GlobalIlluminationTests` fact that a lamp casting shadows bounces no light into
    a room whose walls close it out, twice over: the room read 232 where under 8 was asked, and
    under N 3.7 the engine logged five validation errors, `vkCreateGraphicsPipelines` given `pName
    ""` for a vertex stage whose module's one entry point is `main`. The push run of the same commit
    passed the suite on Linux two hours earlier and every suite since `fc5fdc97` passed the test, so
    the name is empty on some runs and not others, which is a read of the entry point's name that
    can come back empty, from a shader's reflection or a cache's entry, before or while the pipeline
    is made; a pipeline with no vertex entry draws nothing, so the lamp's shadow pipeline missing is
    what let its light into the room. Two things: where the engine takes the entry point's name
    from, read for what returns an empty string and when, and an empty name made a failure where the
    shader is compiled or loaded, with the shader named, rather than a validation error at the
    pipeline; and the suite's Linux job read for what differs between the push run and the pack run
    of one commit, the Mesa cache restored from the other run's job among it. Settled at three green
    Linux suites in a row after the mend. Mended at `2a02ba3e`: the entry point's and the
    application's names handed to Vulkan null-terminated on the stack where unpinned arrays were, an
    empty entry point refused where the shader is made, and a test making pipelines under forced
    compacting collections. Settled on the Linux suites of the push of `2a02ba3e` and of the push
    and the pack run of `683e6e9d`, 2026-10-17, three green in a row after the mend.

63. **Slide's process ends in its startup on the Linux examples job, after the swapchain's resize,
    with nothing logged.** On the push of `2a02ba3e`, 2026-10-17, the examples job failed at Slide's
    build and play in its fifth minute: `frames.wait 10` ended with 4, the session accepting the
    connection and saying nothing, its process ended, and the log's last lines the offscreen frames
    made at 480 by 270, the swapchain resized and the resize complete, as Verdict 57's Linux death
    ended on `88673244` and `c6579710` and never again through the runs since, which settled 57 on
    two green puzzle steps. The death leaves a dump since `b3895bd4`, the runtime's under
    `captures/dumps`, which the job uploads in `example-captures`, and that artifact needs a
    signed-in download. Verdict 62 found a pointer into unpinned managed memory handed to Vulkan,
    which a collection moved before the driver read it; a process that dies with nothing logged in
    the frames after the swapchain is made is what another such handoff does, so it is the first
    suspect. Two things: the dump read, its faulting thread and frame named here; and every handoff
    of managed memory to a native call read for its pinning, the span-to-pointer conversions
    Vortice's types make, SDL's calls given strings or arrays, the audio and input paths, with a
    test under forced compacting collections where one is found unpinned, as
    `PipelineEntryNameTests` is. Settled when the dump's cause is mended and the puzzle step passes
    on three pushes in a row. Read the same day without the dump: every `fixed` block of the Vulkan
    device, every create-info pointer, the native callbacks, SDL's icons, audio and strings,
    Assimp's file callbacks and ImGui's fonts hold their memory pinned or copied for as long as the
    native side reads it, the font bake from memory proven so under forced compacting collections of
    every generation, so no second handoff of Verdict 62's kind stands; and forty openings of Slide
    on lavapipe under the validation layer, each as `build/drive-game.sh` opens it, died in none,
    which leaves the dump to name the thread, SDL's audio thread the first suspect since the first
    sound plays at the opening click. On the pack run of `683e6e9d`, 2026-10-17, the examples job
    failed the same way in its fifth minute, `frames.wait 10` after a gesture ending with 4, the
    process ended and the log ending at the resize, where the push run of the same commit an hour
    before played Slide through, so the death is two of the four examples jobs under the new
    workflow, each after the click or the drag that plays the first sound, and two of them passed.
    What the page says excludes more than it names. The error names a dump when one is left and
    named none, so the runtime's crash path was not taken, which `DOTNET_DbgEnableMiniDump` takes
    for a fault on any thread, managed or not, saying so on standard error on the way, and the log
    holds that stream since `e3d open` sends both to it; a managed exception no one caught would
    have printed its lines there as well, and every end of the main loop that logs, the quit event
    and `AppExitPlugin`'s exit, logged nothing. What is left is a kill from outside the process,
    `SIGKILL` from the kernel's out-of-memory killer or from a `Kill`, twelve `dotnet build` runs
    having left their MSBuild nodes and compiler server alive in the step; a fault whose handler
    could not run, a stack overflow on a thread the runtime does not own, SDL's audio thread or
    lavapipe's rasterizer threads; or an end the engine takes without a line, the automation's
    window close, the main-loop driver's pump returning false and `CloseWindow` itself, which log
    nothing. So the artifact's dumps hold nothing, and the dump is asked no more. Three things in
    its place, each cheap. Every way the main loop ends logs one line naming it, the exit key, the
    driver's pump, the automation's close, `app.quit` with its caller and the quit event,
    `CloseWindow` logs that it was reached and `ProcessExit` the exit code, so a silent log means a
    kill. `build/drive-game.sh` on Linux and macOS opens the program through a wrapper that waits on
    it and writes its wait status, so `whereabouts` says the exit code or the signal there as it
    does on Windows, 137 for the kernel's kill, 139 for a fault and 0 for an end of its own. And a
    death on Linux adds to the error the kernel's last lines (`sudo dmesg | tail -n 30`, which the
    runner allows) and the processes holding the most memory (`ps -eo pid,rss,etime,args --sort=-rss
    | head`), and the step shuts the build servers down (`dotnet build-server shutdown`) after the
    twelve builds and before Slide is played. Settled as before, the cause mended and three green
    puzzle steps in a row. Mended at `7b2486a5`, the three instruments in place. Every way the main
    loop ends logs its reason the first time `WindowShouldClose` turns true, the exit key, the
    driver's pump or the automation's close, the quit event says whether SDL or the window asked,
    `app.quit` logs the process that sent it, the request carrying a caller field, on Linux with the
    parent process and its command line, `CloseWindow` logs its call and `ProcessExit` the exit code
    last, E3D.md saying a log that stops with none of them ended in a kill or a fault;
    `build/drive-game.sh` on Linux and macOS starts the game itself with `--serve` from the
    checkout, its output in its log, under a shell that waits on it and writes its wait status,
    readiness a `frames.wait 0` answered within a minute, and `whereabouts` names 0, 134, 137, 139,
    143 and the other signals; a death on Linux adds the kernel's last thirty lines and the ten
    processes holding the most memory to the step's output and the error; and the script shuts the
    build servers down after its own build, which covers the examples job's twelve. On the working
    machine Slide played through with its log ending in the quit, the close and exit code 0, and a
    `kill -9` sent after the resize gave exit code 137 with the log ending at the resize and no
    dump, the run's signature exactly, so a kill stands as the first suspect and the next death
    names its sender or the kernel's reason. On the pack run of `7b2486a5`, 2026-10-17, the examples
    job died the same way with the instruments in place, and they say what the page could not
    before. Slide's process ended with exit code 132, signal 4, `SIGILL`, an illegal instruction, so
    neither a kill nor a fault of memory; the kernel's last thirty lines hold no out-of-memory kill,
    the balloon driver naming 16 GB; the log ends at the resize as before; and no dump was left, so
    the runtime wrote none for this signal raised in native code, and the crash path the earlier
    reading relied on does not cover this death. In the same run the games job's Summit ended the
    same way within its first sixty frames, said by `e3d command frames.wait 60` through `step.py`
    with no exit code, since that step opens the game through `e3d open` and not through
    `build/drive-game.sh`; the push run's examples job of the same commit, on another runner an hour
    before, played Slide through. An illegal instruction in a process of this kind is one of two
    things, an instruction the runner's processor lacks in code made for another, or a trap, the
    `ud2` a compiler writes for a path it was told is unreachable, which LLVM does when built with
    `LLVM_UNREACHABLE_OPTIMIZE` off, as distribution builds often are, and lavapipe compiles every
    shader variant through that LLVM on the drawing thread as the first draw with it comes, Slide's
    after its first gesture and Summit's as its scene loads, where the suites, drawing other
    variants, have not died. Mesa's shader cache the jobs carry between runners is not it, since
    Mesa keys the cache on the processor's family and feature bits (`lp_disk_cache_create`, Mesa
    24.0 through 25.2), so a cache made on one kind of processor is not run on another. Two things
    name the instruction on the next death. The game runs with core dumps allowed, `ulimit -c
    unlimited` in the wrapper's shell and the kernel's `core_pattern` set in the job to a file under
    `captures`, since the runner's pipes cores to a disabled apport, and a death that left a core
    prints, through `gdb -batch` on the core and the program, the faulting thread's native frames
    and the instruction at its program counter (`x/i $pc`), into the step's output and the error;
    and every opening on Linux prints the processor's model name and flags (`lscpu`), on a pass as
    on a death, so the runs say which processors die. The game steps of the games job that open a
    game through `e3d open` directly, Summit's among them, open it as `build/drive-game.sh` does,
    through the wrapper that writes the wait status, so their deaths say their code too. Settled as
    before. The two asks are done at `2a9dbdcb`, `build/open-game.sh` opening every game a Linux or
    macOS job plays under a shell that writes how it ended and, on Linux, prints the processor at
    each opening and has gdb read the core a death leaves, which the drive, the step's page, the
    soak check and the storm say; an outside `kill -ILL` was found not to end a .NET game, the
    runtime's handler taking a signal another process sends, so the runner's SIGILL was a faulting
    instruction of the process's own. The next death names it. Settled as before. On the push of
    `2a9dbdcb`, 2026-10-17, the examples job died at Slide at 23:05 and the instruments spoke. The
    process ended with exit code 132 on an AMD EPYC 7763, and gdb read the core, the faulting thread
    one other than the main thread, its frames without a symbol and its return address zero, and the
    instruction at its program counter `vgf2p8affineqb $0x0,%xmm4,%xmm3,%xmm3`, followed by
    `vpandn`, a store of a 256-bit register to the frame at 0x520 and `vpcmpgtb`. `vgf2p8affineqb`
    is a GFNI instruction, a Galois-field affine transformation, which LLVM emits for a shift of a
    vector of bytes where the target has GFNI, and which Zen 3, the EPYC 7763, has not, Zen 4 and
    Intel's Ice Lake having it; code with no symbol and a frame of that shape is what lavapipe's
    compiled shaders look like, so a shader was compiled for a processor with GFNI and run on one
    without. Two readings. The code was made on another runner and reached this one through Mesa's
    shader cache, which the jobs restore from any earlier run's save (`restore-keys:
    mesa-examples-Linux-`), Mesa keying the cache on its own capability bits, which have no GFNI
    bit, so two processors of one family whose bits agree share the cache; or LLVM on this runner
    was told the processor has GFNI, by its CPUID under the hypervisor, and compiled for it there.
    The captures job of the pack run of `7b2486a5` had ten of 256 examples die the same death,
    `models_heightmap_rendering`, `models_first_person_maze`, `models_cubicmap_rendering`,
    `models_stress`, `models_reflection_probe`, `models_skybox`, `models_terrain`,
    `models_mesh_generation`, `physics_boxes` and `textures_mipmaps`, every one drawing textures
    with mipmaps, while 228 drew, said with no exit code since the examples open by name through
    `e3d open`. Three things tell the readings apart and mend the first. The death's error says
    whether `gfni` stands among the processor's flags, from the line every opening prints, and
    whether Mesa's cache served the variant, `MESA_SHADER_CACHE_SHOW_STATS=1` printing the hits and
    misses at exit into the log; every cache of Mesa's shaders the workflow keeps, the Linux and
    Windows suites', the examples', the captures' and the games', is keyed by the processor's model
    name and a hash of its flags, read by a step before the cache step into `GITHUB_ENV` (`lscpu` on
    Linux, `Get-CimInstance Win32_Processor` on Windows), so no runner restores code another kind of
    processor made, which is right whichever reading holds; and the examples open through the
    wrapper as the games do, so a capture's death says its code and its instruction. If the deaths
    go on with the caches keyed so, the generator is on the runner and the second reading holds, to
    be taken up then. Settled as before. The three asks are done at `c08343d5`, every Mesa cache
    keyed by the processor's model and flags through `build/processor.sh`, the examples opened
    through `build/open-game.sh` with a capture's death saying its code and instruction, and the
    ending line saying whether `gfni` is among the flags and what Mesa's cache counted where the
    program ended of itself. The next runs compile cold and say which reading holds. On the push and
    the pack run of `704952c5`, 2026-10-18, the first with every Mesa cache keyed by the processor
    and so compiled cold, both examples jobs played Slide through, in 9 minutes each, two green
    puzzle steps toward the three, and the first evidence for the cache reading; the games and
    captures jobs of the pack run are read when they end. The pack run's games job passed at 01:32
    the same day, every game played on cold caches, Slide among them, the third green puzzle step in
    a row; the captures job, where ten examples died on `7b2486a5`, is read when it ends, and its
    passing settles this verdict with the cache reading as the cause, the mend being the keyed
    caches. Settled on the runs of `704952c5`, 2026-10-18. With every Mesa cache keyed by the
    processor and compiled cold, the push run's and the pack run's examples jobs played Slide
    through, the games job played all twelve, and the captures job drew all 256 examples where ten
    had died, three green puzzle steps in a row and no death anywhere; and the captures job's page
    named its processor, an AMD EPYC 9V45 with `gfni` among its flags, the second kind in the runner
    fleet beside the EPYC 7763, which lacks it, both of one family and sharing every bit Mesa's key
    holds. The cause is the first reading, a shader compiled on a runner of the one kind, carried in
    Mesa's cache that the jobs restored from any earlier run, and run on a runner of the other, and
    the mend is the keyed caches; the three instruments stay, so a death that comes again names
    itself.

64. **The hundred-app leak test reads the working set on Windows, which the system trims, so its
    baseline fell in a trough and the test failed with nothing leaking.** On the pack run of
    `683e6e9d`, 2026-10-17, the Windows suite failed one of the 1,661 tests it ran, `AppLeakTests`'
    hundred offscreen apps that draw, the resident memory at the last app 320.5 MB against 230.4 at
    the twentieth, 90 over the 50 allowed, while the heap stood at 38 MB from the tenth app to the
    hundredth, no Vulkan object was alive after the last and the threads were 23 to 25 throughout;
    the push run's Windows suite of the same commit, an hour before, passed it. The ten readings run
    291, 230, 290, 299, 296, 311, 315, 313, 254 and 321, swinging 60 to 90 MB between neighbors with
    no climb, and the figure is `Environment.WorkingSet` (AppLeakTests.cs:165, 186), the working set
    Windows trims when it likes, which the drawing leak test's resident bound met on 2026-10-12 and
    BevyCSharp's memory guard met in its Verdict 14, mended there by reading the private bytes
    committed, which Windows cannot trim. The mend is one thing. The test's resident figure on
    Windows becomes the private bytes, `Process.PrivateMemorySize64`, the commit charge
    `GetProcessMemoryInfo` answers, with the reason in the test's comment, the figure Linux and
    macOS read staying; and whether the bound of 50 stands is said with the series the new figure
    gives on the Windows suites, under N 3.5. Settled at three green Windows suites in a row after
    the mend. Mended at `7b2486a5`, the test reading `Process.PrivateMemorySize64` on Windows and
    the working set elsewhere, the series naming its figure, the bound of 50 kept since no
    measurement here calls for another, to be read against the Windows suites' new series. On the
    push and the pack run of `7b2486a5`, 2026-10-17, the Windows suite failed the test again with
    the private bytes read, and the figure climbs where the working set swung, 740 MB at the tenth
    app to 849 at the hundredth on the push and 724 to 836 on the pack, 1.1 MB an app from the
    fortieth on with no fall, the heap flat at 38 MB, no Vulkan object alive and the threads 24
    throughout, so the mend found what the trimmed figure hid, 110 MB of commit charge that a
    hundred apps leave behind on Windows and the Linux suite's resident set does not show. Whose it
    is comes first. On Windows the Vulkan loader unloads the ICD, lavapipe's with LLVM inside, at
    the last instance's destruction and loads it again for the next app, where Linux's `dlclose`
    leaves a library with unique symbols in place, so what LLVM and Mesa allocate at load and never
    free would be paid a hundred times on Windows alone; the collector's committed regions, which it
    gives back over time rather than at once, are another; the engine's own per-app native
    allocations, the ImGui context, SDL's init and quit and the Slang session, a third. One thing.
    The test prints beside the private bytes the collector's committed bytes
    (`GCMemoryInfo.TotalCommittedBytes`) and runs the hundred apps once with one Vulkan instance of
    its own held open throughout, so the ICD stays loaded; if the climb goes with it, the cost is
    the driver's load and the test holds that instance, saying why, and if it stays, the engine's
    native allocations are read app by app for what is not freed. Settled at three green Windows
    suites in a row after the mend. Done at `c08343d5`, the collector's committed bytes printed
    beside the private bytes and the hundred apps run once more inside a held Vulkan instance, both
    runs under the same bounds; on Linux the held run took 48 seconds against 64, so the loader
    reloads lavapipe at each app there too. The next Windows suite says whose the climb is. On the
    push and the pack run of `704952c5`, 2026-10-18, the macOS suite failed the new twin at its
    first line, `vkCreateInstance` answering `ErrorIncompatibleDriver`, since MoltenVK's loader
    lists itself as a portability driver and gives an instance only to a caller that asks for the
    portability enumeration extension with its flag, as the engine's instance does, and the bare
    instance asked for nothing; the hundred apps without it passed there as before. One thing more.
    The twin's instance is made as the engine's is, `VK_KHR_portability_enumeration` with
    `VK_INSTANCE_CREATE_ENUMERATE_PORTABILITY_BIT_KHR` where the loader offers the extension, so the
    twin runs on the three systems; the Windows suites of these runs, still running, say whose the
    climb is. The twin's instance is made as the engine's at `d6b08853`, the portability extension
    and flag asked where the loader lists them. On the push and the pack run of `704952c5`,
    2026-10-18, the Windows suite failed both runs of the hundred apps, the plain and the held, each
    climbing alike, 725 MB at the tenth app to 828 at the hundredth and 729 to 823, the collector
    holding 84 to 87 MB committed throughout and the heap 38, so the climb is neither the
    collector's nor the driver's load, which the held instance kept, and is the process's own native
    memory, about 1 MB an app, on Windows alone. One thing more. The test reads the private bytes at
    each stage of one app's life, after SDL's init, the instance, the device, the first frame, the
    close and SDL's quit, averaged over the hundred, so the stage whose bytes do not come back names
    the leak, SDL's Windows backend, the audio device, the loader's layers or the engine's own
    allocations, and that allocation is then found and freed, or, where it is a library's and not
    the engine's, bounded with its cost said. Settled at three green Windows suites in a row after
    the mend. Done at `91ec5de1`, the private bytes read at each step of an app's life and averaged
    over the eighty after the twentieth, the device's making and disposal split into finer steps,
    and on Windows the heaps' allocated and committed bytes beside the collector's; the headless
    apps initialize no SDL and open no audio device, so those two candidates are out and the loader
    with its layers and driver, the renderer's shaders and the engine's allocations remain. The next
    Windows suite names the step. On the push and the pack run of `a41cc801`, 2026-10-18, the
    Windows suite failed both hundred-app tests again, the private bytes 744 MB at the twentieth app
    and 816 at the hundredth on the push run, 744 and 827 on the pack run, 0.9 and 1.0 MB an app,
    the heap at 38 MB from the thirtieth app on, the collector committing 65 to 89 MB, no Vulkan
    object alive, and, read for the first time, the process's heaps flat, 148 to 150 MB allocated of
    385 to 394 committed in 8 heaps at every reading from the tenth app to the hundredth, so no
    `malloc` in the process holds the climb, the C runtime's, the loader's, the layer's or
    lavapipe's, and the collector does not. The step line says what an app's life takes and gives
    back, averaged over the eighty, the instance 4.7 MB, the device 6.8, the renderer's shaders
    100.5, the frames drawn 85.1, the device's destruction giving back 152.6, the instance's 31.2,
    the close 2.1 and the end after a full collection 8.8, and it sums to 3.2 and 3.8 kept an app on
    the two runs where the ten readings climb 0.9 and 1.0, so some 2.5 MB an app is given back
    between an app's last reading and the next app's first, which no step holds, the stacks of the
    app's threads that end after it, the tenth-app readings being taken once they have, or memory a
    library frees late. The ten readings also swing, 679 at the fortieth app on the pack run and 714
    at the eightieth on the push run against 745 to 790 around them, 60 to 100 MB with the heaps and
    the collector flat, so a difference of two readings carries that much either way and the bound
    of 50 is read through it. What is left is memory committed outside every heap, by `VirtualAlloc`
    or a pagefile-backed section, and the process holds four kinds of it, the code and data LLVM
    compiles for lavapipe's shaders, the runtime's own code and loader heaps, the stacks of threads,
    and sections mapped. One thing more. On Windows the test sweeps the process's address space with
    `VirtualQuery` at the twentieth and the hundredth app, summing the bytes committed by the
    region's type, private, mapped or image, and by whether its protection is executable, with the
    count of regions in each class, and says the classes on the series' line with each one's growth
    from the twentieth to the hundredth, since LLVM's compiled code is private and executable, the
    runtime's is mapped and executable under its double mapping, a thread's stack is private, not
    executable and under a guard page, and data of either is private and not executable, so the
    class that grew names the owner; and the step line's last reading is taken once the app's
    threads are gone, as the tenth-app readings are, so its kept figure is the series'. What follows
    the reading is said here when it is in. Settled at three green Windows suites in a row after the
    mend. Done at `0af1c9cd`, the sweep on 64-bit Windows at the twentieth app and the last, the
    committed regions summed by type, private, mapped or image, and by what they hold, code by an
    executable protection, a stack by a guard page in its allocation, data otherwise, the kind grown
    most said first on a line of the series; each app's last reading taken once its threads have
    ended, waited for half a second at most, since on Linux the threads of two apps in eighty
    outlived two seconds and on Windows a longer wait for each of a hundred apps would pass the five
    minutes a test is allowed, the apps whose threads outlast the wait counted on the line; and what
    changes between an app's last reading and the next app's first a step of its own, so the steps
    sum to the tenth-app readings' climb. On Linux the steps summed to 0.04 MB given back an app
    with 0.32 between apps. The next Windows suite names the kind. On the push and the pack run of
    `0af1c9cd`, 2026-10-18, the Windows suite failed both tests again, 741 MB at the twentieth app
    to 824 at the hundredth on each, and the two new readings agree with each other. The steps sum
    to the series, 1.08 and 1.07 kept an app, with 2.3 and 2.2 given back between apps and the
    threads of one or two apps in eighty outlasting the half second, so no step and no stack hides
    the climb. The sweep names the kind. Private data grew from 712 MB in 2,901 regions at the
    twentieth app to 792 in 3,367 at the hundredth on the push run and from 712 in 2,908 to 790 in
    3,400 on the pack run, 80 MB in some 470 regions, six regions and a megabyte an app, 170 KB a
    region on average; mapped data grew 0.7 and 2.8 MB, mapped code 0.2, and private stacks, 1.3 MB
    in 50 regions, image data and image code, 86.7 and 136 MB, did not move, and private code is one
    empty region at both readings, so LLVM's compiled code is gone when a reading is taken. The
    climb is memory committed with `VirtualAlloc` for data and never given back, outside every heap,
    not a stack, not code and not a module, by whoever allocates that way, LLVM and lavapipe for the
    data of a compile or a scene, the runtime for its loader heaps and bookkeeping, the layer or the
    loader, and on Windows alone, since Linux and macOS hold. One thing more. For the apps the test
    follows, the twenty-first, thirty-first, forty-first and fifty-first, the sweep runs at every
    step, and the test says the private data regions each step added, their count and their sizes,
    and which of them still stand at the app's end and at the end of the app after it, by the step
    that added them, so the step whose regions stay names the owner by what it does, the layer's
    load at the instance, LLVM's compiles at the shaders and the frames, the physics at its plugin,
    and the sizes say the allocator, 64 KB pieces of a loader heap or the odd sizes of sections;
    where the step alone does not name it, a trace of `VirtualAlloc` with its stacks under Windows
    Performance Recorder on one app is the step after. Settled at three green Windows suites in a
    row after the mend. Done at `4827b705`, the sweep at every step of the four followed apps, by
    allocation, each step's new allocations with their sizes and how many stand at the app's end and
    after the next app, on the series an app a line. An allocation that stood before the app and
    grew within it, as a reservation committing more of itself does, is not among those added, so
    where the lines show few allocations added while the series climbs, the growth is inside
    standing allocations and the reading then says the allocations whose committed kilobytes grew
    over the app, by step. The next Windows suite names the step and the sizes. On the push of
    `4d5db9df`, 2026-10-18, the Windows suite failed the two tests as before, and the twentieth
    reading fell in a trough, 661 MB against 724 at the tenth and 746 at the thirtieth, so the
    sweep's baseline was taken there, private data 630 MB in 3,522 regions at the twentieth and 793
    in 3,303 at the hundredth, 162 MB more in 219 fewer regions, which says the swing of the
    readings is private data as well, committed and given back within allocations that stand, the
    regions joining as the holes fill. The heaps and the collector being flat, the swing and the
    climb belong to an allocator of its own that reserves and commits by itself, and one such is in
    the process on Windows alone. The Khronos validation layer's 64-bit Windows build links mimalloc
    in place of the C runtime's malloc, by its `layers/CMakeLists.txt`, which says the other systems
    do not for their instability under it, and the loader loads the layer with each app's instance
    and unloads it with the instance's destruction, in the held twin as in the plain one, since the
    held instance enables no layer; mimalloc reserves segments of megabytes, commits them in pieces
    and gives pieces back on a delay, and a library unloaded takes none of that with it. So the
    per-step lines are read for allocations added as the instance is made and standing after the
    instance is destroyed, with mimalloc's sizes, and where they say so, the test holds the layer
    loaded through the hundred apps, the held instance enabling it, and the plain test's bound is
    read with the layer's unload no longer in the figure, the layer's own leak said in the test's
    comment with its version; and the Windows suite takes the layer from LunarG's latest SDK
    (`build.yml:196`) where Linux pins 1.4.363.0 in `build/fetch-validation-layer.sh` so a change of
    layer comes in a commit, so the Windows version is pinned alike in the same mend. Done at
    `351cc461` so far as the suite has not spoken, the held instance enabling the validation layer
    wherever the loader lists it, so the layer stays loaded through the hundred apps in the held
    twin, and the Windows job pinned to the SDK's 1.4.363.0 as Linux is; on Linux the plain test's
    instance takes 77.4 MB and gives 148.2 back with 0.08 kept an app, and the held twin's instance
    steps fall under 0.05, so the held layer serves each app's instance. The next Windows suite says
    the step and the sizes and whether the held twin passes beside the plain test, and then the
    plain test's bound is read with the layer's unload out of it and the layer's leak said in the
    test with its version. On the pack run of `4d5db9df`, 2026-10-18, the Windows suite passed both
    tests, the first pass since the figure became the private bytes, by the readings' swing meeting
    the bound of 50 this once and by no mend; it counts for nothing toward the three green suites
    the mend needs. On the push of `351cc461`, 2026-10-18, the Windows suite failed one test of the
    two. The twin holding an instance with the validation layer enabled passed, and the plain test
    failed as before, its private bytes 737 MB at the twentieth app and 836 at the hundredth,
    private data 709 MB in 2,836 regions to 807 in 3,319, 98 MB more, with the layer's library
    absent at both readings, the image code 101 MB where the twin's readings hold 136 with the layer
    loaded, so the failing test is the plain one. The layer's unload with each app's instance is the
    leak, about a megabyte an app on Windows at 1.4.363.0, the layer's own allocator keeping what it
    reserved when the loader lets the library go, and the engine keeps nothing, as the twin says.
    The page lost the four per-step lines under its limit of 4,096 characters, the message ending in
    the regions' line, so the step was not read and did not need to be; and the heaps' allocated
    bytes climbed from 148 to 158 MB across the hundred apps on this run, 0.1 MB an app, where they
    stood flat before, which the test watches once the layer is out of the figure. The mend is one
    thing. The two leak tests run their hundred apps without the validation layer on every system, a
    Debug instance leaving the layer off where a test asks it, with the reason in the test's
    comment, the layer's Windows build at 1.4.363.0 keeping about a megabyte an app on its unload,
    measured here, and the twin passing beside the plain test on this run; so the plain test
    measures the engine and the driver's reload, as it was meant to, the twin the engine alone, and
    `E3D_REQUIRE_VALIDATION` does not count them. Where a Debug instance cannot leave the layer off,
    both tests hold the layer as the twin does. And the failure's message keeps the lines that name
    the cause, the regions and the per-step lines, before the series, within the page's limit.
    Settled at three green Windows suites in a row after the mend. On the pack run of `351cc461` the
    same day the Windows suite said the same, the plain test alone failing with 95 MB of private
    data kept and the layer's library absent at the readings, the twin passing, so the two samples
    agree. Mended at `08b104dd`, both drawing leak tests making their hundred apps with the
    validation layer off on every system through an internal switch of the device, the reason and
    the figures in their comment, the held instance holding the driver alone again, and the
    failure's message naming where a climb lies before its series within the page's limit; settled
    at three green Windows suites in a row after it. On the push of `4c6630a4`, 2026-10-18, both
    memory bounds held on Windows with the layer off, the suite red on the handles of one test
    (Verdict 68), so it is no green suite for the row. On the pack run of `08b104dd`, 2026-10-18,
    the Windows suite passed whole, the first green Windows suite after the mend, one of three. The
    row of three green Windows suites counts from the runs of `1398ed24` on, Verdict 68's mend being
    in the same tests. On the push of `1398ed24`, 2026-10-18, the Windows suite failed the twin
    alone, the hundred apps beside a held instance of the driver, by 9.7 MB, private data 609 MB in
    3,470 regions at the twentieth app and 669 in 2,772 at the hundredth, no allocation a followed
    app's steps added standing once the next app had ended, so the growth lies inside standing
    allocations as the falling count of regions says, while the plain hundred apps held their bound
    with the layer off. The twin's held instance keeps lavapipe loaded through the hundred apps,
    where the plain test lets the loader unload and load it with each, so what the twin measures
    beyond the plain test is the driver kept in the process, whose compiler's state grows with each
    app's shaders compiled, lavapipe's own and no business of the engine's bound, or the readings'
    swing, which the series would tell and the page lost under its limit, the regions and the four
    allocation lines standing first and the series after. The mend is one thing, with its message.
    The twin's private bytes are said and not judged, the twin judging the heap floor and the Vulkan
    objects as the plain test does, its comment saying that beyond the plain test it measures the
    driver kept loaded, with this run's figures, the plain within 50 and the held 59.7 with nothing
    new standing; and a memory failure's message, as a handle failure's, puts the series after every
    ten apps and the heaps before the followed apps' allocation lines, within the page's limit. The
    row of three green Windows suites counts from the mend's runs. Mended at `5a2d7df3`, the twin's
    private bytes said and not judged with the reason and the figures in its comment, the plain test
    judging as before, and a memory failure's message ordered with the series and the heaps first
    within 3,500 characters; settled at three green Windows suites in a row after it. On the push of
    `5a2d7df3`, 2026-10-18, the Windows suite passed whole with the mend in, the first of three.
    Settled on the push and pack runs of `5a2d7df3` and the push run of `a12206af`, 2026-10-18,
    three green Windows suites in a row with the layer off and the twin's figure said.

65. **The games job's soak judges Manor and Swarm as standing still, and both move on.** On the pack
    run of `683e6e9d`, 2026-10-17, the games job's soak of the twelve games, two minutes each on
    lavapipe at once, failed two. Manor's images climbed at their least from 81 to 90 past a bound
    of 87, its descriptor sets from 82 to 95 past 90, its entity ids from 399 to 478 past 448 and
    its assets from 81 to 95 past 93, its resident memory 548 MB at the first reading kept and 573
    at the last; Swarm's entity ids climbed from 108 to 130 past 128, its resident memory 460 then
    462. The check's own comment says what Manor does, its walk on the runner's four cores entering
    rooms the first half never reached, so the least climbs with the world, and the WHOLE table
    gives its buffers the most a whole walk holds, 162, and nothing else, where the images, the
    descriptor sets, the entity ids and the assets of a streamed level climb for the same reason.
    Swarm's soak sets wave 3 at every turn, so a count that still climbs across turns is what a turn
    leaves behind, drops, projectiles or the effects of the dead, 22 ids in two minutes, or the
    wave's own size varying from turn to turn. Two things follow. Manor's four figures are measured
    over a whole walk on the development machine's GPU and on lavapipe, as the buffers were, and
    written into WHOLE with the command that measured them (N 3.6), or the soak's course walks the
    level whole before the readings judged begin. Swarm is read for what a turn at wave 3 leaves
    alive after the next turn's reset, a leak of entities mended where one is found, and otherwise
    its entity ids given the most a turn holds in WHOLE the same way; the soak passed on the pack
    run of `fc5fdc97`, so the climb depends on how far the device gets in two minutes. Settled when
    the games job's soak passes on a pack run. Mended at `7b2486a5`. Manor's images, descriptor
    sets, entity ids and assets are given the most two whole walks of its autopilot held at 320 by
    180 with a collection every 60 frames, 92, 95, 539 and 96 on the GPU and on lavapipe alike, the
    second walk holding no more than the first, the buffers' 162 kept; Swarm's entity ids, which
    count the most ever alive at once and only rise, are given 152, the peak of thirty turns at a
    quarter-second frame, where a slow device's frame reaches Time's clamp and a turn of 300 frames
    covers 75 game-seconds into waves 4 to 6, 100 at a sixtieth, and thirty resets left the same 27
    entities, so nothing leaks; `SoakCheckTests` holds the run's numbers passing, each figure
    failing past its bound and the same climb failing for a game without a whole-walk entry, and
    BUILDING.md's soak paragraph says so. On the pack run of `7b2486a5` the games job ended at
    Summit's death (Verdict 63) before the soak ran, so the mend is unproven there. Settled on the
    pack run of `704952c5`, 2026-10-18, the games job passing with the soak of the twelve games,
    Manor's and Swarm's figures within what a whole walk or turn holds.

66. **The measure against raylib failed and its page says nothing of which pairs, the step's page
    holding the programs that ended well.** On the pack run of `704952c5`, 2026-10-18, the captures
    job drew all 256 examples and then failed at the measure against raylib's program, `compare.py`
    ending with 1, which it does where a pair's share of differing pixels passed the one recorded
    for it by more than its room or a pair is no longer drawn; the script prints the pairs to its
    output and annotates nothing, so the page's one error is `step.py`'s, which under Verdict 63's
    third ask lists how each program opened in the step ended, 21 lines of programs that ended of
    their own with their processor and cache counts, and GitHub's 4,096 characters for an annotation
    ran out before the command's last lines, so a reader not signed in learns which examples ended
    well and not which pairs failed (N 6.7). Two things. `compare.py` says its failures as an error
    annotation of its own, the pairs past their room with the share measured and the share recorded,
    and the pairs no longer drawn; and `step.py` names only the programs that ended other than of
    their own or still run, after the command's last lines, so an ending with exit code 0 costs the
    page nothing. Settled when the measure's page names its pairs on a pack run, and the pairs are
    then read for whether the runner's processor draws them otherwise. Mended at `a41cc801`, the
    pairs said as the script's own errors with their shares and room, in at most ten under the
    limit, and the step's page naming only the programs that ended other than of their own or still
    run, after its last lines. Settled on the pack run of `a41cc801`, 2026-10-18, the measure's page
    naming its one pair as the script's own error, `models_directional_billboard` 7.9 percent apart
    where 5.5 was recorded, past its room of 1 point, and no program named, every one having ended
    of its own; the pair is Verdict 67's.

67. **The raylib measure's pair `models_directional_billboard` stands 7.9 percent apart where 5.5 is
    recorded, on a runner whose processor the page does not say.** On the pack run of `a41cc801`,
    2026-10-18, the captures job's measure failed on that pair alone, 7.9 percent of its pixels
    apart from raylib's frame where `3DEngine.Examples/measured-ci.tsv:71` records 5.5, past the
    point of room every pair has (`build/raylib-bench/compare.py:209`), the other pairs holding and
    every one of the 256 examples drawn. The record was measured on a runner on 2026-10-11 or
    before, and the measure held to it on the pack run of `fc5fdc97` on 2026-10-17 and failed on the
    two pack runs since, `704952c5` with its cause lost under the page's limit (Verdict 66) and this
    one on the pair. Two things changed between. Since `704952c5` each runner draws with shaders
    compiled for its own processor (Verdict 63), where before the cache restored was one processor's
    for every runner, so a frame whose pixels depend on the rounding LLVM's code for a processor
    gives, the direction a billboard faces chosen from an angle near a boundary among them, may
    differ between the two processors the fleet has and no longer be hidden; and the runner image's
    own Mesa, which raylib's program draws with, is whatever its packages hold on the day. The page
    says neither the processor nor its flags, which the game steps' pages do. The mend is one thing,
    with its reading. The measure's page says the processor the runner drew on and whether gfni is
    among its flags, as the game steps do, beside its pairs and its count, so a pair's share is read
    with the processor that drew it; the pair is read on the working machine for what in its frame
    turns on rounding, the billboard's direction at the frame taken first, and where it does, the
    record gives the pair the room the two processors' shares measure, in the third column with the
    reason above it, or the frame taken moves off the boundary and the picture is taken again; and
    where it does not, raylib's frame is compared between the two runs for what Mesa drew otherwise.
    Settled when the measure holds on a pack run, and said here again where a run on the other
    processor fails it. Done at `4d5db9df`, the measure's errors and summary saying the processor
    and its gfni flag and both pictures of every pair kept under `captures/measure`; and read on the
    working machine, where the pair's 45 frames from 196 to 949 are 0.5 to 0.6 percent apart and the
    billboard's direction stands at 7 from frame 118 to 212 with the capture's frame among them, so
    nothing in the frame turns on rounding and the two shares are what the runners' Mesa draws. The
    next two pack runs, one on each processor, say which program's picture moved and whether the
    processor moves it. On the pack run of `0af1c9cd`, 2026-10-18, the measure failed on the same
    pair alone, 7.8 percent apart, the page saying no processor yet; the runs of `4d5db9df` say it.
    On the pack run of `4d5db9df`, 2026-10-18, the measure held on every pair, this one within its
    room, the 256 examples drawn in 1,257 seconds where the two failing runs took 1,565 and 1,572,
    so the pair holds on one runner and not on another, as a processor's drawing would have it; and
    the page of a run that holds says no processor, since `compare.py` prints it to the log and the
    summary, which need a sign-in, and to its errors alone. One thing more, small. The line
    "Measured on <model>, gfni ... its flags" is a notice as well, on every run, so a passing run's
    page says which processor the pairs held on, and the pictures kept under `captures/measure` on
    this run are the holding side of the comparison the next failing run completes. Done at
    `87cd9415`, the processor a notice on every run before any pair is measured, the pairs measured
    for the first time sharing the other nine notices. On the pack run of `351cc461`, 2026-10-18,
    the measure held on every pair again, so the pair has held on two pack runs and failed on two,
    the processor said on neither of the holding ones, which `87cd9415`'s notice ends from the next
    pack run on. On the pack run of `08b104dd`, 2026-10-18, the measure failed on the pair alone,
    7.9 percent apart, and the page said the processor, an AMD EPYC 7763 without gfni among its
    flags, the 256 examples drawn in 1,571 seconds as on the two failing runs before, where the two
    holding runs drew them in 1,257 and 1,247, so the pair fails on the fleet's 7763 and holds on
    its other kind, the 9V45 with gfni, at 2.4 points apart between the two processors' drawings of
    the same frame, a rounding difference the working machine, with gfni, draws as the 9V45 does.
    Both pictures of the pair from this run and from the holding run of `4d5db9df` are in those
    runs' captures artifacts under `measure`, for the owner's eye. The mend is one thing. The record
    gives the pair a room in its third column measured over the two processors, 3 points over the
    5.5 recorded to hold the 7.9 the 7763 draws with slack, with the reason above it naming the two
    processors and their shares, as `compare.py` provides for a pair whose picture moves between
    runs. Settled when the measure holds on a pack run on each kind of processor, the page naming
    it. Mended at `32ce6f4e`, the pair given three points of room in the record's third column with
    the two processors and their shares in the comment above it; settled when the measure holds on a
    pack run on each kind of processor, the page naming it. On the pack run of `32ce6f4e`,
    2026-10-18, the measure held on every pair on an AMD EPYC 9V74 with gfni, a third kind the fleet
    shows, the page naming it, the 256 examples drawn in 1,361 seconds, so the kinds with gfni hold
    and the 7763 without it is the side left; settled when the measure holds on a pack run on a
    7763, the page naming it. On the pack run of `1398ed24` the same day the measure held on every
    pair on AMD EPYC 9V74 80-Core, gfni among its flags, the page naming it, another holding run on
    a kind with gfni; the 7763 is the side left. Settled on the pack run of `5a2d7df3`, 2026-10-18,
    the measure holding on every pair on an AMD EPYC 7763 without gfni, the page naming it, the pair
    within its room of 3 on the kind that drew it 7.9 three times, as the kinds with gfni held
    before.

68. **The leak test's handle count passed its allowance by one at the 89th app on Windows, Thread
    handles of ended threads kept some four an app.** On the push of `4c6630a4`, 2026-10-18, the
    Windows suite failed one of the two hundred-app tests on its handles, 2,413 at the 89th app
    where 2,143 stood at the twentieth and 2,412 were allowed, the allowance being 200 and one
    Thread handle an app for the driver's (`AppLeakTests.cs:91, 167`), while both memory bounds held
    with the layer off (Verdict 64); the pack run of `08b104dd` the same hour passed both tests on
    Windows, so the count crosses the allowance on one run and not another, by a handle, the climb
    being some four handles an app against the two and a half the allowance spreads over eighty. The
    followed apps say where the handles go. The twenty-first kept 7, 5 Thread and 2 Event, its
    Thread handles opened at `LightingPlugin built`, 1, `PhysicsPlugin built`, 3, and `started`, 3,
    with `ended` giving 3 back and the driver's steps netting 1, so some four Thread handles an app
    belong to threads started at those steps, the engine's or the driver's started lazily there,
    which ended and whose handles stay, the process's threads standing at 23 to 25 throughout; the
    count is read after a full collection with its finalizers and after the threads ended, so the
    handles are held by something that lives, a `Thread` object kept after its thread's end or a
    handle duplicated and not closed. The page lost the test's name and the series of handles after
    every ten apps under its limit, the handles-by-step lines of four apps standing first in the
    message. The mend is one thing, with its words. The threads started at the lighting plugin's
    building and at startup, and whatever holds them after they end, are read on the working machine
    with the census at each step, the reference kept to an ended thread dropped where one is and the
    handle closed where one is duplicated, so a closed app keeps no Thread handle but the driver's;
    the allowance stands, since what it absorbed was a climb and not a swing; and the handle
    failure's message puts the series of handles after every ten apps and the kinds kept first, the
    followed apps' steps after and cut to the limit. Settled at three green Windows suites in a row
    after the mend. Read at `2e4187e3` so far as the working machine can, which reads no Thread
    handles: on Linux the followed apps' threads start and end by name at the steps said, the
    physics plugin's three being Bepu's dispatcher workers, disposed at the close, and nothing
    starts at the lighting plugin or at startup on 32 cores, so the handles Windows keeps from those
    steps are most likely threads a four-core runner's pool or lavapipe starts there; a heap census
    grows no Thread object across eighty apps, so nothing managed holds an ended thread, and the
    runtime's bookkeeping, the pool or a native library does. On Windows the census names each kept
    handle's thread, ended or running, with the step that opened it, and the next Windows suite says
    whose they are; the mend follows it. Seen beside it, `PhysicsPlugin.cs:47-58` warms the physics
    world up in two identical blocks one after the other since `118b4adc`, the second returning at
    the warm-up's once-a-process guard and costing a pool item an app, and the second block goes
    with the mend. On the push of `32ce6f4e`, 2026-10-18, the Windows suite failed the plain test's
    handles again, 2,423 at the 91st app where 2,147 stood at the twentieth and 2,418 were allowed,
    the handles after every ten apps climbing 2,147, 2,181, 2,218, 2,261, 2,298, 2,335, 2,376 and
    2,416, some 3.8 an app, with the threads flat at 24 to 26, and the census named the kept
    handles. Every followed app kept four Thread handles of unnamed threads that had ended, opened
    at the device's making, where nine unnamed threads start, and the twenty-first kept one more,
    the runtime's background collector's thread, running, opened at the lighting plugin and the
    process's once. The nine are lavapipe's, which starts its rasterizer and compute threads with a
    device and names none, where on Linux the device's making starts two named pool workers and the
    GPU driver's three, and the handles of four of them stay open after the threads end, a handle a
    thread, which is lavapipe's Windows build leaving its threads' handles unclosed, Mesa's leak and
    not the engine's. The allowance counts the driver's thread handles by the net of the handle
    counts across the driver's steps, which came to 1 where the census shows 4 kept, since the
    engine's own threads' closings fall in those steps too. The mend is one thing, with the warm-up.
    The allowance's count of the driver's thread handles an app is taken from the census, the kept
    Thread handles of ended unnamed threads opened at the driver's steps in the followed apps, the
    most over them, with the comment naming lavapipe's Windows build and the four; the allowance of
    200 stands; and the second warm-up block of `PhysicsPlugin.cs` goes. Settled at three green
    Windows suites in a row after the mend. Mended at `1398ed24`, the driver's thread handles an app
    counted from the census, the comment naming lavapipe's four, the allowance standing and the
    second warm-up gone; settled at three green Windows suites in a row after it. On the pack run of
    `32ce6f4e` the same day the Windows suite passed the handles by its twentieth baseline's chance,
    before the mend, a pass that counts for nothing toward the three. On the push of `1398ed24`,
    2026-10-18, the handle bound held on Windows with the census's allowance, both tests, the suite
    red on the twin's memory bound (Verdict 64), so no green suite yet for the three. On the push of
    `5a2d7df3`, 2026-10-18, the Windows suite passed whole, the handles within the census's
    allowance, the first green Windows suite of three. Settled on the same three Windows suites, the
    push and pack runs of `5a2d7df3` and the push run of `a12206af`, 2026-10-18, the handles within
    the census's allowance on each.

69. **The leak test's heap floor on macOS rose 6 MB from one trough among flat readings, and the
    figure carries the collector's leavings.** On the pack run of `32ce6f4e`, 2026-10-18, the macOS
    suite failed the plain hundred-app test on its heap floor's rise, 6.02 MB where 5 is allowed,
    the heap after every ten apps reading 79.19 at the twentieth, 77.22, 70.81 at the fortieth,
    77.18, 77.25, 76.90, 76.88, 76.83 and 76.85 at the hundredth, so the least of the first half
    fell in the one trough and the least of the second on the plateau, the heap otherwise flat and
    2.3 MB lower at the end than at the twentieth, the resident memory falling from 840 MB to 768
    and no Vulkan object alive; the suites of the day before this one passed it. The figure is
    `GC.GetTotalMemory(true)` after a collection (`AppLeakTests.cs:218`), the bytes the heap holds
    with the fragments the collector left where it did not compact, which on macOS swing by 6 MB
    between collections of the same live set, as the floor's own comment says a census found 0.25 MB
    more alive across such a swing, and the least of each half guards against a crest and a trough
    of two readings and not against a trough alone. The mend is one thing. The heap is read as the
    bytes a full blocking collection promoted, `GC.GetGCMemoryInfo(GCKind.FullBlocking)` after the
    forced collection, the live set without the leavings, as BevyCSharp's soak reads its live
    figure, and the floor's rise is judged on it with the bound of 5 kept until the series says
    otherwise (N 3.5), the reason in the test's comment. Settled at three green macOS suites in a
    row after the mend. Mended at `1398ed24`, the heap read as the bytes a full blocking collection
    promoted, the floor judged on it with its bound of 5, 13.3 MB flat from the twentieth app on the
    working machine; settled at three green macOS suites in a row after it. On the push of
    `1398ed24`, 2026-10-18, the macOS suite passed whole, the first green macOS suite after the
    mend, one of three. On the pack run of `1398ed24` the same day the macOS suite passed whole
    again, the second of three. On the push of `5a2d7df3` the same day the leak tests held on macOS
    and the suite was red on another test (Verdict 70), so the row stands at two green macOS suites
    until the next. Settled on the push and pack runs of `1398ed24` and the pack run of `5a2d7df3`,
    2026-10-18, three green macOS suites with the live heap floor, the push of `5a2d7df3` between
    them red on another test with the leak tests holding.

70. **A script generation whose system threw a type of its own stayed alive after twenty collections
    on macOS, once.** On the push of `5a2d7df3`, 2026-10-18, the macOS suite failed one test of
    1,649, `ScriptGenerationTests`' unload of the first generation after the script is compiled
    again (`ScriptGenerationTests.cs:68`), the load context's weak reference alive after twenty
    rounds of a collection and its finalizers, where the test had passed on every suite of the day
    and on Linux in this run, the file unchanged since 2026-10-12. The generation's types are
    reachable from something that lives past the recompile for a while and not always, which a weak
    reference and twenty collections cannot shake, and three holders of a thrown exception fit that,
    the thread that threw it, which keeps its last thrown exception until it throws again or ends,
    the logger's entries where an entry holds the exception object and not its text until the queue
    drains, and the schedule's catch where it keeps the exception past the frame; a static of the
    engine would hold it on every run and does not. The mend is one thing, with its reading. The
    holders of a system's exception are read, the schedule's catch, the logger's entries and the
    thread's last thrown exception, and whichever keeps the object past the frame that logged it
    drops it, the logger keeping text, the schedule keeping nothing, and where the thread's own hold
    is what remains, the test ends the hold before it judges, a frame or a thrown and caught
    exception of the test's own on the schedule's threads, with the reason in its comment; and where
    the generation still lives after the collections, the test says what holds it by name where it
    can. Settled at three green macOS suites in a row after the mend. Read and mended at `a12206af`.
    The schedule's catch keeps nothing and the engine's logger keeps text, and a probe showed the
    thread's last thrown exception no holder on this runtime once the catching method has returned;
    the one holder found was `ScheduleTests`' listener on the process's `Engine.Schedule` logger,
    which kept the exception object of every line logged while its test ran in a collection beside
    the unload test's, and a scratch provider of that shape kept a generation alive on Linux, a
    cause that fits and is not proven the macOS one. The listener keeps the type's name, and the
    unload test names what may hold a generation that outlives its collections, the app's systems in
    its load context, the process's list of generated behaviors and the schedule logger's providers.
    Settled at three green macOS suites in a row after it. On the push of `a12206af`, 2026-10-18,
    the macOS suite passed whole with the mend in, the first green macOS suite of three. On the pack
    run of `a12206af` the same day the macOS suite passed whole again, the second of three. Settled
    on the push and pack runs of `a12206af` and the push run of `ad5d3692`, 2026-10-19, three green
    macOS suites in a row with the mend.

71. **The scene field's descriptor pool counts its storage images by hand, so the faces image made
    the resolve pass outgrow it and the macOS suite failed 75 tests with nothing wrong in the
    frames.** On the push run of `257006c7`, 2026-10-19, the macOS suite failed 75 of 1,688 tests in
    12 minutes 46 seconds, 1,582 passing, every failure with the render system throwing
    `ErrorOutOfPoolMemory` from the renderer in stage Last, so each test's own assertion failed on a
    frame not drawn and N 3.7 failed it a second time on the logged error, and three reference frame
    tests found the layer's error that a descriptor pool was destroyed while a command buffer still
    used it. The page counts 2,660 of the layer's lines naming binding 11 of a layout's first set
    with a count of one and 380 naming where storage images are found in the pool, and `a52b125b`
    gave the resolve pass two storage images for the faces at binding 11 (`field_resolve.slang:50`),
    while the scene field's pool is sized by hand, twelve storage buffers, six storage images and
    one uniform buffer (`GraphicsDevice.SceneField.cs:657`), as the illumination's, the cube
    filter's and the skinning's pools are; MoltenVK refuses an allocation past a pool's counts, as
    the specification allows, where the drivers the suite ran on before, Mesa's and the development
    machine's, allocate past them, and the layer's line on an allocation past a pool's counts is a
    warning, which the tests' list of validation errors does not hold, so the fault passed every
    check before the push. Two things follow. Every descriptor pool's counts are taken from the
    layouts it serves, counted by type from their bindings times the sets allocated, as the compute
    helper's already are (`Compute.cs:170`), so no pool is sized by hand, and a test under the layer
    allocates every layout from its pool and reads the layer's lines, warnings among them, for an
    allocation past a pool's counts, so the fault fails where the layer runs and not on one driver;
    and the renderer's path out of a failed allocation does not destroy a pool a submitted command
    buffer still uses, waiting for the device or leaving the pool to the frame's end. The pack run's
    macOS suite of the same commit failed the same 75 tests in 17 minutes 4 seconds. Mended at
    `33fd0804` the same day as the head says, every pool counted from its layouts by one helper, the
    layer's five lines on an allocation past a pool counted as errors, the renderer waiting for the
    device when a frame throws, and two tests, one of which failed before the mend under lavapipe
    and the layer. On the pack run of `257006c7` the Windows suite's test step took 115 minutes
    where the push run's took 60 for the same command at the same commit, its page counting 6,944 of
    the layer's lines on binding 11, and one part of the suite ended at its 60-minute limit in the
    hundred-app leak test at its 51st app, an hour in, with every test that ran passing; whether
    that is the fault's second face, the layer's lines slowing every allocation under it, or a slow
    runner, the pack run of `33fd0804`'s Windows suite says, and if it is as slow with no line to
    log, the leak test's time on Windows is a verdict of its own. The push run of `33fd0804` passed
    its macOS suite in 25 minutes on 2026-10-19 at 19:35, the first of three, and the pack run of
    `33fd0804` passed it in 42 minutes at 20:03, the second. Settled on the macOS suites of the push
    run and the pack run of `33fd0804` and the push run of `fa607cb3`, 2026-10-19, three green in a
    row after the mend, in 25, 42 and 21 minutes.

72. **The Windows suite runs in 45 to 48 minutes when it runs through, and twice today a run of it
    went past an hour on the same code, once cut by the job's limit.** On the push run of
    `33fd0804`, 2026-10-19, the Windows job's test step (`build/test.py --no-build --timeout-minutes
    60`) ran from 19:12 to 20:50 and was cut when the job reached its 100 minutes, writing no
    summary, while the pack run's Windows job of the same commit ran the same step in 45 minutes 22
    seconds with 1,674 passed and the whole job, the thirteen games played, in 75; the push run of
    `257006c7` ran it in 48 minutes 3 seconds, and the pack run of `257006c7` took 115 minutes with
    one part lost at its 60-minute limit in the hundred-app leak test at its 51st app, which Verdict
    71 took for the pool fault's face. With no fault left to log, a part still ran an hour or more
    on one runner of two, so the time is a term of its own, a test that hangs or crawls on some
    Windows runners, the hundred-app leak tests the first to read since the one part that was seen
    lost stood in them. The mend is read before it is chosen: the cut job uploaded its artifacts, so
    the part's `output.txt` says which test ran when the hour ran out and what each test before it
    took, read against the pack run's for the same tests; then the slow or hanging test is mended
    where it is, a wait that has no bound given one, a hundred apps made fewer or cheaper if their
    cost is the runner's, and the test script says in its summary, when a part is lost, the test it
    was in and the time each test took, so the next such run reads itself. The job's limits stay as
    measured, 100 on a push and 180 on the pack run, since the suite that runs through fits them.
    The push run of `fa607cb3` ran the suite in 32 minutes 18 seconds, 1,674 passed, the whole job
    in 40, on 2026-10-19 at 22:02. Read by the coder by 02:52 on 2026-10-20 from the cut job's
    artifacts, no test hanging or crawling. The whole suite ran its 60 minutes to the test script's
    limit, finished 1,705 tests and stood at the 30th app of the third hundred-app leak test, 1.8
    seconds an app; the script then ran everything again in parts, the eleven small parts in eight
    minutes and the Rendering part for 28 until the job's 100 ran out midway through the global
    illumination tests, at a test of 49 seconds in the whole run; the createdump came from the
    cancellation, and no page was written. The hour was lavapipe drawing global illumination, 17
    minutes of the global illumination tests, 12 of the glow lights, four each of the glow penumbra,
    the glow pillar and the cascade edge, and three of the hundred-app leak tests, the slowest tests
    four minutes each, the emissive block shut in a room and the illumination held still as the
    camera slides. The time follows the runner's processor, every run cold, 32 minutes on an EPYC
    9V45, 45 on a 9V74, 48 on a Xeon 8573C and over 60 on an EPYC 7763 twice, the 115 under Verdict
    71's fault among them. Cold because no Windows runner has a Mesa shader cache, since the restore
    step finds none, the repository's 60 caches hold none for Windows and one for every Linux
    processor, and the save step after the suite says the path does not exist, so lavapipe from
    mesa-dist-win never writes the directory the job names and every app compiles its shaders anew,
    3.7 seconds an app against 0.84 on a host by the workflow's own remark; the mend of `9a3e2e2a`,
    which saved the cache after a failed job, had nothing to save on Windows, unseen until this
    reading. The reviewer takes two mends, a commit each under N 3.5. The Windows job sets the cache
    enabled beside the cache directory, and a step after the suite says how many files Mesa wrote
    there, so the next run reads whether lavapipe on Windows keeps a cache at all, and if it does,
    every suite after the first on a processor runs warm, about a third of its time; and the test
    script, when the whole suite is lost to its time, takes the outcomes of the tests that finished
    from the whole run's output and runs again only the parts holding tests without a result, its
    page saying, for a lost process, the test it was in and the times of its slowest tests, so a
    runner that finished 1,705 of 1,706 does not start over. If the step reads no cache, the Windows
    suite runs in its parts from the start, about 62 minutes on a 7763 under the job's 100, as the
    third mend. Mended at `81007b4e` and `30993e84` on 2026-10-20 as the head says, the cache asked
    for by name and said after the suite, and the lost whole run's ended tests counted with only the
    parts holding tests without a result run again; the build run of `30993e84` is the first suite
    under them. On the pack run of `193c68b0`, under the old script, the Windows suite ran on an
    EPYC 7763 cold, no cache found, the whole lost at 60 minutes with the bounce rooms' test the
    last to end at 59 minutes 20 seconds, the parts then 62 minutes, 121 minutes 49 seconds in all,
    the Rendering part alone some 50 of them, 1,704 ms a leak app; so on a 7763 the second mend's
    rerun of the lost part alone still passes a push's 100 minutes only with the cache warm or the
    third mend in, which the runs under the mends read. The first suite under the mends, the pack
    run of `448aa3fc` on 2026-10-20, ran on an EPYC 7763 again, cold, and read two things. Lavapipe
    wrote no shader cache though asked for by name, the step after the suite warning so and the save
    finding no folder, so Mesa on Windows from that build keeps no cache at all and the first mend's
    hope is closed, which answers the reading. The whole run was lost at its hour with 1,302 passed,
    after the bounce rooms' rays-past-their-reach test again, the slowest tests that ended 4 minutes
    24 seconds, 4 minutes 15 and 4 minutes 12, and the rerun took only the parts holding tests
    without a result as the second mend has it, but those were thirteen of the fifteen, since the
    whole run's order leaves a test or more of nearly every part for its last third, the Rendering
    part alone 52 minutes 16 seconds, so the suite took 121 minutes 5 seconds in all, 1,667 passed
    and none failed, inside the pack run's 180 and not a push's 100; and the step ended with exit 1
    for the lost whole run though every test had a result and none failed, so the pack job was
    skipped on a green suite. So the third mend is taken, and one more: the Windows suite runs in
    its parts from the start, about 62 minutes cold on a 7763 and less on the others, with no whole
    run to lose, the cache's variables kept and the step after the suite kept as a reading, the
    reason in the job's comment naming this run; and the test script, when every test has a result
    and none failed, ends with exit 0 and says the whole run's loss as a notice with its figures,
    not as a failure, held by a test on the stand-in's lost mode with every test passing. Two
    commits under N 3.5, before the corridor's. The settling condition stands as written, three
    Windows suites in a row that end green with their summary inside the job's limit, each recorded
    with its processor and time. Mended at `14890db6` and `8e80cf46` on 2026-10-20 as the head says,
    the parts from the start held to 70 minutes each and the exit 0 on a run lost to its time alone
    with every test green; the pack run started on `8e80cf46` is the first suite under them. The
    push run of `7eb7079b`, under the first two mends and before the last two, ran its Windows suite
    whole on an EPYC 9V74, cold, in 43 minutes 11 seconds, 1,682 passed, the job in 55, lavapipe
    writing no cache there either, which confirms the reading on a second processor; the count
    toward settling starts at the pack run of `9f9180b7`, the first under all four. The push run of
    `9f9180b7` on 2026-10-20, the first under all four mends, ran its Windows suite in its parts
    from the start on an EPYC 9V45, cold, in 33 minutes 22 seconds, 1,683 passed, exit 0, the job in
    40 minutes, the first of three. The pack run of `9f9180b7` on 2026-10-20 ran its Windows suite
    in its parts on an EPYC 7763, cold, in 66 minutes 23 seconds, 1,683 passed, exit 0, the job in
    99 minutes with the thirteen games, the second of three, and the slow runner's figure for the
    parts, inside a push's 100 by a third. The push run of `82dd08f2` on 2026-10-20 ran its Windows
    suite in its parts on a Xeon 8370C, cold, in 58 minutes 26 seconds, 1,683 passed, exit 0, the
    third of three; the cache steps went at `f4c62380`, since no Windows build of Mesa keeps one.
    Settled on the runs of `9f9180b7` and `82dd08f2` on 2026-10-20, three green Windows suites in a
    row under the mends, 33, 66 and 58 minutes on three processors.

73. **A scene file written again with a later time was not read anew on the macOS runner.** On the
    pack run of `d2de2766`, 2026-10-19 at 23:46, the macOS suite failed
    `SceneRefTests.Copies_Of_A_File_Share_Its_Mesh_Until_The_File_Is_Written_Again` at its line 91,
    the third copy's positions the first mesh's where a new version of the file is read anew, 1,661
    passed and 32 skipped in 16 minutes 2 seconds, while the push run's macOS suite of the same
    commit passed it, so it shows on one runner of two. The test saves a scene, places two copies,
    which share one mesh, sets the file's write time five seconds later and places a third, which
    should read the file anew; the scene reference keeps each file's write time and reads the file
    again where the time it reads is later (`SceneRef.cs:90, 124`). The mend is read before it is
    chosen: whether the time the runner's file system gave back after the set was the one set,
    whether the reference read the time before the set in a frame of its own or caches it for a
    while, and whether a copy placed in the same step as the set is read before the step reads
    times; then the test or the reference mended where the reading points, the test waiting for the
    step that reads times or the reference reading the time at each placing, with the reason in the
    remark. Read and mended at `ed089e37` on 2026-10-20, the reload check's twice-a-second timer
    respawning the first copies in the step after the write where it fell there, the test holding
    the third against the first's mesh from before the write and asserting the file system gives
    back the time set. Settled at three green macOS suites in a row after the mend. The macOS suites
    of the pack run of `80625742` at 01:24 and of the build run and the pack run of `193c68b0` at
    02:45 and 03:00 on 2026-10-20 passed, three in a row. Settled on the runs of `80625742` and
    `193c68b0` on 2026-10-20.

74. **Manor's soak holds its entities at their least to 421, and a walk on the runner reached 422.**
    On the pack run of `80625742`, 2026-10-20 at 01:43, the games job's soak of the thirteen games
    failed Manor alone, its entities climbed from 374 to 422 at its least past its bound of 421, its
    resident memory 544 MB at the first reading kept and 572 at the last, the other twelve passing.
    Verdict 65 set Manor's four figures from a whole walk measured on 2026-10-17 as the most a walk
    holds, and a walk on this runner reached one past the entities' figure, so the figure is the
    most of the walks measured then and not of every walk, the level's streaming letting a walk that
    goes further hold more. The mend is one thing, under N 3.5 and N 3.6: Manor's entity figure
    measured again over whole walks on the development machine's GPU and on lavapipe at the paces
    the runner takes, the most of them written into the table with the command and the walks that
    measured it, with the room a measured spread gives, or the soak's course made to walk the level
    whole before the readings judged begin, so the least holds the whole world; the soak's output
    says which rooms a walk entered when a figure climbs, so the next such run reads itself. Mended
    at `b03017f2` on 2026-10-20, the entities' figure 539 from whole walks at three paces on two
    devices, bounded at 602, and the soak saying where a walk stood when a figure climbs; Manor's
    images stand at 92 in the table where those walks reached 94, under the slack's 98, watched.
    Settled when the games job's soak passes on three pack runs in a row. The games job of the pack
    run of `193c68b0` passed at 03:24 on 2026-10-20, the first of three. The games job of the pack
    run of `448aa3fc` passed at 05:37 on 2026-10-20, the second of three. The games job of the pack
    run of `9f9180b7` passed at 07:58 on 2026-10-20, three in a row. Settled on the pack runs of
    `193c68b0`, `448aa3fc` and `9f9180b7` on 2026-10-20.

75. **The hundred-app leak test's bound on resident growth takes its base from one reading at the
    twentieth app, and a trough there fails a flat series.** On the pack run of `193c68b0`,
    2026-10-20 at 03:29, the Windows suite's Core part failed the offscreen app leak test, the
    private bytes committed after every ten apps in MB 156.5, 80.0, 159.2, 164.0, 163.9, 164.1,
    166.6, 168.7, 165.9 and 169.6, a slow climb but for the twentieth's trough, where the process
    had given memory back between apps, so the last less the twentieth read 89.6 against the bound
    of 50 while the last less the tenth read 13.1 and the last less the thirtieth 10.4, and the
    heap's live objects held 14.3 to 14.8 MB throughout; the regions by kind said the same, private
    data 69.1 MB at the twentieth to 158.7 at the last and every other kind unchanged, the apps
    1,704 ms each on the 7763; no census was taken on Windows, so the twentieth's reading stood as
    the base as read. The figures were corrected by the coder from the run's results file on
    2026-10-20, the reviewer having read the region's 158.7 for the last reading. The fault is the
    bound's form, one reading as the base. The mend is one thing, under N 3.5: the base taken as the
    greatest of the readings at the tenth, twentieth and thirtieth app, or the growth read as the
    slope over the readings after the twentieth, the series printed as it is and the remark saying
    why one reading cannot be the base, with a test of the check on this run's series passing and on
    a climbing one failing. It gates the package, so it comes before (c)'s mend. Mended at
    `f72bda5d` on 2026-10-20 in the first form, as the head says. Settles at three Windows suites in
    a row with the test green after the mend. Settled on the Windows suites of `b3922179`,
    `ac77ef83`, `086d9a00` and `afbe81cf` on 2026-10-20, four in a row green after the mend, 1 hour
    21 minutes, 1 hour 25, 53 minutes and 44 minutes 4 seconds.

76. **Span's drive on macOS found the load not crossing the truss, with no joint broken and no force
    read.** On the pack run of `b4b798d4`, 2026-10-20 at 13:05, the macOS job's step playing every
    game from the package failed on Span: the load did not cross the truss whole, the build at 17
    planks, no ropes, 27 joints, none broken, the most force 0 N at a strength of 7,000, the load's
    place minus 8 by 0.6 by 0, its starting place, the log ending in the renderer's resize to 480 by
    270; the macOS suite passed, 1,677, the Linux games job was still running, and the drive had
    passed on the development machine's card and on lavapipe before the commit. A force of 0 N on
    every joint with the load at its start says the load never rolled or the world never stepped
    before the drive read: the reading comes first from the job's artifacts, the drive's commands
    and the status between them, whether the roll's impulse went to a body not yet awake or before
    the first step on that runner, whether the drive's waits are frames where the macOS runner's
    frames came slower, and whether the Linux games job passed the same drive; then the mend where
    it points, the drive waiting on the load's motion and not a count, or the game applying its push
    once the world runs, with the reason in the remark, one commit. The pack job was skipped on it,
    so the day's package waits. Read and mended at `40283221` on 2026-10-20 as the head says, the
    first read two frames after the go; the Linux games job had passed the same drive on that run.
    Settled when the games' step passes Span on macOS on the next pack run and two after. The macOS
    job of the pack run of `2cc516fc` passed at 15:24 on 2026-10-20 with every game played, Span
    among them, the first of three. The macOS job of the pack run of `b19f09dc` passed at 17:31 on
    2026-10-20 with every game played, the second of three. The macOS job of the pack run of
    `b3922179` passed at 19:56 on 2026-10-20 with every game played, three in a row. Settled on the
    pack runs of `2cc516fc`, `b19f09dc` and `b3922179` on 2026-10-20.

77. **The siege's drive on the Linux games job found the banner standing after eight bombs, where it
    fell after three on the development machine and on its lavapipe.** On the pack run of
    `2cc516fc`, 2026-10-20 at 15:34, the games job's step building and playing the siege from the
    package failed, the status reading 9 shots, 157 of 190 blocks and 170 of 342 welds broken, the
    most force 7,994 N at a strength of 8,000, the banner at 4.0 with 18.3 left, the arm at 222 and
    no shot, the log ending in the screens turning from aim to swing to flight and back; the macOS
    games step had passed the same drive at 15:24, and the drive had passed on the development
    machine's card and on its lavapipe before the commit. So the castle came down but the banner did
    not reach the trigger box that ends the siege within the drive's eight bombs on that runner,
    where the physics' float path on another processor, the four cores' frame pace with each frame
    one step, or rubble holding the banner up may differ; the reading comes first from the job's
    artifacts, the banner's place and what held it, against the machine's runs, and whether the
    result differs run to run on the same machine; then the mend where it points, the drive's
    assertion on what the siege is, the welds broken and the blocks down past a share, with the
    banner's fall as the game's own end and not the drive's, or the trigger box widened to where a
    fallen banner lands, with the reason in the remark, one commit. The pack job was skipped on it,
    so the day's package waits. Read and mended at `62efc173` on 2026-10-20 as the head says, the
    trigger raised to the walls' top; the result had differed run to run on one machine from the
    physics' threads, and the pack run started on the mend is the first of three. Settled when the
    games job passes the siege on three pack runs in a row. The games job of the pack run of
    `b19f09dc` passed the siege at 16:23 on 2026-10-20, the first of three. The games job of the
    pack run of `b3922179` passed the siege at 20:30 on 2026-10-20, the second of three. Settled on
    the games job of the pack run of `f78b8c58` on 2026-10-21, the third in a row playing the siege
    through after the mend.

78. **The shuffled pass's own test fails under the shuffled pass, the stand-in's plain run
    inheriting the seed.** On the build run of `6a289059`, 2026-10-20 at 16:29, the Linux job's
    plain suite passed, 1,722, and the suite run again shuffled by the seed 20261020 failed one
    test, the script's test that a suite run again shuffled names the seed where it fails: its
    stand-in's plain run read 79 passed and 1 failed where 80 and none were expected, since the
    shuffled pass sets the seed in the environment of every process it runs, the test's inner script
    among them, so the stand-in's shuffle-fails mode failed in the inner plain run as in the inner
    shuffled one; on the development machine the test ran under no seed and passed. The mend is one
    thing, under N 3.5: the script clears the seed from the environment of the plain run it starts,
    so a suite run under a seed from outside still runs its plain pass plain, and the test sets and
    clears the variable for its own inner runs as it means them, with a second case running the
    test's own script under a seed from outside; the remark says why. One commit, before the pack
    run, whose Linux suite would fail the same way. Mended at `b19f09dc` on 2026-10-20 as the head
    says; the pack run started on it is the check. Settled when the Linux job passes both passes on
    the next build run. The Linux job of the pack run of `b19f09dc` passed both passes on 2026-10-20
    at 17:37. Settled on the pack run of `b19f09dc` on 2026-10-20.

79. **Tones' heap climbs through the soak, its least reading past the bound.** On the pack run of
    `b19f09dc`, 2026-10-20 at 18:23, the games job's soak failed on Tones alone, its heap's least
    climbing from 7,716,304 bytes to 13,944,080 past the bound of 10,970,901, the first half's least
    with the slack, while its resident memory held at 240 MB at the first reading kept and 247 at
    the last; the soak had passed its 28 turns on the development machine before the commit. The
    managed heap's least climbing while the resident holds says something the game keeps across
    turns grows, the recording's samples kept from turn to turn, a sound made from each recording's
    wave and never unloaded, the drums' waves loaded on each hit, or the processors' buffers made
    anew on each reorder; the reading comes first on the machine with the soak's turns and the
    heap's readings between them, then the mend where it points, the game freeing what a turn made
    before the next or the engine freeing a wave's or a sound's managed side where it does not, with
    the reason in the remark, one commit. The pack job was skipped on it, so the day's package
    waits. Read and mended at `b3922179` on 2026-10-20 as the head says, a recording bounded to
    fifteen seconds and held once; the pack run started on it is the first of three. Settled when
    the games job's soak passes Tones on three pack runs in a row. The games job of the pack run of
    `b3922179` passed the soak at 20:30 on 2026-10-20, the first of three. The games job of the pack
    run of `f78b8c58` on 2026-10-21 played Tones through, the second of three. Settled on the pack
    run of `b5db84f3` on 2026-10-21, Tones played through the third time after the mend.

80. **The pack run's Windows suite loses its games step to the feature test's audio zone, whose
    music had played 0.6 seconds where its rule asks for more than one.** On the pack run of
    `f78b8c58`, 39506383679, on 2026-10-21, the Windows suite passed its tests, 1,762 in 36 minutes
    47 seconds, and its step playing every game from the package failed on the feature test: the
    audio zone's status read the device at 48,000 hertz in 2 channels, the wave written and read
    back, the music 0.6 s played, the stream asked for samples 55 times, the coin played once, and
    the mix's left 2.60 and right 1.57 with the drone alone, so the spatial play of item 58 was
    heard from its side, and no audio ok, since the zone's rule is the wave, the music over one
    second, the stream asked and the sides, and the music fell short. The zone counts frames, Alone
    plus 60, and then judges by seconds of music, which the music stream feeds from the frame; on a
    runner whose frames come slowly after the suite's parts, 60 frames feed less than a second,
    where the Linux and macOS suites and the machine feed more. The fault is a bound in seconds on a
    wait counted in frames. The mend is one thing: the zone's status says the sounds are not heard
    yet until the music has played over a second and the other three hold, within a bound of frames
    after which it says what fell short, and the drive reads the status until it is ok or the bound,
    as the drive reads the other zones that take time, so a slow runner waits and a broken one still
    fails. The pack job did not run, so no package comes from this run; the next pack run with
    publish on follows the mend. Mended at `55141a42` on 2026-10-21 in that form, the drive from the
    package giving audio ok at 3.6 s of music, as the head says. Settles on the next pack run's
    Windows games step green. Settled on the pack run of `b5db84f3` on 2026-10-21, the Windows games
    step green with the audio zone's ok.

81. **The pack run's games job loses its soak to Sumo and the feature test, which could not be
    opened.** On the pack run of `f78b8c58`, 39506383679, on 2026-10-21, the games job played every
    game from the package, the siege and Tones among them, and then its soak step failed: Sumo and
    the feature test could not be played through, `build/open-game.sh` ending with 1 for each, so
    each had 0 readings, too few to judge, while the other games soaked. The log, read at 01:54 once
    the run ended: the step starts all nineteen soaks at once on the runner's four cores, and the
    open script waits a minute for each program to serve; Sumo and the feature test did not serve
    within the minute, their processes still running, Sumo's log at its startup stage complete at 18
    seconds and the feature test's at 18 with its robot model parsed at 20, the other seventeen
    serving and soaking their 120 seconds. The fault is a bound of a minute on a wait that nineteen
    programs starting at once on four cores stretch past it, the feature test and the six games of
    the week having joined the list since the soak last ran green with fewer. The mend is one thing:
    the soak step starts the programs a few seconds apart and gives the open script a longer wait
    for the soak, five minutes, through a variable the script reads, the step's log saying how long
    each took to serve; a program that never serves still fails. It gates the package, so it comes
    before the glow lights' bound and before the next pack run. Mended at `b5db84f3` on 2026-10-21
    in that form, as the head says. Settles on the next pack run's soak green. Settled on the pack
    run of `b5db84f3` on 2026-10-21, the soak of all nineteen through.

82. **The Windows suite of the push of `dc487681` fails the readback pool's test, five buffers kept
    where the test allows four.** On the build run 39534320588 on 2026-10-21, the Linux and macOS
    suites passed and the Windows suite, 1,769 passed in 37 minutes 44 seconds, failed one test, the
    readback asked and unloaded every frame for a hundred frames keeping a few buffers and no more:
    the device held 5 buffers over where it began, where the test's bound is 4 because the device
    holds no buffer a frame more than the pool keeps, and on the machine the pool had stood at 2
    with the device within 4. The pool keeps a few staging buffers a size and returns one released
    before its batch finished when the batch retires, so how many stand at a moment depends on how
    many batches are in flight, which the Windows runner's lavapipe drew out by a frame. The fault
    is a bound of 4 read off one machine's timing, under N 3.5. The mend is one thing: the pool's
    cap is a number the pool itself keeps, the frames in flight and one, with a release past it
    freeing the buffer, and the test holds the device's count to that cap read from the pool rather
    than a figure, so the bound follows the pool wherever it runs and a leak still fails it. One
    commit, after item 69's second part and before the pack run, since the pack job needs the
    Windows suite green. The cause, read before the mend: the test's device count held the last
    dispatch's uniform buffer too, kept until the next dispatch or a wait for compute retires it,
    the fifth beside a pool at four, which the machine's pool at two hid. Mended at `2f57b341` on
    2026-10-21 in that form, the test waiting for compute before both counts, as the head says. The
    mend did not hold: the Windows suite of `2f57b341` on 2026-10-21 found 5 buffers over the base
    against the cap of 4 again, the test waiting for compute before both counts since the mend, so
    the fifth is not the dispatch's uniform buffer alone, or not it on the runner. The second mend,
    under N 3.5: the test names the buffers over the base in its failure, each by its size and
    usage, so the runner says what it holds; the pool's count is asserted against the pool's own
    counter rather than the device's total, which holds the device's other transients; and the leak
    is caught by the device's count returning to the base after a wait for idle at the end, which
    depends on nothing in flight. One commit, before the next pack run. The pack run 39546802297 of
    `2f57b341`, started before its Windows suite was read, ended red on this test alone, 5 over the
    base of 4 again, 1,776 passed in 75 minutes 33 seconds, read at 08:33, the pack job skipped. The
    cause, read shortly before 08:56 by the census the mended test prints: over the start the device
    held two uniform buffers of the frame arena and two staging buffers of the pool, each frame slot
    making its uniform buffer the first time a frame in it holds a dispatch, which the base frame
    did not, so the delta was the pool's count plus the arenas' two, 4 on the machine against the
    bound of 4 and 5 on the runner with the pool at 3; the arenas' working set, kept, not in flight
    and no leak, which no wait takes away. So the mend draws a frame holding a dispatch in every
    slot before the base, the frames in flight and one, then waits for idle and for compute before
    the count and the census, asserts the pool against its own cap and the device's count less the
    pool's back at the base exactly after the loop, naming each line of the census over the start in
    the failure; 3 of 3 on the RTX 4070 and 5 of 5 under lavapipe with the layer, a planted leak of
    every released buffer failing with its line named. The engine is unchanged, the test file alone.
    Mended again at `ec87f64b` on 2026-10-21 in that form, the test file alone; settles on the
    Windows suites of its push and of the pack run 39561720109 started at 09:24. The push's Windows
    suite passed on 2026-10-21, 1,793 in 75 minutes 30 seconds; the pack run's is awaited. Settled
    on the pack run 39561720109 of 2026-10-21, its Windows suite green in 118 minutes, the run
    publishing 5.2.213, read at 19:05 on 2026-10-23.

83. **The Linux suite shuffled by the day's seed fails the data assets' cost test, a scene with no
    reference costing 160 bytes more once some data files are known.** On the build run 39561719650
    of `ec87f64b` on 2026-10-21 the plain Linux suite passed, 1,802 passed in 24 minutes 38 seconds,
    and the suite shuffled by the seed 20261021 failed one test, 1,801 passed in 23 minutes 56
    seconds: the scene with no reference asking nothing of the data files and costing no more once
    some are known read 1,112 bytes known against the 952 expected, 160 over, where the same suite
    shuffled by 20261020 passed on the machine before the push and the pack run's Linux suite of the
    same head passed. So the cost the test reads moves with the order of the stage's systems, under
    N 3.7: either the reading takes in work of a system the shuffle placed between the test's two
    measurements, which the test should fence out or measure alone, or a system's place in the order
    changes what a scene with no reference costs, which is the engine's fault. Read first with
    `build/test.py --shuffle-seed 20261021` on the machine, the 160 bytes named by what allocates
    them; then the mend, the test's measurement made its own or the engine's order dependence
    removed, under N 3.5. One commit, before the next push. The cause, read before 10:55: neither
    the seed nor the table but the log file, since the scene read writes one debug line and the
    logger sends every line to the file once the file provider stands, which the process's first App
    opens and keeps; on the runner a test of another collection made that first app between this
    test's two counts, so the second count alone paid the file's write, 160 bytes, the runner's
    exact numbers given on the machine by initializing the provider between the counts and equal
    counts with it before both, the JIT ruled out under tiering, PGO and quick JIT off. The first of
    the two readings, then, the engine's table costing nothing. The mend, the test file alone under
    N 3.5: the test records the process's log sinks before the first count and after the second and,
    where they changed, takes the pair again from the same start up to three times, the file never
    closing so a second pair stands still; a cost planted in the table's path, a 100-byte array,
    still fails with 1,080 against 952, and the file begun inside the first pair passes on the
    second. Mended at `36cae0c2` on 2026-10-23 in that form, pushed at 19:12; settles on the Linux
    suites of its push and of the pack run after it. Settled on the Linux suites of the build run
    39993188056 of `36cae0c2`, shuffled by 20261023, and of the pack run 39992268286, read at 20:39
    on 2026-10-23.

84. **The Linux suite of the push of `4c3f01ef` fails the upgrading page's test, the 52 modeling
    names of item 59's cherry-picks having no Added entry.** On the build run 40005121932 on
    2026-10-23 the Linux suite, 1,875 passed in 25 minutes 19 seconds, failed the test that every
    name the public surface gained since 5.1 is on the upgrading page, naming the 52 edit mesh calls
    and types, while macOS passed; the test needs the history and so skipped in the cloud, and the
    reviewer pushed the two cherry-picks before the batch's commit carrying the entry. Mended at
    `91c8d363`, the example's commit adding the Modeling entry for the names, read on the machine
    with the suite green plain and shuffled; settles on the Linux suite of the push carrying it. The
    lesson is the reviewer's: a cherry-pick that adds public names is pushed only with the commit
    that carries its upgrading entry. Settled on the Linux suite of the build run 40012951798 of
    `a0e5964e`, green on 2026-10-23, read at 22:49.
