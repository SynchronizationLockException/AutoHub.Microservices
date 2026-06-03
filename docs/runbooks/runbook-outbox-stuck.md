# Runbook: Outbox stuck

## Symptoms

- Integration events not arriving in consumers.
- Metric `outbox.messages.published` flatlines while DB has `ProcessedOnUtc IS NULL` rows.

## Diagnosis

1. Check service logs for `Outbox publish batch failed`.
2. Query pending messages:
   ```sql
   SELECT COUNT(*) FROM "OutboxMessages" WHERE "ProcessedOnUtc" IS NULL;
   ```
3. Verify RabbitMQ connectivity from the service pod/container.

## Mitigation

1. Restore RabbitMQ cluster health.
2. Restart the affected service (outbox publisher reconnects automatically).
3. If rows remain pending > 30 minutes, inspect payload errors and fix root cause before manual replay.

## Prevention

- Alert on outbox lag SLO (see [slo.md](../slo.md)).
