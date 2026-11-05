using Demeure.Application.Common;
using Microsoft.EntityFrameworkCore;

namespace Demeure.Infrastructure.Persistence;

public sealed class SqliteDemeureDbContext(
    DbContextOptions<SqliteDemeureDbContext> options,
    IOrganizationContext organizationContext) : DemeureDbContext(options, organizationContext);
