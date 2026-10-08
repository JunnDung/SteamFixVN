# Steam Fix VN 1.3.3 — DNS IPv4 + DoH/hosts + GoodbyeDPI

## Sửa lựa chọn DPI cho Checkout trên kết nối FPT đã kiểm tra

Ngày 08/10/2026, ở cấu hình 1/6 của bản 1.3.2, phép thử hệ thống tới `https://checkout.steampowered.com/checkout/?accountcart=1` chuyển tới login thành công, nhưng trình duyệt vẫn báo `ERR_CONNECTION_RESET`. Sau khi chuyển tới cấu hình 3/6 **Phân mảnh đảo — payload lớn**, trình duyệt tải được trang đăng nhập của Checkout; người dùng xác nhận Checkout trong Steam mở được và đã thanh toán thành công. Đây là báo cáo trên kết nối FPT này, không bảo đảm mọi đường mạng.

Bản 1.3.3 ưu tiên cấu hình đã giúp mở Checkout đó ở vị trí **1/6**, thay vì dừng ở phân mảnh thuận/payload 1200 chỉ đạt phép thử hệ thống. Check/Apply và chọn IP kiểm tra URL có `accountcart=1`. Engine vẫn là GoodbyeDPI 0.2.2 chính thức, phạm vi hostname Steam giữ nguyên. Không hạ bảo mật TLS hoặc sửa cookie/phiên đăng nhập.

Nếu bản 1.3.2 đang mở Checkout được ở 3/6, có thể tiếp tục dùng và giữ tool mở. Khi chuyển bản: lưu game, đóng tool cũ, chạy `dist/1.3.3/SteamFixVN.exe`, bấm **Apply**, chấp nhận UAC và đợi Steam mở lại. Khi cần DPI, nhật ký sẽ bắt đầu ở **1/6 — Phân mảnh đảo — payload lớn**. Nếu web đạt nhưng Steam vẫn lỗi, **Thử DPI tiếp** ép thử từng cấu hình; cấu hình thất bại dừng engine và cố trả DNS/hosts về trước lần thử. Sau cấu hình 6 sẽ quay về 1.

87 kiểm tra tự động đạt, gồm thứ tự cấu hình đã kiểm chứng, URL account-cart, chọn đúng cấu hình thủ công và rollback. Repo chứa mã nguồn 1.3.3; có thể tự build theo hướng dẫn bên dưới. Release 1.3.3 gồm EXE, ZIP đầy đủ giấy phép/mã nguồn dependency và SHA-256. Báo cáo thanh toán thành công do người dùng cung cấp; phép thử tự động không đăng nhập hoặc tạo giao dịch.

## Sửa kiểm tra Checkout trong 1.3.1

Bản 1.3.1 thêm Store `/cart/` và Checkout `/checkout/` vào các phép thử bắt buộc của Check/Apply và khi chọn IP DoH. Checkout lỗi không còn bị bỏ qua như mục phụ. 81 kiểm tra tự động đạt ở bản này; các thay đổi được giữ trong 1.3.3.

Đóng cửa sổ tool cũ trước khi chạy bản mới, lưu game rồi Apply và chấp nhận UAC. Sau đó mở lại trang Checkout. Phép thử không đăng nhập, không tạo giao dịch; `/checkout/` chuyển tới login chỉ chứng minh đường kết nối đó hoạt động. Nếu vẫn -101/-105 trong phiên đăng nhập, gửi tên miền/đường dẫn lỗi đã bỏ token, mã giao dịch và dữ liệu thẻ. Chưa xác nhận sửa được giao dịch thực tế trên FPT.

Tool Windows 10/11 **64-bit**, giao diện tiếng Việt. Giải nén gói ZIP và chạy `SteamFixVN.exe`, không cần cài .NET.

**[Tải bản v1.3.3 trên GitHub Releases](https://github.com/JunnDung/SteamFixVN/releases/tag/v1.3.3)** — ưu tiên gói ZIP có hướng dẫn, giấy phép và SHA-256. Giải nén và chạy `SteamFixVN.exe`.

## Bản 1.3: áp dụng nghiên cứu DNS/DPI

Bản 1.3 gồm EXE tự chứa runtime, hướng dẫn, báo cáo nghiên cứu và giấy phép dependency. Xem [REPO-RESEARCH.md](REPO-RESEARCH.md) để đối chiếu tám repo, phương pháp áp dụng và phương pháp chưa tích hợp.

- Sáu cấu hình DPI có tên: phân mảnh thuận/đảo, payload lớn, fake sequence/checksum và TCP window. Tự thử tuần tự, dừng cấu hình chưa đạt, giữ cấu hình đạt.
- Nếu IP hiện tại vẫn lỗi sau khi bật DPI, thử lại các IP DoH với DPI đang chạy, cập nhật hosts và kiểm chứng qua Windows.
- Cloudflare/Google DoH chạy song song; một resolver lỗi vẫn giữ kết quả từ resolver kia. Check bổ sung Community TLS 1.2 và phân loại lỗi kết nối.
- 79 kiểm tra tự động đạt. Bộ orchestration dùng engine giả; chưa kiểm chứng sáu cấu hình driver thật hoặc Apply trên FPT. Có thể mất vài phút hoặc lâu hơn khi nhiều IP timeout.

## Store vào được nhưng Community/Profile lỗi trên FPT

Mã -105 là không phân giải được tên miền; -101 là kết nối bị reset, theo [Chromium](https://chromium.googlesource.com/chromium/src/+/main/net/base/net_error_list.h). Các mã này không chứng minh nguyên nhân reset hoặc một kiểu chặn cố định.

Bản 1.2.1 thử cả trang gốc Community, `/discussions/` và `/my/`; theo tối đa 5 chuyển hướng HTTPS trong danh sách hostname Steam tới phản hồi thành công. Không coi HTTP 302, 304 hoặc trang lỗi 403/503 là Community hoạt động. Khi chọn IP cho hosts, cùng các phép thử này phải đạt; truy vấn cả Cloudflare và Google DoH để có thêm IP ứng viên. `/my/` chưa đăng nhập chỉ kiểm tra đường tới trang đăng nhập, không xác nhận profile riêng tư.

Trên máy FPT gặp lỗi: lưu game, đóng bản tool cũ, chạy bản mới, chọn Google hoặc Cloudflare, bật tùy chọn GoodbyeDPI và bấm **Apply**. Sau đó thử Community và Profile trong Steam lẫn trình duyệt. Giữ tool mở nếu nhật ký báo DPI đang bật. Nếu chưa được, bấm **Kiểm tra** và gửi các dòng `steamcommunity.com` cùng lỗi DPI. Nếu tool báo thành công nhưng Steam vẫn lỗi, thoát hẳn Steam và mở lại. Không cần xóa game hoặc đổi vùng tài khoản.

Kiểm tra ngày 04/10/2026: 60 kiểm tra tự động đạt, gồm hồi quy chuyển hướng Profile sang trang lỗi và vòng lặp chuyển hướng. Các URL thử đã đạt trên kết nối máy phát triển qua Windows và IP DoH. **Chưa kiểm chứng Apply/DPI trên kết nối FPT của người báo lỗi; không bảo đảm hết -101/-105.**

## Sử dụng

1. Lưu game đang chơi trước khi dùng.
2. Chọn DNS IPv4: Google `8.8.8.8 / 8.8.4.4`, Cloudflare `1.1.1.1 / 1.0.0.1`, hoặc giữ DNS hiện tại. Mặc định Google để thử trên Viettel/Windows 11 theo bối cảnh hiện tại; không bảo đảm mọi kết nối Viettel.
3. Bấm **Apply**, chấp nhận UAC. Tool mở lại nâng quyền, giữ lựa chọn DNS/DPI và tự tiếp tục.
4. Đợi tool thử từng bước. Nếu DNS/hosts đã giúp truy cập HTTPS được, không nạp driver DPI. Nếu vẫn lỗi và tùy chọn DPI được chọn, tool thử tối đa sáu cấu hình GoodbyeDPI.
5. Kiểm tra Store/Checkout/Community trong Steam sau khi tool mở lại Steam. Nếu nhật ký cho biết **DPI đang bật**, giữ tool mở; đóng tool dừng DPI. DNS/hosts vẫn giữ đến khi Khôi phục. Nếu web đạt nhưng Steam vẫn lỗi, dùng **Thử DPI tiếp** theo hướng dẫn bên trên.

DNS IPv4 đổi trên Wi-Fi/Ethernet vật lý đang có default route tốt nhất; ảnh hưởng các ứng dụng sử dụng card đó. Tool không tự sửa adapter VPN hoặc tất cả card mạng. Không tìm thấy card phù hợp thì báo lỗi trước khi đổi DNS. Khi preset đã giống cấu hình đang có, tool bỏ qua lệnh đổi DNS.

**Khôi phục** dừng engine do tool tạo, trả DNS IPv4 về giá trị gốc (hoặc DHCP nếu trước đó tự động), gỡ riêng vùng SteamFixVN trong hosts và xóa DNS cache. Thoát/mở lại Steam nếu cần. Không ghi đè DNS đã được sửa khác ngoài tool; sao lưu được giữ để kiểm tra/thử lại. Nếu một phần khôi phục thất bại, tool vẫn thử các phần còn lại rồi báo lỗi.

**Kiểm tra** đọc adapter/DNS IPv4/IPv6, thử HTTPS qua Windows và qua IP DoH. Không nạp driver hoặc thay cấu hình mạng. Nhật ký kiểm tra không có nghĩa Apply đã được thực hiện.

## Luồng Apply

1. Kiểm tra hosts hợp lệ, lưu trạng thái trước lần Apply. Nếu chọn đổi DNS, sao lưu DNS gốc trước lệnh đầu tiên: GUID, chế độ tự động/thủ công, danh sách DNS cũ. Dùng netsh **ipv4 dnsservers**, giữ nguyên IPv6/IP/gateway.
2. Gỡ vùng hosts cũ của chính tool để thử DNS mới có ý nghĩa; xóa DNS cache; kiểm tra HTTPS Store/cart, Checkout, Community và Help.
3. Nếu chưa đạt, lấy IP IPv4 mới qua cả DoH Cloudflare và Google (tối đa 3 IP mỗi resolver), thử HTTPS trực tiếp với đúng hostname/SNI và chứng chỉ TLS. Sao lưu hosts nguyên byte trước khi cập nhật vùng riêng, rồi kiểm tra qua Windows lần nữa.
4. Nếu vẫn chưa đạt và cho phép DPI, giải nén engine **GoodbyeDPI 0.2.2 x64** chính thức cùng DLL/driver WinDivert. Kiểm tra SHA-256 gói nhúng, bảo vệ quyền ghi thư mục executable nâng quyền, rồi thử tối đa sáu cấu hình trong `REPO-RESEARCH.md`. Nếu HTTPS chưa đạt, tìm lại IP kiểm chứng với DPI đang bật trước khi chuyển cấu hình.
5. Mọi cấu hình dùng `--blacklist steam-domains.txt` chứa `steampowered.com`, `steamcommunity.com`, `steamstatic.com`. Mỗi cấu hình chờ khoảng 22 giây khởi tạo cộng thời gian thử mạng. Dừng cấu hình hiện tại trước khi thử tiếp.
6. Nếu các phép thử bắt buộc đạt, yêu cầu Steam thoát nhẹ rồi mở lại Store. Không ép tắt nếu Steam chưa chịu thoát. Nếu mọi cách được chọn thất bại, dừng DPI và cố trả DNS/hosts về trước lần Apply, bảo vệ sửa đổi đồng thời.

DNS gốc được giữ qua nhiều lần Apply và qua việc đóng/mở tool, đến khi Khôi phục. Journal được ghi trước lệnh để hỗ trợ khôi phục nếu chương trình bị gián đoạn giữa hai lệnh đặt DNS. GUID giúp tránh nhầm adapter khi Windows đổi/recycle interface index.

Checkout là mục bắt buộc. Login và bốn CDN là mục bổ sung khi chọn IP, nhưng chuyển hướng Checkout tới Login vẫn phải truy cập thành công. Lỗi DNS của mục phụ được ghi rõ và bỏ qua. CDN gốc trả 403/404 chỉ chứng minh TLS có kết nối, không chứng minh mọi tài nguyên tải được. Trang chính cần HTTP 2xx sau khi theo chuyển hướng HTTPS trong danh sách hostname Steam cho phép.

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

## Giấy phép

Mã nguồn SteamFixVN được phát hành theo [Apache License 2.0](LICENSE). GoodbyeDPI và WinDivert cùng các dependency giữ giấy phép riêng, xem [THIRD-PARTY-NOTICES.md](vendor/THIRD-PARTY-NOTICES.md) và các văn bản trong `vendor/engine/licenses`.
