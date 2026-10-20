namespace WhiskeyTracker.Web.Services;

public static class ImageFileCopier
{
    /// Copies an uploaded bottle image to a new unique file so editing or deleting one whiskey's
    /// photo never affects another's. Returns null if the source file is missing on disk.
    public static string? Copy(string webRootPath, string sourceFileName)
    {
        var imagesFolder = Path.Combine(webRootPath, "images");
        var sourcePath = Path.Combine(imagesFolder, Path.GetFileName(sourceFileName));
        if (!File.Exists(sourcePath))
        {
            return null;
        }

        var newFileName = Guid.NewGuid() + Path.GetExtension(sourceFileName);
        File.Copy(sourcePath, Path.Combine(imagesFolder, newFileName));
        return newFileName;
    }
}
