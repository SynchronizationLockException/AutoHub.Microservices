using BuildingBlocks.Hosting;
using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;

namespace RentalService.Api.Services;

public sealed record CatalogReservationDto(
    Guid ReservationId,
    Guid CarId,
    string Purpose,
    string Status,
    string HolderReference,
    DateTime ExpiresAtUtc);

public sealed class CatalogReservationClient(IHttpClientFactory factory, IConfiguration configuration)
{
    public async Task<(CatalogReservationDto? Reservation, string? Error)> ReserveRentAsync(
        Guid carId,
        string holderReference,
        CancellationToken ct)
    {
        var client = factory.CreateClient("catalog");
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/cars/{carId}/reservations");
        configuration.ApplyInternalSecret(request);
        request.Content = JsonContent.Create(new
        {
            purpose = "Rent",
            holderReference,
            ttlMinutes = 15
        });

        var response = await client.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            return (null, string.IsNullOrWhiteSpace(body) ? "Catalog reservation failed." : body);
        }

        var reservation = await response.Content.ReadFromJsonAsync<CatalogReservationDto>(ct);
        return reservation is null ? (null, "Catalog reservation response was empty.") : (reservation, null);
    }

    public async Task ReleaseReservationAsync(
        Guid carId,
        Guid reservationId,
        CancellationToken ct)
    {
        var client = factory.CreateClient("catalog");
        using var request = new HttpRequestMessage(
            HttpMethod.Delete,
            $"/api/internal/cars/{carId}/reservations/{reservationId}");
        configuration.ApplyInternalSecret(request);
        await client.SendAsync(request, ct);
    }

    public async Task<CatalogCar?> GetCarAsync(Guid carId, CancellationToken ct)
    {
        var client = factory.CreateClient("catalog");
        return await client.GetFromJsonAsync<CatalogCar>($"/api/cars/{carId}", ct);
    }
}

public sealed record CatalogCar(Guid Id, decimal PricePerDay, bool IsAvailableForRent);
