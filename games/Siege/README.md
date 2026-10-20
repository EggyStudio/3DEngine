# Siege

`games/Siege` is a catapult thrown at a castle until the banner on its keep falls. The arm turns on
a hinge driven by its motor, and its shot, a stone shaped as a convex hull or a bomb shaped as a
capsule, is welded to the cup and let go at the angle the player chose, the arc it will take drawn
by sweeping a ball along it to the first body it meets. The castle is near two hundred blocks,
boxes and columns shaped as convex hulls, each welded to the blocks it was laid against, and a weld
breaks where `GetPhysicsJointForce` reads it holding harder than its strength. A stone wears down
the blocks it presses by the push of their contact, a bomb bursts on what it meets and pushes every
body near it from the side facing the burst, and the banner falling to the field enters a trigger,
which ends the siege. A spot light sweeps the keep and casts its shadow, under a tonemap and a
depth of field.

It is built outside the solution from the package `build/pack.sh` makes, as a game of your own would
be, as [BUILDING.md](../../.github/BUILDING.md) says.
