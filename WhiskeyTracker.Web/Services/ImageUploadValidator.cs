namespace WhiskeyTracker.Web.Services;

public static class ImageUploadValidator
{
    /// Uploaded images are served from wwwroot, so only extensions that static files serve as images are allowed.
    /// Anything else (e.g. .html, .svg) could be served as active content from the app's own origin.
    public static readonly IReadOnlySet<string> AllowedExtensions =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".gif", ".webp" };

    public const long MaxImageBytes = 10 * 1024 * 1024;

    /// Returns an error message if the upload is not an allowed image, or null if it is acceptable.
    public static string? ValidateUpload(IFormFile file)
    {
        if (!AllowedExtensions.Contains(Path.GetExtension(file.FileName)))
        {
            return "Unsupported image type. Allowed types: " + string.Join(", ", AllowedExtensions) + ".";
        }

        if (file.Length == 0 || file.Length > MaxImageBytes)
        {
            return $"Image must be between 1 byte and {MaxImageBytes / (1024 * 1024)} MB.";
        }

        return null;
    }

    /// The leading dot is intentional: it limits the host to Google subdomains (e.g. lh3.googleusercontent.com)
    /// and rejects look-alikes such as attackergoogleusercontent.com. The bare apex domains are not needed.
    public static bool IsAllowedGooglePhotoUrl(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
        uri.Scheme == Uri.UriSchemeHttps &&
        (uri.Host.EndsWith(".googleusercontent.com") || uri.Host.EndsWith(".googleapis.com"));

    /// Downloads a Google Photos image without following redirects, rejecting non-image responses and
    /// anything over MaxImageBytes. Returns null if the download fails or is rejected.
    public static async Task<byte[]?> DownloadGooglePhotoAsync(string url, string token, HttpMessageHandler? handler = null)
    {
        using var client = new HttpClient(handler ?? new HttpClientHandler { AllowAutoRedirect = false })
        {
            MaxResponseContentBufferSize = MaxImageBytes
        };
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        try
        {
            using var response = await client.GetAsync(url);
            var mediaType = response.Content.Headers.ContentType?.MediaType;
            if (!response.IsSuccessStatusCode || mediaType == null || !mediaType.StartsWith("image/"))
            {
                return null;
            }

            return await response.Content.ReadAsByteArrayAsync();
        }
        catch (HttpRequestException)
        {
            // Thrown when the response exceeds MaxResponseContentBufferSize or the request fails.
            return null;
        }
    }
}
