---
name: coverage
description: Analyze which UI screens, buttons, and navigation flows are tested and which are not. Generates a coverage report with gaps and suggests what to test next.
---

# UI Test Coverage Analysis

Analyze what's tested and what's not by comparing the game map against existing tests.

## Step 1: Load Game Knowledge

```
godot_recall()
```

If no game map exists, run `/godotplay-explore` first.

## Step 2: Scan Existing Tests

Search the project for test files that use GodotPlay:

- Look for `*.cs` files containing `GodotPlaySession`, `Locator`, `ClickAsync`, `Expect`
- Look for `[Test]` or `[TestFixture]` attributes
- Extract which node paths, screens, and interactions are covered

Key patterns to search for:
```
Locator(path: "...")       → covered node path
Locator(className: "...")  → covered class
ClickAsync()               → covered interaction
TypeAsync()                → covered text input
LoadSceneAsync("...")      → covered scene navigation
Expect.That(...)           → covered assertion
```

## Step 3: Build Coverage Matrix

For each known screen from the game map:

| Check | How to verify |
|-------|--------------|
| Screen visited? | Any test loads this scene or navigates to it |
| Buttons tested? | Each button's node path appears in a ClickAsync call |
| Text inputs tested? | Each input's node path appears in a TypeAsync call |
| Navigation tested? | Each navigatesTo path has a test that clicks and verifies destination |
| Properties asserted? | Key labels/states have Expect assertions |
| Visual baseline? | `.godotplay-baselines/` has a baseline for this screen |

## Step 4: Generate Coverage Report

```markdown
# UI Test Coverage Report

## Summary
- Known screens: N
- Screens with tests: N (X%)
- Known buttons: N
- Buttons with tests: N (X%)
- Navigation paths: N
- Paths with tests: N (X%)
- Visual baselines: N/M

## Coverage by Screen

### main_menu — 3/5 buttons tested (60%)
- [x] StartButton — clicked in MainMenuTests.cs:25
- [x] SettingsButton — clicked in MainMenuTests.cs:35
- [ ] QuitButton — NOT TESTED
- [x] Navigation to game_view — verified in MainMenuTests.cs:28
- [ ] Navigation to settings — NOT TESTED
- [ ] Visual baseline — MISSING

### game_view — 0/3 buttons tested (0%)
- [ ] PauseButton — NOT TESTED
- [ ] InventoryButton — NOT TESTED
- [ ] MapButton — NOT TESTED
- [ ] Visual baseline — MISSING

## Recommended Next Tests (priority order)
1. game_view — 0% coverage, 3 untested buttons
2. main_menu/QuitButton — easy win, just verify it quits
3. settings screen — no tests at all
```

## Step 5: Suggest Priority

Rank untested areas by:
1. **Zero coverage screens** — highest priority
2. **Untested navigation paths** — user flows that could break
3. **Untested buttons** — individual interactions
4. **Missing visual baselines** — call `godot_visual_compare` for each
5. **Missing assertions** — screens visited but nothing verified

## Step 6: Optionally Launch and Fill Gaps

If the user wants, launch Godot and:
1. Navigate to untested screens
2. Run `godot_visual_compare` to create baselines
3. `godot_learn` any new discoveries
4. Suggest concrete test code for each gap

## Principles

- **Don't count explore-only as tested** — a screen in the game map without assertions is NOT covered
- **Navigation is a test** — clicking A and verifying you reach B counts as coverage
- **Visual baselines count** — they catch regressions even without explicit assertions
- **Be actionable** — every gap should have a concrete "write this test" suggestion
