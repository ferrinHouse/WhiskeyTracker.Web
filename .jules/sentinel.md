## 2024-04-16 - Missing Admin Authorization on Master Whiskey Data
**Vulnerability:** Any authenticated user could create, edit, or delete entries in the master `Whiskey` global directory because `Create.cshtml.cs`, `Edit.cshtml.cs`, and `Delete.cshtml.cs` under `Pages/Whiskies` lacked authorization attributes. Only the `/Admin` folder was protected by convention.
**Learning:** Razor Pages convention-based folder authorization (`AuthorizeFolder("/Admin")`) does not automatically protect administrative-level entities that reside outside the designated admin folder.
**Prevention:** Always explicitly annotate page models with `[Authorize(Roles = "Admin")]` for global entity modification pages, regardless of folder structure.

## 2024-05-24 - SSRF Vulnerability in Google Photos Integration
**Vulnerability:** The `GooglePhotoUrl` property in `Create.cshtml.cs` and `Edit.cshtml.cs` was passed directly to `HttpClient.GetAsync()` without validation. This allowed an attacker to submit an arbitrary URL, potentially exploiting Server-Side Request Forgery (SSRF) to scan internal networks or access sensitive resources on behalf of the server.
**Learning:** Whenever the application fetches resources from user-provided URLs, the destination must be explicitly validated. Do not assume the frontend (which might set the URL via JavaScript) guarantees a safe endpoint.
**Prevention:** Use `Uri.TryCreate` to ensure the URL is absolute, verify `uri.Scheme == Uri.UriSchemeHttps`, and check that `uri.Host` ends with a trusted domain (e.g., `.googleusercontent.com`) before making the HTTP request.
