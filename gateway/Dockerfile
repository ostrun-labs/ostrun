FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY OstrunGateway.csproj .
RUN dotnet restore OstrunGateway.csproj

COPY . .
RUN dotnet publish OstrunGateway.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
RUN useradd --uid 5678 --user-group --no-create-home appuser
COPY --from=build /app/publish .
USER appuser

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

ENTRYPOINT ["dotnet", "OstrunGateway.dll"]
