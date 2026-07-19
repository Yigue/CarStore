# Use the official .NET 9 runtime as base image
FROM mcr.microsoft.com/dotnet/aspnet:9.0-alpine AS base
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

# Create a non-root user for security
RUN addgroup -S -g 1001 dotnet && \
    adduser -S -u 1001 -G dotnet -s /bin/false dotnet

# Use the official .NET 9 SDK for building
FROM mcr.microsoft.com/dotnet/sdk:9.0-alpine AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Copy project files for better layer caching
COPY ["Directory.Build.props", "Directory.Packages.props", "./"]
COPY ["src/Web.Api/Web.Api.csproj", "src/Web.Api/"]
COPY ["src/Infrastructure/Infrastructure.csproj", "src/Infrastructure/"]
COPY ["src/Application/Application.csproj", "src/Application/"]
COPY ["src/Domain/Domain.csproj", "src/Domain/"]
COPY ["src/SharedKernel/SharedKernel.csproj", "src/SharedKernel/"]

# Restore dependencies using BuildKit cache mount
RUN --mount=type=cache,id=nuget,target=/root/.nuget/packages \
    dotnet restore "./src/Web.Api/Web.Api.csproj" --verbosity normal

# Copy source code
COPY . .

# Build the application using BuildKit cache mount
WORKDIR "/src/src/Web.Api"
RUN --mount=type=cache,id=nuget,target=/root/.nuget/packages \
    dotnet build "./Web.Api.csproj" -c $BUILD_CONFIGURATION -o /app/build --no-restore

# Publish the application
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./Web.Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# Create final runtime image
FROM base AS final
WORKDIR /app

# Copy published application
COPY --from=publish /app/publish .

# Create uploads directory and set permissions
RUN mkdir -p /app/uploads && \
    chown -R dotnet:dotnet /app

# Switch to non-root user
USER dotnet

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=40s --retries=3 \
    CMD wget --quiet --tries=1 --spider http://localhost:8080/health || exit 1

# Set the entry point
ENTRYPOINT ["dotnet", "Web.Api.dll"]