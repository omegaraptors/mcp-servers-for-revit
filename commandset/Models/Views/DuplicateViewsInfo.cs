using Newtonsoft.Json;

namespace RevitMCPCommandSet.Models.Views;

public class DuplicateViewsInfo
{
    [JsonProperty("suffix")]
    public string Suffix { get; set; } = "_Copy";

    [JsonProperty("duplicateOption")]
    public string DuplicateOption { get; set; } = "WithDetailing";

    [JsonProperty("targetViewTypes")]
    public List<string> TargetViewTypes { get; set; }

    [JsonProperty("useSelection")]
    public bool UseSelection { get; set; } = true;

    [JsonProperty("viewIds")]
    public List<long> ViewIds { get; set; }
}

public class DuplicateViewResult
{
    [JsonProperty("originalViewId")]
    public long OriginalViewId { get; set; }

    [JsonProperty("originalViewName")]
    public string OriginalViewName { get; set; }

    [JsonProperty("newViewId")]
    public long NewViewId { get; set; }

    [JsonProperty("newViewName")]
    public string NewViewName { get; set; }

    [JsonProperty("viewType")]
    public string ViewType { get; set; }

    [JsonProperty("success")]
    public bool Success { get; set; }

    [JsonProperty("errorMessage")]
    public string ErrorMessage { get; set; }
}
