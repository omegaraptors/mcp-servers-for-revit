using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPCommandSet.Models.Views;
using RevitMCPCommandSet.Services.Views;
using RevitMCPSDK.API.Base;

namespace RevitMCPCommandSet.Commands.Views;

public class DuplicateViewsCommand : ExternalEventCommandBase
{
    private DuplicateViewsEventHandler _handler => (DuplicateViewsEventHandler)Handler;

    public override string CommandName => "duplicate_views";

    public DuplicateViewsCommand(UIApplication uiApp)
        : base(new DuplicateViewsEventHandler(), uiApp)
    {
    }

    public override object Execute(JObject parameters, string requestId)
    {
        try
        {
            DuplicateViewsInfo data = parameters.ToObject<DuplicateViewsInfo>();

            if (data == null)
                throw new ArgumentNullException(nameof(data), "Duplicate views data is null");

            _handler.SetParameters(data);

            if (RaiseAndWaitForCompletion(30000))
            {
                return _handler.Result;
            }
            else
            {
                throw new TimeoutException("Duplicate views operation timed out");
            }
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to duplicate views: {ex.Message}");
        }
    }
}
