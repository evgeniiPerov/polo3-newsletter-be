# Build stage: restore and publish the web project with the .NET 10 SDK.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY global.json Directory.Build.props .editorconfig ./
COPY src/Polo3.Newsletter.Web/Polo3.Newsletter.Web.csproj src/Polo3.Newsletter.Web/
RUN dotnet restore src/Polo3.Newsletter.Web/Polo3.Newsletter.Web.csproj
COPY src/ src/
RUN dotnet publish src/Polo3.Newsletter.Web/Polo3.Newsletter.Web.csproj -c Release -o /app --no-restore

# Runtime stage: ASP.NET Core runtime only, running as the image's non-root "app" user.
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
USER $APP_UID
ENV Urls=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "Polo3.Newsletter.Web.dll"]
