# The image of the SQL Server test: the server the suite runs against, and the .NET SDK that runs the suite.
#
# The repository is mounted rather than copied, so this file installs software and nothing else.
#
# OS_IMAGE is read by DockerBuild.ps1, which replaces it with the same image in the registry named by
# DOCKER_REGISTRY when that registry is configured. An agent that cannot reach the mirror builds from the
# public image instead. Ubuntu 22.04 is the distribution that Microsoft publishes the server packages for.
ARG OS_IMAGE=ubuntu:22.04
FROM ${OS_IMAGE}

ENV DEBIAN_FRONTEND=noninteractive
ENV LANG=C.UTF-8
ENV LC_ALL=C.UTF-8

RUN apt-get update && apt-get install -y \
    curl \
    ca-certificates \
    libicu70 \
    libssl3 \
    && rm -rf /var/lib/apt/lists/*

# SQL Server 2022 and the command-line tools, from the package repository of Microsoft. The server is
# installed but no instance is started: a container image runs no service, and RunTest.ps1 starts one.
RUN curl -fsSL https://packages.microsoft.com/keys/microsoft.asc -o /etc/apt/trusted.gpg.d/microsoft.asc \
    && curl -fsSL https://packages.microsoft.com/config/ubuntu/22.04/mssql-server-2022.list -o /etc/apt/sources.list.d/mssql-server-2022.list \
    && curl -fsSL https://packages.microsoft.com/config/ubuntu/22.04/prod.list -o /etc/apt/sources.list.d/microsoft-prod.list \
    && apt-get update \
    && apt-get install -y mssql-server \
    && ACCEPT_EULA=Y apt-get install -y mssql-tools18 \
    && rm -rf /var/lib/apt/lists/*

ENV PATH="/opt/mssql-tools18/bin:${PATH}"

# The feature band and not a single version: global.json pins the SDK of the repository to 10.0.1xx and rolls
# forward over its patches alone, so an image carrying another band, such as 10.0.4xx, cannot build the suite.
# The band follows the DotNetSdkVersion that eng/src/Program.cs declares.
ENV DOTNET_ROOT=/usr/share/dotnet
ENV PATH="${DOTNET_ROOT}:${PATH}"
ENV DOTNET_NOLOGO=1
ENV DOTNET_CLI_TELEMETRY_OPTOUT=1

RUN curl -fsSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh \
    && chmod +x /tmp/dotnet-install.sh \
    && /tmp/dotnet-install.sh --channel 10.0.1xx --install-dir $DOTNET_ROOT \
    && rm /tmp/dotnet-install.sh
