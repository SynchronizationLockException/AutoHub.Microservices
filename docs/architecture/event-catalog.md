# Integration event catalog

All integration events are JSON payloads published to RabbitMQ via the transactional outbox.

## Events

| Routing key | Type | Payload | Producers | Consumers |
|-------------|------|---------|-----------|-----------|
| `rental.created` | `RentalCreatedEvent` | `carId`, `rentalId`, `reservationId`, `ownerUsername` | RentalService | CarCatalogService, NotificationService |
| `sale.created` | `SaleCreatedEvent` | `carId`, `saleId`, `reservationId`, `ownerUsername` | SalesService | CarCatalogService, NotificationService |
| `rental.cancelled` | `RentalCancelledEvent` | `carId`, `rentalId`, `reservationId`, `ownerUsername` | RentalService | CarCatalogService, NotificationService |
| `sale.cancelled` | `SaleCancelledEvent` | `carId`, `saleId`, `reservationId`, `ownerUsername` | SalesService | CarCatalogService, NotificationService |
| `payment.completed` | `PaymentCompletedEvent` | `paymentId`, `referenceKind`, `referenceId`, `amount`, `currency`, `ownerUsername` | PaymentService | NotificationService |

## Contract source

Shared records live in [`BuildingBlocks.Contracts/IntegrationEvent.cs`](../../src/BuildingBlocks/BuildingBlocks.Contracts/IntegrationEvent.cs).

## Versioning policy

- Additive changes only (new optional JSON fields) without routing key changes.
- Breaking changes require a new routing key suffix (for example `rental.created.v2`).
