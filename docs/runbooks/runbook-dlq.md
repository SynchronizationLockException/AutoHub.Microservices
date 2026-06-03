# Runbook: RabbitMQ DLQ

## Symptoms

- Alert `DLQ depth > 0` on `catalog.availability.dead`.
- Cars remain available while rentals/sales exist.

## Diagnosis

1. Open RabbitMQ management UI (port 15672).
2. Inspect messages in `catalog.availability.dead`.
3. Check CarCatalog logs for consumer exceptions.
4. Verify `InternalApi__Secret` matches between Catalog, Rental, and Sales.

## Mitigation

1. `CatalogDeadLetterWorker` should POST compensation to Rental/Sales internal endpoints.
2. If compensation failed, manually call:
   - `POST /api/internal/sagas/compensate` on Rental or Sales with payload from DLQ message.
3. After fix, purge DLQ only if all compensations succeeded.

## Verification

- Saga state `Failed` or rental `Cancelled`.
- Car availability restored in catalog.
