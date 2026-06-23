# Cycles database image, based on the SQLDockerDeployKit pattern.
FROM mcr.microsoft.com/mssql/server:2019-latest

ENV ACCEPT_EULA=Y

USER root

RUN apt-get update && apt-get install -y \
    mssql-tools unixodbc-dev \
    && rm -rf /var/lib/apt/lists/*

ENV PATH="${PATH}:/opt/mssql-tools/bin"

WORKDIR /tmp/app

COPY database/sqldockerdeploykit/entrypoint.sh /tmp/app/entrypoint.sh
COPY database/sqldockerdeploykit/SQLScripts /tmp/app/SQLScripts

RUN chmod +x /tmp/app/entrypoint.sh && chown -R mssql /tmp/app

USER mssql

ENTRYPOINT ["/tmp/app/entrypoint.sh"]
