FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 5000

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["EV_Charging.csproj", "."]
RUN dotnet restore "./EV_Charging.csproj"
COPY . .
RUN dotnet build "EV_Charging.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "EV_Charging.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "EV_Charging.dll"]