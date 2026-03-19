#!/usr/bin/env node

import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { StdioServerTransport } from "@modelcontextprotocol/sdk/server/stdio.js";
import { z } from "zod";
import { GodotPlayClient } from "./godot-client.js";
import { exec, type ChildProcess } from "child_process";
import fs from "fs";
import path from "path";

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
  "Click a node in the running Godot instance",
  {
    nodePath: z.string().describe("Absolute node path"),
  },
  async ({ nodePath }) => {
    if (!godotClient) {
      return { content: [{ type: "text" as const, text: "No Godot instance. Use godot_launch first." }], isError: true };
    }
    const result = await godotClient.click(nodePath);
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

// --- Start ---

const transport = new StdioServerTransport();
await server.connect(transport);
