## 2024-04-16 - Missing Admin Authorization on Master Whiskey Data
**Vulnerability:** Any authenticated user could create, edit, or delete entries in the master `Whiskey` global directory because `Create.cshtml.cs`, `Edit.cshtml.cs`, and `Delete.cshtml.cs` under `Pages/Whiskies` lacked authorization attributes. Only the `/Admin` folder was protected by convention.
**Learning:** Razor Pages convention-based folder authorization (`AuthorizeFolder("/Admin")`) does not automatically protect administrative-level entities that reside outside the designated admin folder.
**Prevention:** Always explicitly annotate page models with `[Authorize(Roles = "Admin")]` for global entity modification pages, regardless of folder structure.

## 2024-04-16 - SSRF and Token Leak in Google Photos Integration
**Vulnerability:** The application took an untrusted URL (`GooglePhotoUrl`) provided by the client and immediately performed a GET request to that URL, using the `GooglePhotoToken` as a Bearer token. If an attacker provided a URL pointing to a server they control, they could intercept the OAuth token. This could also be used to scan internal network resources (SSRF).
**Learning:** Any user-provided URL fetched by the server must be strictly validated to ensure it points to the expected domain and uses a secure protocol.
**Prevention:** Always validate `Uri.Scheme` is HTTPS and `Uri.Host` ends with the expected trusted domain(s) (e.g., `.googleusercontent.com` or `.googleapis.com`) before making outbound HTTP requests with sensitive tokens.
