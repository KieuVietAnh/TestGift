# Deploy TetGift monorepo lên VPS

Cập nhật: 08/10/2026.

Repository gồm BE và FE. GitHub Actions build hai Docker image, push lên GHCR, SSH vào VPS và chạy Docker Compose. Caddy phục vụ một domain: frontend ở /, backend ở /api, /hubs, /health, /swagger và /uploads.

## 1. Cấu trúc repository

- BE/: ASP.NET Core API, EF Core migrations và Dockerfile backend.
- FE/: React/Vite, Dockerfile frontend và Nginx SPA fallback.
- .github/workflows/deploy-vps.yml: build và deploy cả hai image.
- docker-compose.prod.yml và Caddyfile: production stack trên VPS.

## 2. Chuẩn bị domain, VPS và Supabase

- VPS Ubuntu 22.04/24.04, tối thiểu 2 GB RAM vì backend dùng Chromium để tạo PDF.
- Tạo DNS A record của gift.example.com trỏ đến IPv4 VPS.
- Tạo Supabase project. Nếu VPS chỉ có IPv4, chọn Connect > Session pooler, port 5432. Nếu VPS có IPv6 ổn định, có thể dùng Direct connection.
- Connection string Npgsql mẫu:

~~~text
Host=POOLER_HOST;Port=5432;Database=postgres;Username=postgres.PROJECT_REF;Password=DATABASE_PASSWORD;SSL Mode=Require;Trust Server Certificate=true;Maximum Pool Size=20
~~~

Không cần Supabase anon key hoặc service-role key vì backend kết nối PostgreSQL trực tiếp.

## 3. Cài Docker trên VPS

~~~bash
sudo apt update
sudo apt install -y ca-certificates curl
curl -fsSL https://get.docker.com | sudo sh
sudo usermod -aG docker "$USER"
mkdir -p ~/tetgift
~~~

Đăng xuất rồi SSH lại. Mở firewall cho cổng SSH thực tế, 80/TCP, 443/TCP và 443/UDP trước khi bật UFW.

## 4. Tạo ~/tetgift/.env trên VPS

Sao chép nội dung .env.example, thay toàn bộ giá trị mẫu rồi chạy:

~~~bash
chmod 600 ~/tetgift/.env
~~~

Giá trị quan trọng:

- WEB_DOMAIN: hostname, không có https:// và không có dấu / cuối.
- ConnectionStrings__DefaultConnection: Supabase connection string.
- Cors__AllowedOrigins và AppUrls__FrontendBaseUrl: https://WEB_DOMAIN.
- Jwt__Key và Otp__Secret: hai chuỗi ngẫu nhiên dài, khác nhau.
- Seed__AdminUsername, Seed__AdminEmail, Seed__AdminPassword: chỉ cần cho lần deploy đầu.
- Resend/Cloudinary cần cho OTP và upload; Gemini/VNPAY bổ sung khi bật tính năng.

Frontend dùng same-origin /api và /hubs/chat nên không cần lưu Vite environment variables trên VPS.

## 5. Tạo SSH deploy key

Trên PowerShell:

~~~powershell
ssh-keygen.exe -t ed25519 -f .\tetgift-github-actions -C github-actions-tetgift
Get-Content .\tetgift-github-actions.pub | ssh VPS_USER@VPS_HOST "umask 077; mkdir -p ~/.ssh; cat >> ~/.ssh/authorized_keys"
ssh-keyscan.exe -H -p 22 VPS_HOST
~~~

## 6. Thêm GitHub Actions secrets

Trong Settings > Secrets and variables > Actions, tạo:

- VPS_SSH_HOST
- VPS_SSH_PORT
- VPS_SSH_USER
- VPS_SSH_PRIVATE_KEY: toàn bộ nội dung private key.
- VPS_KNOWN_HOSTS: output của ssh-keyscan.

## 7. Deploy

Push branch main hoặc chạy Actions > Deploy VPS > Run workflow. Pipeline sẽ:

1. Build BE/ và FE/ độc lập.
2. Push hai image theo commit SHA lên GHCR.
3. Copy Compose và Caddyfile lên VPS.
4. Pull đúng hai image, chạy migration, khởi động FE/BE và cấp HTTPS.
5. Kiểm tra cả trang chủ và /health/ready.

## 8. Kiểm tra

~~~bash
curl -f https://gift.example.com/
curl -f https://gift.example.com/health/ready
cd ~/tetgift
docker compose --env-file .env --env-file image.env -f docker-compose.prod.yml ps
docker compose --env-file .env --env-file image.env -f docker-compose.prod.yml logs -f --tail=200
~~~

Sau lần deploy đầu, xóa Seed__AdminPassword khỏi ~/tetgift/.env rồi chạy workflow lại. Sau khi tích hợp ổn định, đặt Swagger__Enabled=false.

## 9. Secrets cũ

Các khóa từng nằm trong appsettings.json và lịch sử Git phải được thu hồi hoặc tạo lại: database password, Resend, Cloudinary, Gemini, VNPAY và Redis.
