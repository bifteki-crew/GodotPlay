#!/usr/bin/env node

import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { StdioServerTransport } from "@modelcontextprotocol/sdk/server/stdio.js";
import { z } from "zod";
import { GodotPlayClient } from "./godot-client.js";
import { exec, type ChildProcess } from "child_process";

let godotClient: GodotPlayClient | null = null;
let godotProcess: ChildProcess | null = null;

const server = new McpServer({
  name: "godotplay-mcp",
  version: "0.1.0",
});

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
  async ({ projectPath, headless, scene, port, godotPath }) => {
    const args = ["--path", projectPath];
    if (headless) args.push("--headless");
    if (scene) args.push(scene);

    godotProcess = exec(`${godotPath} ${args.join(" ")}`);

    godotClient = new GodotPlayClient(`localhost:${port}`);
    const maxRetries = 30;
    for (let i = 0; i < maxRetries; i++) {
      try {
        const ping = await godotClient.ping();
        if (ping.ready) {
          return {
            content: [{ type: "text" as const, text: `Godot launched. gRPC ready on port ${port}. Version: ${ping.version}` }],
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
  "Get the current scene tree",
  {},
  async () => {
    if (!godotClient) {
      return { content: [{ type: "text" as const, text: "No Godot instance. Use godot_launch first." }], isError: true };
    }
    const tree = await godotClient.getSceneTree();
    return { content: [{ type: "text" as const, text: JSON.stringify(tree, null, 2) }] };
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
  "Take a screenshot of the running Godot instance",
  {},
  async () => {
    if (!godotClient) {
      return { content: [{ type: "text" as const, text: "No Godot instance. Use godot_launch first." }], isError: true };
    }
    const screenshot = await godotClient.takeScreenshot();
    const base64 = Buffer.from(screenshot.pngData).toString("base64");
    return {
      content: [{ type: "image" as const, data: base64, mimeType: "image/png" }],
    };
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

server.resource(
  "scene-tree",
  "godot://scene-tree",
  { description: "Current Godot scene tree as JSON" },
  async () => {
    if (!godotClient) {
      return { contents: [{ uri: "godot://scene-tree", text: "No Godot instance running.", mimeType: "text/plain" }] };
    }
    const tree = await godotClient.getSceneTree();
    return { contents: [{ uri: "godot://scene-tree", text: JSON.stringify(tree, null, 2), mimeType: "application/json" }] };
  }
);

const transport = new StdioServerTransport();
await server.connect(transport);
