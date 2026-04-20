## 2024-04-16 - Missing Admin Authorization on Master Whiskey Data
**Vulnerability:** Any authenticated user could create, edit, or delete entries in the master `Whiskey` global directory because `Create.cshtml.cs`, `Edit.cshtml.cs`, and `Delete.cshtml.cs` under `Pages/Whiskies` lacked authorization attributes. Only the `/Admin` folder was protected by convention.
**Learning:** Razor Pages convention-based folder authorization (`AuthorizeFolder("/Admin")`) does not automatically protect administrative-level entities that reside outside the designated admin folder.
**Prevention:** Always explicitly annotate page models with `[Authorize(Roles = "Admin")]` for global entity modification pages, regardless of folder structure.

## 2026-04-20 - Server-Side Request Forgery (SSRF) in Image Upload
**Vulnerability:** The application fetches images from user-provided URLs in `GooglePhotoUrl` directly via `HttpClient.GetAsync()` without validating the URL in `Create.cshtml.cs` and `Edit.cshtml.cs`. This allows an attacker to specify an internal URL or a malicious external URL, potentially exposing internal services or facilitating SSRF attacks.
**Learning:** Never trust user-provided URLs. Always validate and restrict the URLs an application fetches resources from, especially when fetching them programmatically from the server.
**Prevention:** Strictly validate that the URI uses HTTPS and points to an expected, trusted host (e.g., `.googleusercontent.com`) before performing external requests with `HttpClient`.
