-- Chạy MỘT LẦN bởi người quản trị Neon (owner của project), kết nối bằng tài khoản quản trị, trên database dùng chung.
-- Thay <...> bằng mật khẩu mạnh; KHÔNG commit mật khẩu thật. Xem docs/implementation/neon-database-guide.md.
--   migrator_user: sở hữu schema, chạy EF Core migration (DDL).
--   app_user     : Backend lúc chạy; chỉ SELECT/INSERT/UPDATE, không DELETE, không DDL
--                  (nên không tắt được trigger append-only của AuditLog / LedgerEntry).

CREATE ROLE migrator_user LOGIN PASSWORD '<migrator-password>';
CREATE ROLE app_user LOGIN PASSWORD '<app-password>';

REVOKE CREATE ON SCHEMA public FROM PUBLIC;
ALTER SCHEMA public OWNER TO migrator_user;
GRANT USAGE ON SCHEMA public TO app_user;

ALTER DEFAULT PRIVILEGES FOR ROLE migrator_user IN SCHEMA public
  GRANT SELECT, INSERT, UPDATE ON TABLES TO app_user;
ALTER DEFAULT PRIVILEGES FOR ROLE migrator_user IN SCHEMA public
  GRANT USAGE, SELECT ON SEQUENCES TO app_user;

-- Bảng lịch sử migration do migrator_user tạo; app_user không đọc/sửa được.
SET ROLE migrator_user;
CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
  "MigrationId" character varying(150) NOT NULL,
  "ProductVersion" character varying(32) NOT NULL,
  CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
);
REVOKE ALL ON TABLE "__EFMigrationsHistory" FROM app_user;
RESET ROLE;
