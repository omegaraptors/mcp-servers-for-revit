# opencode.md — mcp-servers-for-revit

## Proyecto
Este repositorio es un fork del proyecto [mcp-servers-for-revit](https://github.com/DTDucas/mcp-servers-for-revit). Es un **MCP Server** que expone herramientas de IA (Claude, opencode, etc.) para operar sobre **Autodesk Revit**.

## Arquitectura (3 capas)

```
Cliente IA (CLI)
    ↕  stdio (MCP protocol) via opencode.json
server/  (TypeScript) → tools/, utils/, database/
    ↕  TCP localhost:8080 (JSON-RPC 2.0)
plugin/  (C# Revit Add-in) → SocketService, CommandManager
    ↕  ExternalEvent (hilo UI de Revit)
commandset/  (C# Commands) → Commands/, Services/, Models/
```

Cada tool del MCP fluye así:
1. **TypeScript** define nombre, descripción, schema Zod, envía comando por TCP
2. **Plugin C#** recibe JSON-RPC, busca comando en `command.json`, lo ejecuta
3. **EventHandler C#** ejecuta la lógica real contra la Revit API

## Estructura del repositorio

```
/
├── opencode.json          → Configuración MCP para opencode
├── opencode.md            ← ESTE ARCHIVO (instrucciones para la IA)
├── command.json           → Manifiesto que mapea nombres de comando → DLL
├── trazabilidad/          → Documentación de respuestas guardadas
│   ├── 001_plan_de_estudio.md
│   └── 002_patron_completo_y_plan_implementacion.md
│
├── server/                → MCP Server (TypeScript, Node.js)
│   ├── src/
│   │   ├── index.ts       → Entry point (crea McpServer, registra tools)
│   │   ├── tools/         → 1 archivo por tool (autodetectados)
│   │   │   ├── register.ts   → Auto-descubre y registra tools
│   │   │   ├── say_hello.ts  → Tool simple (referencia)
│   │   │   ├── duplicate_views.ts  → Tool creada para duplicar vistas
│   │   │   └── *.ts          → Resto de tools
│   │   ├── utils/         → ConnectionManager, SocketClient
│   │   ├── database/      → SQLite local (projects, rooms)
│   │   └── build/         → JS compilado
│   └── package.json
│
├── plugin/                → Plugin Revit (C# .NET)
│   ├── Core/              → SocketService, CommandManager, ExternalEventManager
│   ├── Configuration/     → Config, commandRegistry
│   └── UI/                → SettingsWindow
│
├── commandset/            → Implementaciones de comandos (C# .NET)
│   ├── Commands/          → Por categoría
│   │   ├── Views/DuplicateViewsCommand.cs  → Comando para duplicar vistas
│   │   └── ...
│   ├── Services/          → EventHandlers
│   │   ├── Views/DuplicateViewsEventHandler.cs
│   │   └── ...
│   ├── Models/            → DTOs
│   │   ├── Views/DuplicateViewsInfo.cs
│   │   └── ...
│   └── Utils/
│
└── tests/                 → Integration tests
```

## Tools nativas disponibles (27, llamo directo desde aquí)

Son tools del sistema `revit_*` que puedo invocar sin intermediarios.

### Lectura/Consulta
- `revit_say_hello` → Prueba de conexión (diálogo en Revit)
- `revit_get_current_view_info` → Info de vista activa
- `revit_get_current_view_elements` → Elementos visibles en vista actual
- `revit_get_available_family_types` → Tipos de familia disponibles
- `revit_get_selected_elements` → Elementos seleccionados
- `revit_get_material_quantities` → Cantidades de materiales
- `revit_ai_element_filter` → Filtro inteligente por criterios
- `revit_analyze_model_statistics` → Estadísticas del modelo
- `revit_query_stored_data` → Consultar datos locales SQLite
- `revit_export_room_data` → Exportar datos de habitaciones

### Creación
- `revit_create_point_based_element` → Puertas, ventanas, mobiliario
- `revit_create_line_based_element` → Muros, vigas, tuberías
- `revit_create_surface_based_element` → Pisos, cielos, cubiertas
- `revit_create_grid` → Rejillas con espaciado
- `revit_create_level` → Niveles
- `revit_create_room` → Habitaciones
- `revit_create_dimensions` → Cotas
- `revit_create_structural_framing_system` → Vigas estructurales

### Modificación/Eliminación
- `revit_delete_element` → Eliminar elementos por ID
- `revit_operate_element` → Seleccionar, colorear, ocultar
- `revit_color_elements` → Colorear por parámetro

### Anotación
- `revit_tag_all_walls` → Etiquetar muros
- `revit_tag_all_rooms` → Etiquetar habitaciones

### Datos locales (sin Revit)
- `revit_store_project_data` → Guardar metadatos de proyecto
- `revit_store_room_data` → Guardar habitaciones

### Ejecución
- `revit_send_code_to_revit` → Ejecutar C# dinámico (usado como workaround)

## Tools MCP (server/src/tools/, solo desde cliente externo)

Estas tools las define el MCP server. **NO puedo llamarlas directamente.** Solo un cliente IA externo (Claude Desktop, Cursor, etc.) conectado al MCP server puede verlas y ejecutarlas.

Para usarlas desde aquí, uso `revit_send_code_to_revit` como workaround con el código C# equivalente.

### Tools MCP existentes:
- `duplicate_views` → Duplica vistas seleccionadas (creada)
- Las 23 tools listadas en server/src/tools/

## Tool creada: duplicate_views

### Cómo ejecutarla (workaround)
Cuando el usuario pida duplicar vistas, usar `revit_send_code_to_revit` con el código en `trazabilidad/DuplicateViews.cs` adaptado. Parámetros por defecto:
- suffix: `"_Copy"` (cambiarlo si el usuario pide otro)
- duplicateOption: `"WithDetailing"`
- useSelection: `true`

### Archivos de la tool
| Capa | Archivo |
|------|---------|
| TypeScript (MCP server) | `server/src/tools/duplicate_views.ts` |
| command.json (editado) | `command.json` |
| C# Model | `commandset/Models/Views/DuplicateViewsInfo.cs` |
| C# Command | `commandset/Commands/Views/DuplicateViewsCommand.cs` |
| C# EventHandler | `commandset/Services/Views/DuplicateViewsEventHandler.cs` |

## Cómo crear una nueva tool MCP

Se necesitan 3-4 archivos (dependiendo de la complejidad):

### 1. TypeScript — `server/src/tools/<nombre>.ts`
```typescript
import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { withRevitConnection } from "../utils/ConnectionManager.js";

export function registerMiToolTool(server: McpServer) {
  server.tool("mi_tool", "Descripción", { /* Zod schema */ },
    async (args, extra) => {
      const response = await withRevitConnection(revitClient =>
        revitClient.sendCommand("mi_tool", args));
      return { content: [{ type: "text", text: JSON.stringify(response) }] };
    }
  );
}
```
> **Auto-detectado**: `register.ts` escanea automáticamente `server/src/tools/` y registra cualquier función que empiece con `register`. No hay que tocar `register.ts`.

### 2. command.json — Agregar entrada al array `commands`
```json
{ "commandName": "mi_tool", "description": "...", "assemblyPath": "RevitMCPCommandSet.dll" }
```

### 3. C# Command — `commandset/Commands/<Nombre>Command.cs`
- Hereda `ExternalEventCommandBase`
- `CommandName` debe coincidir con `command.json` y con `sendCommand()` del TS
- Parsea `JObject parameters` y llama al EventHandler

### 4. C# EventHandler — `commandset/Services/<Nombre>EventHandler.cs`
- Implementa `IExternalEventHandler` + `IWaitableExternalEventHandler`
- Toda la lógica Revit API va aquí (dentro de `Transaction`)
- Usa `ManualResetEvent` para sincronización

### Reglas clave
| Regla | Detalle |
|-------|---------|
| **Nombres consistentes** | TS `sendCommand("X")` = command.json `"commandName": "X"` = C# `CommandName => "X"` |
| **Unidades: mm → ft** | TypeScript recibe **mm**. En EventHandler C# dividir por `304.8` para convertir a **ft** |
| **Transacciones** | Toda escritura en Revit va dentro de `using Transaction` |
| **Auto-detección TS** | No tocar `register.ts`; solo crear archivo en `server/src/tools/` |
| **ExternalEventCommandBase** | Viene del NuGet `RevitMCPSDK` (público, de DTDucas) |
| **Después de crear TS** | Ejecutar `npm run build` en `server/` para compilar |
| **Include in Project** | En Visual Studio, incluir los archivos nuevos en el proyecto |
| **Debug auto-copia** | Debug copia a `%AppData%` automáticamente; Release requiere copia manual |

## Comandos útiles

```bash
# Compilar servidor MCP (después de crear/modificar tools TS)
cd server && npm run build

# Compilar comandos C# (con la configuración de Revit que corresponda)
dotnet build commandset/RevitMCPCommandSet.csproj -c "Debug R25" -r win-x64

# Compilar plugin
dotnet build plugin/RevitMCPPlugin.csproj -c "Debug R25" -r win-x64

# Tests
dotnet test -c "Debug R25" -r win-x64 tests/commandset
```

## Notas importantes
- Cada respuesta relevante se guarda en `trazabilidad/` como documentación
- El NuGet `RevitMCPSDK` es público, mantenido por DTDucas en GitHub
- Para usar tools que requieren Revit: Revit debe estar abierto con el plugin cargado y el socket activo (botón "Revit MCP Switch")
- Las tools de base de datos local (`store_*`, `query_*`) funcionan sin Revit
- **Conexión**: verificar con `revit_say_hello` primero
- **Workaround duplicate_views**: usar `revit_send_code_to_revit` con código C# adaptado del ExternalCommand. El código debe usar `document` (minúscula) como variable del Document, y crear `UIApplication` con `new UIApplication(document.Application)`. **No anidar Transaction** porque el template ya inicia una.
- **Despliegue**: los DLLs en `%AppData%\Autodesk\Revit\Addins\<version>\` persisten entre reinicios
- **Guía de portabilidad**: ver `trazabilidad/005_guia_despliegue_rapido.md`
- **Documentación de duplicate_views**: ver `trazabilidad/004_duplicate_views_tool.md`
