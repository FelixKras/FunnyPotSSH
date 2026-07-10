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
ENV SSH_USER=$USERNAME
ENV SSH_PASSWORD=test

# Install git for LibGit2Sharp native dependencies
RUN apt-get update && apt-get install -y git libgit2-dev && rm -rf /var/lib/apt/lists/*

# Drop all capabilities for the container
RUN groupadd $USERNAME && \
    useradd -m -g $USERNAME -s /bin/bash $USERNAME && \
    mkdir -p /home/$USERNAME/app && \
    mkdir -p /var/log/funnypot

WORKDIR /home/$USERNAME/app
COPY --from=publish /app/publish .
COPY --from=build /src/frontend ./frontend
COPY --from=build /src/FunnyPot/data ./data

RUN rm -rf /home/$USERNAME/app/frontend/.git && \
    mkdir -p /home/$USERNAME/app/frontend/sessions && \
    chown -R $USERNAME:$USERNAME /home/$USERNAME/app /var/log/funnypot && \
    chmod -R 755 /home/$USERNAME/app && \
    chmod -R 755 /var/log/funnypot

USER $USERNAME

EXPOSE 22722

HEALTHCHECK --interval=30s --timeout=10s CMD-SHELL timeout 2s bash -c "</dev/tcp/localhost/$SSH_PORT" || exit 1

CMD ["dotnet", "FunnyPot.dll"]
