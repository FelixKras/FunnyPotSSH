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

# Install git for LibGit2Sharp native dependencies and submodule handling
RUN apt-get update && apt-get install -y git libgit2-dev && rm -rf /var/lib/apt/lists/*

RUN groupadd $USERNAME && \
    useradd -m -g $USERNAME -s /bin/bash $USERNAME && \
    mkdir -p /home/$USERNAME/app

WORKDIR /home/$USERNAME/app
COPY --from=publish /app/publish .

# The frontend submodule will be managed at runtime or mounted via volume.
# For now, ensure the directory exists and has correct permissions.
RUN mkdir -p /home/$USERNAME/app/frontend/sessions && \
    chown -R $USERNAME:$USERNAME /home/$USERNAME/app && \
    chmod -R 755 /home/$USERNAME/app

USER $USERNAME

EXPOSE 22422

# Healthcheck
HEALTHCHECK --interval=30s --timeout=10s CMD timeout 2s bash -c '</dev/tcp/localhost/22422' || exit 1

CMD ["dotnet", "FunnyPot.dll"]
