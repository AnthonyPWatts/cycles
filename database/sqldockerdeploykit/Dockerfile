# Cycles database image, based on the SQLDockerDeployKit pattern.
FROM mcr.microsoft.com/mssql/server:2022-latest

ENV ACCEPT_EULA=Y
ENV PATH="${PATH}:/opt/mssql-tools18/bin:/opt/mssql-tools/bin"

USER root

WORKDIR /tmp/app

COPY database/sqldockerdeploykit/entrypoint.sh /tmp/app/entrypoint.sh
COPY database/migrations /tmp/app/Migrations
COPY database/sqldockerdeploykit/SQLScripts /tmp/app/SQLScripts

RUN chmod +x /tmp/app/entrypoint.sh && chown -R mssql /tmp/app

USER mssql

HEALTHCHECK --interval=10s --timeout=5s --start-period=30s --retries=12 \
    CMD bash -c 'SQLCMD="$(command -v sqlcmd || true)"; if [ -z "$SQLCMD" ] && [ -x /opt/mssql-tools18/bin/sqlcmd ]; then SQLCMD=/opt/mssql-tools18/bin/sqlcmd; fi; if [ -z "$SQLCMD" ] && [ -x /opt/mssql-tools/bin/sqlcmd ]; then SQLCMD=/opt/mssql-tools/bin/sqlcmd; fi; PASSWORD="${MSSQL_SA_PASSWORD:-${SA_PASSWORD:-}}"; [ -n "$SQLCMD" ] && [ -n "$PASSWORD" ] && "$SQLCMD" -C -S localhost -U SA -P "$PASSWORD" -Q "SELECT 1" >/dev/null || exit 1'

ENTRYPOINT ["/tmp/app/entrypoint.sh"]
