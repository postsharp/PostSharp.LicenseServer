# The image of the PostgreSQL test: the server the suite runs against, and the .NET SDK that runs the suite.
#
# The repository is mounted rather than copied, so this file installs software and nothing else.
#
# OS_IMAGE is read by DockerBuild.ps1, which replaces it with the same image in the registry named by
# DOCKER_REGISTRY when that registry is configured. An agent that cannot reach the mirror builds from the
# public image instead.
ARG OS_IMAGE=ubuntu:22.04
FROM ${OS_IMAGE}

ENV DEBIAN_FRONTEND=noninteractive
ENV LANG=C.UTF-8
ENV LC_ALL=C.UTF-8

RUN apt-get update && apt-get install -y     curl     ca-certificates     libicu70     libssl3     && rm -rf /var/lib/apt/lists/*

# The packages come from the repository of the PostgreSQL project and not from Ubuntu, whose repository carries
# the version that shipped with the distribution. The tests therefore run against a version a customer can
# deploy today. jammy is Ubuntu 22.04. The server is installed but no cluster is created: a container image
# runs no service, and run.sh creates a cluster under /tmp and starts it.
RUN curl -fsSL https://www.postgresql.org/media/keys/ACCC4CF8.asc -o /etc/apt/trusted.gpg.d/postgresql.asc     && echo "deb http://apt.postgresql.org/pub/repos/apt jammy-pgdg main" > /etc/apt/sources.list.d/pgdg.list     && apt-get update     && apt-get install -y postgresql-17     && rm -rf /var/lib/apt/lists/*

ENV PGBINDIR=/usr/lib/postgresql/17/bin
ENV PATH="${PGBINDIR}:${PATH}"

# The feature band and not a single version: global.json pins the SDK of the repository to 10.0.1xx and rolls
# forward over its patches alone, so an image carrying another band, such as 10.0.4xx, cannot build the suite.
# The band follows the DotNetSdkVersion that eng/src/Program.cs declares.
ENV DOTNET_ROOT=/usr/share/dotnet
ENV PATH="${DOTNET_ROOT}:${PATH}"
ENV DOTNET_NOLOGO=1
ENV DOTNET_CLI_TELEMETRY_OPTOUT=1

RUN curl -fsSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh     && chmod +x /tmp/dotnet-install.sh     && /tmp/dotnet-install.sh --channel 10.0.1xx --install-dir $DOTNET_ROOT     && rm /tmp/dotnet-install.sh
