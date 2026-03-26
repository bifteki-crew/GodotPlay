---
name: debug
description: Use to reproduce and debug a UI problem in a Godot project. Launches Godot, navigates to the problem area, inspects nodes, takes screenshots, and suggests fixes with file references.
---

# Debug a Godot UI Problem

Reproduce and diagnose a UI issue in a running Godot project.

## Step 1: Understand the Problem

Ask the user:
- What screen/scene is the problem on?
- What should happen vs what actually happens?
- Steps to reproduce?

## Step 2: Load Knowledge and Launch

```
godot_recall()
godot_launch(projectPath: "...", headless: false)
```

Use the game map to navigate directly to the problem screen.

## Step 3: Navigate to the Problem

If the screen is known in the game map, use the navigation path.
Otherwise:

```
godot_load_scene(scenePath: "res://scenes/problem_screen.tscn")
```

Then screenshot to confirm we're in the right place:

```
godot_screenshot()
```

## Step 4: Inspect the Problem Area

```
godot_inspect_tree(nodePath: "/root/ProblemScreen", depth: 8)
```

Look for:
- Missing nodes (expected but not in tree)
- Wrong class types
- Unexpected parent/child relationships
- Duplicate nodes

## Step 5: Check Properties

For each suspect node:

```
godot_get_property(nodePath: "/root/Screen/SuspectNode")
```

Common things to check:
- `visible` — is it hidden?
- `size` — is it zero-sized?
- `position` — is it off-screen?
- `disabled` — is it disabled?
- `text` — does it show the right content?

## Step 6: Reproduce the Bug

Follow the user's reproduction steps using:

```
godot_click(nodePath: "...")        // Click buttons
godot_type(nodePath: "...", text: "...")  // Enter text
godot_wait(nodePath: "...", timeout: 3000)  // Wait for response
godot_screenshot()                  // Capture the buggy state
```

After each step, check if the bug manifests:

```
godot_get_property(nodePath: "...")  // Verify state
godot_inspect_tree(nodePath: "...")  // Check tree changes
```

## Step 7: Diagnose

Based on the inspection, determine the root cause:

| Symptom | Likely Cause |
|---------|-------------|
| Node missing from tree | Not instantiated, wrong scene, conditional logic |
| Node visible=false | Hidden by script, wrong initial state |
| Node size=0,0 | Layout issue, missing size flags, wrong container |
| Button disabled | Script sets disabled, wrong game state |
| Click does nothing | Wrong signal connection, handler error |
| Text wrong | Data binding issue, wrong property |
| Visual glitch | Z-order, overlapping nodes, shader issue |

## Step 8: Find the Source Code

Use the project's file structure to locate the relevant scripts:
- Scene file: `res://scenes/...tscn` → contains node structure
- Script file: Look for `script` property on nodes
- Signal connections: Check `_ready()` functions for `.connect()` calls

Read the source files to confirm the diagnosis:
```
Read the .gd or .cs file that controls the problem node
```

## Step 9: Suggest Fix

Provide:
1. **Root cause** — one sentence explaining why
2. **Fix** — exact code change with file:line reference
3. **Verification** — how to test the fix worked

## Step 10: Verify (if user applies fix)

After the fix is applied:
1. Shutdown Godot: `godot_shutdown()`
2. Rebuild if needed
3. Relaunch: `godot_launch(...)`
4. Reproduce steps again
5. Confirm the issue is resolved

## Principles

- **Reproduce first**: Don't guess — see the bug happen
- **Data over screenshots**: Use `godot_get_property` for state, screenshots for visual issues
- **Be specific**: Exact node paths, exact property values, exact file:line references
- **One thing at a time**: Isolate the root cause before suggesting fixes
