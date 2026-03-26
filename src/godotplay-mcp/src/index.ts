#!/usr/bin/env node

import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { StdioServerTransport } from "@modelcontextprotocol/sdk/server/stdio.js";
import { z } from "zod";
import { GodotPlayClient } from "./godot-client.js";
import { exec, type ChildProcess } from "child_process";
import fs from "fs";
import path from "path";
import { registerPrompts } from "./prompts.js";

let godotClient: GodotPlayClient | null = null;
let godotProcess: ChildProcess | null = null;
let projectPath: string | null = null;

const server = new McpServer({
  name: "godotplay-mcp",
  version: "0.2.0",
});

// --- Tree utilities ---

function truncateTree(node: any, maxDepth: number, currentDepth: number = 0): any {
  const result: any = {
    path: node.path,
    className: node.className,
    name: node.name,
  };

  // Copy inline properties
  if (node.properties && Object.keys(node.properties).length > 0) {
    result.properties = node.properties;
  }

  if (currentDepth < maxDepth && node.children?.length > 0) {
    result.children = node.children.map((c: any) =>
      truncateTree(c, maxDepth, currentDepth + 1)
    );
  } else if (node.children?.length > 0) {
    result._childCount = node.children.length;
  }

  return result;
}

function findSubtree(node: any, targetPath: string): any | null {
  if (node.path === targetPath) return node;
  if (node.children) {
    for (const child of node.children) {
      const found = findSubtree(child, targetPath);
      if (found) return found;
    }
  }
  return null;
}

// --- Game knowledge ---

function getKnowledgePath(): string {
  if (projectPath) {
    return path.join(projectPath, ".godotplay-map.json");
  }
  return ".godotplay-map.json";
}

function loadKnowledge(): any {
  try {
    const p = getKnowledgePath();
    if (fs.existsSync(p)) {
      return JSON.parse(fs.readFileSync(p, "utf-8"));
    }
  } catch {}
  return { screens: {}, navigation: [], lastUpdated: null };
}

function saveKnowledge(knowledge: any): void {
  knowledge.lastUpdated = new Date().toISOString();
  fs.writeFileSync(getKnowledgePath(), JSON.stringify(knowledge, null, 2));
}

// --- Tools ---

server.tool(
  "godot_launch",
  "Launch a Godot instance with the GodotPlay plugin",
  {
    projectPath: z.string().describe("Path to the Godot project directory"),
    headless: z.boolean().default(false).describe("Run in headless mode"),
    scene: z.string().optional().describe("Scene to load"),
    port: z.number().default(50051).describe("gRPC server port"),
    godotPath: z.string().default("godot").describe("Path to Godot executable"),
  },
  async (params) => {
    projectPath = params.projectPath;
    const args = ["--path", params.projectPath];
    if (params.headless) args.push("--headless");
    if (params.scene) args.push(params.scene);

    godotProcess = exec(`${params.godotPath} ${args.join(" ")}`);

    godotClient = new GodotPlayClient(`localhost:${params.port}`);
    const maxRetries = 30;
    for (let i = 0; i < maxRetries; i++) {
      try {
        const ping = await godotClient.ping();
        if (ping.ready) {
          // Load existing knowledge
          const knowledge = loadKnowledge();
          const knownScreens = Object.keys(knowledge.screens).length;
          return {
            content: [{
              type: "text" as const,
              text: `Godot launched. gRPC ready on port ${params.port}.${knownScreens > 0 ? ` Game map loaded: ${knownScreens} known screens.` : " No game map yet — explore to build one."}`
            }],
          };
        }
      } catch {
        await new Promise((r) => setTimeout(r, 1000));
      }
    }
    return {
      content: [{ type: "text" as const, text: "Failed to connect to Godot gRPC server." }],
      isError: true,
    };
  }
);

server.tool(
  "godot_inspect_tree",
  "Get the scene tree. Use nodePath to inspect a subtree, depth to control detail level (default 4).",
  {
    nodePath: z.string().optional().describe("Root node path to start from (e.g. /root/MainMenu). Omit for full tree."),
    depth: z.number().default(4).describe("Max depth to traverse (default 4, increase for deeper inspection)"),
  },
  async ({ nodePath, depth }) => {
    if (!godotClient) {
      return { content: [{ type: "text" as const, text: "No Godot instance. Use godot_launch first." }], isError: true };
    }
    // Pass nodePath and depth to the server — filtering happens server-side
    const tree = await godotClient.getSceneTree(nodePath, depth);

    if (tree.root?.properties?.error) {
      return { content: [{ type: "text" as const, text: `Node not found: ${nodePath}` }], isError: true };
    }

    const result: any = { currentScenePath: tree.currentScenePath, tree: tree.root };

    return { content: [{ type: "text" as const, text: JSON.stringify(result, null, 2) }] };
  }
);

server.tool(
  "godot_click",
  "Click a node in the running Godot instance. Supports left/right/middle click and double-click.",
  {
    nodePath: z.string().describe("Absolute node path"),
    button: z.enum(["left", "right", "middle"]).default("left").describe("Mouse button"),
    clickCount: z.number().int().min(1).max(2).default(1).describe("Click count (2 for double-click)"),
  },
  async ({ nodePath, button, clickCount }) => {
    if (!godotClient) {
      return { content: [{ type: "text" as const, text: "No Godot instance. Use godot_launch first." }], isError: true };
    }
    const buttonMap: Record<string, number> = { left: 1, right: 2, middle: 3 };
    const btn = buttonMap[button] || 1;

    let result;
    if (btn === 1 && clickCount === 1) {
      result = await godotClient.click(nodePath);
    } else {
      result = await godotClient.clickNode(nodePath, btn, clickCount);
    }
    return {
      content: [{ type: "text" as const, text: result.success ? `Clicked: ${nodePath}` : `Failed: ${result.error}` }],
      isError: !result.success,
    };
  }
);

server.tool(
  "godot_screenshot",
  "Take a screenshot of the running Godot instance (resized to ~960x540 JPEG)",
  {},
  async () => {
    if (!godotClient) {
      return { content: [{ type: "text" as const, text: "No Godot instance. Use godot_launch first." }], isError: true };
    }
    const screenshot = await godotClient.takeScreenshot();
    const base64 = Buffer.from(screenshot.pngData).toString("base64");
    return {
      content: [{ type: "image" as const, data: base64, mimeType: "image/jpeg" }],
    };
  }
);

server.tool(
  "godot_type",
  "Type text into a LineEdit or TextEdit node",
  {
    nodePath: z.string().describe("Absolute path to the text input node"),
    text: z.string().describe("Text to type"),
    clearFirst: z.boolean().default(false).describe("Clear existing text before typing"),
  },
  async ({ nodePath, text, clearFirst }) => {
    if (!godotClient) return { content: [{ type: "text" as const, text: "No Godot instance." }], isError: true };
    const result = await godotClient.type(nodePath, text, clearFirst);
    return { content: [{ type: "text" as const, text: result.success ? `Typed "${text}" into ${nodePath}` : `Failed: ${result.error}` }], isError: !result.success };
  }
);

server.tool(
  "godot_get_property",
  "Get all properties of a specific node (name, class, visible, size, position, text, disabled)",
  {
    nodePath: z.string().describe("Absolute node path"),
  },
  async ({ nodePath }) => {
    if (!godotClient) return { content: [{ type: "text" as const, text: "No Godot instance." }], isError: true };
    const props = await godotClient.getProperty(nodePath);
    return { content: [{ type: "text" as const, text: JSON.stringify(props.properties, null, 2) }] };
  }
);

server.tool(
  "godot_set_property",
  "Set a property on a node (value auto-parsed to bool/int/float/string)",
  {
    nodePath: z.string().describe("Absolute node path"),
    property: z.string().describe("Property name (e.g. 'text', 'visible', 'modulate')"),
    value: z.string().describe("Value as string"),
  },
  async ({ nodePath, property, value }) => {
    if (!godotClient) return { content: [{ type: "text" as const, text: "No Godot instance." }], isError: true };
    const result = await godotClient.setProperty(nodePath, property, value);
    return { content: [{ type: "text" as const, text: result.success ? `Set ${property}=${value} on ${nodePath}` : `Failed: ${result.error}` }], isError: !result.success };
  }
);

server.tool(
  "godot_load_scene",
  "Navigate to a different scene",
  {
    scenePath: z.string().describe("Scene resource path (e.g. res://scenes/game.tscn)"),
  },
  async ({ scenePath }) => {
    if (!godotClient) return { content: [{ type: "text" as const, text: "No Godot instance." }], isError: true };
    const result = await godotClient.loadScene(scenePath);
    return { content: [{ type: "text" as const, text: result.success ? `Loaded scene: ${scenePath}` : `Failed: ${result.error}` }], isError: !result.success };
  }
);

server.tool(
  "godot_wait",
  "Wait for a node to appear in the scene tree, or for a signal to fire",
  {
    nodePath: z.string().describe("Node path to wait for"),
    signal: z.string().optional().describe("If set, wait for this signal instead of node existence"),
    timeout: z.number().default(5000).describe("Timeout in milliseconds"),
  },
  async ({ nodePath, signal, timeout }) => {
    if (!godotClient) return { content: [{ type: "text" as const, text: "No Godot instance." }], isError: true };
    try {
      if (signal) {
        const result = await godotClient.waitForSignal(nodePath, signal, timeout);
        return { content: [{ type: "text" as const, text: `Signal "${result.signalName}" received from ${result.nodePath}` }] };
      } else {
        const result = await godotClient.waitForNode(nodePath, undefined, timeout);
        return { content: [{ type: "text" as const, text: `Node found: ${result.path}` }] };
      }
    } catch (err: any) {
      return { content: [{ type: "text" as const, text: `Timeout: ${err.message || err}` }], isError: true };
    }
  }
);

server.tool(
  "godot_get_scene",
  "Get info about the currently loaded scene",
  {},
  async () => {
    if (!godotClient) return { content: [{ type: "text" as const, text: "No Godot instance." }], isError: true };
    const info = await godotClient.getCurrentScene();
    return { content: [{ type: "text" as const, text: JSON.stringify(info, null, 2) }] };
  }
);

server.tool(
  "godot_visual_compare",
  "Compare current screenshot against a saved baseline. Saves new baseline if none exists. Returns similarity score.",
  {
    name: z.string().describe("Baseline name (e.g. 'main_menu', 'settings_screen')"),
    threshold: z.number().default(0.95).describe("Minimum similarity to pass (0.0-1.0)"),
  },
  async ({ name, threshold }) => {
    if (!godotClient) return { content: [{ type: "text" as const, text: "No Godot instance." }], isError: true };

    const screenshot = await godotClient.takeScreenshot();
    const imageData = Buffer.from(screenshot.pngData);

    // Compute simple hash
    const computeHash = (data: Buffer): bigint => {
      if (data.length < 64) return 0n;
      const step = Math.floor(data.length / 64);
      let total = 0n;
      for (let i = 0; i < 64; i++) total += BigInt(data[i * step]);
      const avg = total / 64n;
      let hash = 0n;
      for (let i = 0; i < 64; i++) {
        if (BigInt(data[i * step]) >= avg) hash |= (1n << BigInt(i));
      }
      return hash;
    };

    const currentHash = computeHash(imageData);

    // Load or create baseline
    const baselineDir = projectPath ? path.join(projectPath, ".godotplay-baselines") : ".godotplay-baselines";
    const hashFile = path.join(baselineDir, `${name}.hash`);

    if (!fs.existsSync(hashFile)) {
      fs.mkdirSync(baselineDir, { recursive: true });
      fs.writeFileSync(hashFile, currentHash.toString());
      fs.writeFileSync(path.join(baselineDir, `${name}.bin`), imageData);
      return { content: [{ type: "text" as const, text: `New baseline saved for "${name}". Take another screenshot later to compare.` }] };
    }

    const baselineHash = BigInt(fs.readFileSync(hashFile, "utf-8"));
    const xor = currentHash ^ baselineHash;
    let diffBits = 0;
    let temp = xor;
    while (temp > 0n) { diffBits += Number(temp & 1n); temp >>= 1n; }
    const similarity = 1.0 - (diffBits / 64.0);

    const passed = similarity >= threshold;
    const text = passed
      ? `PASS: "${name}" similarity ${(similarity * 100).toFixed(1)}% (threshold: ${(threshold * 100).toFixed(1)}%)`
      : `FAIL: "${name}" similarity ${(similarity * 100).toFixed(1)}% (threshold: ${(threshold * 100).toFixed(1)}%) — visual regression detected!`;

    return { content: [{ type: "text" as const, text }], isError: !passed };
  }
);

server.tool(
  "godot_shutdown",
  "Shut down the running Godot instance",
  {},
  async () => {
    if (godotClient) {
      try { await godotClient.shutdown(); } catch {}
      godotClient.close();
      godotClient = null;
    }
    if (godotProcess) {
      godotProcess.kill();
      godotProcess = null;
    }
    return { content: [{ type: "text" as const, text: "Godot shut down." }] };
  }
);

server.tool(
  "godot_mouse",
  "Mouse input: move, click, down, up, or wheel at viewport coordinates",
  {
    action: z.enum(["move", "click", "down", "up", "wheel"]).describe("Mouse action type"),
    x: z.number().describe("X position in viewport"),
    y: z.number().describe("Y position in viewport"),
    button: z.enum(["left", "right", "middle"]).default("left").describe("Mouse button"),
    clickCount: z.number().int().min(1).max(2).default(1).describe("Click count (2 for double-click)"),
    deltaX: z.number().int().default(0).describe("Horizontal scroll delta (for wheel)"),
    deltaY: z.number().int().default(0).describe("Vertical scroll delta (for wheel, negative=down)"),
    shift: z.boolean().default(false).describe("Hold Shift"),
    ctrl: z.boolean().default(false).describe("Hold Ctrl"),
    alt: z.boolean().default(false).describe("Hold Alt"),
    meta: z.boolean().default(false).describe("Hold Meta/Cmd"),
  },
  async ({ action, x, y, button, clickCount, deltaX, deltaY, shift, ctrl, alt, meta }) => {
    if (!godotClient) return { content: [{ type: "text" as const, text: "No Godot instance." }], isError: true };
    const buttonMap: Record<string, number> = { left: 1, right: 2, middle: 3 };
    const btn = buttonMap[button] || 1;

    let result;
    switch (action) {
      case "move":
        result = await godotClient.mouseMove(x, y);
        break;
      case "click":
        result = await godotClient.mouseClickAt(x, y, btn, clickCount, shift, ctrl, alt, meta);
        break;
      case "down":
        result = await godotClient.mouseButtonEvent(x, y, btn, true, false, shift, ctrl, alt, meta);
        break;
      case "up":
        result = await godotClient.mouseButtonEvent(x, y, btn, false, false, shift, ctrl, alt, meta);
        break;
      case "wheel":
        result = await godotClient.mouseWheel(x, y, deltaX, deltaY);
        break;
      default:
        return { content: [{ type: "text" as const, text: `Unknown mouse action: ${action}` }], isError: true };
    }
    return { content: [{ type: "text" as const, text: result.success ? `Mouse ${action} at (${x},${y})` : `Failed: ${result.error}` }], isError: !result.success };
  }
);

server.tool(
  "godot_key",
  "Keyboard input: press (down+up), down, or up a key. Supports key names like 'Enter', 'Space', 'A', 'F1', and modifiers.",
  {
    action: z.enum(["press", "down", "up"]).default("press").describe("Key action"),
    key: z.string().describe("Key name: 'Enter', 'Space', 'Escape', 'A'-'Z', 'F1'-'F12', 'Up', 'Down', etc."),
    shift: z.boolean().default(false),
    ctrl: z.boolean().default(false),
    alt: z.boolean().default(false),
    meta: z.boolean().default(false),
  },
  async ({ action, key, shift, ctrl, alt, meta }) => {
    if (!godotClient) return { content: [{ type: "text" as const, text: "No Godot instance." }], isError: true };

    let result;
    switch (action) {
      case "press":
        result = await godotClient.keyPress(key, shift, ctrl, alt, meta);
        break;
      case "down":
        result = await godotClient.keyDown(key, shift, ctrl, alt, meta);
        break;
      case "up":
        result = await godotClient.keyUp(key, shift, ctrl, alt, meta);
        break;
      default:
        return { content: [{ type: "text" as const, text: `Unknown key action: ${action}` }], isError: true };
    }
    const modifiers = [shift && "Shift", ctrl && "Ctrl", alt && "Alt", meta && "Meta"].filter(Boolean).join("+");
    const keyDesc = modifiers ? `${modifiers}+${key}` : key;
    return { content: [{ type: "text" as const, text: result.success ? `Key ${action}: ${keyDesc}` : `Failed: ${result.error}` }], isError: !result.success };
  }
);

server.tool(
  "godot_touch",
  "Touch input: tap, down, up, or drag on screen",
  {
    action: z.enum(["tap", "down", "up", "drag"]).describe("Touch action"),
    x: z.number().describe("X position"),
    y: z.number().describe("Y position"),
    toX: z.number().optional().describe("Drag target X (for drag action)"),
    toY: z.number().optional().describe("Drag target Y (for drag action)"),
    finger: z.number().int().min(0).max(9).default(0).describe("Finger/touch index (0-9)"),
  },
  async ({ action, x, y, toX, toY, finger }) => {
    if (!godotClient) return { content: [{ type: "text" as const, text: "No Godot instance." }], isError: true };

    let result;
    switch (action) {
      case "tap": {
        const downResult = await godotClient.touchEvent(finger, x, y, true);
        if (!downResult.success) {
          result = downResult;
          break;
        }
        result = await godotClient.touchEvent(finger, x, y, false);
        break;
      }
      case "down":
        result = await godotClient.touchEvent(finger, x, y, true);
        break;
      case "up":
        result = await godotClient.touchEvent(finger, x, y, false);
        break;
      case "drag":
        if (toX === undefined || toY === undefined) {
          return { content: [{ type: "text" as const, text: "toX and toY required for drag" }], isError: true };
        }
        result = await godotClient.touchDrag(x, y, toX, toY, finger);
        break;
      default:
        return { content: [{ type: "text" as const, text: `Unknown touch action: ${action}` }], isError: true };
    }
    return { content: [{ type: "text" as const, text: result.success ? `Touch ${action} at (${x},${y})` : `Failed: ${result.error}` }], isError: !result.success };
  }
);

server.tool(
  "godot_gesture",
  "Gesture input: pinch (zoom) or pan at a position",
  {
    type: z.enum(["pinch", "pan"]).describe("Gesture type"),
    x: z.number().describe("Center X position"),
    y: z.number().describe("Center Y position"),
    factor: z.number().default(1).describe("Pinch factor (>1 zoom in, <1 zoom out)"),
    deltaX: z.number().default(0).describe("Pan horizontal delta"),
    deltaY: z.number().default(0).describe("Pan vertical delta"),
  },
  async ({ type, x, y, factor, deltaX, deltaY }) => {
    if (!godotClient) return { content: [{ type: "text" as const, text: "No Godot instance." }], isError: true };
    const result = await godotClient.gesture(type, x, y, factor, deltaX, deltaY);
    return { content: [{ type: "text" as const, text: result.success ? `Gesture ${type} at (${x},${y})` : `Failed: ${result.error}` }], isError: !result.success };
  }
);

server.tool(
  "godot_gamepad",
  "Gamepad input: press buttons (a, b, x, y, start, dpad_up, etc.) or move axes (left_x, left_y, etc.)",
  {
    action: z.enum(["button", "axis"]).describe("Input type"),
    button: z.string().optional().describe("Button name: a, b, x, y, lb, rb, start, back, dpad_up, dpad_down, dpad_left, dpad_right"),
    pressed: z.boolean().default(true).describe("Button pressed state"),
    axis: z.string().optional().describe("Axis name: left_x, left_y, right_x, right_y, trigger_left, trigger_right"),
    value: z.number().default(0).describe("Axis value (-1.0 to 1.0)"),
    device: z.number().default(0).describe("Device index"),
  },
  async ({ action, button, pressed, axis, value, device }) => {
    if (!godotClient) return { content: [{ type: "text" as const, text: "No Godot instance." }], isError: true };

    let result;
    if (action === "button") {
      if (!button) return { content: [{ type: "text" as const, text: "button param required for button action" }], isError: true };
      result = await godotClient.gamepadButton(button, pressed, device);
    } else {
      if (!axis) return { content: [{ type: "text" as const, text: "axis param required for axis action" }], isError: true };
      result = await godotClient.gamepadAxis(axis, value, device);
    }
    return { content: [{ type: "text" as const, text: result.success ? `Gamepad ${action}: ${button || axis}` : `Failed: ${result.error}` }], isError: !result.success };
  }
);

server.tool(
  "godot_action",
  "Trigger a Godot Input Map action (e.g. 'ui_accept', 'jump', 'move_left'). Press+release by default.",
  {
    action: z.string().describe("Input Map action name"),
    pressed: z.boolean().optional().describe("If set, only press (true) or release (false). Omit for press+release."),
    strength: z.number().default(1).describe("Action strength 0.0-1.0"),
  },
  async ({ action, pressed, strength }) => {
    if (!godotClient) return { content: [{ type: "text" as const, text: "No Godot instance." }], isError: true };

    let result;
    if (pressed !== undefined) {
      result = await godotClient.actionEvent(action, pressed, strength);
    } else {
      result = await godotClient.actionPress(action, strength);
    }
    return { content: [{ type: "text" as const, text: result.success ? `Action: ${action}` : `Failed: ${result.error}` }], isError: !result.success };
  }
);

server.tool(
  "godot_hover",
  "Move mouse to hover over a node (resolves node center position automatically)",
  {
    nodePath: z.string().describe("Absolute node path"),
  },
  async ({ nodePath }) => {
    if (!godotClient) return { content: [{ type: "text" as const, text: "No Godot instance." }], isError: true };
    const result = await godotClient.hover(nodePath);
    return { content: [{ type: "text" as const, text: result.success ? `Hovering: ${nodePath}` : `Failed: ${result.error}` }], isError: !result.success };
  }
);

server.tool(
  "godot_drag",
  "Drag from one node to another (mouse down at source, move, mouse up at target)",
  {
    fromNodePath: z.string().describe("Source node path"),
    toNodePath: z.string().describe("Target node path"),
  },
  async ({ fromNodePath, toNodePath }) => {
    if (!godotClient) return { content: [{ type: "text" as const, text: "No Godot instance." }], isError: true };
    const result = await godotClient.dragTo(fromNodePath, toNodePath);
    return { content: [{ type: "text" as const, text: result.success ? `Dragged ${fromNodePath} → ${toNodePath}` : `Failed: ${result.error}` }], isError: !result.success };
  }
);

server.tool(
  "godot_scroll",
  "Scroll at a node's position (resolves center automatically)",
  {
    nodePath: z.string().describe("Node path to scroll at"),
    deltaX: z.number().default(0).describe("Horizontal scroll"),
    deltaY: z.number().describe("Vertical scroll (positive=up, negative=down)"),
  },
  async ({ nodePath, deltaX, deltaY }) => {
    if (!godotClient) return { content: [{ type: "text" as const, text: "No Godot instance." }], isError: true };
    const result = await godotClient.scrollNode(nodePath, deltaX, deltaY);
    return { content: [{ type: "text" as const, text: result.success ? `Scrolled at ${nodePath}` : `Failed: ${result.error}` }], isError: !result.success };
  }
);

server.tool(
  "godot_learn",
  "Save discovered knowledge about the game (screens, buttons, navigation paths) for future sessions",
  {
    screenName: z.string().describe("Name of the screen (e.g. 'main_menu', 'research_view')"),
    scenePath: z.string().optional().describe("Scene file path (e.g. res://scenes/main_menu.tscn)"),
    buttons: z.array(z.object({
      name: z.string(),
      path: z.string(),
      action: z.string().optional(),
    })).optional().describe("Clickable buttons on this screen"),
    navigatesTo: z.array(z.object({
      target: z.string().describe("Target screen name"),
      via: z.string().describe("Node path to click"),
    })).optional().describe("Navigation links from this screen"),
    notes: z.string().optional().describe("Any useful observations about this screen"),
  },
  async ({ screenName, scenePath, buttons, navigatesTo, notes }) => {
    const knowledge = loadKnowledge();

    knowledge.screens[screenName] = {
      scenePath: scenePath || knowledge.screens[screenName]?.scenePath,
      buttons: buttons || knowledge.screens[screenName]?.buttons || [],
      notes: notes || knowledge.screens[screenName]?.notes,
    };

    if (navigatesTo) {
      for (const nav of navigatesTo) {
        const existing = knowledge.navigation.find(
          (n: any) => n.from === screenName && n.to === nav.target
        );
        if (existing) {
          existing.via = nav.via;
        } else {
          knowledge.navigation.push({ from: screenName, to: nav.target, via: nav.via });
        }
      }
    }

    saveKnowledge(knowledge);
    const screenCount = Object.keys(knowledge.screens).length;
    const navCount = knowledge.navigation.length;
    return {
      content: [{
        type: "text" as const,
        text: `Saved knowledge for "${screenName}". Game map: ${screenCount} screens, ${navCount} navigation paths.`
      }],
    };
  }
);

server.tool(
  "godot_recall",
  "Recall saved knowledge about the game — screens, buttons, navigation paths from previous sessions",
  {
    screenName: z.string().optional().describe("Specific screen to recall (omit for full map)"),
  },
  async ({ screenName }) => {
    const knowledge = loadKnowledge();

    if (screenName) {
      const screen = knowledge.screens[screenName];
      if (!screen) {
        return { content: [{ type: "text" as const, text: `No knowledge about screen "${screenName}". Known: ${Object.keys(knowledge.screens).join(", ") || "none"}` }] };
      }
      const navFrom = knowledge.navigation.filter((n: any) => n.from === screenName);
      return {
        content: [{
          type: "text" as const,
          text: JSON.stringify({ screen: screenName, ...screen, navigatesTo: navFrom }, null, 2)
        }],
      };
    }

    // Full map summary
    const summary = {
      screens: Object.keys(knowledge.screens),
      navigation: knowledge.navigation,
      lastUpdated: knowledge.lastUpdated,
      details: knowledge.screens,
    };
    return { content: [{ type: "text" as const, text: JSON.stringify(summary, null, 2) }] };
  }
);

// --- Resources ---

server.resource(
  "game-map",
  "godot://game-map",
  { description: "Saved game knowledge — screens, buttons, navigation" },
  async () => {
    const knowledge = loadKnowledge();
    return {
      contents: [{
        uri: "godot://game-map",
        text: JSON.stringify(knowledge, null, 2),
        mimeType: "application/json",
      }],
    };
  }
);

// --- Prompts (skills) ---
registerPrompts(server);

// --- Start ---

const transport = new StdioServerTransport();
await server.connect(transport);
