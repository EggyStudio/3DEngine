# Tumble

`games/Tumble` is a robot walked round an arena for the stars that appear in it, while cannons at
its edges fire at it and a bar sweeps round its middle. The robot is a character controller drawn
as a skinned model, which walks and runs by blending its clips by its speed, aims its upper body at
the pointer and punches on a layer of its own over its legs, and crouches under the bar by the
character's height. Struck by a ball or the bar, it falls as a ragdoll of capsules, ball joints
held in cones at its hips, shoulders, waist and neck and hinges at its knees and elbows, the model
posed from the capsules each frame, and once it lies still it sits up toward its feet and stands,
by a blend from the pose it lies in to the clip that stands it up.

The robot and its clips are Quaternius's, dedicated to the public domain under CC0, carried from
raylib's examples with the notice beside it in `resources/robot-CC0.txt`.

It is built outside the solution from the package `build/pack.sh` makes, as a game of your own would
be, as [BUILDING.md](../../.github/BUILDING.md) says.
