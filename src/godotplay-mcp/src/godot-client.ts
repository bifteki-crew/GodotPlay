import * as grpc from "@grpc/grpc-js";
import * as protoLoader from "@grpc/proto-loader";
import path from "path";
import { fileURLToPath } from "url";

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const PROTO_PATH = path.resolve(__dirname, "../../../proto/godotplay.proto");

const packageDefinition = protoLoader.loadSync(PROTO_PATH, {
  keepCase: false,
  longs: String,
  enums: String,
  defaults: true,
  oneofs: true,
});

const protoDescriptor = grpc.loadPackageDefinition(packageDefinition) as any;
const GodotPlayServiceDef = protoDescriptor.godotplay.GodotPlayService;

export class GodotPlayClient {
  private client: any;

  constructor(address: string = "localhost:50051") {
    this.client = new GodotPlayServiceDef(
      address,
      grpc.credentials.createInsecure()
    );
  }

  ping(): Promise<{ version: string; ready: boolean }> {
    return this.callUnary("ping", {});
  }

  getSceneTree(nodePath?: string, maxDepth?: number): Promise<any> {
    return this.callUnary("getSceneTree", {
      nodePath: nodePath || "",
      maxDepth: maxDepth || 0,
    });
  }

  findNodes(query: {
    path?: string;
    className?: string;
    namePattern?: string;
    group?: string;
  }): Promise<{ nodes: any[] }> {
    return this.callUnary("findNodes", query);
  }

  click(nodePath: string): Promise<{ success: boolean; error: string }> {
    return this.callUnary("click", { path: nodePath });
  }

  takeScreenshot(
    nodePath?: string
  ): Promise<{ pngData: Buffer; width: number; height: number }> {
    return this.callUnary("takeScreenshot", { nodePath: nodePath || "" });
  }

  type(nodePath: string, text: string, clearFirst: boolean = false): Promise<{ success: boolean; error: string }> {
    return this.callUnary("type", { nodePath, text, clearFirst });
  }

  getProperty(nodePath: string): Promise<{ properties: Record<string, string> }> {
    return this.callUnary("getNodeProperties", { path: nodePath });
  }

  setProperty(nodePath: string, propertyName: string, value: string): Promise<{ success: boolean; error: string }> {
    return this.callUnary("setProperty", { nodePath, propertyName, value });
  }

  loadScene(scenePath: string): Promise<{ success: boolean; error: string }> {
    return this.callUnary("loadScene", { scenePath });
  }

  getCurrentScene(): Promise<{ scenePath: string; rootNodePath: string; rootClassName: string }> {
    return this.callUnary("getCurrentScene", {});
  }

  waitForNode(nodePath: string, className?: string, timeoutMs: number = 5000): Promise<{ path: string }> {
    return this.callUnary("waitForNode", { nodePath, className: className || "", timeoutMs });
  }

  waitForSignal(nodePath: string, signalName: string, timeoutMs: number = 5000): Promise<{ signalName: string; nodePath: string; args: string[] }> {
    return this.callUnary("waitForSignal", { nodePath, signalName, timeoutMs });
  }

  shutdown(): Promise<void> {
    return this.callUnary("shutdown", {});
  }

  // --- Low-Level Input ---

  mouseMove(x: number, y: number, speed: number = 0): Promise<{ success: boolean; error: string }> {
    return this.callUnary("mouseMove", { x, y, speed });
  }

  mouseButtonEvent(x: number, y: number, button: number = 1, pressed: boolean = true, doubleClick: boolean = false): Promise<{ success: boolean; error: string }> {
    return this.callUnary("mouseButtonEvent", { x, y, button, pressed, doubleClick });
  }

  mouseClickAt(x: number, y: number, button: number = 1, clickCount: number = 1): Promise<{ success: boolean; error: string }> {
    return this.callUnary("mouseClickAt", { x, y, button, clickCount });
  }

  mouseWheel(x: number, y: number, deltaX: number = 0, deltaY: number = 0): Promise<{ success: boolean; error: string }> {
    return this.callUnary("mouseWheel", { x, y, deltaX, deltaY });
  }

  keyDown(keyLabel: string, shift: boolean = false, ctrl: boolean = false, alt: boolean = false, meta: boolean = false): Promise<{ success: boolean; error: string }> {
    return this.callUnary("keyDown", { keyLabel, pressed: true, shift, ctrl, alt, meta });
  }

  keyUp(keyLabel: string): Promise<{ success: boolean; error: string }> {
    return this.callUnary("keyUp", { keyLabel, pressed: false });
  }

  keyPress(keyLabel: string, shift: boolean = false, ctrl: boolean = false, alt: boolean = false, meta: boolean = false): Promise<{ success: boolean; error: string }> {
    return this.callUnary("keyPress", { keyLabel, shift, ctrl, alt, meta });
  }

  touchEvent(index: number, x: number, y: number, pressed: boolean): Promise<{ success: boolean; error: string }> {
    return this.callUnary("touchEvent", { index, x, y, pressed });
  }

  touchDrag(fromX: number, fromY: number, toX: number, toY: number, index: number = 0, steps: number = 10, durationMs: number = 300): Promise<{ success: boolean; error: string }> {
    return this.callUnary("touchDrag", { index, fromX, fromY, toX, toY, steps, durationMs });
  }

  gesture(type: string, x: number, y: number, factor: number = 1, deltaX: number = 0, deltaY: number = 0): Promise<{ success: boolean; error: string }> {
    return this.callUnary("gesture", { type, x, y, factor, deltaX, deltaY });
  }

  gamepadButton(buttonName: string, pressed: boolean = true, device: number = 0, pressure: number = 1): Promise<{ success: boolean; error: string }> {
    return this.callUnary("gamepadButtonEvent", { buttonName, pressed, device, pressure });
  }

  gamepadAxis(axisName: string, value: number, device: number = 0): Promise<{ success: boolean; error: string }> {
    return this.callUnary("gamepadAxisEvent", { axisName, value, device });
  }

  actionEvent(action: string, pressed: boolean, strength: number = 1): Promise<{ success: boolean; error: string }> {
    return this.callUnary("actionEvent", { action, pressed, strength });
  }

  actionPress(action: string, strength: number = 1, durationMs: number = 100): Promise<{ success: boolean; error: string }> {
    return this.callUnary("actionPress", { action, strength, durationMs });
  }

  // --- High-Level Input ---

  hover(nodePath: string): Promise<{ success: boolean; error: string }> {
    return this.callUnary("hover", { nodePath });
  }

  dragTo(fromNodePath: string, toNodePath: string, steps: number = 10, durationMs: number = 300): Promise<{ success: boolean; error: string }> {
    return this.callUnary("dragTo", { fromNodePath, toNodePath, steps, durationMs });
  }

  clickNode(nodePath: string, button: number = 1, clickCount: number = 1): Promise<{ success: boolean; error: string }> {
    return this.callUnary("clickNode", { nodePath, button, clickCount });
  }

  scrollNode(nodePath: string, deltaX: number = 0, deltaY: number = 0): Promise<{ success: boolean; error: string }> {
    return this.callUnary("scrollNode", { nodePath, deltaX, deltaY });
  }

  close(): void {
    this.client.close();
  }

  private callUnary(method: string, request: any): Promise<any> {
    return new Promise((resolve, reject) => {
      this.client[method](request, (err: any, response: any) => {
        if (err) reject(err);
        else resolve(response);
      });
    });
  }
}
