using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using WhiskeyTracker.Web.Data;
using WhiskeyTracker.Web.Pages.Admin;
using WhiskeyTracker.Web.Services;
using Xunit;

namespace WhiskeyTracker.Tests;

public class AdminWhiskeyValuesTests : TestBase
{
    private WhiskeyValuesModel CreatePage(AppDbContext context, WhiskeyValueField field)
    {
        var page = new WhiskeyValuesModel(context, NullLogger<WhiskeyValuesModel>.Instance) { Field = field };
        SetMockUser(page, "admin-user");
        return page;
    }

    private static Whiskey W(string brand, string name, string type = "", int? age = null) =>
        new() { Brand = brand, Name = name, Distillery = brand, Type = type, Age = age };

    [Fact]
    public void SuggestGroups_GroupsPrefixAndNormalizedVariants()
    {
        var values = new List<WhiskeyValueCount>
        {
            new("Kentucky Straight Bourbon", 5),
            new("Kentucky Straight Bourbon Whiskey", 2),
            new("New Riff", 1),
            new("New Riff Distilling", 3),
            new("Maker's Mark", 2),
            new("Makers Mark", 1),
            new("Rye", 4),
            new("", 6)
        };

        var groups = WhiskeyValueMerge.SuggestGroups(values);

        Assert.Equal(3, groups.Count);
        Assert.Contains(groups, g => g.Select(v => v.Value).SequenceEqual(new[] { "Kentucky Straight Bourbon", "Kentucky Straight Bourbon Whiskey" }));
        // Most-used value comes first so it is the default "keep".
        Assert.Contains(groups, g => g.Select(v => v.Value).SequenceEqual(new[] { "New Riff Distilling", "New Riff" }));
        Assert.Contains(groups, g => g.Select(v => v.Value).SequenceEqual(new[] { "Maker's Mark", "Makers Mark" }));
    }

    [Fact]
    public void SuggestGroups_DoesNotMatchPartialWords()
    {
        var groups = WhiskeyValueMerge.SuggestGroups(new List<WhiskeyValueCount> { new("Rye", 1), new("Ryeland", 1) });
        Assert.Empty(groups);
    }

    [Fact]
    public async Task OnGet_ListsValuesWithCountsAndSuggestions()
    {
        using var context = GetInMemoryContext();
        context.Whiskies.AddRange(W("New Riff", "A"), W("New Riff Distilling", "B"), W("New Riff Distilling", "C"), W("Buffalo Trace", "D"));
        await context.SaveChangesAsync();

        var page = CreatePage(context, WhiskeyValueField.Brand);
        await page.OnGetAsync();

        Assert.Equal(3, page.Values.Count);
        Assert.Equal(2, page.Values.Single(v => v.Value == "New Riff Distilling").Count);
        var group = Assert.Single(page.Suggestions);
        Assert.Equal("New Riff Distilling", group[0].Value);
    }

    [Fact]
    public async Task OnPostPreview_ShowsCountsWithoutChangingData()
    {
        using var context = GetInMemoryContext();
        context.Whiskies.AddRange(
            W("X", "A", "Kentucky Straight Bourbon"),
            W("X", "B", "Kentucky Straight Bourbon Whiskey"),
            W("X", "C", "Kentucky Straight Bourbon Whiskey"));
        await context.SaveChangesAsync();

        var page = CreatePage(context, WhiskeyValueField.Type);
        var result = await page.OnPostPreviewAsync("Kentucky Straight Bourbon", new[] { "Kentucky Straight Bourbon Whiskey" });

        Assert.IsType<PageResult>(result);
        Assert.NotNull(page.Preview);
        Assert.Equal(2, page.Preview!.AffectedCount);
        Assert.Empty(page.Preview.NewDuplicates);
        Assert.Equal(2, await context.Whiskies.CountAsync(w => w.Type == "Kentucky Straight Bourbon Whiskey"));
    }

    [Fact]
    public async Task OnPostPreview_WarnsWhenBrandMergeCreatesDuplicates()
    {
        using var context = GetInMemoryContext();
        context.Whiskies.AddRange(W("New Riff", "Bottled in Bond"), W("New Riff Distilling", "Bottled in Bond"), W("New Riff", "Rye"));
        await context.SaveChangesAsync();

        var page = CreatePage(context, WhiskeyValueField.Brand);
        await page.OnPostPreviewAsync("New Riff Distilling", new[] { "New Riff" });

        var dup = Assert.Single(page.Preview!.NewDuplicates);
        Assert.Contains("Bottled in Bond", dup);
    }

    [Fact]
    public async Task OnPostMerge_RewritesValuesAndNormalizedKey()
    {
        using var context = GetInMemoryContext();
        context.Whiskies.AddRange(W("New Riff", "Rye"), W("New Riff Distilling", "Bourbon"), W("Buffalo Trace", "Bourbon"));
        await context.SaveChangesAsync();

        var page = CreatePage(context, WhiskeyValueField.Brand);
        var result = await page.OnPostMergeAsync(" New Riff Distilling ", new[] { "New Riff" });

        Assert.IsType<RedirectToPageResult>(result);
        var brands = await context.Whiskies.AsNoTracking().Select(w => w.Brand).ToListAsync();
        Assert.Equal(2, brands.Count(b => b == "New Riff Distilling"));
        Assert.DoesNotContain("New Riff", brands);
        Assert.Contains("Buffalo Trace", brands);

        var rye = await context.Whiskies.AsNoTracking().SingleAsync(w => w.Name == "Rye");
        Assert.Equal(WhiskeyKey.For("New Riff Distilling", "Rye", null), rye.NormalizedKey);
    }

    [Fact]
    public async Task OnPostMerge_OnlyChangesTheChosenField()
    {
        using var context = GetInMemoryContext();
        var whiskey = W("Bourbon Co", "A", "Bourbon");
        whiskey.Region = "Bourbon";
        context.Whiskies.Add(whiskey);
        await context.SaveChangesAsync();

        var page = CreatePage(context, WhiskeyValueField.Type);
        await page.OnPostMergeAsync("Straight Bourbon", new[] { "Bourbon" });

        var saved = await context.Whiskies.AsNoTracking().SingleAsync();
        Assert.Equal("Straight Bourbon", saved.Type);
        Assert.Equal("Bourbon", saved.Region);
        Assert.Equal("Bourbon Co", saved.Brand);
    }

    [Theory]
    [InlineData("", new[] { "New Riff" })]
    [InlineData("New Riff Distilling", new string[0])]
    [InlineData("New Riff", new[] { "New Riff" })]
    public async Task OnPostMerge_RejectsInvalidInput(string target, string[] sources)
    {
        using var context = GetInMemoryContext();
        context.Whiskies.AddRange(W("New Riff", "Rye"), W("New Riff Distilling", "Bourbon"));
        await context.SaveChangesAsync();

        var page = CreatePage(context, WhiskeyValueField.Brand);
        var result = await page.OnPostMergeAsync(target, sources);

        Assert.IsType<PageResult>(result);
        Assert.NotNull(page.ErrorMessage);
        Assert.Equal(1, await context.Whiskies.CountAsync(w => w.Brand == "New Riff"));
    }

    [Fact]
    public async Task OnPostMerge_RejectsTargetLongerThanField()
    {
        using var context = GetInMemoryContext();
        context.Whiskies.Add(W("X", "A", "Bourbon"));
        await context.SaveChangesAsync();

        var page = CreatePage(context, WhiskeyValueField.Type);
        var result = await page.OnPostMergeAsync(new string('a', 61), new[] { "Bourbon" });

        Assert.IsType<PageResult>(result);
        Assert.Equal("Bourbon", (await context.Whiskies.AsNoTracking().SingleAsync()).Type);
    }
}
