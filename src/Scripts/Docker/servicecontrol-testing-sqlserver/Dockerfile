FROM ubuntu:22.04
ENV DEBIAN_FRONTEND=noninteractive

RUN apt-get update && \
    apt-get install -y curl gnupg2 apt-transport-https && \
    curl -fsSL https://packages.microsoft.com/keys/microsoft.asc | \
        gpg --dearmor -o /etc/apt/trusted.gpg.d/microsoft.gpg && \
    curl -fsSL https://packages.microsoft.com/config/ubuntu/22.04/mssql-server-2025.list | \
        tee /etc/apt/sources.list.d/mssql-server-2025.list && \
    curl -fsSL https://packages.microsoft.com/config/ubuntu/22.04/prod.list | \
        tee /etc/apt/sources.list.d/mssql-release.list && \
    apt-get update && \
    ACCEPT_EULA=Y apt-get install -y mssql-server mssql-server-fts && \
    ACCEPT_EULA=Y apt-get install -y mssql-tools18 unixodbc-dev && \
    # Clean up apt cache
    apt-get clean && \
    rm -rf /var/lib/apt/lists/*

EXPOSE 1433

ENTRYPOINT [ "/opt/mssql/bin/sqlservr" ]