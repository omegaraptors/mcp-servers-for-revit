# Respuesta 005 - Guía de Despliegue Rápido y Portabilidad

## Fecha
2026-05-22

## Propósito
Guía para instalar, desplegar y recuperar el entorno mcp-servers-for-revit en cualquier PC, o después de reiniciar el equipo.

---

## Requisitos del sistema

| Componente | Versión mínima | Dónde se usa |
|------------|---------------|--------------|
| **Node.js** | ≥ 18 | MCP Server (`server/`) |
| **.NET SDK** | 8.0 | Compilación plugin C# |
| **Revit** | 2020–2026 | Entorno objetivo |
| **Git** (opcional) | Cualquiera | Clonar repositorio |

---

## Instalación desde cero (PC nuevo)

### 1. Obtener el repositorio
```bash
git clone <url-del-repositorio>
cd mcp-servers-for-revit
```
O copiar la carpeta completa desde otro PC / USB / backup.

### 2. Instalar dependencias del MCP Server
```bash
cd server
npm install
npm run build       # Compila TypeScript → build/
```

### 3. Compilar el plugin y comandos C#
Desde Visual Studio:
- Abrir `RevitMCPPlugin.csproj` o la solución
- Seleccionar configuración (ej: `Debug R25`, `Release R26`)
- Compilar ambos proyectos: `RevitMCPPlugin` + `RevitMCPCommandSet`

Desde CLI (alternativa):
```bash
# Compilar commandset
dotnet build commandset/RevitMCPCommandSet.csproj -c "Debug R25" -r win-x64

# Compilar plugin
dotnet build plugin/RevitMCPPlugin.csproj -c "Debug R25" -r win-x64
```

> **Modo Debug** → copia automática a `%AppData%\Autodesk\Revit\Addins\<version>\`
> **Modo Release** → copia manual requerida (ver paso 4)

### 4. Despliegue manual (solo Release)
Si compilaste en Release, copiar estos archivos:

**Plugin:**
```
repo/plugin/bin/AddIn <version> Release R<ver>/revit_mcp_plugin/
  └── → %AppData%\Autodesk\Revit\Addins\<version>\revit_mcp_plugin\

repo/plugin/bin/AddIn <version> Release R<ver>/*.addin
  └── → %AppData%\Autodesk\Revit\Addins\<version>\
```

**CommandSet:**
```
repo/plugin/bin/AddIn <version> Release R<ver>/revit_mcp_plugin/Commands/RevitMCPCommandSet/
  └── → %AppData%\Autodesk\Revit\Addins\<version>\revit_mcp_plugin\Commands\RevitMCPCommandSet\
```

### 5. Verificar instalación
```bash
# Estructura final esperada:
%AppData%\Autodesk\Revit\Addins\<version>\
├── mcp-servers-for-revit.addin
└── revit_mcp_plugin\
    ├── RevitMCPPlugin.dll
    ├── RevitMCPSDK.dll
    ├── Newtonsoft.Json.dll
    └── Commands\RevitMCPCommandSet\
        ├── command.json
        └── <version>\
            └── RevitMCPCommandSet.dll
```

---

## Después de reiniciar el PC

Nada se pierde. Solo seguir esta secuencia diaria:

| Paso | Acción |
|------|--------|
| 1 | Abrir **Revit** |
| 2 | En Revit, hacer clic en **"Revit MCP Switch"** (activa socket) |
| 3 | Usar opencode / Claude normalmente |

El MCP server se auto-lanza cuando opencode lo necesita (configurado en `opencode.json`).

---

## Notas importantes

- **Los DLLs en `%AppData%` persisten** entre reinicios — no se pierden
- **El repositorio local** también persiste — solo hay que compilar si se modifica código
- Si se actualiza el código (TypeScript o C#), **recompilar**:
  - TypeScript: `cd server && npm run build`
  - C#: compilar en Visual Studio (o `dotnet build`)
- El `.addin` solo se copia una vez; Revit lo lee al iniciar

---

## Archivos que NO se pierden al reiniciar

| Qué | Dónde | Persiste |
|-----|-------|----------|
| Repositorio | `D:\GitHub\mcp-servers-for-revit\` | ✅ Sí |
| DLLs desplegados | `%AppData%\Autodesk\Revit\Addins\*.dll` | ✅ Sí |
| command.json | `%AppData%\...\RevitMCPCommandSet\command.json` | ✅ Sí |
| Config opencode | `opencode.json` (en el repo) | ✅ Sí |
| Documentación | `trazabilidad/*.md` | ✅ Sí |
