namespace CentralPsi.Web.Options;

public class FlowOptions
{
    public const string SectionName = "Flow";

    /// <summary>"Sandbox" (sandbox.flow.cl, self-service test credentials) or "Production" (www.flow.cl).</summary>
    public string Environment { get; set; } = "Sandbox";

    public string? ApiKey { get; set; }
    public string? SecretKey { get; set; }
}
