# Build stage: Use .NET SDK image to compile the application
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
# Set working directory for build operations
WORKDIR /src

# Copy only project files first (for layer caching optimization)
COPY src/Persistence/Persistence.csproj src/Persistence/
COPY src/Web.Api/Web.Api.csproj src/Web.Api/
# Restore NuGet packages (cached until project files change)
RUN dotnet restore src/Web.Api/Web.Api.csproj

# Copy all source code files
COPY src/ src/
# Build and publish the application in Release configuration
RUN dotnet publish src/Web.Api/Web.Api.csproj -c Release -o /app/publish

# Runtime stage: Use smaller runtime-only image for final container
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
# Set working directory for the application
WORKDIR /app
# Copy published files from build stage
COPY --from=build /app/publish .

# Configure ASP.NET Core to listen on port 8080
ENV ASPNETCORE_URLS=http://+:8080
# Document which port the container listens on
EXPOSE 8080

# Define the command to run when container starts
ENTRYPOINT ["dotnet", "Web.Api.dll"]
