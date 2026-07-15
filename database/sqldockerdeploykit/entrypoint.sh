#!/bin/bash
set -euo pipefail

SQL_SA_PASSWORD="${MSSQL_SA_PASSWORD:-${SA_PASSWORD:-}}"
if [ -z "$SQL_SA_PASSWORD" ]; then
    echo "Error: MSSQL_SA_PASSWORD environment variable is required."
    echo "Set it before starting the container. SA_PASSWORD is still accepted as a backwards-compatible alias."
    exit 1
fi

export MSSQL_SA_PASSWORD="$SQL_SA_PASSWORD"
export SA_PASSWORD="$SQL_SA_PASSWORD"

find_sqlcmd() {
    if command -v sqlcmd >/dev/null 2>&1; then
        command -v sqlcmd
        return 0
    fi

    if [ -x /opt/mssql-tools18/bin/sqlcmd ]; then
        echo /opt/mssql-tools18/bin/sqlcmd
        return 0
    fi

    if [ -x /opt/mssql-tools/bin/sqlcmd ]; then
        echo /opt/mssql-tools/bin/sqlcmd
        return 0
    fi

    echo "Error: sqlcmd was not found in the SQL Server image." >&2
    return 1
}

SQLCMD="$(find_sqlcmd)"
SQL_READY_TIMEOUT_SECONDS="${SQL_READY_TIMEOUT_SECONDS:-120}"
SQL_READY_POLL_SECONDS="${SQL_READY_POLL_SECONDS:-2}"
SQLSERVR_PID=""

stop_sql_server() {
    if [ -n "$SQLSERVR_PID" ] && kill -0 "$SQLSERVR_PID" 2>/dev/null; then
        echo "Stopping SQL Server."
        kill "$SQLSERVR_PID"
        wait "$SQLSERVR_PID" || true
    fi
}

wait_for_sql_server() {
    local elapsed_seconds=0

    echo "Waiting up to ${SQL_READY_TIMEOUT_SECONDS}s for SQL Server readiness."
    while [ "$elapsed_seconds" -lt "$SQL_READY_TIMEOUT_SECONDS" ]; do
        if ! kill -0 "$SQLSERVR_PID" 2>/dev/null; then
            echo "Error: SQL Server exited before it became ready."
            set +e
            wait "$SQLSERVR_PID"
            sqlservr_exit_code=$?
            set -e

            if [ "$sqlservr_exit_code" -eq 0 ]; then
                exit 1
            fi

            exit "$sqlservr_exit_code"
        fi

        if "$SQLCMD" -b -C -S localhost -U SA -P "$SQL_SA_PASSWORD" -Q "SELECT 1" >/dev/null 2>&1; then
            echo "SQL Server is ready."
            return 0
        fi

        sleep "$SQL_READY_POLL_SECONDS"
        elapsed_seconds=$((elapsed_seconds + SQL_READY_POLL_SECONDS))
    done

    echo "Error: SQL Server did not become ready within ${SQL_READY_TIMEOUT_SECONDS}s."
    stop_sql_server
    exit 1
}

wait_for_database() {
    local database_name="$1"
    local elapsed_seconds=0

    echo "Waiting up to ${SQL_READY_TIMEOUT_SECONDS}s for ${database_name} readiness."
    while [ "$elapsed_seconds" -lt "$SQL_READY_TIMEOUT_SECONDS" ]; do
        if ! kill -0 "$SQLSERVR_PID" 2>/dev/null; then
            echo "Error: SQL Server exited before ${database_name} became ready."
            set +e
            wait "$SQLSERVR_PID"
            sqlservr_exit_code=$?
            set -e

            if [ "$sqlservr_exit_code" -eq 0 ]; then
                exit 1
            fi

            exit "$sqlservr_exit_code"
        fi

        if "$SQLCMD" -b -C -S localhost -U SA -P "$SQL_SA_PASSWORD" -d "$database_name" -Q "SELECT 1" >/dev/null 2>&1; then
            echo "${database_name} is ready."
            return 0
        fi

        sleep "$SQL_READY_POLL_SECONDS"
        elapsed_seconds=$((elapsed_seconds + SQL_READY_POLL_SECONDS))
    done

    echo "Error: ${database_name} did not become ready within ${SQL_READY_TIMEOUT_SECONDS}s."
    stop_sql_server
    exit 1
}

run_sql_file() {
    local database_name="$1"
    local sql_file="$2"

    echo "Executing $sql_file"
    "$SQLCMD" -b -C -S localhost -U SA -P "$SQL_SA_PASSWORD" -d "$database_name" -i "$sql_file"
}

trap stop_sql_server SIGINT SIGTERM

/opt/mssql/bin/sqlservr &
SQLSERVR_PID=$!

wait_for_sql_server

echo "Ensuring CyclesDb database exists"
"$SQLCMD" -b -C -S localhost -U SA -P "$SQL_SA_PASSWORD" -Q "IF DB_ID(N'CyclesDb') IS NULL CREATE DATABASE CyclesDb;"
wait_for_database CyclesDb

for sql_file in /tmp/app/Migrations/*.sql; do
    run_sql_file CyclesDb "$sql_file"
done

for sql_file in /tmp/app/SQLScripts/*.sql; do
    run_sql_file CyclesDb "$sql_file"
done

echo "SQL initialization complete. Waiting on SQL Server process."
wait "$SQLSERVR_PID"
