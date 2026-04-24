## 2024-04-16 - Missing Admin Authorization on Master Whiskey Data
**Vulnerability:** Any authenticated user could create, edit, or delete entries in the master `Whiskey` global directory because `Create.cshtml.cs`, `Edit.cshtml.cs`, and `Delete.cshtml.cs` under `Pages/Whiskies` lacked authorization attributes. Only the `/Admin` folder was protected by convention.
**Learning:** Razor Pages convention-based folder authorization (`AuthorizeFolder("/Admin")`) does not automatically protect administrative-level entities that reside outside the designated admin folder.
**Prevention:** Always explicitly annotate page models with `[Authorize(Roles = "Admin")]` for global entity modification pages, regardless of folder structure.

## 2024-04-24 - SSRF Vulnerability in Google Photos Integration
**Vulnerability:** The `Create.cshtml.cs` and `Edit.cshtml.cs` pages under `Pages/Whiskies` accepted an arbitrary, unsanitized `GooglePhotoUrl` via `[BindProperty]` and subsequently passed it to `HttpClient.GetAsync()`. This created a Server-Side Request Forgery (SSRF) vulnerability where an attacker could force the server to make HTTP requests to internal or unintended external systems.
**Learning:** External user input, particularly URLs, should never be trusted and passed directly to an HTTP client. Doing so permits SSRF attacks.
**Prevention:** Always validate external URLs using strict whitelists before issuing outbound requests. Ensure the URI scheme is HTTPS and the host matches the expected, trusted provider (e.g., `*.googleusercontent.com`).
