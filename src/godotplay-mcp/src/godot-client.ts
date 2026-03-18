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

  getSceneTree(): Promise<any> {
    return this.callUnary("getSceneTree", {});
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
