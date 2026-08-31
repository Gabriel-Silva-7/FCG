using FCG.Application.Common;
using FCG.Domain.Catalog;

namespace FCG.Application.Catalog;

public sealed class CreatePromotionHandler(
    IGameRepository gameRepository,
    IPromotionRepository promotionRepository,
    IClock clock)
{
    public async Task<PromotionSummary?> HandleAsync(
        CreatePromotionCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var game = await gameRepository.FindActiveByIdAsync(
            command.GameId,
            cancellationToken);

        if (game is null)
        {
            return null;
        }

        Promotion promotion;

        try
        {
            promotion = Promotion.Create(
                command.GameId,
                command.DiscountPercentage,
                command.StartsAtUtc,
                command.EndsAtUtc,
                command.CreatedByUserId);
        }
        catch (ArgumentException exception) when (TryGetPublicInputName(exception, out var inputName))
        {
            throw new ApplicationValidationException(
                new Dictionary<string, string[]>(StringComparer.Ordinal)
                {
                    [inputName] = [exception.Message],
                });
        }

        promotionRepository.Add(promotion);
        await promotionRepository.SaveChangesAsync(cancellationToken);

        return new PromotionSummary(
            promotion.Id,
            promotion.GameId,
            promotion.DiscountPercentage,
            promotion.StartsAtUtc,
            promotion.EndsAtUtc,
            promotion.IsActiveAt(clock.UtcNow));
    }

    private static bool TryGetPublicInputName(ArgumentException exception, out string inputName)
    {
        inputName = exception.ParamName switch
        {
            "discountPercentage" => nameof(CreatePromotionCommand.DiscountPercentage),
            "startsAtUtc" => "StartsAt",
            "endsAtUtc" => "EndsAt",
            _ => string.Empty,
        };

        return inputName.Length > 0;
    }
}
