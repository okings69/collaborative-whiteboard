FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY CollaborativeBoard.csproj ./
RUN dotnet restore CollaborativeBoard.csproj

COPY . ./
RUN dotnet publish CollaborativeBoard.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_ENVIRONMENT=Production
COPY --from=build /app/publish ./
ENTRYPOINT ["dotnet", "CollaborativeBoard.dll"]

