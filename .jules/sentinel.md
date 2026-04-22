## 2024-04-16 - Missing Admin Authorization on Master Whiskey Data
**Vulnerability:** Any authenticated user could create, edit, or delete entries in the master `Whiskey` global directory because `Create.cshtml.cs`, `Edit.cshtml.cs`, and `Delete.cshtml.cs` under `Pages/Whiskies` lacked authorization attributes. Only the `/Admin` folder was protected by convention.
**Learning:** Razor Pages convention-based folder authorization (`AuthorizeFolder("/Admin")`) does not automatically protect administrative-level entities that reside outside the designated admin folder.
**Prevention:** Always explicitly annotate page models with `[Authorize(Roles = "Admin")]` for global entity modification pages, regardless of folder structure.

## 2024-04-22 - Unvalidated URI Input Causing SSRF and Path Traversal in File Uploads
**Vulnerability:** The application was vulnerable to Server-Side Request Forgery (SSRF) by unconditionally passing user-provided `GooglePhotoUrl` inputs to `HttpClient.GetAsync()`. Additionally, file uploads used the original `ImageUpload.FileName` directly when saving to disk, introducing a Path Traversal risk and naming collisions.
**Learning:** Never trust external URIs for backend HTTP requests without validating the protocol and host. Similarly, never trust user-supplied filenames for file system operations.
**Prevention:** For SSRF protection, validate that the input URI uses `https://` and points to a trusted domain (e.g., `.googleusercontent.com`) before dispatching a request. For Path Traversal, always extract only the extension using `Path.GetExtension(FileName)` and prepend it with a locally generated `Guid` before writing to disk.
