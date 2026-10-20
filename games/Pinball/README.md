# Pinball

`games/Pinball` is three balls on a table of bumpers, slingshots and two flippers. The ball is a
continuous body, swept over each step, since it crosses near a unit a step at speed and the rails
are a tenth of a unit thick. The flippers turn on hinges to kinematic posts, driven up and down by
the hinges' motors and stopped by their limits, and the plunger slides on a slider driven by its own
motor, its force held so it speeds up over its travel as a spring does. The table lies flat with
gravity tilted toward the player, its walls static boxes turned about the vertical, the bumpers and
slings kick the ball from their contacts, each bumper lighting a point light, a nudge too many tilts
the table, and the best score is saved to a file.

It is built outside the solution from the package `build/pack.sh` makes, as a game of your own would
be, as [BUILDING.md](../../.github/BUILDING.md) says.
