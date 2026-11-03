## 2024-04-16 - Missing Admin Authorization on Master Whiskey Data
**Vulnerability:** Any authenticated user could create, edit, or delete entries in the master `Whiskey` global directory because `Create.cshtml.cs`, `Edit.cshtml.cs`, and `Delete.cshtml.cs` under `Pages/Whiskies` lacked authorization attributes. Only the `/Admin` folder was protected by convention.
**Learning:** Razor Pages convention-based folder authorization (`AuthorizeFolder("/Admin")`) does not automatically protect administrative-level entities that reside outside the designated admin folder.
**Prevention:** Always explicitly annotate page models with `[Authorize(Roles = "Admin")]` for global entity modification pages, regardless of folder structure.

## 2024-05-18 - Server-Side Request Forgery (SSRF) in Image Upload
**Vulnerability:** The application allowed users to provide a `GooglePhotoUrl` that was passed directly to `HttpClient.GetAsync()` without any validation in `Create.cshtml.cs` and `Edit.cshtml.cs`. This could allow an attacker to make the server send arbitrary HTTP requests to internal or external systems.
**Learning:** External URLs provided by users must be strictly validated before being used in server-side HTTP requests to prevent SSRF vulnerabilities.
**Prevention:** Use `Uri.TryCreate` with `UriKind.Absolute` to validate the URL format, enforce HTTPS (`Uri.UriSchemeHttps`), and restrict the host to an allowlist of trusted domains (e.g., `.googleusercontent.com`, `.googleapis.com`).

## 2024-08-20 - Path Traversal in Image Uploads
**Vulnerability:** The application appended the unvalidated, user-provided `ImageUpload.FileName` directly to a generated GUID when saving uploaded files in `Create.cshtml.cs` and `Edit.cshtml.cs`. This allowed directory traversal payloads (e.g., `../../../`) to escape the intended `images` directory and save files in arbitrary locations.
**Learning:** Never trust the `FileName` property of a user-uploaded file directly for filesystem paths, even if prefixed with a unique identifier, because path navigation segments are still evaluated.
**Prevention:** Use `Path.GetExtension()` to extract only the extension from the original file name, and combine it with a server-generated unique identifier (like a GUID) to construct the final filename.

## 2026-09-17 - SSRF in Image Upload via HttpClient Auto-Redirect
**Vulnerability:** The host-allowlist check on `GooglePhotoUrl` only validated the initial request URL. `HttpClient` follows HTTP redirects by default, so a redirect response from an allowed host (e.g. an open redirect on `googleusercontent.com`) could send the server's request to an unvalidated internal or external destination, bypassing the check entirely.
**Learning:** Validating the initial URL is not sufficient when the HTTP client automatically follows redirects - the redirect target is never re-validated against the allowlist.
**Prevention:** Disable automatic redirects (`new HttpClientHandler { AllowAutoRedirect = false }`) when fetching from a user-supplied, allowlist-validated URL, so any redirect response fails closed instead of being silently followed.

## 2026-11-03 - Google Photo Host Allowlist Is Intentionally Subdomain-Only (Not a Vulnerability)
**Vulnerability:** None. Sentinel repeatedly proposed adding exact apex matches (`uri.Host == "googleusercontent.com"` / `"googleapis.com"`) to the `GooglePhotoUrl` allowlist as an "SSRF bypass fix" (PRs #273, #285, #286, all closed). The existing `EndsWith(".googleusercontent.com")` check, with the leading dot, already rejects look-alike hosts such as `attackergoogleusercontent.com`.
**Learning:** Adding the apex domains *widens* an SSRF allowlist; it does not tighten it. Google Photos images are served only from subdomains (e.g. `lh3.googleusercontent.com`), so the apex hosts are never needed. The check now lives in `ImageUploadValidator.IsAllowedGooglePhotoUrl`.
**Prevention:** Do not open PRs that add apex domains to this allowlist. Only propose allowlist changes that remove hosts or fix an actual reproducible bypass.

## 2026-11-03 - Unrestricted Upload Extension and Size in Image Uploads
**Vulnerability:** `Create.cshtml.cs` and `Edit.cshtml.cs` kept the uploaded file's extension (`Path.GetExtension(ImageUpload.FileName)`) and saved it under `wwwroot/images`, so an upload such as `x.html` was served as active content from the app's own origin (stored XSS). Uploads and Google Photo downloads also had no size limit.
**Learning:** Fixing path traversal by keeping only the extension is not enough when the file lands in a statically served folder - the extension decides the served `Content-Type`.
**Prevention:** Validate uploads with `ImageUploadValidator.ValidateUpload` (extension allowlist `.jpg/.jpeg/.png/.gif/.webp`, 10 MB cap) and fetch Google Photos with `ImageUploadValidator.DownloadGooglePhotoAsync` (no redirects, `image/*` content type, `MaxResponseContentBufferSize` cap).
