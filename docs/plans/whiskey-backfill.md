# Plan: Whiskey Backfill (Import API, MCP Server, Photo-to-Whiskey)

Status: Proposed (2026-11-05)

## Goals
1. **Photo to whiskey:** a user uploads a bottle photo, the app reads the label, and the Create form opens already filled in. The photo becomes the whiskey's image.
2. **Bulk import:** an admin-only REST endpoint loads batches of whiskeys (JSON/CSV) from sources such as Wikidata or the TTB COLA registry. It skips duplicates.
3. **MCP server:** an agent (Claude Code, Claude Desktop) can search the catalog and propose new whiskeys through tools. Proposals land in a review queue and are not published until approved.

All three share one import service, so validation and duplicate checks happen in exactly one place.

## Non-goals
- No scraping of Whiskybase or Distiller (their terms forbid it, and there is no public API).
- Nothing is auto-published. Every machine-sourced record is either reviewed by the user who scanned it or goes to the Pending queue.

---

## Phase 1: Data model and `WhiskeyImportService` (PR 1)

### Model changes (`Data/Whiskey.cs` + migration)
| Field | Type | Notes |
|---|---|---|
| `Barcode` | `string?` (max 32) | UPC/EAN, indexed |
| `Source` | `WhiskeySource` enum (`Manual`, `PhotoScan`, `Import`, `Mcp`) | default `Manual` |
| `SourceRef` | `string?` (max 200) | e.g. Wikidata QID, TTB ID, import batch ID |
| `Status` | `WhiskeyStatus` enum (`Approved`, `Pending`, `Rejected`) | default `Approved`; existing rows stay Approved |
| `NormalizedKey` | `string` (max 300) | `brand|name|age`: lowercased, punctuation and extra spaces stripped |
| `CreatedAt` | `DateTime` | |

- `NormalizedKey` gets an index. **Before making it unique**, run a duplicate check against prod (`SELECT "NormalizedKey", count(*) ... HAVING count(*) > 1`). If prod has duplicates, start with a non-unique index and merge the duplicates first (the Admin/Whiskies page can already delete them).
- `Pending` and `Rejected` whiskeys must be filtered out of the library list, search, bottle/tasting pickers and stats. Put the filter in a shared `IQueryable` extension (`.Approved()`) so new pages can't forget it.

### `Services/WhiskeyImportService.cs`
- `Normalize(WhiskeyCandidate)` trims text, caps lengths at the existing `[MaxLength]` limits, clamps ABV to 0–100 and age to 0–80, and maps type/region names onto canonical values ("Bourbon Whiskey" becomes "Bourbon").
- `FindMatchesAsync(candidate)` returns exact `NormalizedKey` matches, plus near matches (same brand, similar name) for display.
- `ImportAsync(IEnumerable<WhiskeyCandidate>, ImportOptions { DryRun, Status, Source, SourceRef })` returns an `ImportResult` with one row per candidate: `Created | Duplicate(existingId) | Invalid(errors)`.
- `WhiskeyCandidate` is a plain DTO used by all three entry points.

---

## Phase 2: Photo to whiskey (PR 2)

### Flow
1. Whiskies/Create gets a **"Fill from a photo"** panel with three ways to supply the image:
   - **Upload image:** `<input type="file" accept="image/*">` with no `capture` attribute. On desktop it opens the file picker; on mobile it offers the photo library, files *and* the camera. Desktop also supports drag-and-drop onto the panel.
   - **Take photo:** shown only on touch devices. It is a second input with `capture="environment"`, which goes straight to the rear camera. This needs its own button because `capture` stops many mobile browsers from offering the photo library.
   - **From Google Photos:** reuses the existing Google Photos picker and the `DownloadGooglePhotoAsync` path on the Create page.
   - Up to **2 images** per scan (e.g. front and back label). Back labels often carry the ABV, age statement and barcode. Every image is sent to Claude in the same request, and the first one becomes the whiskey's image.
2. The images are posted to `OnPostScanAsync`, and each one is checked by the existing `ImageUploadValidator` (same types and 10 MB cap as normal uploads).
3. The server shrinks the image so its long edge is at most 1568 px. This keeps it under the API's 5 MB image limit and reduces cost. The resized copy is saved to the photo store under a temporary GUID name.
4. `IWhiskeyLabelExtractor.ExtractAsync(bytes, mediaType)` calls Claude and returns a `WhiskeyCandidate` with a confidence score and notes for each field.
5. `WhiskeyImportService.FindMatchesAsync` runs on the result:
   - **If a match exists:** the page shows "Looks like you already have *X*". It offers **Add a bottle** (links to `AddBottle`) or **Create anyway**.
   - **If no match exists:** the page re-renders Create with the extracted values filled in and low-confidence fields highlighted. The temporary image name goes into a hidden field.
6. The user reviews and saves as usual. On save, the temporary image becomes the whiskey's `ImageFileName` and `Source = PhotoScan`. A job cleans up temporary images that were never saved after 24 hours.

### `Services/ClaudeWhiskeyLabelExtractor.cs`
- Package: official Anthropic C# SDK (`dotnet add package Anthropic`).
- Model: `claude-opus-5-5`, with effort `low` or `medium` (label reading needs little reasoning), plus the server-side refusal `fallbacks: "default"` option.
- Request: one user message holding a base64 image block and a short instruction. It asks Claude to report only what is visible on the label or reliably known about that exact product, and to return null instead of guessing.
- Structured output: `OutputConfig.Format = new JsonOutputFormat { Schema = ... }` with fields `name, brand, distillery, region, type, age, abv, caskType, barcode, isWhiskey, confidence{...}, notes`, each nullable. `isWhiskey: false` produces a friendly "That doesn't look like a whiskey bottle" message.
- Config: `Anthropic:ApiKey` comes from user secrets locally and from `whiskey-secrets` in k8s (also add it to `Set-Secrets.ps1`). **If no key is configured, the scan button is hidden**, so dev and CI run without one.
- Cost control: an ASP.NET Core rate limiter on the scan handler (e.g. 20 scans per user per day). Log token usage from `response.Usage`.
- Image resizing: SkiaSharp (MIT license, has linux-arm64 native assets for the Pi nodes). Avoid ImageSharp because of its licensing terms.

---

## Phase 3: API-key auth and the REST import endpoint (PR 3)

### API keys
- New `ApiKey` entity: `Id, Name, KeyHash (SHA-256), Prefix, CreatedByUserId, CreatedAt, LastUsedAt, RevokedAt`.
- An Admin/ApiKeys page creates keys (the full key is shown once), lists them by prefix and revokes them.
- A custom `ApiKeyAuthenticationHandler` reads `Authorization: Bearer wt_...`, hashes the key and looks it up. Successful auth carries the Admin role claim.
- Policy `RequireAdminOrApiKey` accepts either a cookie or an API key, but only for the `/api/admin/*` and `/mcp` routes.

### `Controllers/AdminWhiskiesImportController.cs`
- `POST /api/admin/whiskies/import?dryRun=true&status=Pending` accepts a JSON array of `WhiskeyCandidate`, or `text/csv`, and returns the `ImportResult`.
- `dryRun` defaults to **true**. `status` defaults to `Pending`; `Approved` is allowed for trusted bulk loads.
- The batch size cap is 500. `SourceRef` is set to a batch ID so a bad import can be found and rolled back.

---

## Phase 4: MCP server and review queue (PR 4)

### MCP server
- Package: `ModelContextProtocol.AspNetCore`. Register it with `builder.Services.AddMcpServer().WithHttpTransport().WithTools<WhiskeyCatalogTools>()` and map it with `app.MapMcp("/mcp").RequireAuthorization("RequireAdminOrApiKey")`.
- Tools (thin wrappers over `WhiskeyImportService`):
  | Tool | Purpose |
  |---|---|
  | `search_whiskeys(query, limit)` | Searches existing Approved and Pending whiskeys, so the agent checks before proposing |
  | `get_whiskey(id)` | Returns full details |
  | `propose_whiskeys(candidates[], sourceRef)` | Creates Pending records and returns per-item results |
  | `lookup_barcode(upc)` | Server-side Open Food Facts lookup (Phase 5) |
  | `catalog_stats()` | Returns counts by type, region and status to help the agent find gaps |
- Ingress: confirm that nginx/Cloudflare don't buffer streamable-HTTP responses on `/mcp` (`proxy_buffering off` on that path if needed).
- Client setup (document this in `docs/MCP.md`):
  ```bash
  claude mcp add --transport http whiskeytracker https://evolvingpour.com/mcp --header "Authorization: Bearer <key>"
  ```

### Admin/WhiskeyReview page
- Lists Pending whiskeys with their source, `SourceRef` and any near-duplicate matches.
- Actions: **Approve**, **Edit & approve**, **Reject**, **Merge into existing** (the proposal is deleted and the existing whiskey is kept), and bulk approve/reject by batch.

---

## Phase 5 (optional): Source adapters
- **Open Food Facts:** `GET https://world.openfoodfacts.org/api/v2/product/{barcode}` with a generic `User-Agent: WhiskeyTracker/1.0`. Used by `lookup_barcode` and as a hint during photo scans when a barcode is visible.
- **Wikidata:** a SPARQL query for whisky distilleries and brands (region, country, owner). This is reference data more than individual expressions, and fits the PRD's planned "structured categorization" work.
- **TTB COLA:** a one-off CSV import script through the Phase 3 endpoint. Deferred.

---

## Suggested PR order
1. Model fields, the `.Approved()` filter and `WhiskeyImportService`, with tests
2. Photo to whiskey (the most visible to users, and it only needs Phase 1)
3. API keys and the REST import endpoint
4. MCP server and the review queue
5. Source adapters, as needed

## Open questions
- Should Pending whiskeys be visible to the user who proposed them, or only to admins?
- Should photo scanning be open to every signed-in user (rate-limited), or limited to admins at first?
- Should the daily scan cap be a config value?

---

## Verification Plan

### Automated Tests (`WhiskeyTracker.Tests`, using the existing `TestBase` in-memory database)
- `WhiskeyImportServiceTests`: normalization (lengths, ABV/age clamping, type mapping), `NormalizedKey` generation, duplicate detection, dry run writes nothing, Created/Duplicate/Invalid results.
- `ApprovedFilterTests`: Pending and Rejected whiskeys never appear in library or search page models.
- `WhiskeyScanTests`: Create page scan handler with a fake `IWhiskeyLabelExtractor`. Covers prefill, one vs two images (the first becomes the whiskey image), a third image rejected, the existing-match route, the non-whiskey message, the hidden button when no key is configured, and invalid uploads rejected by `ImageUploadValidator`.
- `ClaudeWhiskeyLabelExtractorTests`: parsing a canned structured-output JSON payload into `WhiskeyCandidate`, including null fields. No live API calls in CI.
- `ApiKeyAuthTests`: valid, revoked and malformed keys; a key can't reach non-admin Razor pages.
- `AdminWhiskiesImportControllerTests`: JSON and CSV bodies, the batch cap, dry-run default, status handling.
- `WhiskeyCatalogToolsTests`: each MCP tool method called directly against the in-memory database.
- `dotnet test` passes.

### Manual Verification
- Run locally with a real `Anthropic:ApiKey`. Scan 3–5 real bottle photos (including an angled shot and one that isn't whiskey), then check the prefilled values, the low-confidence highlights and the saved image.
- Scan a bottle that's already in the catalog and confirm the "Add a bottle" route appears.
- Desktop: upload from the file picker and by drag-and-drop; a Google Photos pick; a front + back label pair.
- Mobile width in the browser preview: "Upload image" offers the library, "Take photo" goes to the camera, and the form layout works.
- Create an API key, call the import endpoint with `curl` (dry run, then for real), and check the results in Admin/WhiskeyReview.
- Connect Claude Code to `/mcp` and ask it to backfill about 10 whiskeys from a distillery. Confirm they arrive as Pending, contain no duplicates, and can be approved or merged.
- After deploying: confirm `/mcp` works through the ingress and that the photo scan works on the Pi nodes (SkiaSharp arm64).
