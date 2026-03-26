using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Moq;
using WhiskeyTracker.Web.Data;
using Microsoft.AspNetCore.SignalR;
using WhiskeyTracker.Web.Hubs;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace WhiskeyTracker.Tests;

public class TestBase
{
    protected AppDbContext GetInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    protected UserManager<ApplicationUser> GetMockUserManager(AppDbContext? context = null)
    {
        var store = new Mock<IUserStore<ApplicationUser>>();
        var mockUserManager = new Mock<UserManager<ApplicationUser>>(
            store.Object, null!, null!, null!, null!, null!, null!, null!, null!);
        
        // Setup GetUserId to return the user ID from claims
        mockUserManager.Setup(um => um.GetUserId(It.IsAny<System.Security.Claims.ClaimsPrincipal>()))
            .Returns<System.Security.Claims.ClaimsPrincipal>(principal => 
                principal.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value);
        
        // Setup Users property to return the context's Users DbSet if context is provided
        if (context != null)
        {
             mockUserManager.Setup(um => um.Users).Returns(context.Users);
        }
        
        return mockUserManager.Object;
    }

    protected void SetMockUser(PageModel page, string userId)
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

        // Mock TempData by default
        SetMockTempData(page);
    }

    protected void SetMockTempData(PageModel page)
    {
        var mockTempData = new Mock<ITempDataDictionary>();
        page.TempData = mockTempData.Object;
    }

    protected Mock<IHubContext<TastingHub>> GetMockHubContext()
    {
        var mockHubContext = new Mock<IHubContext<TastingHub>>();
        var mockClients = new Mock<IHubClients>();
        var mockClientProxy = new Mock<IClientProxy>();

        mockHubContext.Setup(h => h.Clients).Returns(mockClients.Object);
        mockClients.Setup(c => c.Group(It.IsAny<string>())).Returns(mockClientProxy.Object);
        
        return mockHubContext;
    }
}
