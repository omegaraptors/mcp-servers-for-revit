using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitMCPCommandSet.Models.Common;
using RevitMCPCommandSet.Models.Views;
using RevitMCPSDK.API.Interfaces;

namespace RevitMCPCommandSet.Services.Views;

public class DuplicateViewsEventHandler : IExternalEventHandler, IWaitableExternalEventHandler
{
    private UIApplication uiApp;
    private UIDocument uiDoc => uiApp.ActiveUIDocument;
    private Document doc => uiDoc.Document;

    private readonly ManualResetEvent _resetEvent = new ManualResetEvent(false);

    public DuplicateViewsInfo Parameters { get; private set; }

    public AIResult<List<DuplicateViewResult>> Result { get; private set; }

    private static readonly HashSet<ViewType> SupportedViewTypes = new HashSet<ViewType>
    {
        ViewType.FloorPlan,
        ViewType.Section,
        ViewType.Elevation,
        ViewType.EngineeringPlan,
        ViewType.CeilingPlan,
        ViewType.ThreeD,
        ViewType.DraftingView,
        ViewType.Schedule,
    };

    public void SetParameters(DuplicateViewsInfo parameters)
    {
        Parameters = parameters;
        _resetEvent.Reset();
    }

    public void Execute(UIApplication uiapp)
    {
        uiApp = uiapp;

        try
        {
            string suffix = Parameters.Suffix ?? "_Copy";
            ViewDuplicateOption duplicateOption = ParseDuplicateOption(Parameters.DuplicateOption);

            HashSet<ViewType> targetViewTypes = null;
            if (Parameters.TargetViewTypes != null && Parameters.TargetViewTypes.Count > 0)
            {
                targetViewTypes = new HashSet<ViewType>();
                foreach (string vType in Parameters.TargetViewTypes)
                {
                    if (Enum.TryParse(vType, true, out ViewType parsed))
                        targetViewTypes.Add(parsed);
                }
            }

            List<View> viewsToDuplicate = new List<View>();

            if (Parameters.UseSelection)
            {
                ICollection<ElementId> selectionIds = uiDoc.Selection.GetElementIds();
                foreach (ElementId elId in selectionIds)
                {
                    Element element = doc.GetElement(elId);
                    if (element is View view)
                        viewsToDuplicate.Add(view);
                }
            }
            else if (Parameters.ViewIds != null && Parameters.ViewIds.Count > 0)
            {
                foreach (long id in Parameters.ViewIds)
                {
                    Element element = doc.GetElement(new ElementId(id));
                    if (element is View view)
                        viewsToDuplicate.Add(view);
                }
            }

            List<View> validViews = new List<View>();
            foreach (View view in viewsToDuplicate)
            {
                if (view.IsTemplate)
                    continue;
                if (!SupportedViewTypes.Contains(view.ViewType))
                    continue;
                if (targetViewTypes != null && !targetViewTypes.Contains(view.ViewType))
                    continue;
                validViews.Add(view);
            }

            if (validViews.Count == 0)
            {
                Result = new AIResult<List<DuplicateViewResult>>
                {
                    Success = false,
                    Message = "No valid views found to duplicate. Select views in the Project Browser or provide valid view IDs.",
                    Response = null
                };
                return;
            }

            List<DuplicateViewResult> results = new List<DuplicateViewResult>();
            var existingViewNames = new FilteredElementCollector(doc)
                .OfClass(typeof(View))
                .Cast<View>()
                .Where(v => !v.IsTemplate)
                .Select(v => v.Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            using (Transaction trans = new Transaction(doc, "Duplicate Views"))
            {
                trans.Start();

                foreach (View view in validViews)
                {
                    try
                    {
                        ViewDuplicateOption currentOption = duplicateOption;
                        if (view.ViewType == ViewType.Schedule && duplicateOption != ViewDuplicateOption.Duplicate)
                            currentOption = ViewDuplicateOption.Duplicate;

                        ElementId newViewId = view.Duplicate(currentOption);

                        if (newViewId == ElementId.InvalidElementId)
                        {
                            results.Add(new DuplicateViewResult
                            {
                                OriginalViewId = GetElementIdValue(view.Id),
                                OriginalViewName = view.Name,
                                ViewType = view.ViewType.ToString(),
                                Success = false,
                                ErrorMessage = "View could not be duplicated"
                            });
                            continue;
                        }

                        View newView = doc.GetElement(newViewId) as View;

                        string newName = GenerateUniqueName(view.Name, suffix, existingViewNames);
                        existingViewNames.Add(newName);
                        newView.Name = newName;

#if REVIT2024_OR_GREATER
                        long newId = newViewId.Value;
#else
                        long newId = newViewId.IntegerValue;
#endif

                        results.Add(new DuplicateViewResult
                        {
                            OriginalViewId = GetElementIdValue(view.Id),
                            OriginalViewName = view.Name,
                            NewViewId = newId,
                            NewViewName = newName,
                            ViewType = view.ViewType.ToString(),
                            Success = true,
                            ErrorMessage = null
                        });
                    }
                    catch (Exception ex)
                    {
                        results.Add(new DuplicateViewResult
                        {
                            OriginalViewId = GetElementIdValue(view.Id),
                            OriginalViewName = view.Name,
                            ViewType = view.ViewType.ToString(),
                            Success = false,
                            ErrorMessage = ex.Message
                        });
                    }
                }

                trans.Commit();
            }

            int successCount = results.Count(r => r.Success);
            int failCount = results.Count(r => !r.Success);
            string message = $"Successfully duplicated {successCount} of {results.Count} views.";
            if (failCount > 0)
                message += $" {failCount} view(s) failed.";

            Result = new AIResult<List<DuplicateViewResult>>
            {
                Success = failCount == 0,
                Message = message,
                Response = results
            };
        }
        catch (Exception ex)
        {
            Result = new AIResult<List<DuplicateViewResult>>
            {
                Success = false,
                Message = $"Failed to duplicate views: {ex.Message}",
                Response = null
            };
        }
        finally
        {
            _resetEvent.Set();
        }
    }

    private ViewDuplicateOption ParseDuplicateOption(string option)
    {
        switch (option?.ToLower())
        {
            case "duplicate":
                return ViewDuplicateOption.Duplicate;
            case "asdependent":
                return ViewDuplicateOption.AsDependent;
            case "withdetailing":
            default:
                return ViewDuplicateOption.WithDetailing;
        }
    }

    private string GenerateUniqueName(string originalName, string suffix, HashSet<string> existingNames)
    {
        string baseName = originalName + suffix;
        string candidate = baseName;
        int counter = 1;

        while (existingNames.Contains(candidate))
        {
            candidate = $"{baseName}_{counter}";
            counter++;
        }

        return candidate;
    }

    private static long GetElementIdValue(ElementId id)
    {
#if REVIT2024_OR_GREATER
        return id.Value;
#else
        return id.IntegerValue;
#endif
    }

    public bool WaitForCompletion(int timeoutMilliseconds = 30000)
    {
        _resetEvent.Reset();
        return _resetEvent.WaitOne(timeoutMilliseconds);
    }

    public string GetName()
    {
        return "Duplicate Views";
    }
}
