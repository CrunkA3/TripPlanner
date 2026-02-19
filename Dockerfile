# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0-preview AS build
WORKDIR /src

# Copy csproj files and restore dependencies
COPY ["TripPlanner.Web/TripPlanner.Web.csproj", "TripPlanner.Web/"]
COPY ["TripPlanner.ServiceDefaults/TripPlanner.ServiceDefaults.csproj", "TripPlanner.ServiceDefaults/"]
RUN dotnet restore "TripPlanner.Web/TripPlanner.Web.csproj"

# Copy everything else and build
COPY . .
WORKDIR "/src/TripPlanner.Web"
RUN dotnet build "TripPlanner.Web.csproj" -c Release -o /app/build

# Publish stage
FROM build AS publish
RUN dotnet publish "TripPlanner.Web.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0-preview AS final
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "TripPlanner.Web.dll"]
