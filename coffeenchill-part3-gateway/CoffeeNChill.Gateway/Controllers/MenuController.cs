using CoffeeNChill.Gateway.Auth;
using CoffeeNChill.Gateway.DTOs;
using CoffeeNChill.Gateway.Exceptions;
using CoffeeNChill.Gateway.Mapping;
using CoffeeNChill.Gateway.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoffeeNChill.Gateway.Controllers;

/// <summary>Menu management. Reading the menu is public; changing it needs an Admin or Staff token.</summary>
[ApiController]
[Route("api/menu")]
[Produces("application/json")]
public class MenuController : ControllerBase
{
    private readonly IMenuService _menu;

    public MenuController(IMenuService menu) => _menu = menu;

    /// <summary>Gets every menu item.</summary>
    /// <response code="200">The full menu.</response>
    /// <response code="503">Storage is unreachable.</response>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(IEnumerable<MenuItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<IEnumerable<MenuItemDto>>> GetAll(CancellationToken ct)
    {
        var items = await _menu.GetAllAsync(ct);
        return Ok(items.Select(i => i.ToDto()));
    }

    /// <summary>Gets all menu items in one category.</summary>
    /// <param name="category">Category name, e.g. "Hot Drinks".</param>
    /// <response code="200">Items in the category (empty list if none).</response>
    [HttpGet("category/{category}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(IEnumerable<MenuItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<IEnumerable<MenuItemDto>>> GetByCategory(string category, CancellationToken ct)
    {
        var items = await _menu.GetByCategoryAsync(category, ct);
        return Ok(items.Select(i => i.ToDto()));
    }

    /// <summary>Gets a single menu item.</summary>
    /// <param name="category">Category (partition key).</param>
    /// <param name="id">Item SKU (row key).</param>
    /// <response code="200">The item.</response>
    /// <response code="404">No such item.</response>
    [HttpGet("{category}/{id}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(MenuItemDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MenuItemDto>> GetMenuItem(string category, string id, CancellationToken ct)
    {
        var item = await _menu.GetAsync(category, id, ct)
                   ?? throw new NotFoundException($"Menu item '{id}' was not found in '{category}'.");
        return Ok(item.ToDto());
    }

    /// <summary>Creates a menu item (Admin or Staff).</summary>
    /// <response code="201">Item created; the Location header points to it.</response>
    /// <response code="400">Validation failed.</response>
    /// <response code="401">Missing or invalid token.</response>
    /// <response code="403">Token is valid but the role is not allowed.</response>
    /// <response code="409">An item with this category and SKU already exists.</response>
    [HttpPost]
    [Authorize(Roles = AppRoles.AdminOrStaff)]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(MenuItemDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<MenuItemDto>> Create([FromBody] CreateMenuItemDto request, CancellationToken ct)
    {
        var created = await _menu.CreateAsync(request.ToEntity(), ct);
        var dto = created.ToDto();
        return CreatedAtAction(nameof(GetMenuItem), new { category = dto.Category, id = dto.Sku }, dto);
    }

    /// <summary>Updates a menu item's name, description, price or availability (Admin or Staff).</summary>
    /// <param name="category">Category (partition key).</param>
    /// <param name="id">Item SKU (row key).</param>
    /// <response code="200">The updated item.</response>
    /// <response code="400">Validation failed.</response>
    /// <response code="401">Missing or invalid token.</response>
    /// <response code="403">Token is valid but the role is not allowed.</response>
    /// <response code="404">No such item.</response>
    [HttpPut("{category}/{id}")]
    [Authorize(Roles = AppRoles.AdminOrStaff)]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(MenuItemDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MenuItemDto>> Update(
        string category, string id, [FromBody] UpdateMenuItemDto request, CancellationToken ct)
    {
        var updated = await _menu.UpdateAsync(category, id, request, ct)
                      ?? throw new NotFoundException($"Menu item '{id}' was not found in '{category}'.");
        return Ok(updated.ToDto());
    }

    /// <summary>Deletes a menu item (Admin or Staff).</summary>
    /// <param name="category">Category (partition key).</param>
    /// <param name="id">Item SKU (row key).</param>
    /// <response code="204">Item deleted.</response>
    /// <response code="401">Missing or invalid token.</response>
    /// <response code="403">Token is valid but the role is not allowed.</response>
    /// <response code="404">No such item.</response>
    [HttpDelete("{category}/{id}")]
    [Authorize(Roles = AppRoles.AdminOrStaff)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(string category, string id, CancellationToken ct)
    {
        if (!await _menu.DeleteAsync(category, id, ct))
            throw new NotFoundException($"Menu item '{id}' was not found in '{category}'.");

        return NoContent();
    }
}
