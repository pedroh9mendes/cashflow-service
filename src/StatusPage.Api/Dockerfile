FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["src/StatusPage.Api/StatusPage.Api.csproj", "src/StatusPage.Api/"]
RUN dotnet restore "src/StatusPage.Api/StatusPage.Api.csproj"

COPY . .
RUN dotnet publish "src/StatusPage.Api/StatusPage.Api.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "StatusPage.Api.dll"]
