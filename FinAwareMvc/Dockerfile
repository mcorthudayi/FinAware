FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["FinAwareMvc/FinAwareMvc.csproj", "FinAwareMvc/"]
RUN dotnet restore "FinAwareMvc/FinAwareMvc.csproj"
COPY . .
WORKDIR "/src/FinAwareMvc"
RUN dotnet publish "FinAwareMvc.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "FinAwareMvc.dll"]