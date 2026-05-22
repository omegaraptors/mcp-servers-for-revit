using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace ScriptViews
{
    [Transaction(TransactionMode.Manual)]
    public class DuplicateViewsCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            // Variables globales de Revit
            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            Document doc = uidoc.Document;

            // ==========================================
            // CONFIGURACIÓN DEL SCRIPT
            // ==========================================
            // Sufijo que se añadirá al nombre de la vista duplicada
            string suffix = "_Copy";

            // Opción de duplicado (Duplicate, WithDetailing, AsDependent)
            ViewDuplicateOption duplicateOption = ViewDuplicateOption.WithDetailing;

            // Tipos de vistas permitidas para duplicar (Filtro por tipo de vista)
            List<ViewType> targetViewTypes = new List<ViewType>
            {
                ViewType.FloorPlan,
                ViewType.Section,
                ViewType.Elevation,
                ViewType.EngineeringPlan,
                ViewType.CeilingPlan,
                ViewType.ThreeD,
                ViewType.DraftingView,
                ViewType.Schedule // <-- Añadido aquí
            };
            // ==========================================

            // 1. Obtener la selección actual del usuario (desde el Project Browser)
            ICollection<ElementId> selectionIds = uidoc.Selection.GetElementIds();

            if (selectionIds.Count == 0)
            {
                TaskDialog.Show("Aviso", "Por favor, selecciona vistas en el Project Browser antes de ejecutar el comando.");
                return Result.Cancelled;
            }

            // 2. Filtrar los elementos seleccionados para obtener solo las Vistas válidas
            List<View> viewsToDuplicate = new List<View>();
            foreach (ElementId elId in selectionIds)
            {
                Element element = doc.GetElement(elId);
                
                // Verificar que es una vista, no es un View Template y el tipo está en la lista de permitidos
                if (element is View view)
                {
                    if (!view.IsTemplate && targetViewTypes.Contains(view.ViewType))
                    {
                        viewsToDuplicate.Add(view);
                    }
                }
            }

            if (viewsToDuplicate.Count == 0)
            {
                TaskDialog.Show("Aviso", "No se encontraron vistas válidas para duplicar en la selección activa.");
                return Result.Cancelled;
            }

            int duplicationCount = 0;
            List<string> errors = new List<string>();

            // 3. Iniciar una transacción para modificar el modelo
            using (Transaction t = new Transaction(doc, "Duplicar Múltiples Vistas (C#)"))
            {
                t.Start();

                foreach (View view in viewsToDuplicate)
                {
                    try
                    {
                        // 4. Determinar la opción de duplicado adecuada (Schedules solo soportan 'Duplicate' normal)
                        ViewDuplicateOption currentOption = duplicateOption;
                        if (view.ViewType == ViewType.Schedule)
                        {
                            currentOption = ViewDuplicateOption.Duplicate;
                        }

                        // Duplicar la vista con la opción ajustada
                        ElementId newViewId = view.Duplicate(currentOption);

                        if (newViewId == ElementId.InvalidElementId)
                        {
                            errors.Add($"La vista {view.Name} no se pudo duplicar.");
                            continue;
                        }

                        View newView = doc.GetElement(newViewId) as View;

                        // 5. Renombrar la vista duplicada automáticamente
                        string originalName = view.Name;
                        string newName = originalName + suffix;

                        // Manejo de nombres duplicados (si el nombre ya existe en el modelo, agregamos un correlativo)
                        int contador = 1;
                        while (true)
                        {
                            try
                            {
                                newView.Name = newName;
                                break;
                            }
                            catch
                            {
                                // Revit lanza un error si intentamos asignar un nombre que ya está en uso
                                newName = originalName + suffix + "_" + contador;
                                contador++;
                            }
                        }

                        duplicationCount++;
                    }
                    catch (Exception ex)
                    {
                        // Manejo de excepciones en caso de que la vista no soporte el duplicado
                        errors.Add($"Error al duplicar vista '{view.Name}': {ex.Message}");
                    }
                }

                // 6. Finalizar y guardar los cambios de la transacción
                t.Commit();
            }

            // 7. Reporte final
            string msg = $"Se han duplicado {duplicationCount} vistas exitosamente.\n";
            if (errors.Count > 0)
            {
                msg += "\nAdvertencias/Omisiones:\n" + string.Join("\n", errors);
            }

            TaskDialog.Show("Resultado de Duplicación", msg);
            return Result.Succeeded;
        }
    }
}
