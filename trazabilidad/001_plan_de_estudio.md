# Respuesta 001 - Plan de Estudio del Repositorio

## Fecha
2026-05-22

## Arquitectura General

```
Cliente IA (Claude)
    ↕  stdio (JSON-RPC)
MCP Server (TypeScript) → server/src/tools/   ← tools definidas aquí
    ↕  TCP localhost:8080 (JSON-RPC)
Plugin Revit (C#) → plugin/Core/SocketService.cs  ← enruta comandos
    ↕  ExternalEvent (hilo UI de Revit)
Command Set (C#) → commandset/Commands/ + Services/  ← lógica real con Revit API
```

Cada herramienta del MCP:
1. TypeScript define **nombre**, **descripción**, **schema Zod** y envía comando por TCP
2. Plugin C# recibe JSON-RPC, busca comando en `command.json` y lo ejecuta
3. `EventHandler` C# ejecuta la lógica real contra la **Revit API**

---

## Herramientas Disponibles (26 tools)

### Lectura / Consulta
| Tool | Descripción |
|------|-------------|
| `say_hello` | Prueba de conexión (muestra diálogo en Revit) |
| `get_current_view_info` | Info de la vista activa (tipo, nombre, escala) |
| `get_current_view_elements` | Elementos visibles en la vista actual |
| `get_available_family_types` | Tipos de familia disponibles en el proyecto |
| `get_selected_elements` | Elementos actualmente seleccionados |
| `get_material_quantities` | Cálculo de cantidades de materiales |
| `ai_element_filter` | Filtro inteligente de elementos por múltiples criterios |
| `analyze_model_statistics` | Estadísticas de complejidad del modelo |

### Creación
| Tool | Descripción |
|------|-------------|
| `create_point_based_element` | Puertas, ventanas, mobiliario (batch) |
| `create_line_based_element` | Muros, vigas, tuberías (batch) |
| `create_surface_based_element` | Pisos, cielos, cubiertas (batch) |
| `create_grid` | Sistema de rejillas con espaciado inteligente |
| `create_level` | Niveles en elevaciones especificadas (batch) |
| `create_room` | Crear y colocar habitaciones (batch) |
| `create_dimensions` | Anotaciones de cota (batch) |
| `create_structural_framing_system` | Sistema de vigas estructurales |

### Modificación / Eliminación
| Tool | Descripción |
|------|-------------|
| `delete_element` | Eliminar elementos por ID |
| `operate_element` | Seleccionar, colorear, ocultar, resaltar elementos |
| `color_elements` | Colorear elementos según valor de parámetro |

### Anotación
| Tool | Descripción |
|------|-------------|
| `tag_all_walls` | Etiquetar todos los muros en la vista actual |
| `tag_all_rooms` | Etiquetar todas las habitaciones en la vista actual |

### Exportación / Datos
| Tool | Descripción |
|------|-------------|
| `export_room_data` | Exportar datos de habitaciones |
| `send_code_to_revit` | Ejecutar código C# dinámico en Revit (Roslyn) |

### Base de datos local (SQLite — no requiere Revit)
| Tool | Descripción |
|------|-------------|
| `store_project_data` | Guardar metadatos de proyecto en SQLite local |
| `store_room_data` | Guardar datos de habitaciones en SQLite local |
| `query_stored_data` | Consultar datos almacenados localmente |

---

## Cómo Crear una Nueva Herramienta

Se necesitan tocar **3 capas**:

### 1. TypeScript — Definir la tool MCP
**Archivo:** `server/src/tools/<nombre>.ts` (nuevo)

```typescript
import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { withRevitConnection } from "../utils/ConnectionManager.js";

export function registerMiToolTool(server: McpServer) {
  server.tool("mi_tool", "Descripción", { /* schema Zod */ },
    async (args, extra) => {
      const response = await withRevitConnection(revitClient =>
        revitClient.sendCommand("mi_tool", args));
      return { content: [{ type: "text", text: JSON.stringify(response) }] };
    }
  );
}
```

> `register.ts` **autodetecta** automáticamente cualquier archivo nuevo en `server/src/tools/` que exporte una función que empiece con `register`.

### 2. command.json — Registrar el comando
**Archivo:** `command.json` (editar)

```json
{
  "commandName": "mi_tool",
  "description": "Descripción",
  "assemblyPath": "RevitMCPCommandSet.dll"
}
```

### 3. C# Command Set — Lógica real de Revit

- **EventHandler:** `commandset/Services/<Nombre>EventHandler.cs` — Implementa `IExternalEventHandler` + `IWaitableExternalEventHandler`, usa `ManualResetEvent`, aquí va toda la Revit API
- **Command:** `commandset/Commands/<Nombre>Command.cs` — Hereda `ExternalEventCommandBase`, `CommandName` debe coincidir con `command.json`, parsea `JObject`, llama `RaiseAndWaitForCompletion`
- **Modelo (opcional):** `commandset/Models/<categoria>/<Modelo>.cs` — DTOs tipados

---

## Resumen de Archivos por Capa

| Capa | Archivo | Acción |
|------|---------|--------|
| TypeScript | `server/src/tools/<nueva_tool>.ts` | **Crear** |
| TypeScript | `server/src/tools/register.ts` | **Nada** — detección automática |
| Manifiesto | `command.json` | **Editar** |
| C# Command | `commandset/Commands/<NuevoCommand>.cs` | **Crear** |
| C# EventHandler | `commandset/Services/<NuevoEventHandler>.cs` | **Crear** |
| C# Modelo | `commandset/Models/<categoria>/<Modelo>.cs` | **Opcional** |

> Las unidades en TypeScript están en **milímetros**. La conversión a pies se hace en el EventHandler C# dividiendo por 304.8.
