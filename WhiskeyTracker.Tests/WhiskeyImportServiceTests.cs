using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using WhiskeyTracker.Web.Data;
using WhiskeyTracker.Web.Pages.Whiskies;
using WhiskeyTracker.Web.Services;
using Xunit;

namespace WhiskeyTracker.Tests;

public class WhiskeyImportServiceTests : TestBase
{
    private static WhiskeyCandidate Candidate(string brand, string name, int? age = null) =>
        new() { Brand = brand, Name = name, Age = age };

    // --- WhiskeyKey ---

    [Theory]
    [InlineData("Maker's Mark", "Maker’s Mark")]
    [InlineData("Maker's Mark", "  makers   MARK ")]
    [InlineData("Johnnie Walker", "Johnnie-Walker")]
    [InlineData("Bruichladdich", "Bruichláddich")]
    [InlineData("Smith & Sons", "Smith and Sons")]
    public void WhiskeyKey_NormalizeText_TreatsVariantsAsEqual(string a, string b)
    {
        Assert.Equal(WhiskeyKey.NormalizeText(a), WhiskeyKey.NormalizeText(b));
    }

    [Fact]
    public void WhiskeyKey_For_IncludesAge()
    {
        Assert.Equal("lagavulin|lagavulin|16", WhiskeyKey.For("Lagavulin", "Lagavulin", 16));
        Assert.NotEqual(WhiskeyKey.For("Lagavulin", "Lagavulin", 16), WhiskeyKey.For("Lagavulin", "Lagavulin", 8));
        Assert.Equal("ardbeg|uigeadail|", WhiskeyKey.For("Ardbeg", "Uigeadail", null));
    }

    [Fact]
    public async Task SaveChanges_SetsNormalizedKeyAndCreatedAt_OnAdd_AndUpdatesKeyOnEdit()
    {
        using var context = GetInMemoryContext();
        var whiskey = new Whiskey { Brand = "Ardbeg", Name = "Ten", Distillery = "Ardbeg" };
        context.Whiskies.Add(whiskey);
        await context.SaveChangesAsync();

        Assert.Equal("ardbeg|ten|", whiskey.NormalizedKey);
        Assert.NotNull(whiskey.CreatedAt);

        whiskey.Age = 10;
        await context.SaveChangesAsync();

        Assert.Equal("ardbeg|ten|10", whiskey.NormalizedKey);
    }

    // --- Normalize ---

    [Fact]
    public void Normalize_TrimsTruncatesAndMapsType()
    {
        using var context = GetInMemoryContext();
        var service = new WhiskeyImportService(context);

        var result = service.Normalize(new WhiskeyCandidate
        {
            Brand = "  Buffalo   Trace ",
            Name = new string('x', 150),
            Type = "Kentucky Straight Bourbon Whiskey",
            Barcode = "0 80244-00975 0",
        });

        Assert.True(result.IsValid);
        Assert.Equal("Buffalo Trace", result.Whiskey.Brand);
        Assert.Equal(100, result.Whiskey.Name.Length);
        Assert.Equal("Bourbon", result.Whiskey.Type);
        Assert.Equal("080244009750", result.Whiskey.Barcode);
        Assert.Equal("Buffalo Trace", result.Whiskey.Distillery);
        Assert.Contains(result.Warnings, w => w.Contains("Distillery"));
    }

    [Fact]
    public void Normalize_DropsOutOfRangeNumbersWithWarnings()
    {
        using var context = GetInMemoryContext();
        var service = new WhiskeyImportService(context);

        var result = service.Normalize(new WhiskeyCandidate
        {
            Brand = "Booker's", Name = "Batch", Distillery = "Jim Beam",
            ABV = 126.4, Age = 200, Barcode = "abc"
        });

        Assert.True(result.IsValid);
        Assert.Null(result.Whiskey.ABV);
        Assert.Null(result.Whiskey.Age);
        Assert.Null(result.Whiskey.Barcode);
        Assert.Equal(3, result.Warnings.Count);
    }

    [Fact]
    public void Normalize_RequiresNameAndBrand()
    {
        using var context = GetInMemoryContext();
        var service = new WhiskeyImportService(context);

        var result = service.Normalize(new WhiskeyCandidate { Name = " ", Brand = null });

        Assert.False(result.IsValid);
        Assert.Equal(2, result.Errors.Count);
    }

    // --- ImportAsync ---

    [Fact]
    public async Task Import_DryRun_ReportsWithoutWriting()
    {
        using var context = GetInMemoryContext();
        var service = new WhiskeyImportService(context);

        var result = await service.ImportAsync(
            new[] { Candidate("Ardbeg", "Ten"), Candidate("Lagavulin", "Lagavulin", 16) },
            new ImportOptions());

        Assert.True(result.DryRun);
        Assert.Equal(2, result.Created);
        Assert.All(result.Items, i => Assert.Equal(ImportOutcome.WouldCreate, i.Outcome));
        Assert.Empty(context.Whiskies);
    }

    [Fact]
    public async Task Import_CreatesPendingWhiskeysWithSource()
    {
        using var context = GetInMemoryContext();
        var service = new WhiskeyImportService(context);

        var result = await service.ImportAsync(
            new[] { Candidate("Ardbeg", "Ten") },
            new ImportOptions { DryRun = false, SourceRef = "batch-1" });

        var item = Assert.Single(result.Items);
        Assert.Equal(ImportOutcome.Created, item.Outcome);
        var saved = await context.Whiskies.SingleAsync();
        Assert.Equal(item.WhiskeyId, saved.Id);
        Assert.Equal(WhiskeyStatus.Pending, saved.Status);
        Assert.Equal(WhiskeySource.Import, saved.Source);
        Assert.Equal("batch-1", saved.SourceRef);
        Assert.Equal("ardbeg|ten|", saved.NormalizedKey);
    }

    [Fact]
    public async Task Import_SkipsDuplicatesOfExistingWhiskeys_IncludingRejected()
    {
        using var context = GetInMemoryContext();
        context.Whiskies.Add(new Whiskey { Brand = "Maker's Mark", Name = "46", Distillery = "Maker's Mark" });
        context.Whiskies.Add(new Whiskey { Brand = "Ardbeg", Name = "Ten", Distillery = "Ardbeg", Status = WhiskeyStatus.Rejected });
        await context.SaveChangesAsync();
        var service = new WhiskeyImportService(context);

        var result = await service.ImportAsync(
            new[] { Candidate("makers mark", "46"), Candidate("ARDBEG", "ten"), Candidate("Ardbeg", "Wee Beastie") },
            new ImportOptions { DryRun = false });

        Assert.Equal(ImportOutcome.Duplicate, result.Items[0].Outcome);
        Assert.Equal(ImportOutcome.Duplicate, result.Items[1].Outcome);
        Assert.Equal(WhiskeyStatus.Rejected, result.Items[1].ExistingStatus);
        Assert.Equal(ImportOutcome.Created, result.Items[2].Outcome);
        Assert.Equal(3, await context.Whiskies.CountAsync());
    }

    [Fact]
    public async Task Import_SkipsDuplicatesWithinBatch_AndBarcodeMatches()
    {
        using var context = GetInMemoryContext();
        context.Whiskies.Add(new Whiskey { Brand = "Buffalo Trace", Name = "Bourbon", Distillery = "Buffalo Trace", Barcode = "080244009750" });
        await context.SaveChangesAsync();
        var service = new WhiskeyImportService(context);

        var byBarcode = new WhiskeyCandidate { Brand = "BT", Name = "Kentucky Straight", Barcode = "080244009750" };
        var result = await service.ImportAsync(
            new[] { Candidate("Ardbeg", "Ten"), Candidate("Ardbeg", "Ten"), byBarcode },
            new ImportOptions { DryRun = true });

        Assert.Equal(ImportOutcome.WouldCreate, result.Items[0].Outcome);
        Assert.Equal(ImportOutcome.Duplicate, result.Items[1].Outcome);
        Assert.Equal(0, result.Items[1].DuplicateOfIndex);
        Assert.Equal(ImportOutcome.Duplicate, result.Items[2].Outcome);
        Assert.NotNull(result.Items[2].WhiskeyId);
    }

    [Fact]
    public async Task Import_ReportsInvalidItems()
    {
        using var context = GetInMemoryContext();
        var service = new WhiskeyImportService(context);

        var result = await service.ImportAsync(
            new[] { new WhiskeyCandidate { Name = "No brand" } },
            new ImportOptions { DryRun = false });

        Assert.Equal(ImportOutcome.Invalid, Assert.Single(result.Items).Outcome);
        Assert.Empty(context.Whiskies);
    }

    [Fact]
    public async Task Import_RejectsOversizedBatch()
    {
        using var context = GetInMemoryContext();
        var service = new WhiskeyImportService(context);
        var batch = Enumerable.Range(0, WhiskeyImportService.MaxBatchSize + 1)
            .Select(i => Candidate("Brand", $"Name {i}")).ToList();

        await Assert.ThrowsAsync<ArgumentException>(() => service.ImportAsync(batch, new ImportOptions()));
    }

    // --- FindMatchesAsync ---

    [Fact]
    public async Task FindMatches_ReturnsExactAndSimilar()
    {
        using var context = GetInMemoryContext();
        context.Whiskies.Add(new Whiskey { Brand = "Ardbeg", Name = "Ten", Distillery = "Ardbeg" });
        context.Whiskies.Add(new Whiskey { Brand = "Ardbeg", Name = "Ten Year Old Limited", Distillery = "Ardbeg" });
        context.Whiskies.Add(new Whiskey { Brand = "Laphroaig", Name = "Ten", Distillery = "Laphroaig" });
        await context.SaveChangesAsync();
        var service = new WhiskeyImportService(context);

        var matches = await service.FindMatchesAsync(Candidate("Ardbeg", "Ten"));

        Assert.Equal(2, matches.Count);
        Assert.Equal(MatchKind.Exact, matches[0].Kind);
        Assert.Equal("Ten", matches[0].Name);
        Assert.Equal(MatchKind.Similar, matches[1].Kind);
    }

    // --- Backfill ---

    [Fact]
    public async Task BackfillNormalizedKeys_FillsMissingKeys()
    {
        // Simulate a row saved before the column existed: the interceptor runs after AppDbContext
        // stamps the key, so the row is stored with an empty key.
        var dbName = Guid.NewGuid().ToString();
        using (var legacy = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
                   .UseInMemoryDatabase(dbName)
                   .AddInterceptors(new ClearNormalizedKeyInterceptor())
                   .Options))
        {
            legacy.Whiskies.Add(new Whiskey { Brand = "Ardbeg", Name = "Ten", Distillery = "Ardbeg" });
            await legacy.SaveChangesAsync();
        }

        using var context = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName).Options);
        Assert.Equal("", (await context.Whiskies.AsNoTracking().SingleAsync()).NormalizedKey);

        var service = new WhiskeyImportService(context);
        var count = await service.BackfillNormalizedKeysAsync();

        Assert.Equal(1, count);
        Assert.Equal("ardbeg|ten|", (await context.Whiskies.SingleAsync()).NormalizedKey);
        Assert.Equal(0, await service.BackfillNormalizedKeysAsync());
    }

    // --- Approved filter and page integration ---

    [Fact]
    public async Task Library_HidesPendingAndRejectedWhiskeys()
    {
        using var context = GetInMemoryContext();
        context.Whiskies.Add(new Whiskey { Brand = "Ardbeg", Name = "Ten", Distillery = "Ardbeg", Region = "Islay" });
        context.Whiskies.Add(new Whiskey { Brand = "Pending Co", Name = "Proposed", Distillery = "P", Region = "Nowhere", Status = WhiskeyStatus.Pending });
        context.Whiskies.Add(new Whiskey { Brand = "Rejected Co", Name = "Nope", Distillery = "R", Status = WhiskeyStatus.Rejected });
        await context.SaveChangesAsync();

        var pageModel = new IndexModel(context, new LegacyMigrationService(context));
        SetMockUser(pageModel, "test-user");
        await pageModel.OnGetAsync();

        var only = Assert.Single(pageModel.Whiskies);
        Assert.Equal("Ardbeg", only.Brand);
        Assert.DoesNotContain(pageModel.Regions!, r => r.Text == "Nowhere");
        Assert.DoesNotContain(pageModel.Brands!, b => b.Text == "Pending Co");
    }

    [Fact]
    public async Task Edit_PreservesImportFieldsNotOnTheForm()
    {
        using var context = GetInMemoryContext();
        context.Whiskies.Add(new Whiskey
        {
            Id = 1, Brand = "Ardbeg", Name = "Ten", Distillery = "Ardbeg",
            Status = WhiskeyStatus.Pending, Source = WhiskeySource.Mcp, SourceRef = "Q123",
            Barcode = "5010494195286"
        });
        await context.SaveChangesAsync();
        var createdAt = (await context.Whiskies.SingleAsync()).CreatedAt;
        context.ChangeTracker.Clear();

        // The form posts only the editable fields; the rest arrive as defaults.
        var pageModel = new EditModel(context, new Mock<IWebHostEnvironment>().Object)
        {
            Whiskey = new Whiskey { Id = 1, Brand = "Ardbeg", Name = "Ten Year Old", Distillery = "Ardbeg" }
        };

        var result = await pageModel.OnPostAsync();

        Assert.IsType<RedirectToPageResult>(result);
        context.ChangeTracker.Clear();
        var saved = await context.Whiskies.SingleAsync();
        Assert.Equal("Ten Year Old", saved.Name);
        Assert.Equal(WhiskeyStatus.Pending, saved.Status);
        Assert.Equal(WhiskeySource.Mcp, saved.Source);
        Assert.Equal("Q123", saved.SourceRef);
        Assert.Equal("5010494195286", saved.Barcode);
        Assert.Equal(createdAt, saved.CreatedAt);
        Assert.Equal("ardbeg|ten year old|", saved.NormalizedKey);
    }

    private sealed class ClearNormalizedKeyInterceptor : Microsoft.EntityFrameworkCore.Diagnostics.SaveChangesInterceptor
    {
        public override ValueTask<Microsoft.EntityFrameworkCore.Diagnostics.InterceptionResult<int>> SavingChangesAsync(
            Microsoft.EntityFrameworkCore.Diagnostics.DbContextEventData eventData,
            Microsoft.EntityFrameworkCore.Diagnostics.InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            foreach (var entry in eventData.Context!.ChangeTracker.Entries<Whiskey>())
            {
                entry.Entity.NormalizedKey = "";
            }
            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }
}
