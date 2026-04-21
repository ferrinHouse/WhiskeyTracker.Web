## 2024-04-16 - Missing Admin Authorization on Master Whiskey Data
**Vulnerability:** Any authenticated user could create, edit, or delete entries in the master `Whiskey` global directory because `Create.cshtml.cs`, `Edit.cshtml.cs`, and `Delete.cshtml.cs` under `Pages/Whiskies` lacked authorization attributes. Only the `/Admin` folder was protected by convention.
**Learning:** Razor Pages convention-based folder authorization (`AuthorizeFolder("/Admin")`) does not automatically protect administrative-level entities that reside outside the designated admin folder.
**Prevention:** Always explicitly annotate page models with `[Authorize(Roles = "Admin")]` for global entity modification pages, regardless of folder structure.

## 2026-04-21 - Prevent SSRF and Path Traversal in Image Uploads
**Vulnerability:** The application was vulnerable to Server-Side Request Forgery (SSRF) and Path Traversal. In `Create.cshtml.cs` and `Edit.cshtml.cs`, `GooglePhotoUrl` was used in `HttpClient.GetAsync()` without validation, allowing potential access to internal network resources. Additionally, `ImageUpload.FileName` was appended directly to a GUID when saving files, which could allow path traversal if a malicious filename was provided.
**Learning:** Always validate user-provided URLs before making HTTP requests, ensuring they use HTTPS and restrict domains to trusted sources. For file uploads, never trust the user-provided filename; generate a safe, unique filename and only preserve the original extension.
**Prevention:**
1. Validate `GooglePhotoUrl` using `Uri.TryCreate`, checking `Uri.SchemeHttps` and specific trusted domains (e.g., `uriResult.Host.EndsWith(".googleusercontent.com")`).
2. Use `Path.GetExtension(ImageUpload.FileName)` when generating safe server-side file paths to prevent directory traversal attacks.
