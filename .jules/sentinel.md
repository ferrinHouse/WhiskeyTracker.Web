## 2024-04-16 - Missing Admin Authorization on Master Whiskey Data
**Vulnerability:** Any authenticated user could create, edit, or delete entries in the master `Whiskey` global directory because `Create.cshtml.cs`, `Edit.cshtml.cs`, and `Delete.cshtml.cs` under `Pages/Whiskies` lacked authorization attributes. Only the `/Admin` folder was protected by convention.
**Learning:** Razor Pages convention-based folder authorization (`AuthorizeFolder("/Admin")`) does not automatically protect administrative-level entities that reside outside the designated admin folder.
**Prevention:** Always explicitly annotate page models with `[Authorize(Roles = "Admin")]` for global entity modification pages, regardless of folder structure.

## 2025-04-25 - Fix SSRF and Path Traversal on File Uploads
**Vulnerability:** The application was vulnerable to Server-Side Request Forgery (SSRF) when downloading images from user-provided Google Photos URLs because the URL wasn't validated before calling `HttpClient.GetAsync()`. It was also vulnerable to Path Traversal during direct file uploads because the raw `ImageUpload.FileName` was appended to a GUID to form the stored filename, allowing potentially dangerous characters (e.g., `../`).
**Learning:** External URLs provided via client-side hidden fields must be strictly validated on the server for an expected scheme (HTTPS) and trusted host. Original file names from multipart form uploads are entirely attacker-controlled and should never be used as part of the saved file path.
**Prevention:** Always parse and validate URIs for scheme and host before making outbound requests. For file uploads, generate a completely new random filename (like a GUID) and only safely extract and append the original file extension using `Path.GetExtension()`.
