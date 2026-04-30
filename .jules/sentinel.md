## 2024-04-16 - Missing Admin Authorization on Master Whiskey Data
**Vulnerability:** Any authenticated user could create, edit, or delete entries in the master `Whiskey` global directory because `Create.cshtml.cs`, `Edit.cshtml.cs`, and `Delete.cshtml.cs` under `Pages/Whiskies` lacked authorization attributes. Only the `/Admin` folder was protected by convention.
**Learning:** Razor Pages convention-based folder authorization (`AuthorizeFolder("/Admin")`) does not automatically protect administrative-level entities that reside outside the designated admin folder.
**Prevention:** Always explicitly annotate page models with `[Authorize(Roles = "Admin")]` for global entity modification pages, regardless of folder structure.

## 2026-04-30 - [SSRF and Path Traversal in Image Uploads]
**Vulnerability:**
1. Server-Side Request Forgery (SSRF) allowed the application to fetch from arbitrary URLs using `HttpClient.GetAsync(GooglePhotoUrl)` without checking the domain.
2. Path Traversal vulnerability when handling image uploads directly concatenating `ImageUpload.FileName` with a Guid, allowing directory traversal with characters like `../`.
**Learning:** External user inputs used in sensitive contexts like file downloads (SSRF) and file saves (Path Traversal) must be strictly validated.
**Prevention:**
1. Use `Uri.TryCreate` to ensure the URL is absolute and uses HTTPS, and strictly match the host to a known-safe domain (e.g. `.googleusercontent.com`).
2. Only retain the extension using `Path.GetExtension(ImageUpload.FileName)` when constructing safe filenames.
