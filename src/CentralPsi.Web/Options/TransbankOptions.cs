namespace CentralPsi.Web.Options;

public class TransbankOptions
{
    public const string SectionName = "Transbank";

    /// <summary>"Integration" (sandbox, no credentials needed) or "Production".</summary>
    public string Environment { get; set; } = "Integration";

    /// <summary>Only required when Environment = Production. Sandbox uses Transbank's public test codes.</summary>
    public string? CommerceCode { get; set; }
    public string? ApiKey { get; set; }
}
