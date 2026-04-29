## 2024-04-16 - Missing Admin Authorization on Master Whiskey Data
**Vulnerability:** Any authenticated user could create, edit, or delete entries in the master `Whiskey` global directory because `Create.cshtml.cs`, `Edit.cshtml.cs`, and `Delete.cshtml.cs` under `Pages/Whiskies` lacked authorization attributes. Only the `/Admin` folder was protected by convention.
**Learning:** Razor Pages convention-based folder authorization (`AuthorizeFolder("/Admin")`) does not automatically protect administrative-level entities that reside outside the designated admin folder.
**Prevention:** Always explicitly annotate page models with `[Authorize(Roles = "Admin")]` for global entity modification pages, regardless of folder structure.

## 2025-02-27 - Server-Side Request Forgery (SSRF) in Image Import
**Vulnerability:** The application allowed users to provide a `GooglePhotoUrl` that the server would fetch via `HttpClient.GetAsync()` without validating the URL scheme or host. This could be abused to make the server perform requests to internal networks or unapproved external domains.
**Learning:** Accepting user-provided URLs for server-side fetching is dangerous unless strictly constrained to expected domains.
**Prevention:** Always validate URLs using `Uri.TryCreate`, verify the scheme is HTTPS, and strictly enforce allowed hosts (e.g., `parsedUri.Host.EndsWith(".googleusercontent.com")`) before invoking HTTP clients.
