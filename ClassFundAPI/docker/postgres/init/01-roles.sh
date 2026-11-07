#!/bin/sh
# Chạy một lần khi khởi tạo volume. Hai tài khoản theo docs (TD-01 → TD-31):
#   migrator_user – sở hữu schema, chạy migration (DDL);
#   app_user      – chỉ SELECT / INSERT / UPDATE, không DDL nên không tắt được trigger TR_NoDelete / TR_AppendOnly.
set -eu

psql -v ON_ERROR_STOP=1 \
  -v migrator_password="$MIGRATOR_DB_PASSWORD" \
  -v app_password="$APP_DB_PASSWORD" \
  -v db_name="$POSTGRES_DB" \
  --username "$POSTGRES_USER" --dbname "$POSTGRES_DB" <<'SQL'
CREATE ROLE migrator_user LOGIN PASSWORD :'migrator_password';
CREATE ROLE app_user LOGIN PASSWORD :'app_password';

REVOKE ALL ON DATABASE :"db_name" FROM PUBLIC;
GRANT CONNECT ON DATABASE :"db_name" TO migrator_user, app_user;

REVOKE CREATE ON SCHEMA public FROM PUBLIC;
ALTER SCHEMA public OWNER TO migrator_user;
GRANT USAGE ON SCHEMA public TO app_user;

ALTER DEFAULT PRIVILEGES FOR ROLE migrator_user IN SCHEMA public
  GRANT SELECT, INSERT, UPDATE ON TABLES TO app_user;
ALTER DEFAULT PRIVILEGES FOR ROLE migrator_user IN SCHEMA public
  GRANT USAGE, SELECT ON SEQUENCES TO app_user;

-- Bảng lịch sử migration (cấu trúc của EF Core) tạo sẵn để app_user không đọc / sửa được.
SET ROLE migrator_user;
CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
  "MigrationId" character varying(150) NOT NULL,
  "ProductVersion" character varying(32) NOT NULL,
  CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
);
REVOKE ALL ON TABLE "__EFMigrationsHistory" FROM app_user;
RESET ROLE;
SQL
