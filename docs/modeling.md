# Modeling

An `EditMesh` is a polygon mesh shaped by operations, for blocking out a level or a prop from code.
It starts empty, takes primitives, and is changed through a selection, each call acting on what is
selected:

```csharp
var room = LoadEditMesh();
EditMeshBox(room, new Vector3(8, 3, 6));
EditMeshSelectFacing(room, Vector3.UnitY);
EditMeshMove(room, new Vector3(0, 1, 0));      // the ceiling a meter higher
```

Each operation writes a line to the mesh's recipe, so those three calls are these three lines, and
the lines run from an empty mesh make it again:

```text
box 8 3 6
select +y
move 0 1 0
```

The recipe is the mesh's history, its undo and its file, as [The recipe](#the-recipe) shows, and
each section here gives an operation's line beside its call. The same words make the same mesh in
BevyCSharp's edit mesh.

A vertex is a point, and a face is a loop of three or more vertices, wound counterclockwise seen
from the side it faces. A face of any count is kept whole, so a box's side is one face of four
corners and a staircase's side is one face of many, and it is cut into triangles only when the mesh
is built into a model. Edges are read from the faces, each pair of corners that follow one another
round a face.

What a mesh holds is read from it:

<!-- compiled with:
var room = LoadEditMesh();
-->
```csharp
var info = GetEditMeshInfo(room);
Console.WriteLine($"{info.VertexCount} vertices, {info.EdgeCount} edges, {info.FaceCount} faces");
Console.WriteLine($"{info.Volume} cubic meters, {info.OpenEdges} open edges, closed {info.IsClosed}");
```

`Volume` is the space a closed mesh holds, positive when its faces face out. `OpenEdges` counts the
edges not shared by two faces running along them in opposite directions, the edge of a hole, an
edge three faces meet at, or one beside a face turned the wrong way, and `IsClosed` is true where
there are none, which a solid needs for its volume and its collider to mean anything.
`GetEditMeshVertex`, `GetEditMeshFace`, `GetEditMeshFaceNormal` and `GetEditMeshFaceSlot` read a
vertex and a face by index. An edit mesh is data on the CPU, so it is made and shaped before
`InitWindow` as well as after it, and `UnloadEditMesh` frees it.

## Primitives

Each primitive is added to what the mesh holds and selected, so the call after it acts on it. Each
is placed by its lowest corner, at the origin unless `at` says otherwise, or by its middle where
`centered` is set:

```csharp
var mesh = LoadEditMesh();
EditMeshBox(mesh, new Vector3(2, 3, 4));                                       // from (0, 0, 0) to (2, 3, 4)
EditMeshBox(mesh, Vector3.One, at: new Vector3(5, 0, 0));                      // its corner at (5, 0, 0)
EditMeshBox(mesh, Vector3.One, at: new Vector3(0, 5, 0), centered: true);      // its middle at (0, 5, 0)
```

A primitive's line ends in `at X Y Z` where it is placed elsewhere and `center` where it is placed
by its middle, as `box 1 1 1 at 0 5 0 center`. A measure is above zero, and one that is not is
refused with an `ArgumentOutOfRangeException`.

### Box

`EditMeshBox(mesh, size)`, or `box X Y Z`, is eight vertices and six faces of four corners, as wide
along X, as tall along Y and as deep along Z as `size` says. A box of two by three by four holds 24
cubic meters.

### Plane

`EditMeshPlane(mesh, width, depth)`, or `plane X Z`, is one face of four corners lying flat and
facing up, as wide along X and as deep along Z as given. Its four edges are open, as a floor's are
before walls meet it.

### Cylinder and cone

`EditMeshCylinder(mesh, radius, height, sides)`, or `cylinder RADIUS HEIGHT SIDES`, stands along Y,
its round side made of `sides` flat sides, and `EditMeshCone(mesh, radius, height, sides)`, or
`cone RADIUS HEIGHT SIDES`, stands on a base of as many sides with its point over the middle. Their
corners stand half a side round from the axes, so a cylinder of four sides is a box with its sides
facing along X and Z, and one of eight has a flat side facing each way along them. A cylinder of
eight sides has 16 vertices, 24 edges and 10 faces, and a cone of six 7, 12 and 7.

### Wedge

`EditMeshWedge(mesh, size)`, or `wedge X Y Z`, is a box cut through from one bottom edge to the
opposite top edge. It is nothing tall at its lowest Z and its full height at its highest, so it
rises along Z as stairs do, and a wedge laid over a flight of stairs makes their ramp.

### Stairs

`EditMeshStairs(mesh, steps, rise, run, width)`, or `stairs STEPS RISE RUN WIDTH`, is a flight
rising along Z, each step `rise` high and `run` deep, as wide along X as `width`. It is closed
underneath, a back face as tall as the flight at its far end and a floor under it, so it is a solid
that stands on the ground and against a wall:

<!-- compiled with:
var mesh = LoadEditMesh();
-->
```csharp
EditMeshStairs(mesh, 12, 0.25f, 0.3f, 2);   // three meters up over 3.6
```

Its two sides are faces of one outline each, a step's corner at a time. A flight of four steps has
twenty vertices, thirty edges and twelve faces, and holds the sum of its steps, each as tall as it
stands.

### Arch

`EditMeshArch(mesh, radius, thickness, depth, sides)`, or `arch RADIUS THICKNESS DEPTH SIDES`, is
half a ring standing on its two feet, its curve made of `sides` flat sides and its opening along Z.
It is twice its radius wide and its radius tall, so it stands over a doorway as wide as its inside,
`radius - thickness` either side of its middle.

### Clear

`EditMeshClear(mesh)`, or `clear`, takes away every vertex, face and material slot but the first.
It is a line of the recipe like the others, so an undo after it brings back what it took.

## The selection

Operations act on what is selected, a set of vertices and a set of faces. Selecting a face selects
its corners, so moving the vertices selected moves the faces selected, and an edge is selected
where both its ends are.

| Call | Line | Selects |
|---|---|---|
| `EditMeshSelectAll(mesh)`, `EditMeshSelectNone(mesh)` | `select all`, `select none` | Everything, or nothing |
| `EditMeshSelectInvert(mesh)` | `select invert` | What was not selected, faces with their corners |
| `EditMeshSelectFaces(mesh, [0, 3])` | `select face 0 3` | Faces by their indices |
| `EditMeshSelectFacing(mesh, Vector3.UnitY)` | `select +y` | The faces facing nearest a way along an axis, here up |
| `EditMeshSelectInside(mesh, corner, opposite)` | `select box 0 0 0 4 3 4` | The vertices inside a box, and the faces all of whose corners are |
| `EditMeshSelectSlot(mesh, "wall")` | `select slot wall` | The faces in a material slot |

Each but the first three takes a `SelectionMode` after it, `Replace` unless it says `Add` or
`Remove`, which a line says after `select`, as `select add -y`. `EditMeshSelectFacing` sorts a face
by which of the six directions along the axes its normal is nearest, so every face is a floor, a
ceiling or one of four walls. A slope rising exactly as far as it runs, at forty-five degrees, is
taken as a floor, where a character walks it.

```csharp
var hall = LoadEditMesh();
EditMeshBox(hall, new Vector3(10, 3, 4));
EditMeshSelectFacing(hall, Vector3.UnitX);
EditMeshSelectFacing(hall, -Vector3.UnitX, SelectionMode.Add);   // both ends of the hall
```

## Moving, turning and scaling

`EditMeshMove(mesh, by)` moves the selected vertices, `EditMeshRotate(mesh, axis, degrees)` turns
them about an axis and `EditMeshScale(mesh, factor)` stretches them along each axis, the lines
`move X Y Z`, `rotate y 90` (or an axis's three numbers, `rotate 1 0 1 45`) and `scale 2` or
`scale 1 2 1`, each ending in `at X Y Z` where a pivot is given. A turn or a scale is about the
middle of the box holding the selection unless a pivot is given, so a part turned a quarter in
place stays on the grid it stood on, and a negative scale mirrors the selection with its faces
still facing out.

```csharp
var tower = LoadEditMesh();
EditMeshBox(tower, new Vector3(4, 12, 4));
EditMeshSelectFacing(tower, Vector3.UnitY);
EditMeshScale(tower, new Vector3(0.5f, 1, 0.5f));               // the top drawn in, a taper
EditMeshSelectAll(tower);
EditMeshRotate(tower, Vector3.UnitY, 45, pivot: Vector3.Zero);   // the whole turned about the origin
```

## The grid

A mesh holds a grid step, a meter unless `EditMeshGrid(mesh, step)`, or `grid STEP`, sets a power
of two of one from a sixteenth to sixty-four. A measure typed is taken as given, so a move of a
fifth of a meter moves a fifth of a meter whatever the step. `EditMeshSnap(mesh)`, or `snap`, puts
each selected vertex on the nearest point of the grid, which mends what turns and scales left off
it:

```csharp
var crate = LoadEditMesh();
EditMeshBox(crate, Vector3.One);
EditMeshRotate(crate, Vector3.UnitY, 30);
EditMeshGrid(crate, 0.25f);
EditMeshSnap(crate);
```

## Material slots and smoothing

Each face has a material slot, a name, the first of which, `default`, every new face takes.
`EditMeshMaterial(mesh, "wall")`, or `material wall`, puts the selected faces in the slot of that
name, made where there is none, so a mesh's faces are sorted into floors, walls and trims before
what each is drawn with is decided. A model built from the mesh has a mesh and a material for each
slot, in the order `GetEditMeshSlots` gives:

```csharp
var cell = LoadEditMesh();
EditMeshBox(cell, new Vector3(6, 3, 6));
EditMeshMaterial(cell, "wall");
EditMeshSelectFacing(cell, -Vector3.UnitY);
EditMeshMaterial(cell, "floor");
```

`EditMeshShade(mesh, true, degrees)`, or `shade smooth 30`, smooths the selected faces into one
another where two meet at under the angle, thirty degrees where none is given, so a cylinder's sides
are round and its ends stay flat, and `EditMeshShade(mesh, false)`, or `shade flat`, draws them
flat.

## Grid prototyping

A level is blocked out in textures that show a meter and say by their color what each surface is
for, as the dev textures of the engines of the 2000s did. `GenImageGrid(pixelsPerMeter, face,
line, fine)` makes the image of one meter's cell, its edges a line and a finer line every tenth of
a meter, beside `GenImageChecked`, and `GenImageGrid(1024)` the same in its default grays.

`LoadMaterialPrototype(name)` is the grid tinted by the color of a part of a level, the names
`floor`, `wall`, `ceiling`, `stairs`, `door`, `window` and `hazard` each a color of its own
(`GetPrototypeColor`) and any other name the grid's gray, and `LoadMaterialPrototype(color)` the
grid tinted by any color. The materials share one texture of 1024 pixels a meter with its mip
levels, repeating and filtered along a slanted surface so its lines stay sharp down a corridor,
which `CloseWindow` frees, so a prototype material is not unloaded by the program.

A model built from an edit mesh draws each slot with the prototype material of the slot's name, so
faces put in the slots `floor` and `wall` read as a floor and walls with no material set. A face's
texture coordinates are its corners' places in meters seen along the axis its normal is nearest, so
the grid shows a meter on every face, a face two meters wide spanning two cells, and carries on
across faces meeting along a line:

```csharp
var cell = LoadEditMesh();
EditMeshBox(cell, new Vector3(6, 3, 6));
EditMeshMaterial(cell, "wall");
EditMeshSelectFacing(cell, -Vector3.UnitY);
EditMeshMaterial(cell, "floor");
var model = LoadModelFromEditMesh(cell);    // the walls a pale warm gray, the floor a gray
```

## Booleans and openings

`EditMeshUnion(mesh, other)`, `EditMeshSubtract(mesh, other)` and `EditMeshIntersect(mesh, other)`,
or `union`, `subtract` and `intersect`, join another solid into the mesh, take it away, or keep
what the two both hold. The other is a `.model` file moved by an offset, as
`union models/pillar.model at 2 0 2`, or a solid primitive's line placed by its own words, as
`union stairs 16 0.2 0.3 1.2 at 0.2 0.2 0.6`, which a box, cylinder, cone, wedge, stairs, arch, room
or ramp is. `EditMeshCut(mesh, "box 1 1 1 at 1.5 1 -0.25")`, or `cut box 1 1 1 at 1.5 1 -0.25`, takes
a primitive away. The other's faces are selected after, the sides of the hole for a cut.

They are csg.js's binary space partition, written in the engine. Each solid is cut by the other's
planes, the parts the operation keeps are joined back into faces where they lie in one plane and
one slot, and the vertices are joined where they meet, so a window cut from a wall is a few faces
and not a hundred triangles, and two boxes side by side united are a box of eight vertices. Both
are closed solids, as a boolean needs. A point within a hundredth of a millimeter of a plane lies
in it, which `EditMeshEpsilon(mesh, distance)`, or `epsilon 0.0001`, changes. Two faces in one plane
are cut as the planes say, so an opening is cut with a solid a little deeper than the wall.

A stair joined by its line stands on its floor and against its wall as one solid with them, where
one added beside them as a primitive is a second solid whose edges a later boolean welds to theirs,
four faces then along each. A level is cut from one block, as `games/Blockout`'s level in
BevyCSharp is, so every cut works on one closed solid.

`EditMeshRoom(mesh, size, thickness)`, or `room 6 3 5 0.2`, adds a room, a box hollowed out by its
walls' thickness. `EditMeshCutWindow(mesh, width, height, along, sill)`, or `cut window 1.6 1.2 2.9 1`,
cuts a window through the wall whose face was selected first, its middle `along` from the wall's left
edge seen from in front and its bottom `sill` above the wall's, and `EditMeshCutDoor(mesh, width,
height, along)`, or `cut door 1.2 2.2 3.8`, a door standing on the wall's bottom, which in a room
goes down to the ground outside and steps up to the floor inside. Each goes a centimeter past the
wall on each side and as deep as the wall, so the wall across a room is not cut, and stops above a
floor behind the wall, which a door cut down through a room's slab leaves whole. A wall an earlier
opening cut into pieces is measured whole where its pieces are selected.
`EditMeshRamp(mesh, rise, run, width)`, or `ramp 1 3 2`, adds a wedge in a flight of stairs' words:

```csharp
var house = LoadEditMesh();
EditMeshRoom(house, new Vector3(6, 3, 5), 0.2f);
EditMeshSelectFacing(house, Vector3.UnitZ);             // the front wall's outside
EditMeshCutWindow(house, 1.2f, 1, 4.5f, 1);
EditMeshSelectFacing(house, Vector3.UnitZ);
EditMeshCutDoor(house, 1, 2.1f, 1.5f);
EditMeshUnion(house, "stairs 6 0.2 0.3 1 at 0.2 0.2 2");
```

A file that is missing or not a recipe, given to a boolean, leaves the mesh as it was and is
answered with false and a warning naming it.

## Extrude and inset

`EditMeshExtrude(mesh, distance)`, or `extrude 1`, pushes the selected faces out along their
normals, the sides between them and what they left filled. The faces move together as one region,
so two side by side stay joined, and a corner where they meet at an angle moves so each goes the
whole distance along its own normal. The faces moved stay selected, so a second extrude pushes them
further, and a negative distance pushes them in. `EditMeshExtrude(mesh, by)`, or `extrude 0 1 0`,
moves every corner of the region by one vector, as a wall is pushed up from a floor's edge.

`EditMeshInset(mesh, distance)`, or `inset 0.25`, makes each selected face a smaller face inside it
with a band of faces round it, the smaller faces selected after, so an extrude after it pulls out a
panel or pushes in a recess:

```csharp
var wall = LoadEditMesh();
EditMeshBox(wall, new Vector3(4, 3, 0.4f));
EditMeshSelectFacing(wall, Vector3.UnitZ);
EditMeshInset(wall, 0.5f);
EditMeshExtrude(wall, -0.2f);   // a recess two tenths deep
```

The top of a box of two by three by four extruded a meter makes a box of twelve vertices and ten
faces that holds 32 cubic meters, and an inset of its top changes nothing it holds.

## Bevel

`EditMeshBevel(mesh, width, segments)`, or `bevel 0.1 3`, cuts each selected edge back by the
width into a band of faces, round across in the number of segments, one where none is given. An
edge is beveled where both its ends are selected and two faces run along it, so selecting all of a
box bevels its twelve edges and selecting its top bevels the four round the top. The band follows
the arc tangent to both faces at the width from the edge, a quarter circle across a box's edge, and
a single segment is a flat chamfer. Where three or more beveled edges meet, as at a box's corner,
the corner is closed with one face, and the bands and the corners are selected after:

```csharp
var crate = LoadEditMesh();
EditMeshBox(crate, new Vector3(2, 2, 2));
EditMeshBevel(crate, 0.25f, 2);   // 48 vertices, 84 edges and 38 faces
```

An edge that is not beveled, at a corner where one is, gives up the end of it the bevel needs, so a
box with its top beveled keeps its upright edges, shorter by the width.

## Loop cut and subdivide

`EditMeshLoopCut(mesh, fraction)`, or `loopcut 0.5`, cuts round the ring of four-cornered faces a
selected edge crosses. The ring runs from the edge through the faces beside it to their opposite
edges and on, both ways, until it comes round again or meets a face that has not four corners, and
each edge it crosses takes a vertex at the fraction along it, measured from the selected edge's
lower-numbered end. A box's upright edge cut at a half makes a loop round its middle, whose
vertices are selected after:

```csharp
var pillar = LoadEditMesh();
EditMeshBox(pillar, new Vector3(1, 4, 1));
EditMeshSelectInside(pillar, Vector3.Zero, new Vector3(0, 4, 0));   // one upright edge
EditMeshLoopCut(pillar, 0.5f);
```

`EditMeshSubdivide(mesh, cuts)`, or `subdivide 2`, cuts each selected face into a grid, each of its
edges into one more part than the count. A face of four corners becomes a grid of them and a
triangle a grid of triangles, and a face of more corners a fan of triangles from its middle. A face
beside one subdivided takes the new vertices on the edge they share as corners of its own, so the
mesh stays closed, and a box subdivided once has 26 vertices and 24 faces.

## Knife

`EditMeshKnife(mesh, normal, point)`, or `knife y at 0 1.5 0`, cuts the selected faces along the
plane through the point square to the normal, the point the origin where the line says none. Each
selected face the plane crosses is split in two along it, the faces beside a cut edge take its new
vertex so the mesh stays closed, and the vertices along the cut are selected after. A box cut with
every face selected has a loop round it, and cut with its top alone has its top split in two.

## Bridge, fill and dissolve

`EditMeshBridge(mesh)`, or `bridge`, joins the loops round two regions of selected faces with a band
of four-cornered faces and takes the selected faces away. Each region has one loop round it of the
same count of vertices, as two faces facing each other across a gap have, and the band runs
straight across between them, so two boxes bridged at their facing sides are one solid:

```csharp
var dumbbell = LoadEditMesh();
EditMeshBox(dumbbell, Vector3.One);
EditMeshBox(dumbbell, Vector3.One, at: new Vector3(3, 0, 0));
EditMeshSelectInside(dumbbell, new Vector3(1, 0, 0), new Vector3(3, 1, 1));   // the two facing sides
EditMeshBridge(dumbbell);
```

`EditMeshFill(mesh)`, or `fill`, closes each hole whose edges all have both ends selected with one
face, facing the way the faces round it face. `EditMeshDissolve(mesh)`, or `dissolve`, joins each
region of selected faces into one face round it, taking away the edges and vertices inside it, and
`EditMeshDissolveEdges(mesh)`, or `dissolve edges`, takes away each selected edge two faces run
along, joining them. Each keeps the shape, the vertices on the joined face's outline staying, and a
region with a hole in it is left as it was.

## Mirror, spin and smooth

`EditMeshMirror(mesh, axis, pivot)`, or `mirror x`, copies the selected faces mirrored across the
plane through the pivot square to the axis, the origin where none is given, and selects the copy.
The vertices on the plane are shared by both halves, so a half whose open side lies on the plane
makes a whole with no vertex twice. A face lying in the plane is not copied, so a half to be
mirrored is left open there:

```csharp
var slab = LoadEditMesh();
EditMeshBox(slab, new Vector3(1, 2, 2));
EditMeshSelectFacing(slab, -Vector3.UnitX);
EditMeshDelete(slab);                          // the half, open on the plane x = 0
EditMeshSelectAll(slab);
EditMeshMirror(slab, Vector3.UnitX);           // the whole, two meters wide
```

`EditMeshSpin(mesh, axis, degrees, steps, center)`, or `spin y 360 16`, sweeps the selected faces
round the axis through the center by the angle in the number of steps. The faces turn as an extrude
moves them, a band of faces swept each step, and a whole turn joins the sweep's end to its start, so
a face beside the axis spun round is a closed ring. With no face selected, each selected edge sweeps
a band of its own, as a lathe turns a profile.

`EditMeshSmooth(mesh, factor)`, or `smooth 0.5`, moves each selected vertex toward the mean of the
vertices it shares an edge with, by the factor from zero to one, so smoothing a subdivided box
rounds it and smoothing a box's own eight corners by a half draws it in to a smaller box.

## Delete, merge, flip, triangulate and duplicate

`EditMeshDelete(mesh)`, or `delete`, takes away the selected faces, or where none is selected the
selected vertices and every face using them, and `EditMeshDelete(mesh, fill: true)`, or
`delete fill`, closes each hole left with one face, which is selected after. A vertex no face uses
is taken away with them.

`EditMeshMerge(mesh, distance)`, or `merge 0.001`, joins the selected vertices nearer one another
than the distance into one, and a face left with fewer than three corners is taken away, so a top
moved onto the bottom of a box and merged leaves two faces on four vertices.

`EditMeshFlip(mesh)`, or `flip`, turns the selected faces to face the other way.
`EditMeshTriangulate(mesh)`, or `triangulate`, cuts each selected face of more than three corners
into triangles by ear clipping, a concave face inside its outline, as the build cuts it.
`EditMeshDuplicate(mesh)`, or `duplicate`, copies the selected faces where they are, with vertices
of their own, and selects the copy, which then moves alone.

## Modifiers

A mesh holds a stack of modifiers, which apply in order to a copy of it when it is built, so the
mesh stays as it was shaped and every change to it is built with them again. Each is a recipe line
beginning `modifier`:

| Call | Line | Does |
|---|---|---|
| `EditMeshModifierMirror(mesh, "xz")` | `modifier mirror xz` | Mirrors the mesh across the plane square to each axis named, through the origin or `at X Y Z` |
| `EditMeshModifierArray(mesh, 3, new Vector3(2, 0, 0))` | `modifier array 3 2 0 0` | Repeats the mesh, three in all, each two meters along X from the last |
| `EditMeshModifierArrayCurve(mesh, positions)` | `modifier array curve 0 0 0 2 0 1` | Repeats it at each position, moved by the position's offset from the first |
| `EditMeshModifierBevel(mesh, 0.1f, 2, 30)` | `modifier bevel 0.1 2 30` | Bevels the edges whose faces meet at more than thirty degrees, or every edge with no angle |
| `EditMeshModifierSubdivision(mesh, 2)` | `modifier subdivision 2` | Subdivides it by Catmull and Clark's rule to level two, rounding it |
| `EditMeshModifierSolidify(mesh, 0.1f)` | `modifier solidify 0.1` | Gives it a thickness out along its normals, or in for a thickness below zero |
| `EditMeshModifierTriangulate(mesh)` | `modifier triangulate` | Cuts every face into triangles |

A half shaped with its open side on the plane and a mirror modifier builds whole while it is shaped
as a half, and a box subdivided to level one has 26 vertices and to level two 98:

```csharp
var pebble = LoadEditMesh();
EditMeshBox(pebble, new Vector3(2, 1, 1.5f));
EditMeshModifierBevel(pebble, 0.1f);
EditMeshModifierSubdivision(pebble, 2);
var model = LoadModelFromEditMesh(pebble);      // the box rounded, the box itself left to shape
```

`GetEditMeshModifiers(mesh)` lists the stack as its lines' words, and `GetEditMeshModifiedInfo(mesh)`
reads what the mesh builds into, as `GetEditMeshInfo` reads the mesh itself.
`EditMeshModifierMove(mesh, from, to)` moves a modifier in the stack, `EditMeshModifierRemove(mesh,
index)` takes one out, and `EditMeshModifierApply(mesh, index)` does one to the mesh itself, as
though it were first, and takes it out, so what it made is shaped from then on, the lines
`modifier move 1 0`, `modifier remove 1` and `modifier apply 0`. Each is a line of the recipe, so an
undo after an apply brings the modifier back.

## Joining another model

`EditMeshJoin(mesh, "models/pillar.model")`, or `join models/pillar.model`, adds the model a recipe's
file makes and selects it, its faces keeping their slots by name. The recipe keeps the file's name
rather than what it held, so a change to the pillar's model reaches every model that joins it the
next time each is made. A file that is missing, is not a model's recipe or joins itself leaves the
mesh as it was and is answered with false and a warning naming it.

## The recipe

`GetEditMeshRecipe(mesh)` gives the recipe as the text of its file, its `model 1` header first and
then a line an operation. `SaveEditMesh(mesh, "models/room.model")` writes it, beside the program
where the name is relative, and `LoadEditMesh("models/room.model")` makes the mesh again from it,
so a level's models are short files a reader follows and a diff reads:

```text
model 1
# The hall, its ceiling a slot of its own with a panel pushed up in it.
box 8 3 6
select +y
material ceiling
inset 1
extrude 0.5
```

Blank lines and comments after `#` are skipped. A file that does not start with `model 1`, or
holds a line that is not an operation, gives an edit mesh `IsEditMeshValid` says is not loaded,
with a warning naming the file and the line. `LoadEditMeshFromMemory(text)` reads a recipe from a
string, its header optional, and `EditMeshRun(mesh, "extrude 1")` runs one line as though its
operation were called, the line it writes being the line given.

`EditMeshUndo(mesh)` takes back the last operation by making the mesh again from the lines before
it, so an undo is right for every operation and the selection is the one those lines left, and
`EditMeshRedo(mesh)` does it again. An operation done after an undo forgets what was taken back.

## Blender and other programs

`ExportEditMesh(mesh, "level.gltf")` writes what the mesh builds into, its modifiers applied, as a
glTF 2.0 file, positions, normals, texture coordinates and triangles, one primitive a material
slot with faces and a material named as the slot is, so a level shaped in the game is opened in
Blender and finished there. Its buffer is written beside it as `level.bin`, or into the file itself
with `ExportEditMesh(mesh, "level.gltf", embedBuffer: true)`. The writer is the engine's own and
writes the JSON form, `.gltf`, and not `.glb`.

`EditMeshImport(mesh, "rock.gltf")`, or `import rock.gltf`, adds the meshes of a model file, read as
`LoadModel` reads it, so a glTF, an FBX, an OBJ or any of the others, and selects them. The file's
meshes are placed as its nodes place them, its vertices in one place joined, and triangles in one
plane and one material that share an edge are joined back into one face round them, so a box made
in Blender is six faces again and is cut and shaped in the game. A material is a slot of its name,
its words joined by hyphens, so "Stone Wall" is the slot `Stone-Wall`:

<!-- compiled with:
var level = LoadEditMesh();
-->
```csharp
var rock = LoadEditMesh();
if (EditMeshImport(rock, "models/rock.gltf"))
{
    EditMeshSelectFacing(rock, -Vector3.UnitY);
    EditMeshDelete(rock, fill: true);       // its underside made one flat face
}
ExportEditMesh(level, "level.gltf");
```

A file that is missing or is not a model leaves the mesh as it was and is answered with false and a
warning naming it. The recipe keeps the file's name, so the mesh is read again from the file when
the recipe runs.

## Building a model

`LoadModelFromEditMesh(mesh)` builds the mesh into a `Model`, a mesh for each material slot drawn
with the prototype material of the slot's name, as [Grid prototyping](#grid-prototyping) shows,
which is drawn and given materials as any model is:

<!-- compiled with:
var room = LoadEditMesh();
Camera3D camera = default;
-->
```csharp
var model = LoadModelFromEditMesh(room);
var floor = CreatePhysicsStaticModel(model, Vector3.Zero);
// ...
BeginMode3D(camera);
DrawModel(model, Vector3.Zero, 1, Color.White);
EndMode3D();
```

Each face is cut into triangles by ear clipping and has corners of its own, so its normal is its
own, or the mean of the smoothed faces' normals round a corner within its angle. Its texture
coordinates are each corner's place in meters seen along the axis the face's normal is nearest, so
a texture of a meter's grid shows a meter on every face and carries on across faces meeting along a
line. A slot no face is in is a mesh of one triangle with no area, which draws nothing.

After the mesh changes, `UpdateModelFromEditMesh(model, mesh)` writes the model's meshes again in
place, each keeping its id, so whatever draws the model draws the change, and the materials the
program gave it stay with their slots. `CreatePhysicsStaticModel(model, position)` makes a body of
the built model's triangles, the floors and walls a character walks, and a body made before a
change is destroyed and made again to follow it:

<!-- compiled with:
var room = LoadEditMesh();
var model = LoadModelFromEditMesh(room);
var floor = CreatePhysicsStaticModel(model, Vector3.Zero);
-->
```csharp
EditMeshSelectFacing(room, Vector3.UnitY);
EditMeshExtrude(room, 1);
UpdateModelFromEditMesh(model, room);
DestroyPhysicsBody(floor);
floor = CreatePhysicsStaticModel(model, Vector3.Zero);
```

## See also

- Examples: [`models_edit_mesh`](../3DEngine.Examples/Models/ModelsEditMesh.cs), the seven
  primitives each built into a model,
  [`models_edit_mesh_cuts`](../3DEngine.Examples/Models/ModelsEditMeshCuts.cs), a box beveled, inset
  and loop cut, [`models_edit_mesh_modifiers`](../3DEngine.Examples/Models/ModelsEditMeshModifiers.cs),
  a box built through a bevel and a subdivision modifier, and
  [`models_edit_mesh_room`](../3DEngine.Examples/Models/ModelsEditMeshRoom.cs), a room with a window,
  a door and a stair blocked out in prototype materials
- [Models and animation](models-and-animation.md), for drawing a model and its materials, and
  [Physics](physics.md), for the bodies a built model collides as
- The cheatsheet's [Modeling](../CHEATSHEET.md#modeling)
- Previous: [Models and animation](models-and-animation.md)
- Next: [Materials, light and shadows](materials-light-and-shadows.md)
