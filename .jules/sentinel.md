## 2024-04-16 - Missing Admin Authorization on Master Whiskey Data
**Vulnerability:** Any authenticated user could create, edit, or delete entries in the master `Whiskey` global directory because `Create.cshtml.cs`, `Edit.cshtml.cs`, and `Delete.cshtml.cs` under `Pages/Whiskies` lacked authorization attributes. Only the `/Admin` folder was protected by convention.
**Learning:** Razor Pages convention-based folder authorization (`AuthorizeFolder("/Admin")`) does not automatically protect administrative-level entities that reside outside the designated admin folder.
**Prevention:** Always explicitly annotate page models with `[Authorize(Roles = "Admin")]` for global entity modification pages, regardless of folder structure.

## 2026-04-27 - Missing URL Validation Leading to SSRF Risk
**Vulnerability:** GooglePhotoUrl parameter was used directly in HttpClient.GetAsync without validating the URI scheme or host, leading to a potential Server-Side Request Forgery (SSRF) vulnerability where an attacker could force the server to make requests to internal network resources.
**Learning:** Always validate user-provided URLs before fetching them on the server side to ensure they point to expected external hosts and use secure protocols.
**Prevention:** Use Uri.TryCreate to ensure absolute URIs, restrict the scheme to HTTPS, and validate that the host ends with the expected domain (e.g., .googleusercontent.com) before passing the URL to HttpClient.
