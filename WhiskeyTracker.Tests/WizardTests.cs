using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WhiskeyTracker.Web.Data;
using WhiskeyTracker.Web.Pages.Tasting;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using Moq;
using WhiskeyTracker.Web.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using WhiskeyTracker.Web.Services;

namespace WhiskeyTracker.Tests;

public class WizardTests : TestBase
{

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

        var hubMock = GetMockHubContext();
        var service = new TastingSessionService(context, hubMock.Object);
        var pageModel = new WizardModel(context, hubMock.Object, service)
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

        var hubMock = GetMockHubContext();
        var service = new TastingSessionService(context, hubMock.Object);
        var pageModel = new WizardModel(context, hubMock.Object, service)
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

        var hubMock = GetMockHubContext();
        var service = new TastingSessionService(context, hubMock.Object);
        var pageModel = new WizardModel(context, hubMock.Object, service)
        {
            PourAmountOz = null, // Missing
            NewNote = new TastingNote { Notes = "Missing pour" }
        };
        SetMockUser(pageModel, userId);

        // ACT
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

        var hubMock = GetMockHubContext();
        var service = new TastingSessionService(context, hubMock.Object);
        var pageModel = new WizardModel(context, hubMock.Object, service)
        {
            EditNoteId = existingNote.Id,
            SelectedBottleId = bottle.Id,
            PourAmountOz = 2.0,
            NewNote = new TastingNote { Rating = 5, Notes = "Updated notes" }
        };
        SetMockUser(pageModel, userId);

        // ACT
        var result = await pageModel.OnPostAsync(session.Id);

        // ASSERT
        Assert.IsType<RedirectToPageResult>(result);

        var updated = await context.TastingNotes.FindAsync(existingNote.Id);
        Assert.Equal(5, updated!.Rating);
        Assert.Equal("Updated notes", updated.Notes);
        Assert.Equal(59, updated.PourAmountMl);
        
        var updatedBottle = await context.Bottles.FindAsync(bottle.Id);
        Assert.Equal(700 - 15, updatedBottle!.CurrentVolumeMl);
    }

    [Fact]
    public async Task OnPost_SavesTagsCorrectly()
    {
        // ARRANGE
        using var context = GetInMemoryContext();
        var userId = "test-user";
        var whiskey = new Whiskey { Name = "Tag Test", Distillery = "Distillery" };
        context.Whiskies.Add(whiskey);
        await context.SaveChangesAsync();

        var session = new TastingSession { Title = "Session", UserId = userId, Date = DateOnly.FromDateTime(DateTime.Now) };
        context.TastingSessions.Add(session);
        await context.SaveChangesAsync();

        var hubMock = GetMockHubContext();
        var service = new TastingSessionService(context, hubMock.Object);
        var pageModel = new WizardModel(context, hubMock.Object, service)
        {
            SelectedWhiskeyId = whiskey.Id,
            PourAmountOz = 1.0,
            NewNote = new TastingNote { Rating = 4, Notes = "With tags" },
            NoseTags = "Vanilla, Oak",
            PalateTags = "Caramel, Spice",
            FinishTags = "Long"
        };
        SetMockUser(pageModel, userId);

        // ACT
        await pageModel.OnPostAsync(session.Id);

        // ASSERT
        var note = await context.TastingNotes
            .Include(n => n.TastingNoteTags)
            .ThenInclude(tnt => tnt.Tag)
            .FirstAsync();

        Assert.Equal(5, note.TastingNoteTags.Count); 
        
        var noseTags = note.TastingNoteTags.Where(t => t.Field == TastingField.Nose).Select(t => t.Tag.Name).ToList();
        Assert.Contains("vanilla", noseTags);
        Assert.Contains("oak", noseTags);

        var palateTags = note.TastingNoteTags.Where(t => t.Field == TastingField.Palate).Select(t => t.Tag.Name).ToList();
        Assert.Contains("caramel", palateTags);
        Assert.Contains("spice", palateTags);

        var finishTags = note.TastingNoteTags.Where(t => t.Field == TastingField.Finish).Select(t => t.Tag.Name).ToList();
        Assert.Contains("long", finishTags);
    }
}