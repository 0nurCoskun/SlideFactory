# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project overview

SlideFactory (internal codename "CardCraft") is a Unity mobile game. The core loop: a deck of cards
sit on screen, four production "stations" are assigned to the four swipe directions (Up/Down/Left/Right)
and reshuffle periodically, and the player swipes each card toward the station its recipe requires
before the level timer runs out. Correct swipes advance a card through a production chain (e.g. raw
ore → ingot → sword); wrong swipes reset the card to its raw stage.

Engine: Unity **6000.5.4f1** (Unity 6), Universal Render Pipeline (2D), new Input System, TextMeshPro,
DOTween (Demigiant, in `Assets/Plugins/`).

**Code comments and Inspector tooltips throughout the codebase are written in Turkish.** Preserve this
convention when editing existing files; match the existing language/style of whatever file you're in.

## Working with this repo

This is a Unity project, not a CLI/npm/dotnet-CLI-driven one — there is no `npm run build` or `dotnet test`
equivalent from the shell. Builds, running the game, and running tests are normally done from inside the
Unity Editor (Unity 6000.5.4f1). If Unity Editor automation is needed from the command line (e.g. batch
mode builds or `-runTests`), invoke the Unity executable directly with `-batchmode -projectPath .`; there
is no existing CI/build script in this repo to reference.

There is an `Assets/_Project/Scripts/Tests/` folder and `com.unity.test-framework` is a project dependency,
but **no test files currently exist in that folder** — don't assume test coverage exists for a system
before checking.

All runtime/editor scripts compile into the default Unity assemblies (`Assembly-CSharp`,
`Assembly-CSharp-Editor`) — there are no custom `.asmdef` assembly definitions splitting the code up.

## Architecture

Scripts live under `Assets/_Project/Scripts/Runtime/`, organized by role rather than by feature:

- **Core/** — MonoBehaviour "manager" singletons/controllers that drive gameplay and app flow:
  `GameManager` (central game loop), `StationAssignmentManager` (direction↔station shuffling),
  `LevelTimerManager`, `SwipeInputManager` lives in `Input/` but is wired to `GameManager`,
  `LevelSession` (static, non-MonoBehaviour bridge carrying the selected level across scenes),
  `AppBootstrap` (one-time device/screen/perf setup, deliberately has zero gameplay knowledge),
  `MainMenuController`, `AudioManager`, `BackgroundMusicPlayer`, `PageScrollSnap`/`PageIndicatorManager`.
- **Data/** — plain data types and `ScriptableObject` assets: `CardData` (a single production stage,
  created as an asset via `CardCraft/Card Data`), `StationData` (`CardCraft/Station Data`), `LevelData`
  (`CardCraft/Level Data` — deck contents, timer, star thresholds, which 4 stations are used),
  `CardInstance` (plain C# class, NOT a MonoBehaviour/ScriptableObject — see below), `LevelProgress`
  (static PlayerPrefs-backed completion/star tracking), `ProductionChainUtility`.
- **StateMachine/** — `CardState.cs` defines `ICardState`/`CardStateMachine` and the three card states
  (`RawCardState`, `ProcessingCardState`, `CompletedCardState`). Each `CardInstance` owns its own state
  machine instance, so multiple cards can be in different states simultaneously.
- **Input/** — `SwipeInputManager` (new Input System touch/mouse swipe detection, direction-agnostic of
  game rules) and `ButtonTextOffset`.
- **View/** — everything that reacts to Core/Data events to update visuals/UI/audio: `CardView`,
  `StationLabelsView`, `LevelTimerView`, `LevelResultView`, `RecipePreviewView`, `PauseMenuView`,
  `SettingsView`, `DeckShuffleView`, `ParallaxCard`, etc. These are intentionally "dumb" — they subscribe
  to events exposed by Core classes rather than owning game logic.

### Key design decisions (read before modifying gameplay logic)

- **Cards are direction-agnostic; recipes are keyed by station.** `CardData.outcomes` maps a `StationData`
  to a result `CardData`, never a `SwipeDirection` directly. `StationAssignmentManager` is the only thing
  that knows the current direction↔station mapping (re-shuffled on a random interval), and `GameManager`
  resolves `swipe direction → station → recipe outcome`. This keeps recipes stable while the on-screen
  layout keeps changing — don't reintroduce direction-based recipes.
- **`CardData`/`StationData`/`LevelData` are `ScriptableObject` assets shared across the whole project.**
  Never mutate their fields at runtime. Per-card runtime state (which stage it currently represents, its
  state machine) lives in `CardInstance`, a plain C# class created fresh per physical card in the deck.
- **`GameManager` never touches visuals/animation directly.** It only mutates deck/card state and fires
  events (`OnCardChanged`, `OnCardProcessed`, `OnCardCompleted`, `OnInvalidSwipe`, `OnSwipeResolved`,
  `OnDeckEmptied`, `OnLevelWon`, `OnLevelFailed`). `CardView` and other View-layer classes subscribe to
  these to drive animation/SFX. Keep this separation when adding new gameplay behavior.
- **Level lifecycle is split into two phases.** `GameManager.Awake()` resolves the active `LevelData`
  (from `LevelSession.SelectedLevel`, falling back to the Inspector's `fallbackLevelData` when a scene is
  opened directly in the Editor) and pre-configures the timer display only. Actual play — building the
  deck, starting the timer, starting station shuffling — only begins when `BeginLevelPlay()` is called,
  which `RecipePreviewView` triggers once the player closes the recipe-chain preview panel.
  `PauseLevel()`/`ResumeLevel()` freeze/resume the timer and station shuffling without resetting them.
- **Wrong swipes reset a card to its raw stage** (via `CardData.rawStageVersion`), they don't just
  discard the move. A station with no outcome defined for the card counts as "wrong."
- **`LevelSession`** is a static (non-MonoBehaviour) class used purely to pass the selected `LevelData`
  from the level-select screen into the `Game` scene, and a flag to make `MainMenuController` jump
  straight to the level-select panel on return. It intentionally doesn't need `DontDestroyOnLoad` since
  it's not a MonoBehaviour.
- **`AppBootstrap`** is deliberately decoupled from gameplay — it only does device/screen/frame-rate setup
  once per app run (singleton via `DontDestroyOnLoad`), and adaptively targets the display's actual
  refresh rate rather than a hardcoded value.
- **Star rating** is computed in `GameManager.CalculateStars()` from remaining-time ratio against
  `LevelData.threeStarRemainingRatio`/`twoStarRemainingRatio`, and persisted via `LevelProgress`
  (PlayerPrefs), which only ever raises a level's saved star count, never lowers it.

### Scenes

Two scenes: `Assets/Scenes/MainMenu.unity` (main menu + level select, panel-toggle driven by
`MainMenuController`) and `Assets/Scenes/Game.unity` (gameplay). Levels are not separate scenes —
adding a level means creating a new `LevelData` asset, not duplicating a scene.
