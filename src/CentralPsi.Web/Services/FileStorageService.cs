using CentralPsi.Web.Options;
using Microsoft.Extensions.Options;

namespace CentralPsi.Web.Services;

public class FileStorageService : IFileStorageService
{
    private readonly string _publicRoot;
    private readonly string _privateRoot;

    /// <summary>Physical path uploads are actually served from - exposed so Program.cs can wire up an extra
    /// static-files middleware when it points outside wwwroot (i.e. at a persistent disk mount).</summary>
    public string PublicRoot => _publicRoot;

    public FileStorageService(IWebHostEnvironment env, IOptions<StorageOptions> storageOptions)
    {
        var rootPath = storageOptions.Value.RootPath;
        if (!string.IsNullOrWhiteSpace(rootPath))
        {
            _publicRoot = Path.Combine(rootPath, "uploads");
            _privateRoot = Path.Combine(rootPath, "private-uploads");
        }
        else
        {
            _publicRoot = Path.Combine(env.WebRootPath, "uploads");
            _privateRoot = Path.Combine(env.ContentRootPath, "App_Data", "private-uploads");
        }
        Directory.CreateDirectory(_publicRoot);
        Directory.CreateDirectory(_privateRoot);
    }

    public async Task<string> SavePublicAsync(IFormFile file, string subfolder)
    {
        var relative = await SaveAsync(file, Path.Combine(_publicRoot, subfolder), subfolder);
        return $"/uploads/{relative}";
    }

    public async Task<string> SavePrivateAsync(IFormFile file, string subfolder)
    {
        var relative = await SaveAsync(file, Path.Combine(_privateRoot, subfolder), subfolder);
        return relative;
    }

    public async Task<string> SavePrivateStreamAsync(Stream content, string originalFileName, string subfolder)
    {
        var extension = Path.GetExtension(originalFileName);
        if (!AllowedExtensions.Contains(extension))
        {
            throw new InvalidOperationException($"Extensión de archivo no permitida: {extension}");
        }

        var targetDir = Path.Combine(_privateRoot, subfolder);
        Directory.CreateDirectory(targetDir);
        var fileName = $"{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
        var fullPath = Path.Combine(targetDir, fileName);

        await using (var stream = new FileStream(fullPath, FileMode.Create))
        {
            await content.CopyToAsync(stream);
        }

        return $"{subfolder}/{fileName}";
    }

    public string GetPrivatePhysicalPath(string relativePath) => Path.Combine(_privateRoot, relativePath);
    public string GetPublicPhysicalPath(string relativePath) => Path.Combine(_publicRoot, relativePath);

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp", ".pdf"
    };

    private static async Task<string> SaveAsync(IFormFile file, string targetDir, string subfolder)
    {
        var extension = Path.GetExtension(file.FileName);
        if (!AllowedExtensions.Contains(extension))
        {
            throw new InvalidOperationException($"Extensión de archivo no permitida: {extension}");
        }

        Directory.CreateDirectory(targetDir);
        var fileName = $"{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
        var fullPath = Path.Combine(targetDir, fileName);

        await using (var stream = new FileStream(fullPath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        return $"{subfolder}/{fileName}";
    }
}
