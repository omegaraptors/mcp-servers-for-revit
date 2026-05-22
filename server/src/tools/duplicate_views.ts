import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { withRevitConnection } from "../utils/ConnectionManager.js";

export function registerDuplicateViewsTool(server: McpServer) {
  server.tool(
    "duplicate_views",
    "Duplicate one or more views in Revit. Supports batch duplication with configurable suffix, duplicate option (WithDetailing, Duplicate, AsDependent), and optional view type filtering. Can use current UI selection or specific view IDs.",
    {
      suffix: z
        .string()
        .default("_Copy")
        .describe("Suffix to append to the duplicated view name (e.g., '_Copy', '_BACKUP')"),
      duplicateOption: z
        .enum(["Duplicate", "WithDetailing", "AsDependent"])
        .default("WithDetailing")
        .describe("Duplicate option: 'Duplicate' (no detailing), 'WithDetailing' (copy elements), 'AsDependent' (dependent view)"),
      targetViewTypes: z
        .array(z.string())
        .optional()
        .describe("Filter by view types (e.g., ['FloorPlan', 'Section', 'Elevation', 'CeilingPlan', 'ThreeD', 'DraftingView', 'Schedule']). If omitted, all supported types are included."),
      useSelection: z
        .boolean()
        .default(true)
        .describe("If true, uses the current selection from the Project Browser. If false, provide viewIds."),
      viewIds: z
        .array(z.number())
        .optional()
        .describe("Specific Element IDs of views to duplicate (required when useSelection=false)"),
    },
    async (args, extra) => {
      const params = {
        suffix: args.suffix,
        duplicateOption: args.duplicateOption,
        targetViewTypes: args.targetViewTypes,
        useSelection: args.useSelection,
        viewIds: args.viewIds,
      };

      try {
        const response = await withRevitConnection(async (revitClient) => {
          return await revitClient.sendCommand("duplicate_views", params);
        });

        return {
          content: [
            {
              type: "text",
              text: JSON.stringify(response, null, 2),
            },
          ],
        };
      } catch (error) {
        return {
          content: [
            {
              type: "text",
              text: `Duplicate views failed: ${
                error instanceof Error ? error.message : String(error)
              }`,
            },
          ],
        };
      }
    }
  );
}
