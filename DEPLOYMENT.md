# Deploy TetGift monorepo lên VPS

Cập nhật: 08/10/2026.

Repository dùng GitHub Actions self-hosted runner đã cài trên VPS. Runner checkout source, build hai Docker image ngay trên VPS và chạy Docker Compose cục bộ. Nginx của VPS nhận request theo domain; Cloudflare Tunnel chỉ công bố domain HTTPS ra Internet.

## 1. Kiến trúc production

- Cloudflare Tunnel: `tetgift.khaifrost.com` đến `http://192.168.10.109`.
- Nginx: nhận domain TetGift trên cổng `80`.
- Frontend container: bind `127.0.0.1:3001` đến cổng `80` của container.
- Backend container: bind `127.0.0.1:10000` đến cổng `10000` của container.
- Nginx chuyển `/api`, `/hubs`, `/health`, `/swagger` và `/uploads` sang backend; các path còn lại sang frontend.
- Urban Service tiếp tục dùng cấu hình và container riêng, không bị TetGift thay thế.

## 2. Supabase

Dùng project `TestGift_DB`, project ref `odevqyiajlnsaumbcjpd`. Backend kết nối bằng Session pooler cổng `5432` và tự chạy EF Core migrations khi `Database__AutoMigrate=true`.

Connection string Npgsql:

~~~text
Host=SESSION_POOLER_HOST;Port=5432;Database=postgres;Username=postgres.odevqyiajlnsaumbcjpd;Password=DATABASE_PASSWORD;SSL Mode=Require;Trust Server Certificate=true;Maximum Pool Size=20
~~~

Không commit connection string hoặc mật khẩu database.

## 3. GitHub self-hosted runner

Workflow `.github/workflows/deploy-vps.yml` yêu cầu runner có các label:

- `self-hosted`
- `Linux`
- `X64`

Runner phải chạy bằng user có quyền Docker và quyền ghi vào `/home/vietanh/tetgift`. Không cần mở SSH ra Internet, không cần GHCR và không cần deploy key.

## 4. GitHub Environment secret

Trong repository, tạo Environment `production`. Thêm một Environment secret:

- `VPS_ENV_FILE`: toàn bộ nội dung file `.env.production`.

Workflow không in nội dung secret. Trên VPS, secret được chuẩn hóa về LF rồi ghi vào `/home/vietanh/tetgift/.env` với quyền `600`.

Các giá trị bắt buộc trong secret:

- `WEB_DOMAIN`
- `ConnectionStrings__DefaultConnection`
- `Jwt__Key`
- `Otp__Secret`

Cloudinary là tùy chọn. Nếu bật upload media, phải cấu hình đủ `CloudinarySettings__CloudName`, `CloudinarySettings__ApiKey` và `CloudinarySettings__ApiSecret`; không được chỉ cấu hình một phần.

Production không tự động thay đổi schema khi container khởi động. Áp dụng EF migrations có kiểm soát trước khi deploy phiên bản cần schema mới; smoke test API sẽ chặn pipeline nếu schema chưa sẵn sàng.

Lần deploy đầu nên có thêm `Seed__AdminUsername`, `Seed__AdminEmail` và `Seed__AdminPassword`. Sau khi admin đã được tạo, có thể xóa `Seed__AdminPassword` khỏi secret và chạy workflow lại.

## 5. Pipeline deploy

Push branch `main` hoặc chạy `Actions > CI and Deploy TetGift Fullstack > Run workflow`. Pipeline sẽ:

1. Checkout source trên self-hosted runner.
2. Kiểm tra `VPS_ENV_FILE` và domain production.
3. Build image `tetgift-api:latest` từ `BE/Dockerfile`.
4. Build image `tetgift-web:latest` từ `FE/Dockerfile`.
5. Ghi environment và Compose vào `/home/vietanh/tetgift`.
6. Chạy hai container bằng Docker Compose.
7. Kiểm tra backend và frontend thông qua Nginx cục bộ.
8. Kiểm tra các API public `/api/configs`, `/api/products` và `/api/inventories/stocks`.
9. Hiển thị trạng thái container và xóa image dangling.

Pipeline không purge Cloudflare cache.

## 6. Cloudflare Tunnel

Chỉ tạo Published application sau khi deploy local thành công:

~~~text
Hostname: tetgift.khaifrost.com
Service:  http://192.168.10.109
~~~

Không cần hostname API riêng vì Nginx xử lý backend theo path.

## 7. Kiểm tra trên VPS

~~~bash
curl -f -H "Host: tetgift.khaifrost.com" http://127.0.0.1/
curl -f -H "Host: tetgift.khaifrost.com" http://127.0.0.1/health/ready

cd /home/vietanh/tetgift
docker compose --env-file .env --env-file image.env -f docker-compose.prod.yml ps
docker compose --env-file .env --env-file image.env -f docker-compose.prod.yml logs -f --tail=200
~~~

Sau khi Cloudflare route hoạt động:

~~~bash
curl -f https://tetgift.khaifrost.com/
curl -f https://tetgift.khaifrost.com/health/ready
~~~

## 8. Bảo mật

- Không commit `.env.production` hoặc `.env`.
- Không công khai cổng `3001` hoặc `10000`.
- Không dùng Supabase service-role key trong frontend.
- Sau khi production ổn định, đặt `Swagger__Enabled=false`.
- Thu hồi ngay mọi secret từng bị commit hoặc xuất hiện trong log.
