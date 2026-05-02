FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["FinAware.Bot/FinAware.Bot.csproj", "FinAware.Bot/"]
RUN dotnet restore "FinAware.Bot/FinAware.Bot.csproj"
COPY . .
WORKDIR "/src/FinAware.Bot"
RUN dotnet publish "FinAware.Bot.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "FinAware.Bot.dll"]