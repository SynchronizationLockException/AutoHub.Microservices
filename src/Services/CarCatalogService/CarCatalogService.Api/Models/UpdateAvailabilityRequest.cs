namespace CarCatalogService.Api.Models;

public sealed record UpdateAvailabilityRequest(
    bool IsAvailableForRent,
    bool IsAvailableForSale);
