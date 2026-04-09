using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;

namespace BuildingBlocks.Hosting.Persistence;

public interface IIdempotentRequestRecord
{
    string KeyHash { get; init; }
    string Path { get; init; }
    int? StatusCode { get; set; }
    string? ResponseBody { get; set; }
}

public static class IdempotencyExecutionHelper
{
    private const string HeaderName = "Idempotency-Key";

    public static async Task<IResult> ExecuteAsync<TDbContext, TRecord>(
        HttpContext httpContext,
        TDbContext db,
        string pathKey,
        Func<string, string, TRecord> createRecord,
        Func<Task<IResult>> action,
        CancellationToken ct)
        where TDbContext : DbContext
        where TRecord : class, IIdempotentRequestRecord
    {
        if (!httpContext.Request.Headers.TryGetValue(HeaderName, out var keyValues) ||
            string.IsNullOrWhiteSpace(keyValues.ToString()))
        {
            return await action();
        }

        var key = keyValues.ToString().Trim();
        var keyHash = ComputeHash(key);

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var set = db.Set<TRecord>();

        var existing = await set.FirstOrDefaultAsync(x => x.KeyHash == keyHash && x.Path == pathKey, ct);
        if (existing is not null && existing.StatusCode is not null)
        {
            var status = existing.StatusCode.Value;
            var body = existing.ResponseBody;

            if (string.IsNullOrEmpty(body))
            {
                return Results.StatusCode(status);
            }

            httpContext.Response.StatusCode = status;
            httpContext.Response.Headers[HeaderNames.ContentType] = "application/json";
            await httpContext.Response.WriteAsync(body, ct);
            return Results.Empty;
        }

        if (existing is null)
        {
            set.Add(createRecord(keyHash, pathKey));
            await db.SaveChangesAsync(ct);
            existing = await set.FirstAsync(x => x.KeyHash == keyHash && x.Path == pathKey, ct);
        }

        var result = await action();
        var (statusCode, bodyJson) = await ToStatusAndBodyAsync(result, ct);

        existing.StatusCode = statusCode;
        existing.ResponseBody = bodyJson;

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return result;
    }

    private static string ComputeHash(string key)
    {
        var bytes = Encoding.UTF8.GetBytes(key);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }

    private static async Task<(int StatusCode, string? BodyJson)> ToStatusAndBodyAsync(
        IResult result,
        CancellationToken ct)
    {
        var context = new DefaultHttpContext();
        await using var stream = new MemoryStream();
        context.Response.Body = stream;

        await result.ExecuteAsync(context);

        var status = context.Response.StatusCode == 0
            ? StatusCodes.Status200OK
            : context.Response.StatusCode;

        stream.Position = 0;
        using var reader = new StreamReader(stream);
        var body = await reader.ReadToEndAsync(ct);

        return (status, string.IsNullOrWhiteSpace(body) ? null : body);
    }
}
