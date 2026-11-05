using Demeure.Application.Common;
using Demeure.Application.Common.Exceptions;
using Demeure.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using System.Data;

namespace Demeure.Infrastructure.Authorization;

internal sealed class OrganizationDataTransaction(DemeureDbContext db, IOrganizationContext context)
{
    public async Task<IDbContextTransaction> BeginAsync(Guid userId, bool platform, CancellationToken cancellationToken,
        bool consistentSnapshot = false)
    {
        if (context.OrganizationId is not { } id || id == Guid.Empty)
            throw new ForbiddenException("An authorized organization is required.");
        var transaction = consistentSnapshot
            ? await db.Database.BeginTransactionAsync(
                db.Database.IsNpgsql() ? IsolationLevel.RepeatableRead : IsolationLevel.Serializable, cancellationToken)
            : await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            if (db.Database.IsNpgsql())
                await db.Database.ExecuteSqlInterpolatedAsync(
                    $"""
                    SELECT set_config('demeure.organization_id', {id.ToString()}, true),
                           set_config('demeure.user_id', {userId.ToString()}, true),
                           set_config('demeure.platform_access', {(platform ? "true" : "false")}, true)
                    """, cancellationToken);
            return transaction;
        }
        catch
        {
            await transaction.DisposeAsync();
            throw;
        }
    }
}
