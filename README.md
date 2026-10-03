# Steam Fix VN 1.2 — DNS IPv4 + DoH/hosts + GoodbyeDPI

Tool Windows 10/11 **64-bit**, giao diện tiếng Việt. Chạy `dist/SteamFixVN.exe`, không cần cài .NET. Gói đầy đủ: `releases/SteamFixVN-1.2.zip`.

**[Tải bản v1.2 trên GitHub Releases](https://github.com/JunnDung/SteamFixVN/releases/tag/v1.2)** — ưu tiên gói ZIP có hướng dẫn, giấy phép và SHA-256. Giải nén và chạy `SteamFixVN.exe`.

## Sử dụng

1. Lưu game đang chơi trước khi dùng.
2. Chọn DNS IPv4: Google `8.8.8.8 / 8.8.4.4`, Cloudflare `1.1.1.1 / 1.0.0.1`, hoặc giữ DNS hiện tại. Mặc định Google để thử trên Viettel/Windows 11 theo bối cảnh hiện tại; không bảo đảm mọi kết nối Viettel.
3. Bấm **Apply**, chấp nhận UAC. Tool mở lại nâng quyền, giữ lựa chọn DNS/DPI và tự tiếp tục.
4. Đợi tool thử từng bước. Nếu DNS/hosts đã giúp truy cập HTTPS được, không nạp driver DPI. Nếu vẫn lỗi và tùy chọn DPI được chọn, tool thử hai cấu hình GoodbyeDPI.
5. Kiểm tra Store/Community trong Steam sau khi tool mở lại Steam. Nếu nhật ký cho biết **DPI đang bật**, giữ tool mở; đóng tool dừng DPI. DNS/hosts vẫn giữ đến khi Khôi phục.

DNS IPv4 đổi trên Wi-Fi/Ethernet vật lý đang có default route tốt nhất; ảnh hưởng các ứng dụng sử dụng card đó. Tool không tự sửa adapter VPN hoặc tất cả card mạng. Không tìm thấy card phù hợp thì báo lỗi trước khi đổi DNS. Khi preset đã giống cấu hình đang có, tool bỏ qua lệnh đổi DNS.

**Khôi phục** dừng engine do tool tạo, trả DNS IPv4 về giá trị gốc (hoặc DHCP nếu trước đó tự động), gỡ riêng vùng SteamFixVN trong hosts và xóa DNS cache. Thoát/mở lại Steam nếu cần. Không ghi đè DNS đã được sửa khác ngoài tool; sao lưu được giữ để kiểm tra/thử lại. Nếu một phần khôi phục thất bại, tool vẫn thử các phần còn lại rồi báo lỗi.

**Kiểm tra** đọc adapter/DNS IPv4/IPv6, thử HTTPS qua Windows và qua IP DoH. Không nạp driver hoặc thay cấu hình mạng. Nhật ký kiểm tra không có nghĩa Apply đã được thực hiện.

## Luồng Apply

1. Kiểm tra hosts hợp lệ, lưu trạng thái trước lần Apply. Nếu chọn đổi DNS, sao lưu DNS gốc trước lệnh đầu tiên: GUID, chế độ tự động/thủ công, danh sách DNS cũ. Dùng netsh **ipv4 dnsservers**, giữ nguyên IPv6/IP/gateway.
2. Gỡ vùng hosts cũ của chính tool để thử DNS mới có ý nghĩa; xóa DNS cache; kiểm tra HTTPS Store, Community và Help.
3. Nếu chưa đạt, lấy IP IPv4 mới qua DoH Cloudflare, dự phòng Google, thử HTTPS trực tiếp với đúng hostname/SNI và chứng chỉ TLS. Sao lưu hosts nguyên byte trước khi cập nhật vùng riêng, rồi kiểm tra qua Windows lần nữa.
4. Nếu vẫn chưa đạt và cho phép DPI, giải nén engine **GoodbyeDPI 0.2.2 x64** chính thức cùng DLL/driver WinDivert. Kiểm tra SHA-256 gói nhúng, bảo vệ quyền ghi thư mục executable nâng quyền, rồi thử cấu hình 1: `-f 2 -e 2 --native-frag --max-payload=1200`; cấu hình 2: `-6`.
5. Cả hai cấu hình dùng `--blacklist steam-domains.txt` chứa `steampowered.com`, `steamcommunity.com`, `steamstatic.com`. Mỗi cấu hình chờ khoảng 22 giây khởi tạo cộng thời gian thử mạng. Dừng cấu hình hiện tại trước khi thử tiếp.
6. Nếu ba trang chính đạt, yêu cầu Steam thoát nhẹ rồi mở lại Store. Không ép tắt nếu Steam chưa chịu thoát. Nếu mọi cách được chọn thất bại, dừng DPI và cố trả DNS/hosts về trước lần Apply, bảo vệ sửa đổi đồng thời.

DNS gốc được giữ qua nhiều lần Apply và qua việc đóng/mở tool, đến khi Khôi phục. Journal được ghi trước lệnh để hỗ trợ khôi phục nếu chương trình bị gián đoạn giữa hai lệnh đặt DNS. GUID giúp tránh nhầm adapter khi Windows đổi/recycle interface index.

Login, Checkout và bốn CDN là mục bổ sung. Lỗi DNS của mục phụ được ghi rõ và bỏ qua. CDN gốc trả 403/404 chỉ chứng minh TLS có kết nối, không chứng minh mọi tài nguyên tải được. Trang chính cần HTTP 2xx/3xx.

## Phạm vi

Tool xử lý một số lỗi DNS và thử vượt một số kiểu DPI/SNI. **Không phải VPN, không vượt chặn IP thuần túy và không bảo đảm mọi lỗi -7.** Phép thử HTTPS không xác nhận đăng nhập, tải game, multiplayer hoặc Steam client đã hoạt động.

Đổi DNS IPv4 đơn thuần không bảo đảm DoH của Windows được bật. DoH trong bước hosts là truy vấn của riêng tool. IPv6 được giữ nguyên; nếu lỗi liên quan resolver/tuyến IPv6 thì chỉ đổi IPv4 có thể chưa đủ. Không tự tắt IPv6.

Phạm vi GoodbyeDPI theo **HTTP Host/TLS SNI**, không theo tiến trình Steam: truy cập tên miền Steam từ trình duyệt cũng được xử lý. WinDivert vẫn đi qua các gói TCP 80/443 để engine quyết định xử lý/gửi lại. Không bật chặn RST toàn máy, chặn QUIC toàn máy hay DNS redirection của GoodbyeDPI.

Không tắt firewall/antivirus, không sửa proxy, không xóa game/tài khoản/cache hoặc đổi vùng tài khoản Steam. Không cài dịch vụ tự khởi động SteamFixVN. WinDivert nạp driver theo nhu cầu bằng quyền quản trị; driver/đăng ký dùng chung có thể còn đến khi khởi động lại. Khôi phục không xóa dịch vụ driver dùng chung của ứng dụng khác.

Engine thuộc Windows Job Object: đóng/crash tool sẽ kết thúc tiến trình engine do tool tạo; không tìm/tắt bản GoodbyeDPI khác. Nếu engine dừng ngoài dự kiến, UI báo để Apply lại. IP CDN có thể đổi: Apply lại cập nhật hoặc Khôi phục để trở về phân giải thường.

## File

- Hosts: `%SystemRoot%\System32\drivers\etc\hosts`.
- Sao lưu hosts: `%ProgramData%\SteamFixVN\backups`.
- DNS gốc/journal: `%ProgramData%\SteamFixVN\dns-original-<guid>.json`; không xóa khi đang cần khôi phục.
- Nhật ký: `%ProgramData%\SteamFixVN\latest.log`.
- Engine: thư mục riêng `engine-0.2.2-<id>` dưới dữ liệu, tạo khi thực sự cần DPI; giấy phép được giải nén cùng engine.
- Gói phát hành có `licenses`, `third-party-sources`, `THIRD-PARTY-NOTICES.md`, `SHA256.txt` và báo cáo nghiên cứu.

## Kiểm chứng ngày 03/10/2026

- **51 kiểm tra đạt**: sửa/khôi phục hosts nguyên byte, cập nhật IP, bảo vệ sửa đổi đồng thời; chế độ DHCP/thủ công, DNS riêng nhiều địa chỉ, IPv4-only, adapter GUID, từ chối đầu vào chèn lệnh, journal/khôi phục sau gián đoạn, bảo vệ thay đổi DNS bên ngoài; engine/giấy phép/arguments; kill tiến trình giả khi dừng/crash.
- Build Release, render giao diện và khởi động EXE đã được kiểm tra.
- Kiểm tra chỉ đọc adapter và truy cập lại bằng GUID đạt. Máy thử đã đặt Google DNS cho cả IPv4 và IPv6. HTTPS theo Windows: Store/Community chưa đạt, Help đạt. HTTPS trực tiếp theo IP từ DoH: cả 9 hostname đạt. Nhật ký mạng riêng không được đưa vào repo hoặc gói phát hành.
- **Chưa thực hiện lệnh đổi/khôi phục DNS trên adapter thật, chưa Apply hosts thật, chưa nạp driver thật, chưa xác nhận Steam client hết lỗi.** Kiểm tra nội bộ và chỉ đọc không thay thế kiểm chứng toàn bộ Apply/Khôi phục trên kết nối người dùng.
- Windows antivirus đã chặn archive WinDivert-1.4.3-A đầy đủ tải riêng khi đối chiếu bản 1.1; không tắt bảo vệ hay thêm ngoại lệ. Engine nhúng lấy từ release GoodbyeDPI chính thức tải trước đó; chữ ký driver được báo Valid, nhưng không bảo đảm Windows/antivirus cho nạp. Nếu bị chặn khi Apply, tool báo lỗi và cố khôi phục.

EXE SteamFixVN chưa ký bằng chứng chỉ phát hành thương mại, có thể hiện nhà phát hành không xác định. SHA-256 ở dist để đối chiếu bản này.

## Nghiên cứu và build

Xem `RESEARCH-STEAM-VN.md` để biết nguồn, phân biệt tin 2024 và báo cáo tháng 10/2026, cách đổi DNS thủ công và các hướng sửa lỗi tải game/cache/firewall theo Steam Support.

Cần .NET SDK 8 trên Windows và Internet lần đầu:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1
```

Script chạy kiểm tra, đóng gói EXE tự chứa runtime rồi chép giấy phép/mã nguồn dependency và hướng dẫn vào dist. Cache nằm trong workspace. Mã: `src/DnsSettings.cs`, `src/FixEngine.cs`, `src/DpiRuntime.cs`, `src/Program.cs`.
