namespace CentralPsi.Web.Options;

public class StorageOptions
{
    public const string SectionName = "Storage";

    /// <summary>
    /// Absolute path to a persistent disk mount (e.g. a Render Disk, or any durable volume on a self-hosted
    /// server) where uploaded files (professional photos, cédulas, certificates, news images) are stored instead
    /// of the app's own directories. Those live on the container's ephemeral filesystem, which is wiped on every
    /// deploy/restart - anything uploaded there disappears the next time the app redeploys. Leave empty for
    /// local development, where the previous in-app folders (wwwroot/uploads, App_Data/private-uploads) are used.
    /// </summary>
    public string? RootPath { get; set; }
}
