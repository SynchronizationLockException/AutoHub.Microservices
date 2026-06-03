# Create rental saga

## States

| State | Meaning |
|-------|---------|
| Reserved | Catalog reservation created |
| Persisted | Rental saved + outbox row |
| Published | Outbox message sent to RabbitMQ |
| Completed | Catalog confirmed reservation |
| Compensating | Rolling back |
| Failed | Terminal failure |

## Flow

1. `POST /api/rentals` creates saga correlation id.
2. Rental service reserves car in catalog (`POST /api/cars/{id}/reservations`).
3. Rental + `RentalCreated` outbox saved in one DB transaction.
4. Outbox publisher emits `rental.created`.
5. Catalog consumer confirms reservation and marks car unavailable.
6. Catalog notifies rental internal API → saga `Completed`.

## Compensation

- Outbox publish timeout → saga worker compensates (cancel rental, release reservation, emit `rental.cancelled`).
- Catalog consumer DLQ → dead-letter worker calls rental `POST /api/internal/sagas/compensate`.
