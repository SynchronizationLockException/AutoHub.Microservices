# AutoHub SLO

## Service Level Objectives (30d window)

| SLI | SLO | Measurement |
|-----|-----|-------------|
| Gateway availability | >= 99.5% | `up{job="api-gateway"}` or synthetic probe success rate |
| `POST /rentals` latency | p95 < 500ms | Histogram `http.server.request.duration` |
| Outbox publish lag | p99 < 30s | `outbox.batch.duration.ms` + pending rows |
| RabbitMQ DLQ depth | = 0 | Queue `catalog.availability.dead` messages |

## Error budget policy

- Burn rate > 2x for 1h: page on-call.
- DLQ > 0 for 15m: create incident, run [runbook-dlq.md](runbooks/runbook-dlq.md).

## Dashboards

- Grafana: `deploy/grafana/dashboards/autohub-red.json`
- Traces: Jaeger UI `http://localhost:16686`
