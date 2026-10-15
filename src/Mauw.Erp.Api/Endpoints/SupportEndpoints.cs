using Mauw.Erp.Application.Mappers;
using Mauw.Erp.Contracts.Dtos;
using Mauw.Erp.Contracts.Requests;
using Mauw.Erp.Domain.Entities;
using Mauw.Erp.Domain.Repositories;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Mauw.Erp.Api.Endpoints;

/// <summary>
/// API endpoints for support entities (Color, Size, Category, Collection, Material, MacroCategory).
/// </summary>
public static class SupportEndpoints
{
    public static void MapSupportEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/support")
            .WithTags("Support");

        // Colors endpoints
        group.MapGet("/colors", GetColors)
            .WithName("GetColors")
            .WithSummary("Get all colors");

        group.MapGet("/colors/{id}", GetColor)
            .WithName("GetColor")
            .WithSummary("Get color by ID");

        group.MapPost("/colors", CreateColor)
            .WithName("CreateColor")
            .WithSummary("Create a new color");

        group.MapPut("/colors/{id}", UpdateColor)
            .WithName("UpdateColor")
            .WithSummary("Update a color");

        group.MapDelete("/colors/{id}", DeleteColor)
            .WithName("DeleteColor")
            .WithSummary("Delete a color");

        // Sizes endpoints
        group.MapGet("/sizes", GetSizes)
            .WithName("GetSizes")
            .WithSummary("Get all sizes");

        group.MapGet("/sizes/{id}", GetSize)
            .WithName("GetSize")
            .WithSummary("Get size by ID");

        group.MapPost("/sizes", CreateSize)
            .WithName("CreateSize")
            .WithSummary("Create a new size");

        group.MapPut("/sizes/{id}", UpdateSize)
            .WithName("UpdateSize")
            .WithSummary("Update a size");

        group.MapDelete("/sizes/{id}", DeleteSize)
            .WithName("DeleteSize")
            .WithSummary("Delete a size");

        // Categories endpoints
        group.MapGet("/categories", GetCategories)
            .WithName("GetCategories")
            .WithSummary("Get all categories");

        group.MapGet("/categories/{id:guid}", GetCategory)
            .WithName("GetCategory")
            .WithSummary("Get category by ID");

        group.MapPost("/categories", CreateCategory)
            .WithName("CreateCategory")
            .WithSummary("Create a new category");

        group.MapPut("/categories/{id:guid}", UpdateCategory)
            .WithName("UpdateCategory")
            .WithSummary("Update a category");

        group.MapDelete("/categories/{id:guid}", DeleteCategory)
            .WithName("DeleteCategory")
            .WithSummary("Delete a category");

        // Collections endpoints
        group.MapGet("/collections", GetCollections)
            .WithName("GetCollections")
            .WithSummary("Get all collections");

        group.MapGet("/collections/{id:guid}", GetCollection)
            .WithName("GetCollection")
            .WithSummary("Get collection by ID");

        group.MapPost("/collections", CreateCollection)
            .WithName("CreateCollection")
            .WithSummary("Create a new collection");

        group.MapPut("/collections/{id:guid}", UpdateCollection)
            .WithName("UpdateCollection")
            .WithSummary("Update a collection");

        group.MapDelete("/collections/{id:guid}", DeleteCollection)
            .WithName("DeleteCollection")
            .WithSummary("Delete a collection");

        // Materials endpoints
        group.MapGet("/materials", GetMaterials)
            .WithName("GetMaterials")
            .WithSummary("Get all materials with composition");

        group.MapGet("/materials/{id:guid}", GetMaterial)
            .WithName("GetMaterial")
            .WithSummary("Get material by ID with composition");

        group.MapPost("/materials", CreateMaterial)
            .WithName("CreateMaterial")
            .WithSummary("Create a new material");

        group.MapPut("/materials/{id:guid}", UpdateMaterial)
            .WithName("UpdateMaterial")
            .WithSummary("Update a material");

        group.MapDelete("/materials/{id:guid}", DeleteMaterial)
            .WithName("DeleteMaterial")
            .WithSummary("Delete a material");

        // MacroCategories endpoints
        group.MapGet("/macro-categories", GetMacroCategories)
            .WithName("GetMacroCategories")
            .WithSummary("Get all macro categories");

        group.MapGet("/macro-categories/{id:guid}", GetMacroCategory)
            .WithName("GetMacroCategory")
            .WithSummary("Get macro category by ID");

        group.MapPost("/macro-categories", CreateMacroCategory)
            .WithName("CreateMacroCategory")
            .WithSummary("Create a new macro category");

        group.MapPut("/macro-categories/{id:guid}", UpdateMacroCategory)
            .WithName("UpdateMacroCategory")
            .WithSummary("Update a macro category");

        group.MapDelete("/macro-categories/{id:guid}", DeleteMacroCategory)
            .WithName("DeleteMacroCategory")
            .WithSummary("Delete a macro category");
    }

    // Color handlers
    private static async Task<Ok<List<ColorDto>>> GetColors(IUnitOfWork uow)
    {
        var colors = await uow.Colors.GetAllAsync();
        return TypedResults.Ok(colors.Select(SupportMapper.MapToDto).ToList());
    }

    private static async Task<Results<Ok<ColorDto>, NotFound>> GetColor(string id, IUnitOfWork uow)
    {
        var color = await uow.Colors.GetByIdAsync(id);
        return color == null ? TypedResults.NotFound() : TypedResults.Ok(SupportMapper.MapToDto(color));
    }

    private static async Task<Created<ColorDto>> CreateColor(CreateColorRequest request, IUnitOfWork uow)
    {
        var generatedId = GenerateColorId(request.Name);
        var color = new Color(generatedId, request.Name, request.HtmlColor);
        await uow.Colors.AddAsync(color);
        await uow.SaveChangesAsync();
        return TypedResults.Created($"/api/v1/support/colors/{color.Id}", SupportMapper.MapToDto(color));
    }

    private static async Task<Results<Ok<ColorDto>, NotFound>> UpdateColor(
        string id,
        UpdateColorRequest request,
        IUnitOfWork uow)
    {
        var color = await uow.Colors.GetByIdAsync(id);
        if (color == null) return TypedResults.NotFound();

        color.Update(request.Name, request.HtmlColor);
        color.IsVisible = request.IsVisible;
        await uow.SaveChangesAsync();
        return TypedResults.Ok(SupportMapper.MapToDto(color));
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> DeleteColor(string id, IUnitOfWork uow)
    {
        var color = await uow.Colors.GetByIdAsync(id);
        if (color == null) return TypedResults.NoContent();

        var variantsWithColor = await uow.ProductVariants.GetAllAsync();
        if (variantsWithColor.Any(pv => pv.ColorId == id))
        {
            return TypedResults.Problem(
                detail: "Impossibile eliminare: il colore è utilizzato in una o più varianti prodotto.",
                statusCode: StatusCodes.Status409Conflict);
        }

        await uow.Colors.DeleteAsync(color);
        await uow.SaveChangesAsync();
        return TypedResults.NoContent();
    }

    // Size handlers
    private static async Task<Ok<List<SizeDto>>> GetSizes(IUnitOfWork uow)
    {
        var sizes = await uow.Sizes.GetAllAsync();
        return TypedResults.Ok(sizes.Select(SupportMapper.MapToDto).ToList());
    }

    private static async Task<Results<Ok<SizeDto>, NotFound>> GetSize(string id, IUnitOfWork uow)
    {
        var size = await uow.Sizes.GetByIdAsync(id);
        return size == null ? TypedResults.NotFound() : TypedResults.Ok(SupportMapper.MapToDto(size));
    }

    private static async Task<Created<SizeDto>> CreateSize(CreateSizeRequest request, IUnitOfWork uow)
    {
        var generatedId = GenerateSizeId(request.Name);
        var size = new Size(generatedId, request.Name);
        await uow.Sizes.AddAsync(size);
        await uow.SaveChangesAsync();
        return TypedResults.Created($"/api/v1/support/sizes/{size.Id}", SupportMapper.MapToDto(size));
    }

    private static async Task<Results<Ok<SizeDto>, NotFound>> UpdateSize(
        string id,
        UpdateSizeRequest request,
        IUnitOfWork uow)
    {
        var size = await uow.Sizes.GetByIdAsync(id);
        if (size == null) return TypedResults.NotFound();

        size.Update(request.Name);
        size.IsVisible = request.IsVisible;
        await uow.SaveChangesAsync();
        return TypedResults.Ok(SupportMapper.MapToDto(size));
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> DeleteSize(string id, IUnitOfWork uow)
    {
        var size = await uow.Sizes.GetByIdAsync(id);
        if (size == null) return TypedResults.NoContent();

        var variantsWithSize = await uow.ProductVariants.GetAllAsync();
        if (variantsWithSize.Any(pv => pv.SizeId == id))
        {
            return TypedResults.Problem(
                detail: "Impossibile eliminare: la taglia è utilizzata in una o più varianti prodotto.",
                statusCode: StatusCodes.Status409Conflict);
        }

        await uow.Sizes.DeleteAsync(size);
        await uow.SaveChangesAsync();
        return TypedResults.NoContent();
    }

    // Category handlers
    private static async Task<Ok<List<CategoryDto>>> GetCategories(IUnitOfWork uow)
    {
        var categories = await uow.Categories.GetAllAsync();
        return TypedResults.Ok(categories.Select(SupportMapper.MapToDto).ToList());
    }

    private static async Task<Results<Ok<CategoryDto>, NotFound>> GetCategory(Guid id, IUnitOfWork uow)
    {
        var category = await uow.Categories.GetByIdAsync(id);
        return category == null ? TypedResults.NotFound() : TypedResults.Ok(SupportMapper.MapToDto(category));
    }

    private static async Task<Created<CategoryDto>> CreateCategory(CreateCategoryRequest request, IUnitOfWork uow)
    {
        var category = new Category(request.Name, request.MacroCategoryId);
        await uow.Categories.AddAsync(category);
        await uow.SaveChangesAsync();
        return TypedResults.Created($"/api/v1/support/categories/{category.Id}", SupportMapper.MapToDto(category));
    }

    private static async Task<Results<Ok<CategoryDto>, NotFound>> UpdateCategory(
        Guid id,
        UpdateCategoryRequest request,
        IUnitOfWork uow)
    {
        var category = await uow.Categories.GetByIdAsync(id);
        if (category == null) return TypedResults.NotFound();

        category.Update(request.Name, request.MacroCategoryId);
        category.IsVisible = request.IsVisible;
        await uow.SaveChangesAsync();
        return TypedResults.Ok(SupportMapper.MapToDto(category));
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> DeleteCategory(Guid id, IUnitOfWork uow)
    {
        var category = await uow.Categories.GetByIdAsync(id);
        if (category == null) return TypedResults.NoContent();

        var productsWithCategory = await uow.Products.GetAllAsync();
        if (productsWithCategory.Any(p => p.CategoryId == id))
        {
            return TypedResults.Problem(
                detail: "Impossibile eliminare: la categoria è utilizzata in uno o più prodotti.",
                statusCode: StatusCodes.Status409Conflict);
        }

        await uow.Categories.DeleteAsync(category);
        await uow.SaveChangesAsync();
        return TypedResults.NoContent();
    }

    // Collection handlers
    private static async Task<Ok<List<CollectionDto>>> GetCollections(IUnitOfWork uow)
    {
        var collections = await uow.Collections.GetAllAsync();
        return TypedResults.Ok(collections.Select(SupportMapper.MapToDto).ToList());
    }

    private static async Task<Results<Ok<CollectionDto>, NotFound>> GetCollection(Guid id, IUnitOfWork uow)
    {
        var collection = await uow.Collections.GetByIdAsync(id);
        return collection == null ? TypedResults.NotFound() : TypedResults.Ok(SupportMapper.MapToDto(collection));
    }

    private static async Task<Created<CollectionDto>> CreateCollection(CreateCollectionRequest request, IUnitOfWork uow)
    {
        var collection = new Collection(request.Name, request.Description);
        await uow.Collections.AddAsync(collection);
        await uow.SaveChangesAsync();
        return TypedResults.Created($"/api/v1/support/collections/{collection.Id}", SupportMapper.MapToDto(collection));
    }

    private static async Task<Results<Ok<CollectionDto>, NotFound>> UpdateCollection(
        Guid id,
        UpdateCollectionRequest request,
        IUnitOfWork uow)
    {
        var collection = await uow.Collections.GetByIdAsync(id);
        if (collection == null) return TypedResults.NotFound();

        collection.Update(request.Name, request.Description);
        await uow.SaveChangesAsync();
        return TypedResults.Ok(SupportMapper.MapToDto(collection));
    }

    private static async Task<Results<NoContent, NotFound>> DeleteCollection(Guid id, IUnitOfWork uow)
    {
        var collection = await uow.Collections.GetByIdAsync(id);
        if (collection == null) return TypedResults.NotFound();

        await uow.Collections.DeleteAsync(collection);
        await uow.SaveChangesAsync();
        return TypedResults.NoContent();
    }

    // Material handlers
    private static async Task<Ok<List<MaterialDto>>> GetMaterials(IUnitOfWork uow)
    {
        var materials = await uow.Materials.GetAllAsync();
        return TypedResults.Ok(materials.Select(SupportMapper.MapToDto).ToList());
    }

    private static async Task<Results<Ok<MaterialDto>, NotFound>> GetMaterial(Guid id, IUnitOfWork uow)
    {
        var material = await uow.Materials.GetByIdAsync(id);
        return material == null ? TypedResults.NotFound() : TypedResults.Ok(SupportMapper.MapToDto(material));
    }

    private static async Task<Created<MaterialDto>> CreateMaterial(CreateMaterialRequest request, IUnitOfWork uow)
    {
        var material = new Material(request.Name);

        if (request.Composition != null)
        {
            foreach (var comp in request.Composition)
            {
                material.Composition.Add(new(
                    material.Id,
                    comp.ComponentName,
                    comp.PercentageValue
                ));
            }
        }

        await uow.Materials.AddAsync(material);
        await uow.SaveChangesAsync();
        return TypedResults.Created($"/api/v1/support/materials/{material.Id}", SupportMapper.MapToDto(material));
    }

    private static async Task<Results<Ok<MaterialDto>, NotFound>> UpdateMaterial(
        Guid id,
        UpdateMaterialRequest request,
        IUnitOfWork uow)
    {
        var material = await uow.Materials.GetByIdAsync(id);
        if (material == null) return TypedResults.NotFound();

        material.Update(request.Name);
        material.IsVisible = request.IsVisible;
        await uow.SaveChangesAsync();
        return TypedResults.Ok(SupportMapper.MapToDto(material));
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> DeleteMaterial(Guid id, IUnitOfWork uow)
    {
        var material = await uow.Materials.GetByIdAsync(id);
        if (material == null) return TypedResults.NoContent();

        var productsWithMaterial = await uow.Products.GetAllAsync();
        if (productsWithMaterial.Any(p => p.MaterialId == id))
        {
            return TypedResults.Problem(
                detail: "Impossibile eliminare: il materiale è utilizzato in uno o più prodotti.",
                statusCode: StatusCodes.Status409Conflict);
        }

        await uow.Materials.DeleteAsync(material);
        await uow.SaveChangesAsync();
        return TypedResults.NoContent();
    }

    // MacroCategory handlers
    private static async Task<Ok<List<MacroCategoryDto>>> GetMacroCategories(IUnitOfWork uow)
    {
        var macroCategories = await uow.MacroCategories.GetAllAsync();
        return TypedResults.Ok(macroCategories.Select(SupportMapper.MapToDto).ToList());
    }

    private static async Task<Results<Ok<MacroCategoryDto>, NotFound>> GetMacroCategory(Guid id, IUnitOfWork uow)
    {
        var macroCategory = await uow.MacroCategories.GetByIdAsync(id);
        return macroCategory == null
            ? TypedResults.NotFound()
            : TypedResults.Ok(SupportMapper.MapToDto(macroCategory));
    }

    private static async Task<Created<MacroCategoryDto>> CreateMacroCategory(
        CreateMacroCategoryRequest request,
        IUnitOfWork uow)
    {
        var macroCategory = new MacroCategory(request.Name);
        await uow.MacroCategories.AddAsync(macroCategory);
        await uow.SaveChangesAsync();
        return TypedResults.Created(
            $"/api/v1/support/macro-categories/{macroCategory.Id}",
            SupportMapper.MapToDto(macroCategory));
    }

    private static async Task<Results<Ok<MacroCategoryDto>, NotFound>> UpdateMacroCategory(
        Guid id,
        UpdateMacroCategoryRequest request,
        IUnitOfWork uow)
    {
        var macroCategory = await uow.MacroCategories.GetByIdAsync(id);
        if (macroCategory == null) return TypedResults.NotFound();

        macroCategory.Update(request.Name);
        macroCategory.IsVisible = request.IsVisible;
        await uow.SaveChangesAsync();
        return TypedResults.Ok(SupportMapper.MapToDto(macroCategory));
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> DeleteMacroCategory(Guid id, IUnitOfWork uow)
    {
        var macroCategory = await uow.MacroCategories.GetByIdAsync(id);
        if (macroCategory == null) return TypedResults.NoContent();

        var categoriesWithMacroCategory = await uow.Categories.GetAllAsync();
        if (categoriesWithMacroCategory.Any(c => c.MacroCategoryId == id))
        {
            return TypedResults.Problem(
                detail: "Impossibile eliminare: la macrocategoria è utilizzata in una o più categorie.",
                statusCode: StatusCodes.Status409Conflict);
        }

        await uow.MacroCategories.DeleteAsync(macroCategory);
        await uow.SaveChangesAsync();
        return TypedResults.NoContent();
    }

    private static string GenerateColorId(string name)
    {
        var normalized = name.ToUpper();
        return normalized.Length >= 3 ? normalized.Substring(0, 3) : normalized.PadRight(3, '_');
    }

    private static string GenerateSizeId(string name)
    {
        var normalized = name.ToUpper();
        return normalized.Length >= 5 ? normalized.Substring(0, 5) : normalized.PadRight(5, '_');
    }
}
