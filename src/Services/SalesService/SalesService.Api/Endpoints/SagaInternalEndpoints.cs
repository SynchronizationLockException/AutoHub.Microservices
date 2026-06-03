using BuildingBlocks.Hosting;
using SalesService.Api.Services;

namespace SalesService.Api.Endpoints;

public static class SagaInternalEndpoints
{
    public static void MapSagaInternalEndpoints(this WebApplication app)
    {
        app.MapPost("/api/internal/sagas/complete", async (
            CompleteSagaRequest request,
            HttpContext httpContext,
            IConfiguration configuration,
            SalesSagaService sagaService,
            CancellationToken ct) =>
        {
            if (!httpContext.IsValidInternalRequest(configuration))
            {
                return Results.Unauthorized();
            }

            if (!string.Equals(request.Kind, "sale", StringComparison.OrdinalIgnoreCase))
            {
                return Results.BadRequest("Unsupported saga kind.");
            }

            await sagaService.CompleteAsync(request.HolderReference, request.SaleId, ct);
            return Results.Ok();
        });

        app.MapPost("/api/internal/sagas/compensate", async (
            CompensateSagaRequest request,
            HttpContext httpContext,
            IConfiguration configuration,
            SalesSagaService sagaService,
            CancellationToken ct) =>
        {
            if (!httpContext.IsValidInternalRequest(configuration))
            {
                return Results.Unauthorized();
            }

            if (!string.Equals(request.Kind, "sale", StringComparison.OrdinalIgnoreCase))
            {
                return Results.BadRequest("Unsupported saga kind.");
            }

            await sagaService.CompensateAsync(request.EntityId, request.ReservationId, request.CarId, ct);
            return Results.Ok();
        });
    }

    public sealed record CompleteSagaRequest(string HolderReference, Guid SaleId, string Kind);

    public sealed record CompensateSagaRequest(string Kind, Guid EntityId, Guid ReservationId, Guid CarId);
}
