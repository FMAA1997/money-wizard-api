using ErrorOr;

namespace Domain.Errors;

public static class InvestmentErrors
{
    public static readonly Error NotFound = Error.NotFound(
        "Investment.NotFound", "Investment not found.");

    public static readonly Error InvalidQuantity = Error.Validation(
        "Investment.InvalidQuantity", "Quantity must be greater than zero.");

    public static readonly Error InvalidAssetClass = Error.Validation(
        "Investment.InvalidAssetClass", "Unsupported asset class.");

    public static readonly Error UnknownTicker = Error.Validation(
        "Investment.UnknownTicker", "Ticker not found for the selected asset class.");
}
