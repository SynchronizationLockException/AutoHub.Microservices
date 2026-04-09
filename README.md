# AutoHub.Microservices

Микросервисный проект аренды и продажи автомобилей

## Состав решения

- `ApiGateway` - единая точка входа (YARP Reverse Proxy).
- `CarCatalogService` - каталог автомобилей и управление доступностью.
- `RentalService` - оформление договоров аренды.
- `SalesService` - оформление продаж.
- `PaymentService` - фиксация оплаты по договору продажи или аренды, событие `payment.completed` в RabbitMQ.
- `AuthService` - выпуск JWT токенов для ролей `Client`, `Manager`, `Admin`.
- `BuildingBlocks.Observability` - общий модуль OpenTelemetry.
- `BuildingBlocks.Contracts` - общие контракты интеграционных событий.

## Быстрый старт

1. Перейдите в корень проекта:

   ```bash
   cd AutoHub.Microservices
   ```

2. Создайте `.env` в корне (можно скопировать `deploy/env.example`) и задайте свои значения:

   ```env
   POSTGRES_PASSWORD=...
   RABBITMQ_DEFAULT_USER=...
   RABBITMQ_DEFAULT_PASS=...
   ```

3. Поднимите окружение:

   ```bash
   docker compose up --build
   ```

4. Откройте:
   - Gateway: `http://localhost:5000`
   - Jaeger UI: `http://localhost:16686`

## Примеры запросов через Gateway

- Получить JWT:
  - `POST http://localhost:5000/auth/api/auth/token`
  - body: `{"username":"manager","password":"<password_from_env_seed>"}`
- Обновить access token:
  - `POST http://localhost:5000/auth/api/auth/refresh`
  - body: `{"refreshToken":"..."}`
- Отозвать refresh token:
  - `POST http://localhost:5000/auth/api/auth/revoke`
  - body: `{"refreshToken":"..."}`
- Получить каталог:
  - `GET http://localhost:5000/catalog/api/cars`
- Создать аренду:
  - `POST http://localhost:5000/rentals/api/rentals`
- Создать продажу:
  - `POST http://localhost:5000/sales/api/sales`
- Список платежей:
  - `GET http://localhost:5000/payments/api/payments`
- Оформить платёж по существующей продаже или аренде (роли `Manager` / `Admin`; сумма должна совпадать с заказом):
  - `POST http://localhost:5000/payments/api/payments`
  - body: `{"referenceKind":0,"referenceId":"<guid-продажи>","amount":<decimal>}` — `referenceKind`: `0` = продажа, `1` = аренда

Для защищенных endpoint передавайте `Authorization: Bearer <token>`.

> Demo пользователи не создаются автоматически.  
> Для локального демо включите `AUTH_SEED_ENABLED=true` и задайте `AUTH_SEED_CLIENT_PASSWORD`, `AUTH_SEED_MANAGER_PASSWORD`, `AUTH_SEED_ADMIN_PASSWORD`.

