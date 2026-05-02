## 2024-04-16 - Missing Admin Authorization on Master Whiskey Data
**Vulnerability:** Any authenticated user could create, edit, or delete entries in the master `Whiskey` global directory because `Create.cshtml.cs`, `Edit.cshtml.cs`, and `Delete.cshtml.cs` under `Pages/Whiskies` lacked authorization attributes. Only the `/Admin` folder was protected by convention.
**Learning:** Razor Pages convention-based folder authorization (`AuthorizeFolder("/Admin")`) does not automatically protect administrative-level entities that reside outside the designated admin folder.
**Prevention:** Always explicitly annotate page models with `[Authorize(Roles = "Admin")]` for global entity modification pages, regardless of folder structure.

## 2026-05-02 - SSRF Vulnerability in Google Photos Integration
**Vulnerability:** The application accepted arbitrary, user-supplied URLs for `GooglePhotoUrl` during whiskey creation and editing. It passed these unchecked to `HttpClient.GetAsync()`, allowing potential Server-Side Request Forgery (SSRF) against internal or unexpected external hosts.
**Learning:** Accepting user input to fetch resources requires explicit validation to ensure requests only hit expected, safe endpoints.
**Prevention:** Strictly validate that any fetched URI uses HTTPS and targets a trusted host (e.g., `.googleusercontent.com` or `.googleapis.com`) before invoking HTTP requests.
