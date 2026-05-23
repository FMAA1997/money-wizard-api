namespace Domain.Abstractions.Clients;

// argentinadatos exposes FCI rows where `fondo` already contains the fund name and class
// merged as "Fund Name - Clase X" — so a single FundName field is enough; no separate Class.
public sealed record FciQuote(
    string Ticker,
    string FundName,
    string Category,
    decimal CuotaParte,
    string Currency,
    DateOnly AsOf);
