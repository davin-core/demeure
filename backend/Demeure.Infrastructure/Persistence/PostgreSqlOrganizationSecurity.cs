namespace Demeure.Infrastructure.Persistence;

internal static class PostgreSqlOrganizationSecurity
{
    private static readonly string[] Tables =
    [
        "properties", "property_manager_assignments", "units", "tenants", "rental_applications",
        "leases", "payments", "expenses", "maintenance_requests", "contractors", "documents",
        "notifications", "audit_logs"
    ];

    internal static string EnableSql => """
        CREATE FUNCTION public.demeure_can_access_organization(target uuid)
        RETURNS boolean LANGUAGE sql STABLE SECURITY INVOKER
        SET search_path = pg_catalog
        AS $security$
            SELECT target = nullif(current_setting('demeure.organization_id', true), '')::uuid
              AND EXISTS (
                SELECT 1 FROM public.users u JOIN public.identity_accounts a ON a."Id" = u."Id"
                WHERE u."Id" = nullif(current_setting('demeure.user_id', true), '')::uuid
                  AND u."Status" = 'Active' AND a."EmailConfirmed"
                  AND CASE WHEN current_setting('demeure.platform_access', true) = 'true'
                    THEN EXISTS (
                      SELECT 1 FROM public.identity_user_roles ur
                      JOIN public.identity_roles r ON r."Id" = ur."RoleId"
                      WHERE ur."UserId" = u."Id" AND r."NormalizedName" = 'SUPERADMINISTRATOR')
                    ELSE EXISTS (
                      SELECT 1 FROM public.organization_memberships m
                      JOIN public.organizations o ON o."Id" = m."OrganizationId"
                      WHERE m."UserId" = u."Id" AND m."OrganizationId" = target
                        AND m."Status" = 'Active' AND o."Status" = 'Active')
                  END);
        $security$;
        """ + Environment.NewLine + string.Join(Environment.NewLine, Tables.Select(table => $"""
        ALTER TABLE public.{table} ENABLE ROW LEVEL SECURITY;
        ALTER TABLE public.{table} FORCE ROW LEVEL SECURITY;
        CREATE POLICY demeure_organization_isolation ON public.{table}
            USING (public.demeure_can_access_organization("OrganizationId"))
            WITH CHECK (public.demeure_can_access_organization("OrganizationId"));
        """));

    internal static string DisableSql => string.Join(Environment.NewLine, Tables.Select(table => $"""
        DROP POLICY demeure_organization_isolation ON public.{table};
        ALTER TABLE public.{table} NO FORCE ROW LEVEL SECURITY;
        ALTER TABLE public.{table} DISABLE ROW LEVEL SECURITY;
        """)) + Environment.NewLine + "DROP FUNCTION public.demeure_can_access_organization(uuid);";
}
