using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WhiskeyTracker.Web.Data;
using WhiskeyTracker.Web.Pages.Tasting;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

using Moq;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace WhiskeyTracker.Tests;

public class WizardTests
{
    private AppDbContext GetInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private void SetMockUser(PageModel page, string userId)
    {
        var claims = new List<System.Security.Claims.Claim>
        {
            new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, userId)
        };
        var identity = new System.Security.Claims.ClaimsIdentity(claims, "TestAuthType");
        var claimsPrincipal = new System.Security.Claims.ClaimsPrincipal(identity);

        page.PageContext = new PageContext
        {
            HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext
            {
                User = claimsPrincipal
            }
        };

        // Mock TempData using Moq
        var mockTempData = new Mock<ITempDataDictionary>();
        page.TempData = mockTempData.Object;
    }

    [Fact]
    public async Task OnPost_ConvertsOzToMl_AndUpdatesBottle()
    {
        // ARRANGE
        using var context = GetInMemoryContext();
        var userId = "test-user";
        var whiskey = new Whiskey { Name = "Test Whiskey", Distillery = "Test Distillery" };
        context.Whiskies.Add(whiskey);
        await context.SaveChangesAsync();

        var collection = new Collection { Name = "Test Collection" };
        context.Collections.Add(collection);
        await context.SaveChangesAsync();

        context.CollectionMembers.Add(new CollectionMember { CollectionId = collection.Id, UserId = userId, Role = CollectionRole.Owner });
        
        var bottle = new Bottle 
        { 
            WhiskeyId = whiskey.Id, 
            CollectionId = collection.Id, 
            UserId = userId, 
            CurrentVolumeMl = 750, 
            Status = BottleStatus.Full 
        };
        context.Bottles.Add(bottle);
        await context.SaveChangesAsync();

        var session = new TastingSession { Title = "Test Session", UserId = userId, Date = DateOnly.FromDateTime(DateTime.Now) };
        context.TastingSessions.Add(session);
        await context.SaveChangesAsync();

        var pageModel = new WizardModel(context)
        {
            SelectedBottleId = bottle.Id,
            PourAmountOz = 1.5,
            NewNote = new TastingNote { Notes = "Tastes great!" }
        };
        SetMockUser(pageModel, userId);

        // ACT
        var result = await pageModel.OnPostAsync(session.Id);

        // ASSERT
        Assert.IsType<RedirectToPageResult>(result);
        
        var updatedBottle = await context.Bottles.FindAsync(bottle.Id);
        // 1.5 oz * 29.5735 = 44.36025 -> rounded to 44
        Assert.Equal(750 - 44, updatedBottle!.CurrentVolumeMl);
        Assert.Equal(BottleStatus.Opened, updatedBottle.Status);

        var note = await context.TastingNotes.FirstAsync();
        Assert.Equal(44, note.PourAmountMl);
    }

    [Fact]
    public async Task OnPost_Succeeds_WhenNotesAreEmpty()
    {
        // ARRANGE
        using var context = GetInMemoryContext();
        var userId = "test-user";
        var whiskey = new Whiskey { Name = "Test Whiskey", Distillery = "Test Distillery" };
        context.Whiskies.Add(whiskey);
        await context.SaveChangesAsync();

        var collection = new Collection { Name = "Test Collection" };
        context.Collections.Add(collection);
        await context.SaveChangesAsync();

        context.CollectionMembers.Add(new CollectionMember { CollectionId = collection.Id, UserId = userId, Role = CollectionRole.Owner });

        var bottle = new Bottle
        {
            WhiskeyId = whiskey.Id,
            CollectionId = collection.Id,
            UserId = userId,
            CurrentVolumeMl = 750,
            Status = BottleStatus.Full
        };
        context.Bottles.Add(bottle);
        await context.SaveChangesAsync();

        var session = new TastingSession { Title = "Test Session", UserId = userId, Date = DateOnly.FromDateTime(DateTime.Now) };
        context.TastingSessions.Add(session);
        await context.SaveChangesAsync();

        var pageModel = new WizardModel(context)
        {
            SelectedBottleId = bottle.Id,
            PourAmountOz = 1.0,
            NewNote = new TastingNote { Notes = null } // No notes
        };
        SetMockUser(pageModel, userId);

        // ACT
        var result = await pageModel.OnPostAsync(session.Id);

        // ASSERT
        Assert.IsType<RedirectToPageResult>(result);
        var note = await context.TastingNotes.FirstAsync();
        Assert.Null(note.Notes);
    }

    [Fact]
    public async Task OnPost_Fails_WhenOzIsMissing()
    {
        // ARRANGE
        using var context = GetInMemoryContext();
        var userId = "test-user";
        var session = new TastingSession { Title = "Test Session", UserId = userId, Date = DateOnly.FromDateTime(DateTime.Now) };
        context.TastingSessions.Add(session);
        await context.SaveChangesAsync();

        var pageModel = new WizardModel(context)
        {
            PourAmountOz = null, // Missing
            NewNote = new TastingNote { Notes = "Missing pour" }
        };
        SetMockUser(pageModel, userId);

        // ACT
        // Manually trigger validation since it doesn't run automatically in unit tests
        var validationContext = new ValidationContext(pageModel, null, null);
        var validationResults = new List<ValidationResult>();
        Validator.TryValidateObject(pageModel, validationContext, validationResults, true);
        foreach (var error in validationResults)
        {
            foreach (var memberName in error.MemberNames)
            {
                pageModel.ModelState.AddModelError(memberName, error.ErrorMessage ?? "Error");
            }
        }

        var result = await pageModel.OnPostAsync(session.Id);

        // ASSERT
        Assert.IsType<PageResult>(result);
        Assert.False(pageModel.ModelState.IsValid);
    }

    [Fact]
    public async Task OnPost_EditMode_UpdatesNoteWithoutChangingInventory()
    {
        // ARRANGE
        using var context = GetInMemoryContext();
        var userId = "test-user";
        var whiskey = new Whiskey { Name = "Original", Distillery = "Distillery A" };
        var newWhiskey = new Whiskey { Name = "Updated", Distillery = "Distillery B" };
        context.Whiskies.AddRange(whiskey, newWhiskey);
        await context.SaveChangesAsync();

        var collection = new Collection { Name = "Test Collection" };
        context.Collections.Add(collection);
        await context.SaveChangesAsync();

        context.CollectionMembers.Add(new CollectionMember { CollectionId = collection.Id, UserId = userId, Role = CollectionRole.Owner });

        var bottle = new Bottle { WhiskeyId = whiskey.Id, CollectionId = collection.Id, UserId = userId, CurrentVolumeMl = 700, Status = BottleStatus.Opened };
        context.Bottles.Add(bottle);
        await context.SaveChangesAsync();

        var session = new TastingSession { Title = "Test Session", UserId = userId, Date = DateOnly.FromDateTime(DateTime.Now) };
        context.TastingSessions.Add(session);
        await context.SaveChangesAsync();

        var existingNote = new TastingNote { WhiskeyId = whiskey.Id, BottleId = bottle.Id, TastingSessionId = session.Id, Rating = 2, Notes = "Old notes", OrderIndex = 1, UserId = userId, PourAmountMl = 44 };
        context.TastingNotes.Add(existingNote);
        await context.SaveChangesAsync();

        var pageModel = new WizardModel(context)
        {
            EditNoteId = existingNote.Id,
            SelectedBottleId = bottle.Id, // Same bottle, just changing pour amount
            PourAmountOz = 2.0, // Changed from ~1.5oz (44ml) to 2oz (59ml)
            NewNote = new TastingNote { Rating = 5, Notes = "Updated notes" }
        };
        SetMockUser(pageModel, userId);

        // ACT
        var result = await pageModel.OnPostAsync(session.Id);

        // ASSERT
        Assert.IsType<RedirectToPageResult>(result);

        var updated = await context.TastingNotes.FindAsync(existingNote.Id);
        Assert.Equal(whiskey.Id, updated!.WhiskeyId); // whiskey derived from bottle, unchanged
        Assert.Equal(5, updated.Rating);
        Assert.Equal("Updated notes", updated.Notes);
        // Pour amount updated to 2oz = 59ml
        Assert.Equal(59, updated.PourAmountMl);
        // Bottle volume adjusted by the difference (59 - 44 = 15ml more deducted)
        var updatedBottle = await context.Bottles.FindAsync(bottle.Id);
        Assert.Equal(700 - 15, updatedBottle!.CurrentVolumeMl);
        // OrderIndex must be preserved
        Assert.Equal(1, updated.OrderIndex);
    }

    [Fact]
    public async Task OnPost_EditMode_ReturnsNotFound_WhenNoteNotInSession()
    {
        // ARRANGE
        using var context = GetInMemoryContext();
        var userId = "test-user";
        var whiskey = new Whiskey { Name = "Test Whiskey", Distillery = "Test Distillery" };
        context.Whiskies.Add(whiskey);
        await context.SaveChangesAsync();

        var session = new TastingSession { Title = "My Session", UserId = userId, Date = DateOnly.FromDateTime(DateTime.Now) };
        var otherSession = new TastingSession { Title = "Other Session", UserId = "other-user", Date = DateOnly.FromDateTime(DateTime.Now) };
        context.TastingSessions.AddRange(session, otherSession);
        await context.SaveChangesAsync();

        // Note belongs to a different session
        var noteInOtherSession = new TastingNote { WhiskeyId = whiskey.Id, TastingSessionId = otherSession.Id, Rating = 3, OrderIndex = 1, UserId = "other-user" };
        context.TastingNotes.Add(noteInOtherSession);
        await context.SaveChangesAsync();

        var pageModel = new WizardModel(context)
        {
            EditNoteId = noteInOtherSession.Id,
            SelectedWhiskeyId = whiskey.Id,
            NewNote = new TastingNote { Rating = 5, Notes = "Hacked" }
        };
        SetMockUser(pageModel, userId);

        // ACT
        var result = await pageModel.OnPostAsync(session.Id);

        // ASSERT
        Assert.IsType<NotFoundResult>(result);
    }
}