import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { SKILLS } from "./skill-content.js";

const PROMPT_DEFS = [
  {
    name: "godot_explore",
    description: "Launch and interactively explore a Godot project — take screenshots, inspect scene tree, click UI elements",
    skillKey: "explore",
  },
  {
    name: "godot_test",
    description: "Systematically test Godot UI with navigation verification and test report generation",
    skillKey: "test",
  },
  {
    name: "godot_debug",
    description: "Reproduce and diagnose UI problems with root cause analysis",
    skillKey: "debug",
  },
  {
    name: "godot_coverage",
    description: "Analyze UI test coverage and identify gaps",
    skillKey: "coverage",
  },
] as const;

export function registerPrompts(server: McpServer): void {
  for (const def of PROMPT_DEFS) {
    server.prompt(def.name, def.description, () => ({
      messages: [
        {
          role: "user" as const,
          content: { type: "text" as const, text: SKILLS[def.skillKey] },
        },
      ],
    }));
  }
}
