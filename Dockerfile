FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["src/ArcadeLink.Users.Api/ArcadeLink.Users.Api.csproj", "src/ArcadeLink.Users.Api/"]
RUN dotnet restore "src/ArcadeLink.Users.Api/ArcadeLink.Users.Api.csproj"

COPY . .
RUN dotnet publish "src/ArcadeLink.Users.Api/ArcadeLink.Users.Api.csproj" -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
ARG APP_VERSION=dev
ENV APP_VERSION=$APP_VERSION
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "ArcadeLink.Users.Api.dll"]
