using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WhiskeyTracker.Web.Data;
using WhiskeyTracker.Web.Pages.Admin;
using Microsoft.EntityFrameworkCore;
using Moq;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace WhiskeyTracker.Tests;

public class AdminTagTests
{
    private AppDbContext GetInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private void SetMockAdmin(PageModel page)
    {
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, "admin-user"),
            new Claim(ClaimTypes.Role, "Admin")
        };
        var identity = new ClaimsIdentity(claims, "TestAuthType");
        var claimsPrincipal = new ClaimsPrincipal(identity);

        page.PageContext = new PageContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = claimsPrincipal
            }
        };

        var mockTempData = new Mock<ITempDataDictionary>();
        page.TempData = mockTempData.Object;
    }

    [Fact]
    public async Task OnGet_LoadsPendingAndApprovedTags()
    {
        // ARRANGE
        using var context = GetInMemoryContext();
        context.Tags.AddRange(
            new Tag { Name = "Approved1", IsApproved = true },
            new Tag { Name = "Pending1", IsApproved = false }
        );
        await context.SaveChangesAsync();

        var pageModel = new TagsModel(context);
        SetMockAdmin(pageModel);

        // ACT
        await pageModel.OnGetAsync();

        // ASSERT
        Assert.Single(pageModel.ApprovedTags);
        Assert.Equal("Approved1", pageModel.ApprovedTags[0].Name);
        Assert.Single(pageModel.PendingTags);
        Assert.Equal("Pending1", pageModel.PendingTags[0].Name);
    }

    [Fact]
    public async Task OnPostApprove_SetsIsApprovedToTrue()
    {
        // ARRANGE
        using var context = GetInMemoryContext();
        var tag = new Tag { Name = "Pending", IsApproved = false };
        context.Tags.Add(tag);
        await context.SaveChangesAsync();

        var pageModel = new TagsModel(context);
        SetMockAdmin(pageModel);

        // ACT
        var result = await pageModel.OnPostApproveAsync(tag.Id);

        // ASSERT
        Assert.IsType<RedirectToPageResult>(result);
        var updatedTag = await context.Tags.FindAsync(tag.Id);
        Assert.True(updatedTag!.IsApproved);
    }

    [Fact]
    public async Task OnPostMerge_RemapsTagsAndDeleteSource()
    {
        // ARRANGE
        using var context = GetInMemoryContext();
        var source = new Tag { Name = "Source", IsApproved = false };
        var target = new Tag { Name = "Target", IsApproved = true };
        context.Tags.AddRange(source, target);
        await context.SaveChangesAsync();

        var note = new TastingNote { Rating = 5, UserId = "user1", OrderIndex = 1 };
        context.TastingNotes.Add(note);
        await context.SaveChangesAsync();

        var tnt = new TastingNoteTag { TastingNoteId = note.Id, TagId = source.Id, Field = TastingField.Nose };
        context.TastingNoteTags.Add(tnt);
        await context.SaveChangesAsync();

        var pageModel = new TagsModel(context);
        SetMockAdmin(pageModel);

        // ACT
        var result = await pageModel.OnPostMergeAsync(source.Id, target.Id);

        // ASSERT
        Assert.IsType<RedirectToPageResult>(result);
        
        // Target should be used now
        var associations = await context.TastingNoteTags.Where(t => t.TastingNoteId == note.Id).ToListAsync();
        Assert.Single(associations);
        Assert.Equal(target.Id, associations[0].TagId);

        // Source should be deleted
        var deletedSource = await context.Tags.FindAsync(source.Id);
        Assert.Null(deletedSource);
    }
}
