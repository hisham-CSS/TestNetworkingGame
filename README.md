# Learning Activity 1 — Single-Player Bomberman with ECS  (REFERENCE SOLUTION)

The complete LA1 game: data-oriented ECS, a fixed 60 Hz loop, a 256-frame input buffer, determinism
verification (record → replay + state hashing), all gameplay (bombs, crate destruction, powerups), a
main menu (Menu / Playing / Game Over), and the lose condition (hit by a bomb blast).

## Build · run · test  (.NET 9 SDK + MonoGame DesktopGL)
    dotnet run         # Menu: press ENTER to play.  WASD/arrows move, SPACE drops a bomb.
    dotnet test        # GameLogicTests pass

The `la1-starter` branch is the student-facing version of this project, with the gameplay systems,
input buffer, and state hasher left as `TODO` hooks.
