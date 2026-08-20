# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Approach
- Read existing files before writing. Don't re-read unless changed.
- Thorough in reasoning, concise in output.
- Skip files over 100KB unless required.
- No sycophantic openers or closing fluff.
- No emojis or em-dashes.
- Do not guess APIs, versions, flags, commit SHAs, or package names. Verify by reading code or docs before asserting.

## Project overview

SlideFactory (internal codename "CardCraft") is a Unity mobile game. The core loop: a deck of cards
sit on screen, four production "stations" are assigned to the four swipe directions (Up/Down/Left/Right)
and reshuffle periodically, and the player swipes each card toward the station its recipe requires
before the level timer runs out. Correct swipes advance a card through a production chain (e.g. raw
ore → ingot → sword); wrong swipes reset the card to its raw stage. A combo/score layer
(`ScoreManager`) tracks a multiplier across consecutive correct swipes and now drives star ratings
(see "Scoring and stars" below), separate from the raw win/lose/deck-empty rules `GameManager` owns.

Engine: Unity **6000.5.4f1** (Unity 6), Universal Render Pipeline (2D), new Input System, TextMeshPro,
DOTween (Demigiant, in `Assets/Plugins/`).

**Code comments and Inspector tooltips throughout the codebase are written in Turkish.** Preserve this
convention when editing existing files; match the existing language/style of whatever file you're in.

## Working with this repo

This is a Unity project, not a CLI/npm/dotnet-CLI-driven one — there is no `npm run build` or `dotnet test`
equivalent from the shell. Builds, running the game, and running tests are normally done from inside the
Unity Editor (Unity 6000.5.4f1). If Unity Editor automation is needed from the command line (e.g. batch
mode builds or `-runTests`), invoke the Unity executable directly with `-batchmode -projectPath .`; there
is no existing CI/build script in this repo to reference. One concrete example already in the repo:
`Assets/_Project/Scripts/Editor/CardIconAutoWirer.cs` auto-wires `CardData.icon` fields from PNGs in
`Assets/_Project/Textures/CardIcons/` and exposes a batch-mode entry point
(`Unity.exe -batchmode -nographics -quit -projectPath . -executeMethod CardIconAutoWirer.WireAllFromCommandLine`).

There is an `Assets/_Project/Scripts/Tests/` folder (now covered by `SlideFactory.Tests.asmdef`) and
`com.unity.test-framework` is a project dependency, but **no test files currently exist in that folder**
— don't assume test coverage exists for a system before checking.

**[UniCli](https://github.com/yucchiy/UniCli) is set up for running tests from the terminal without
opening the Editor UI.** This is a third-party tool (not published by Unity Technologies), added via
`com.yucchiy.unicli-server` in `Packages/manifest.json` plus a local `unicli` CLI binary. It requires
the Unity Editor to already be open on this project — the server runs inside that Editor process, not
standalone. From a terminal:

```
unicli check                          # verify the package is installed and the in-Editor server is running
unicli exec Compile --json            # trigger a script compile, get errors/warnings back as JSON
unicli exec TestRunner.RunEditMode    # run EditMode tests
unicli exec TestRunner.RunPlayMode '{"dirtyAction":"save"}' --json   # run PlayMode tests
unicli exec TestRunner.List '{"mode":"EditMode"}' --json             # discover tests without running them
```

Most editor-only scripts (e.g. `Assets/Editor/`, `Assets/_Project/Scripts/Editor/`) still compile into
the default `Assembly-CSharp-Editor`. Three custom assembly definitions exist to support testing:
`Assets/_Project/Scripts/Runtime/SlideFactory.Runtime.asmdef` (wraps the whole `Runtime/` tree — Core,
Data, StateMachine, Input, View), `Assets/_Project/Scripts/Tests/SlideFactory.Tests.asmdef`
(Editor-only NUnit test assembly referencing `SlideFactory.Runtime`), and
`Assets/Plugins/Demigiant/DOTween/Modules/DOTween.Modules.asmdef` (wraps DOTween's loose-source
extension-method modules — `DOFade`, `DOAnchorPos`, `DOColor`, `DOShakeAnchorPos`, etc. — which
`SlideFactory.Runtime` references). New gameplay scripts under `Runtime/` compile into
`SlideFactory.Runtime`, not `Assembly-CSharp`, as long as they stay under that folder.

**Gotcha that cost real debugging time creating the above:** `DOTween.dll` itself (core `DG.Tweening`
types like `Tween`/`Sequence`) is a precompiled DLL, auto-referenced everywhere, so it compiles fine
into any custom asmdef with no explicit reference. But DOTween's *extension methods* for UGUI/TMPro
types (`DOFade` on `CanvasGroup`, `DOAnchorPos` on `RectTransform`, etc.) are loose `.cs` source under
`Assets/Plugins/Demigiant/DOTween/Modules/`, which — before `DOTween.Modules.asmdef` existed — compiled
into Unity's predefined `Assembly-CSharp-firstpass` (the special assembly for code in `Plugins/`
folders). **A custom `.asmdef` does NOT automatically get a reference to `Assembly-CSharp-firstpass`**,
even though it compiles earlier in Unity's build order — that implicit-reference behavior only applies
to `Assembly-CSharp` (the default, un-asmdef'd assembly), not to explicit asmdef-covered code. The fix
was to give the offending `Plugins/` subfolder (or any third-party code you need to reference from a
new asmdef) its own asmdef, so it becomes referenceable by GUID like any other assembly. If you add
another asmdef later and hit `CS1061`/`CS1929` "extension method not found" errors pointing at
`Assets/Plugins/...`, this is almost certainly why — do not assume "it compiled fine in Assembly-CSharp
before" means it'll compile fine inside a new asmdef.

## Architecture

Scripts live under `Assets/_Project/Scripts/Runtime/`, organized by role rather than by feature:

- **Core/** — MonoBehaviour "manager" singletons/controllers that drive gameplay and app flow:
  `GameManager` (central game loop), `ScoreManager` (combo/multiplier scoring, layered on top of
  `GameManager`'s events — see "Scoring and stars" below), `StationAssignmentManager`
  (direction↔station shuffling), `LevelTimerManager`, `SwipeInputManager` lives in `Input/` but is
  wired to `GameManager`, `LevelSession` (static, non-MonoBehaviour bridge carrying the selected level
  across scenes), `AppBootstrap` (one-time device/screen/perf setup, deliberately has zero gameplay
  knowledge), `MainMenuController` (also owns panel transitions between MainMenu/LevelSelect/Settings),
  `AudioManager`, `BackgroundMusicPlayer`, `SceneFader` (DontDestroyOnLoad fade-to-black scene
  transition singleton, same pattern as `AudioManager`), `LocalizationManager` (Türkçe/English
  selection, PlayerPrefs-backed, wraps the async Unity Localization package init), `GameLocalization`
  (static helper that pulls display strings from Localization String Tables with an English-fallback if
  a table row is missing), `PageScrollSnap`/`PageIndicatorManager`.
- **Data/** — plain data types and `ScriptableObject` assets: `CardData` (a single production stage,
  created as an asset via `CardCraft/Card Data`), `StationData` (`CardCraft/Station Data`), `LevelData`
  (`CardCraft/Level Data` — deck contents, timer, star thresholds, which 4 stations are used, an
  `isTutorial` flag), `LevelCatalog` (`CardCraft/Level Catalog` — the single ordered source of which
  levels exist and how they're grouped into chapters/pages; Level Select is generated from this, not
  hand-placed scene buttons), `CardInstance` (plain C# class, NOT a MonoBehaviour/ScriptableObject —
  see below), `LevelProgress` (static PlayerPrefs-backed completion/star tracking), `ScoreProgress`
  (static PlayerPrefs-backed best-score tracking, same key pattern as `LevelProgress` but a separate
  class so the two responsibilities — unlock/star state vs. score — stay split),
  `ProductionChainUtility`.
- **StateMachine/** — `CardState.cs` defines `ICardState`/`CardStateMachine` and the three card states
  (`RawCardState`, `ProcessingCardState`, `CompletedCardState`). Each `CardInstance` owns its own state
  machine instance, so multiple cards can be in different states simultaneously.
- **Input/** — `SwipeInputManager` (new Input System touch/mouse swipe detection, direction-agnostic of
  game rules) and `ButtonTextOffset`.
- **View/** — everything that reacts to Core/Data events to update visuals/UI/audio: `CardView`,
  `StationLabelsView`, `LevelTimerView`, `LevelResultView`, `ScoreHudView`, `RecipePreviewView`,
  `PauseMenuView`, `SettingsView`, `DeckShuffleView`, `DeckCountView`, `ParallaxCard`,
  `SwipeHintArrowView`, `ProcessedCardPopupView`/`ProcessedCardPopupItem`, `MuteButtonView`,
  `TutorialFlowView` (tutorial-only hint banner, self-disables when `LevelData.isTutorial` is false —
  see "Tutorial levels" below), `LevelSelectView`/`LevelPageView`/`LevelButton`/`LevelInfoPanelView`
  (build Level Select at runtime from `LevelCatalog`), `AudioTriggerView`, `SafeArea`, `UIButtonSound`,
  `ResetProgressButton`, `UnlockAllLevelsButton`, etc. These are intentionally "dumb" — they subscribe
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
  events (`OnCardChanged`, `OnCardProcessed`, `OnCardCompleted`, `OnInvalidSwipe`, `OnValidSwipe`,
  `OnSwipeResolved`, `OnDeckEmptied`, `OnLevelWon`, `OnLevelFailed`). `CardView` and other View-layer
  classes subscribe to these to drive animation/SFX. Keep this separation when adding new gameplay
  behavior.
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
- **Scoring and stars.** `ScoreManager` is a separate MonoBehaviour that listens to `GameManager`'s
  `OnValidSwipe`/`OnInvalidSwipe`/`OnCardCompleted` events and accumulates a score with a
  combo multiplier (multiplier rises on correct swipes, decays after an idle delay, drops a step on a
  wrong swipe). It never decides what counts as a correct swipe — that's still `GameManager`'s job.
  `ScoreManager` also computes each level's "par score" (the score a flawless run would earn) directly
  from `LevelData.initialDeck`'s production chains via `ProductionChainUtility`, with a "farming
  protection" cap so deliberately resetting a card to replay its chain doesn't out-score playing
  correctly. This is the one place with a deliberate bidirectional reference: `GameManager` holds a
  `ScoreManager` reference too, because the end-of-level time bonus and best-score persistence must
  happen (via `ScoreManager.FinalizeScore`) *before* `GameManager.CalculateStars()` and
  `LevelResultView` read the final score — every other manager in the project is one-directional, don't
  copy this pattern elsewhere without the same reason. `GameManager.CalculateStars()`'s primary rule is
  now the score/par ratio against `LevelData.threeStarScoreRatio`/`twoStarScoreRatio`; the old
  remaining-time ratio (`threeStarRemainingRatio`/`twoStarRemainingRatio`) is kept only as a fallback
  for when no `ScoreManager` is assigned or par can't be computed. Stars persist via `LevelProgress`
  (PlayerPrefs), which only ever raises a level's saved star count, never lowers it; best score persists
  separately via `ScoreProgress`, same never-lowers rule.
- **Tutorial levels** (`LevelData.isTutorial`) bypass the normal flow: the timer never fails the level
  (it resets to zero instead), `LevelProgress`/`ScoreProgress` are never written to, `LevelCatalog`
  rejects tutorial levels if one is added to it (they're only reachable from a dedicated main-menu
  button, not Level Select), and `TutorialFlowView` — not the normal Win/Lose panels — drives the
  post-completion flow, returning to `MainMenu` via `SceneFader`.
- **Level Select is generated at runtime from `LevelCatalog`,** not hand-placed scene buttons.
  `LevelSelectView` builds one `LevelPageView` per page up front (for correct scroll-content sizing) but
  only instantiates `LevelButton`s for pages near the currently visible one, recycling them through a
  pool as the player scrolls — this keeps a 200-level catalog from meaning 200 always-live UI objects.
  A level's displayed number comes from its position in `LevelCatalog`, not a field on `LevelData`
  itself, so inserting a level doesn't require renumbering other assets.
- **Localization** goes through `GameLocalization` (static helper) and `LocalizationManager`
  (MonoBehaviour singleton, PlayerPrefs-backed language choice), wrapping the Unity Localization
  package. Card/Station/Level display names and UI strings are pulled from String Tables by id/key;
  if a table lookup misses (e.g. localization tables haven't been generated yet via
  `CardCraft/Localization/Setup Locales And Tables`), `GameLocalization` falls back to the asset's raw
  `displayName` or the raw key, so the game never shows blank text. Because Unity Localization
  initializes asynchronously (via Addressables), don't call `GameLocalization`/`LocalizationSettings`
  APIs expecting synchronous results during early `Awake()` — views should listen for
  `LocalizationManager.OnLanguageChanged` and redraw when it fires.

### Scenes

Two scenes: `Assets/Scenes/MainMenu.unity` (main menu + level select, panel-toggle driven by
`MainMenuController`) and `Assets/Scenes/Game.unity` (gameplay). Levels are not separate scenes —
adding a level means creating a new `LevelData` asset, not duplicating a scene.
