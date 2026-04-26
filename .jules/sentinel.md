## 2024-04-16 - Missing Admin Authorization on Master Whiskey Data
**Vulnerability:** Any authenticated user could create, edit, or delete entries in the master `Whiskey` global directory because `Create.cshtml.cs`, `Edit.cshtml.cs`, and `Delete.cshtml.cs` under `Pages/Whiskies` lacked authorization attributes. Only the `/Admin` folder was protected by convention.
**Learning:** Razor Pages convention-based folder authorization (`AuthorizeFolder("/Admin")`) does not automatically protect administrative-level entities that reside outside the designated admin folder.
**Prevention:** Always explicitly annotate page models with `[Authorize(Roles = "Admin")]` for global entity modification pages, regardless of folder structure.

## 2024-04-26 - Server-Side Request Forgery (SSRF) in Google Photos Integration
**Vulnerability:** The application took user-provided URLs (`GooglePhotoUrl`) and directly executed `HttpClient.GetAsync(GooglePhotoUrl)` without checking the scheme or host. This allowed an attacker to potentially access internal network resources or read internal files.
**Learning:** External URLs provided via user input must always be validated before being fetched on the server-side to prevent SSRF attacks. Relying on client-side logic or hoping the user provides a valid Google Photos URL is insufficient.
**Prevention:** Always validate that user-provided URLs use a secure scheme (`https`) and target an expected, trusted domain (e.g., `.googleusercontent.com`) before passing them to `HttpClient`.
