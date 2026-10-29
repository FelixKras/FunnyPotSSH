# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["FunnyPot/FunnyPot.csproj", "FunnyPot/"]
RUN dotnet restore "FunnyPot/FunnyPot.csproj"
COPY . .
WORKDIR "/src/FunnyPot"
RUN dotnet build "FunnyPot.csproj" -c Release -o /app/build

# Publish stage
FROM build AS publish
RUN dotnet publish "FunnyPot.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/runtime:8.0 AS final
ARG USERNAME=test
ARG USER_UID=1000
ARG USER_GID=1000
ENV SSH_USER=$USERNAME
ENV SSH_PASSWORD=test
ENV FUNNYPOT_RUN_USER=$USERNAME

# Install git for LibGit2Sharp native dependencies
RUN apt-get update && apt-get install -y --no-install-recommends git libgit2-dev python3 && rm -rf /var/lib/apt/lists/*

# Drop all capabilities for the container and match host UID/GID
RUN groupadd -g ${USER_GID} $USERNAME && \
    useradd -u ${USER_UID} -m -g $USERNAME -s /bin/bash $USERNAME && \
    mkdir -p /home/$USERNAME/app && \
    mkdir -p /var/log/funnypot /var/lib/funnypot

# The optional in-container bridge has a different UID from the SSH application.
RUN useradd -u 20001 -g ${USER_GID} -M -s /usr/sbin/nologin subscription-bridge && \
    mkdir -p /opt/subscription-bridge /run/host-subscription && \
    chmod 700 /run/host-subscription
COPY subscription-bridge/bridge.py subscription-bridge/supervise.py subscription-bridge/healthcheck.py /opt/subscription-bridge/

WORKDIR /home/$USERNAME/app
COPY --from=publish /app/publish .
COPY --from=build /src/frontend ./frontend
COPY --from=build /src/FunnyPot/data ./data
COPY --from=build /src/FunnyPot/data/command_responses.json /var/lib/funnypot/command_responses.json

RUN rm -rf /home/$USERNAME/app/frontend/.git && \
    mkdir -p /home/$USERNAME/app/frontend/sessions && \
    chown -R $USERNAME:$USERNAME /home/$USERNAME/app /var/log/funnypot /var/lib/funnypot && \
    chmod -R 755 /home/$USERNAME/app && \
    chmod -R 755 /var/log/funnypot && \
    chmod -R 750 /var/lib/funnypot

USER $USERNAME

EXPOSE 22722

HEALTHCHECK --interval=30s --timeout=10s CMD ["dotnet", "FunnyPot.dll", "--healthcheck"]

CMD ["dotnet", "FunnyPot.dll"]
