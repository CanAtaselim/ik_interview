# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Önce yalnızca proje dosyaları: kaynak değişse de paket geri yükleme katmanı önbellekte kalır.
COPY Directory.Build.props ./
COPY src/Recruitment.Domain/Recruitment.Domain.csproj src/Recruitment.Domain/
COPY src/Recruitment.Application/Recruitment.Application.csproj src/Recruitment.Application/
COPY src/Recruitment.Infrastructure/Recruitment.Infrastructure.csproj src/Recruitment.Infrastructure/
COPY src/Recruitment.Api/Recruitment.Api.csproj src/Recruitment.Api/
RUN dotnet restore src/Recruitment.Api/Recruitment.Api.csproj

COPY src/ src/
RUN dotnet publish src/Recruitment.Api/Recruitment.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app

# libldap: System.DirectoryServices.Protocols gerçek LDAP bağlantısında buna ihtiyaç duyar.
# libgssapi-krb5: PostgreSQL sürücüsü bağlanırken yoklar; yoksa her açılışta uyarı basar.
RUN apt-get update \
    && apt-get install -y --no-install-recommends libldap2 libgssapi-krb5-2 \
    && rm -rf /var/lib/apt/lists/* \
    && mkdir -p logs storage \
    && chown -R "$APP_UID" logs storage

COPY --from=build /app .

USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "Recruitment.Api.dll"]
