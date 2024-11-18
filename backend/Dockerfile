# Stage 1: Build and publish the application
FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore "./aspnetapp.csproj"
RUN dotnet build "./aspnetapp.csproj" -c Release -o /app/build
RUN dotnet publish "./aspnetapp.csproj" -c Release -o /app/publish

# Stage 2: Development with hot-reload
FROM mcr.microsoft.com/dotnet/sdk:6.0 AS dev
WORKDIR /app
COPY --from=build /app/publish .

# Install inotify-tools for file change detection
RUN apt-get update && apt-get install -y inotify-tools

# Set environment variable for development
ENV ASPNETCORE_ENVIRONMENT=Development

# Expose port 80
EXPOSE 80

# Use dotnet watch for hot reloading, specifying the project explicitly
ENTRYPOINT ["dotnet", "watch", "run", "--project", "/src/aspnetapp.csproj", "--urls", "http://0.0.0.0:80"]
