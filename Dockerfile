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

# 待ち受けるポートはアプリの設定(MkRss:PublicPort / MkRss:AdminPort)で決める。
# 8080 は公開用でリバースプロキシから転送する。9000 は管理用で外部に公開しない。
# ベースイメージが設定する ASPNETCORE_HTTP_PORTS は使わないため空にする。
ENV ASPNETCORE_HTTP_PORTS=
EXPOSE 8080 9000

# イメージが用意する非rootユーザーで動かす。
USER $APP_UID

ENTRYPOINT ["dotnet", "MkRss.Web.dll"]
