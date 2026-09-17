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
