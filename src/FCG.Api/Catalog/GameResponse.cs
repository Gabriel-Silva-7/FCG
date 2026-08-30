namespace FCG.Api.Catalog;

public sealed record GameResponse(
    Guid Id,
    string Title,
    string? Description,
    decimal BasePrice,
    decimal CurrentPrice,
    decimal DiscountPercentage,
    bool IsActive);
