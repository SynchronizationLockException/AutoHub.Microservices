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
        var set = db.Set<TRecord>();

        var existing = await set.AsNoTracking().FirstOrDefaultAsync(x => x.KeyHash == keyHash && x.Path == pathKey, ct);
        if (existing is not null)
        {
            if (existing.StatusCode is not null)
            {
                return await WriteReplayAsync(httpContext, existing.StatusCode.Value, existing.ResponseBody, ct);
            }

            // The first request is still executing; do not run side effects twice.
            return Results.Conflict("A request with this Idempotency-Key is already in progress.");
        }

        try
        {
            set.Add(createRecord(keyHash, pathKey));
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            var concurrent = await set.AsNoTracking().FirstOrDefaultAsync(x => x.KeyHash == keyHash && x.Path == pathKey, ct);
            if (concurrent is not null && concurrent.StatusCode is not null)
            {
                return await WriteReplayAsync(httpContext, concurrent.StatusCode.Value, concurrent.ResponseBody, ct);
            }

            return Results.Conflict("A request with this Idempotency-Key is already in progress.");
        }

        var result = await action();
        var (statusCode, bodyJson) = await ToStatusAndBodyAsync(result, ct);

        existing = await set.FirstOrDefaultAsync(x => x.KeyHash == keyHash && x.Path == pathKey, ct);
        if (existing is null)
        {
            set.Add(createRecord(keyHash, pathKey));
            existing = await set.FirstAsync(x => x.KeyHash == keyHash && x.Path == pathKey, ct);
        }

        existing.StatusCode = statusCode;
        existing.ResponseBody = bodyJson;
        await db.SaveChangesAsync(ct);

        return result;
    }

    private static async Task<IResult> WriteReplayAsync(
        HttpContext httpContext,
        int statusCode,
        string? body,
        CancellationToken ct)
    {
        if (string.IsNullOrEmpty(body))
        {
            return Results.StatusCode(statusCode);
        }

        httpContext.Response.StatusCode = statusCode;
        httpContext.Response.Headers[HeaderNames.ContentType] = "application/json";
        await httpContext.Response.WriteAsync(body, ct);
        return Results.Empty;
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
