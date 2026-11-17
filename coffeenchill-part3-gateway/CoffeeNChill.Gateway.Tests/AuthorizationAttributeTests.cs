using System.Reflection;
using CoffeeNChill.Gateway.Controllers;
using Microsoft.AspNetCore.Authorization;
using Xunit;

namespace CoffeeNChill.Gateway.Tests;

/// <summary>
/// Guards the access rules from the brief: public menu/order placement, staff-only changes.
/// These test the attributes themselves, so they keep passing once the real JWT auth is merged.
/// </summary>
public class AuthorizationAttributeTests
{
    private static MethodInfo Action(Type controller, string name) =>
        controller.GetMethod(name) ?? throw new InvalidOperationException($"{controller.Name}.{name} not found");

    [Theory]
    [InlineData(typeof(MenuController), nameof(MenuController.GetAll))]
    [InlineData(typeof(MenuController), nameof(MenuController.GetByCategory))]
    [InlineData(typeof(OrdersController), nameof(OrdersController.QueueOrder))]
    public void PublicEndpoints_AreAllowAnonymous(Type controller, string action)
        => Assert.NotNull(Action(controller, action).GetCustomAttribute<AllowAnonymousAttribute>());

    [Theory]
    [InlineData(typeof(MenuController), nameof(MenuController.Create))]
    [InlineData(typeof(MenuController), nameof(MenuController.Update))]
    [InlineData(typeof(MenuController), nameof(MenuController.Delete))]
    [InlineData(typeof(OrdersController), nameof(OrdersController.UpdateStatus))]
    public void StaffEndpoints_RequireAdminOrStaffRole(Type controller, string action)
    {
        var attr = Action(controller, action).GetCustomAttribute<AuthorizeAttribute>();

        Assert.NotNull(attr);
        Assert.Equal("Admin,Staff", attr!.Roles);
    }

    [Fact]
    public void DocumentsController_RequiresAdminOrStaffRole_OnEveryEndpoint()
    {
        var attr = typeof(DocumentsController).GetCustomAttribute<AuthorizeAttribute>();

        Assert.NotNull(attr);
        Assert.Equal("Admin,Staff", attr!.Roles);
    }
}
