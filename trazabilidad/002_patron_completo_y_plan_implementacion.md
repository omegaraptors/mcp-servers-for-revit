# Respuesta 002 - Patrón Completo y Plan de Implementación

## Fecha
2026-05-22

## Archivos de referencia leídos

| Archivo | Propósito |
|---------|-----------|
| `server/src/tools/say_hello.ts` | Tool MCP más simple (1 parámetro opcional) |
| `server/src/tools/create_line_based_element.ts` | Tool MCP compleja con batch y array de objetos |
| `server/src/tools/register.ts` | Autoregistro dinámico de tools |
| `command.json` | Manifiesto que mapea commandName → DLL |
| `commandset/Commands/Test/SayHelloCommand.cs` | Command C# simple |
| `commandset/Commands/CreateLineElementCommand.cs` | Command C# complejo (batch) |
| `commandset/Services/SayHelloEventHandler.cs` | EventHandler mínimo |
| `commandset/Services/CreateLineElementEventHandler.cs` | EventHandler completo con transacciones Revit |
| `commandset/Models/Common/JZPoint.cs` | Modelo DTO con conversión mm → ft |
| `commandset/Models/Common/AIResult.cs` | Wrapper genérico de respuesta |

---

## Arquitectura General

```
Cliente IA (Claude / Cursor / etc.)
    ↕  stdio (JSON-RPC)
MCP Server (TypeScript) → server/src/tools/
    ↕  TCP localhost:8080 (JSON-RPC 2.0)
Plugin Revit (C#) → plugin/Core/SocketService.cs
    ↕  ExternalEvent (hilo UI de Revit)
Command Set (C#) → commandset/Commands/ + Services/
```

---

## Patrón por Capa

### Capa 1: TypeScript — Definir el tool MCP

**Template:**
```typescript
import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { withRevitConnection } from "../utils/ConnectionManager.js";

export function registerMiComandoTool(server: McpServer) {
  server.tool(
    "mi_comando",
    "Descripción clara de lo que hace.",
    {
      parametro1: z.string().describe("Descripción del parámetro"),
      parametro2: z.number().describe("Descripción"),
    },
    async (args, extra) => {
      try {
        const response = await withRevitConnection(async (revitClient) => {
          return await revitClient.sendCommand("mi_comando", args);
        });
        return {
          content: [{ type: "text", text: JSON.stringify(response, null, 2) }],
        };
      } catch (error) {
        return {
          content: [{ type: "text", text: `Error: ${error instanceof Error ? error.message : String(error)}` }],
        };
      }
    }
  );
}
```

**Reglas:**
- El nombre de la función debe empezar con `register` para que `register.ts` la autodetecte
- El primer argumento de `server.tool()` es el nombre público del tool
- Zod valida los parámetros automáticamente
- `withRevitConnection` maneja la conexión TCP con Revit (mutex, reconnect)

---

### Capa 2: command.json — Registrar el comando

**Template (agregar al array `commands`):**
```json
{
  "commandName": "mi_comando",
  "description": "Descripción de lo que hace",
  "assemblyPath": "RevitMCPCommandSet.dll"
}
```

---

### Capa 3: C# Command — Parser de parámetros y coordinación

**Template:**
```csharp
using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPSDK.API.Base;
using RevitMCPCommandSet.Services;

namespace RevitMCPCommandSet.Commands;

public class MiComandoCommand : ExternalEventCommandBase
{
    private MiComandoEventHandler _handler => (MiComandoEventHandler)Handler;

    public override string CommandName => "mi_comando";  // debe coincidir con command.json

    public MiComandoCommand(UIApplication uiApp)
        : base(new MiComandoEventHandler(), uiApp) { }

    public override object Execute(JObject parameters, string requestId)
    {
        _handler.SetParameters(parameters);

        if (RaiseAndWaitForCompletion(15000))
            return _handler.Result;
        else
            throw new TimeoutException("Mi comando timed out");
    }
}
```

---

### Capa 4: C# EventHandler — Lógica real de Revit API

**Template:**
```csharp
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPSDK.API.Interfaces;
using RevitMCPCommandSet.Models.Common;

namespace RevitMCPCommandSet.Services;

public class MiComandoEventHandler : IExternalEventHandler, IWaitableExternalEventHandler
{
    private readonly ManualResetEvent _resetEvent = new ManualResetEvent(false);
    private UIApplication _uiApp;

    public AIResult<object> Result { get; private set; }
    public JObject Parameters { get; private set; }

    public void SetParameters(JObject parameters)
    {
        Parameters = parameters;
        _resetEvent.Reset();
    }

    public void Execute(UIApplication app)
    {
        _uiApp = app;
        try
        {
            var doc = app.ActiveUIDocument.Document;

            // Parsear parámetros
            // string valor = Parameters["parametro1"]?.ToString();
            // double numero = Parameters["parametro2"]?.Value<double>() ?? 0;

            using var tx = new Transaction(doc, "Mi Comando");
            tx.Start();

            // ★★★ LÓGICA DE REVIT API AQUÍ ★★★

            tx.Commit();

            Result = new AIResult<object>
            {
                Success = true,
                Message = "Operación completada exitosamente",
                Response = new { /* resultados */ }
            };
        }
        catch (Exception ex)
        {
            Result = new AIResult<object>
            {
                Success = false,
                Message = $"Error: {ex.Message}"
            };
        }
        finally
        {
            _resetEvent.Set();  // Libera el hilo que está esperando
        }
    }

    public bool WaitForCompletion(int timeoutMilliseconds = 10000)
    {
        _resetEvent.Reset();
        return _resetEvent.WaitOne(timeoutMilliseconds);
    }

    public string GetName() => "Mi Comando";
}
```

---

### Capa 5 (Opcional): Modelos DTO

En `commandset/Models/`:

```csharp
using Newtonsoft.Json;

namespace RevitMCPCommandSet.Models.Common;

public class JZPoint
{
    [JsonProperty("x")] public double X { get; set; }
    [JsonProperty("y")] public double Y { get; set; }
    [JsonProperty("z")] public double Z { get; set; }

    public static XYZ ToXYZ(JZPoint p) => new(p.X / 304.8, p.Y / 304.8, p.Z / 304.8);
}
```

**Result wrapper genérico:**
```csharp
namespace RevitMCPCommandSet.Models.Common;

public class AIResult<T>
{
    public bool Success { get; set; }
    public string Message { get; set; }
    public T Response { get; set; }
}
```

---

## Reglas y Convenciones

| Concepto | Regla |
|----------|-------|
| **Conexión de nombres** | TypeScript `sendCommand("X")` → command.json `"commandName": "X"` → C# `CommandName => "X"` |
| **Unidades** | TypeScript recibe **mm**. En C# dividir por `304.8` para convertir a **ft** (unidad Revit) |
| **Auto-detección TS** | `register.ts` escanea `server/src/tools/` y ejecuta cualquier función exportada que empiece con `register` |
| **ExternalEventCommandBase** | Viene del NuGet `RevitMCPSDK`. Provee `RaiseAndWaitForCompletion(timeout)` |
| **ManualResetEvent** | Sincronización: Command espera con `WaitOne()`, EventHandler libera con `Set()` |
| **Transacciones** | Toda operación de escritura en DB de Revit debe ir dentro de `using Transaction` |
| **Timeout** | `RaiseAndWaitForCompletion(15000)` = 15s máx. Ajustable según la operación |
| **Thread safety** | `_executionLock` (opcional) si el Command podría recibir múltiples llamadas |

---

## Flujo Completo de una Llamada

```
1. Usuario escribe en Claude: "crea un muro de 5m de largo"
2. Claude → LLM decide llamar tool "create_line_based_element"
3. MCP Server (TS) recibe la llamada stdio
4. withRevitConnection() crea conexión TCP a localhost:8080
5. sendCommand("create_line_based_element", params) envía JSON-RPC
6. SocketService.cs recibe, busca "create_line_based_element" en registry
7. CreateLineElementCommand.Execute(parameters) se ejecuta
8. SetParameters(data) → _resetEvent.Reset()
9. RaiseAndWaitForCompletion(10000) → ExternalEvent.Raise()
10. CreateLineElementEventHandler.Execute(UIApplication) en hilo Revit
11. Lógica Revit API (transacción, creación de muro)
12. _resetEvent.Set() → se libera el hilo del Command
13. Result viaja de vuelta: Command → SocketService → TCP → MCP Server → Claude
14. Claude interpreta el JSON y responde al usuario
```

---

## Lo que NO necesita cambios

- `server/src/tools/index.ts` — solo importa `registerTools`
- `server/src/tools/register.ts` — autodetecta automáticamente
- `plugin/` — carga dinámica por reflexión
- `server/src/utils/ConnectionManager.ts` — manejo de conexiones
- `server/src/utils/SocketClient.ts` — cliente TCP
