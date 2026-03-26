---
name: test
description: Use to systematically test a Godot project's UI. Navigates all known screens, verifies buttons work, checks text content, generates a test report, and suggests NUnit tests.
---

# Test a Godot Project

Systematically test a Godot project's UI by navigating screens, clicking buttons, verifying content, and generating a report.

## Step 1: Load Knowledge

```
godot_recall()
```

If no game map exists, run `/godotplay-explore` first to build one.

## Step 2: Launch Godot

```
godot_launch(projectPath: "...", headless: false)
```

## Step 3: Test Each Known Screen

For each screen in the game map:

### Navigate
Use the known navigation path from `godot_recall`. If no path is known, use `godot_load_scene`.

### Verify Structure
```
godot_inspect_tree(nodePath: "/root/ScreenName", depth: 6)
```

Check that expected nodes exist.

### Verify Content
```
godot_get_property(nodePath: "/root/Screen/Label")
```

Verify text, visibility, disabled state of key elements.

### Test Interactions
For each known button:
```
godot_click(nodePath: "...")
godot_screenshot()
```

Verify the click produced the expected result (scene change, UI update, etc.).

### Test Text Input (if applicable)
```
godot_type(nodePath: "/root/Screen/Input", text: "test input", clearFirst: true)
godot_get_property(nodePath: "/root/Screen/Input")
```

Verify the text was entered correctly.

### Screenshot Evidence
Take a screenshot of each screen for the report.

## Step 4: Generate Report

Create a structured test report:

```markdown
# GodotPlay Test Report — [Project Name]
Date: [date]

## Summary
- Screens tested: N
- Tests passed: N
- Tests failed: N
- Issues found: N

## Screen: [name]
- [PASS] Button "X" navigates to Y
- [PASS] Label shows correct text
- [FAIL] Button "Z" does not respond — node path: /root/...

## Issues
1. [severity] Description — node path, expected vs actual
```

## Step 5: Save Discoveries

```
godot_learn(screenName: "...", buttons: [...], navigatesTo: [...], notes: "...")
```

Update the game map with any new discoveries.

## Step 6: Suggest NUnit Tests

Based on what was tested, suggest concrete NUnit test code:

```csharp
[Test]
public async Task MainMenu_StartButton_NavigatesToGame()
{
    var startButton = _session.Locator(path: "/root/MainMenu/StartButton");
    await startButton.ClickAsync();
    await Expect.That(_session.Locator(className: "GameScreen")).ToExistAsync();
}
```

## Step 7: Shutdown

```
godot_shutdown()
```

## Principles

- **Data-first**: Use `godot_get_property` before `godot_screenshot` — it's faster and doesn't use context
- **Save everything**: Use `godot_learn` after each screen so next run is faster
- **Evidence**: Screenshot only for visual issues or final evidence
- **Be specific**: Report exact node paths for failures so they're actionable
