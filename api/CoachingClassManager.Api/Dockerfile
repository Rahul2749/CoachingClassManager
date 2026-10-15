FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source
COPY api/CoachingClassManager.Api/*.csproj api/CoachingClassManager.Api/packages.lock.json api/CoachingClassManager.Api/
RUN dotnet restore api/CoachingClassManager.Api --locked-mode
COPY api/CoachingClassManager.Api/ api/CoachingClassManager.Api/
RUN dotnet publish api/CoachingClassManager.Api --no-restore -c Release -o /out

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /out .
USER $APP_UID
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "CoachingClassManager.Api.dll"]
