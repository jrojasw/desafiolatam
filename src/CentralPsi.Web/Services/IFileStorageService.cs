namespace CentralPsi.Web.Services;

public interface IFileStorageService
{
    /// <summary>Saves a file under the public wwwroot/uploads tree (for content meant to be shown on the site).</summary>
    Task<string> SavePublicAsync(IFormFile file, string subfolder);

    /// <summary>
    /// Saves a file outside wwwroot (identity documents, certificates) so it can never be served as a static
    /// file - only through an authenticated admin action.
    /// </summary>
    Task<string> SavePrivateAsync(IFormFile file, string subfolder);

    string GetPrivatePhysicalPath(string relativePath);
    string GetPublicPhysicalPath(string relativePath);
}
