using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace SalesService.Api.Services;

public sealed record CatalogReservationDto(
    Guid ReservationId,
    Guid CarId,
    string Purpose,
    string Status,
    string HolderReference,
    DateTime ExpiresAtUtc);

public sealed class CatalogReservationClient(IHttpClientFactory factory)
{
    public async Task<(CatalogReservationDto? Reservation, string? Error)> ReserveSaleAsync(
        Guid carId,
        string holderReference,
        string? bearerToken,
        CancellationToken ct)
    {
        var client = factory.CreateClient("catalog");
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/cars/{carId}/reservations");
        ApplyBearer(request, bearerToken);
        request.Content = JsonContent.Create(new { purpose = "Sale", holderReference, ttlMinutes = 15 });

        var response = await client.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            return (null, string.IsNullOrWhiteSpace(body) ? "Catalog reservation failed." : body);
        }

        var reservation = await response.Content.ReadFromJsonAsync<CatalogReservationDto>(ct);
        return reservation is null ? (null, "Catalog reservation response was empty.") : (reservation, null);
    }

    public async Task ReleaseReservationAsync(Guid carId, Guid reservationId, string? bearerToken, CancellationToken ct)
    {
        var client = factory.CreateClient("catalog");
        using var request = new HttpRequestMessage(HttpMethod.Delete, $"/api/cars/{carId}/reservations/{reservationId}");
        ApplyBearer(request, bearerToken);
        await client.SendAsync(request, ct);
    }

    public async Task<CatalogCar?> GetCarAsync(Guid carId, CancellationToken ct)
    {
        var client = factory.CreateClient("catalog");
        return await client.GetFromJsonAsync<CatalogCar>($"/api/cars/{carId}", ct);
    }

    private static void ApplyBearer(HttpRequestMessage request, string? bearerToken)
    {
        if (!string.IsNullOrWhiteSpace(bearerToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
        }
    }
}

public sealed record CatalogCar(Guid Id, decimal SalePrice, bool IsAvailableForSale);
