FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["FinAware/FinAware.csproj", "FinAware/"]
RUN dotnet restore "FinAware/FinAware.csproj"
COPY . .
WORKDIR "/src/FinAware"
RUN dotnet publish "FinAware.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "FinAware.dll"]