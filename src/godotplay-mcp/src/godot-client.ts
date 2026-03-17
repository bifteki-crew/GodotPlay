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
