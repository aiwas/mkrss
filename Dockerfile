# 依存の復元とビルド。
# csproj だけを先に入れて復元することで、ソースの変更で復元をやり直さずに済ませる。
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Build.props Directory.Packages.props         ./
COPY global.json                                            ./
COPY src/MkRss.Core/MkRss.Core.csproj                       src/MkRss.Core/
COPY src/MkRss.Infrastructure/MkRss.Infrastructure.csproj   src/MkRss.Infrastructure/
COPY src/MkRss.Web/MkRss.Web.csproj                         src/MkRss.Web/
RUN dotnet restore src/MkRss.Web/MkRss.Web.csproj

COPY src/ src/
RUN dotnet publish src/MkRss.Web/MkRss.Web.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app

COPY --from=build /app ./

# 8080 で待ち受ける。外部への公開はリバースプロキシが担当する。
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

# イメージが用意する非rootユーザーで動かす。
USER $APP_UID

ENTRYPOINT ["dotnet", "MkRss.Web.dll"]
