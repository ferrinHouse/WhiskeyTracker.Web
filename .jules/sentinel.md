## 2024-04-16 - Missing Admin Authorization on Master Whiskey Data
**Vulnerability:** Any authenticated user could create, edit, or delete entries in the master `Whiskey` global directory because `Create.cshtml.cs`, `Edit.cshtml.cs`, and `Delete.cshtml.cs` under `Pages/Whiskies` lacked authorization attributes. Only the `/Admin` folder was protected by convention.
**Learning:** Razor Pages convention-based folder authorization (`AuthorizeFolder("/Admin")`) does not automatically protect administrative-level entities that reside outside the designated admin folder.
**Prevention:** Always explicitly annotate page models with `[Authorize(Roles = "Admin")]` for global entity modification pages, regardless of folder structure.

## 2024-05-24 - SSRF and Path Traversal in File Uploads
**Vulnerability:** The application was vulnerable to Server-Side Request Forgery (SSRF) because it fetched images from `GooglePhotoUrl` using `HttpClient` without validating the scheme, host, or URL format. It also had a Path Traversal vulnerability because it used `ImageUpload.FileName` directly when saving uploaded files, potentially allowing malicious files to be saved outside the intended directory.
**Learning:** Never trust user-provided URLs or filenames directly. Always validate that URLs are absolute, use HTTPS, and point to expected hosts (e.g., `.googleusercontent.com`). For file uploads, discard the user-provided filename entirely and generate a new safe filename, retaining only the safe extension using `Path.GetExtension()`.
**Prevention:** Always validate external URLs using `Uri.TryCreate` and enforce specific schemes and domains before issuing HTTP requests. When accepting file uploads, generate a UUID for the filename and append only the extracted extension.
