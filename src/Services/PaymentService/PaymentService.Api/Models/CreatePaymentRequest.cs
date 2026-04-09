namespace PaymentService.Api.Models;

public sealed record CreatePaymentRequest(
    PaymentReferenceKind ReferenceKind,
    Guid ReferenceId,
    decimal Amount,
    string? Currency = null);
