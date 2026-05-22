# Respuesta 004 - Tool MCP: Duplicate Views

## Fecha
2026-05-22

## Archivo original
`trazabilidad/DuplicateViews.cs` — `ExternalCommand` que duplica vistas seleccionadas en Revit.

## Qué hace el código original

El `DuplicateViewsCommand` (ScriptViews namespace) funciona así:

1. Obtiene la **selección actual** del Project Browser vía `uidoc.Selection.GetElementIds()`
2. Filtra los elementos seleccionados dejando solo **vistas válidas**:
   - Que sean tipo `View`
   - Que NO sean `View Template` (`!view.IsTemplate`)
   - Que su `ViewType` esté en una lista permitida (`FloorPlan`, `Section`, `Elevation`, `EngineeringPlan`, `CeilingPlan`, `ThreeD`, `DraftingView`, `Schedule`)
3. Por cada vista válida, **duplica** con `ViewDuplicateOption.WithDetailing` (por defecto)
4. **Renombra** la copia con sufijo `_Copy` (configurable)
5. Si el nombre ya existe, agrega un correlativo (`_Copy_1`, `_Copy_2`, etc.)
6. Manejo especial: **Schedule** solo soporta `ViewDuplicateOption.Duplicate`
7. Muestra resultado en `TaskDialog`

### Limitaciones del original
- Solo funciona con **selección manual** del usuario
- No expone configuración externa (sufijo fijo, opción de duplicado fija)
- Usa `TaskDialog` para resultados (no retorna datos estructurados)

---

## Plan de implementación como Tool MCP

Se seguirá el patrón de 4 capas del proyecto:

### Capa 1: TypeScript — `server/src/tools/duplicate_views.ts`

**Nombre de tool:** `duplicate_views`

**Parámetros (Zod):**

| Parámetro | Tipo | Default | Descripción |
|-----------|------|---------|-------------|
| `suffix` | `string` | `"_Copy"` | Sufijo para el nombre de la vista duplicada |
| `duplicateOption` | `enum` | `"WithDetailing"` | Opción de duplicado: `Duplicate`, `WithDetailing`, `AsDependent` |
| `targetViewTypes` | `string[]` | `null` (todos) | Filtro por tipos de vista (ej: `["FloorPlan", "Section"]`) |
| `useSelection` | `boolean` | `true` | Si es `true`, usa la selección actual del usuario |
| `viewIds` | `number[]` | `null` | IDs específicos de vistas a duplicar (si `useSelection=false`) |

### Capa 2: command.json

```json
{
  "commandName": "duplicate_views",
  "description": "Duplicate selected views with configurable suffix and options. Supports filtering by view type and bulk operations.",
  "assemblyPath": "RevitMCPCommandSet.dll"
}
```

### Capa 3: Modelo — `commandset/Models/Views/DuplicateViewsInfo.cs`

DTO con todas las opciones de duplicado:
- `Suffix` (string, default `"_Copy"`)
- `DuplicateOption` (string: `"Duplicate"`, `"WithDetailing"`, `"AsDependent"`)
- `TargetViewTypes` (List<string>, opcional — filtra por ViewType)
- `UseSelection` (bool, default `true`)
- `ViewIds` (List<long>, opcional — IDs específicos)

### Capa 4: Command — `commandset/Commands/Views/DuplicateViewsCommand.cs`

- Hereda `ExternalEventCommandBase`
- `CommandName => "duplicate_views"`
- Parsea `JObject parameters` a `DuplicateViewsInfo`
- Llama al EventHandler

### Capa 5: EventHandler — `commandset/Services/Views/DuplicateViewsEventHandler.cs`

- Implementa `IExternalEventHandler` + `IWaitableExternalEventHandler`
- Lógica adaptada del `DuplicateViewsCommand.cs` original:
  - Obtiene vistas desde selección O por IDs explícitos
  - Filtra por tipo de vista (si se especifica)
  - Excluye `ViewTemplate`
  - Duplica cada vista con opción configurable
  - Renombra con manejo de colisiones
  - Retorna `AIResult<List<DuplicateViewResult>>` con resultados estructurados

### Resultado estructurado (`DuplicateViewResult`)

| Campo | Tipo | Descripción |
|-------|------|-------------|
| `originalViewId` | `long` | ID de la vista original |
| `originalViewName` | `string` | Nombre de la vista original |
| `newViewId` | `long` | ID de la vista duplicada |
| `newViewName` | `string` | Nombre de la vista duplicada |
| `viewType` | `string` | Tipo de vista |
| `success` | `boolean` | Si se duplicó correctamente |
| `errorMessage` | `string` | Mensaje de error (si falló) |

---

## Diferencias con el ExternalCommand original

| Aspecto | Original | Tool MCP |
|---------|----------|----------|
| Entrada | Solo selección UI | Selección UI O IDs explícitos |
| Parámetros | Hardcodeados | Configurables vía JSON |
| Filtro vista | Lista fija en código | Lista configurable |
| Resultado | `TaskDialog` | `AIResult` estructurado |
| Transacción | 1 transacción para todo | 1 transacción para todo |
| Error handling | Por vista (sigue con las demás) | Por vista (sigue con las demás) |

---

## Archivos creados/modificados

| Archivo | Acción |
|---------|--------|
| `trazabilidad/004_duplicate_views_tool.md` | **Creado** — Esta documentación |
| `server/src/tools/duplicate_views.ts` | **Creado** — Tool TypeScript |
| `command.json` | **Editado** — Nuevo comando `duplicate_views` |
| `commandset/Models/Views/DuplicateViewsInfo.cs` | **Creado** — DTO de parámetros |
| `commandset/Commands/Views/DuplicateViewsCommand.cs` | **Creado** — Command C# |
| `commandset/Services/Views/DuplicateViewsEventHandler.cs` | **Creado** — EventHandler C# |
