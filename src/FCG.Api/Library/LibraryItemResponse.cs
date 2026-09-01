namespace FCG.Api.Library;

public sealed record LibraryItemResponse(
    Guid GameId,
    string Title,
    DateTime AcquiredAt,
    decimal AcquisitionPrice);
