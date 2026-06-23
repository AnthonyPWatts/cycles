#!/bin/bash
set -euo pipefail

if [ -z "${MSSQL_SA_PASSWORD:-}" ] && [ -z "${SA_PASSWORD:-}" ]; then
    echo "Error: MSSQL_SA_PASSWORD or SA_PASSWORD environment variable is required."
    exit 1
fi

export MSSQL_SA_PASSWORD="${MSSQL_SA_PASSWORD:-$SA_PASSWORD}"

/opt/mssql/bin/sqlservr &
sqlservr_pid=$!

for attempt in {1..60}; do
    if /opt/mssql-tools/bin/sqlcmd -S localhost -U SA -P "$MSSQL_SA_PASSWORD" -Q "SELECT 1" >/tmp/sql-ready.log 2>&1; then
        break
    fi

    if ! kill -0 "$sqlservr_pid" 2>/dev/null; then
        echo "Error: SQL Server exited before it became ready."
        cat /tmp/sql-ready.log || true
        exit 1
    fi

    if [ "$attempt" -eq 60 ]; then
        echo "Error: SQL Server did not become ready in time."
        cat /tmp/sql-ready.log || true
        exit 1
    fi

    sleep 2
done

for sql_file in /tmp/app/SQLScripts/*.sql; do
    echo "Executing $sql_file"
    /opt/mssql-tools/bin/sqlcmd -b -S localhost -U SA -P "$MSSQL_SA_PASSWORD" -i "$sql_file"
done

wait "$sqlservr_pid"
