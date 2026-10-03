# Steam tại Việt Nam và hướng sửa — kiểm tra ngày 03/10/2026

## Bổ sung ngày 04/10/2026: FPT, Store hoạt động nhưng Community/Profile lỗi

Người báo lỗi cho biết máy FPT gặp -101/-105; trình duyệt cũng không truy cập được `steamcommunity.com` trong khi Store hoạt động. Chưa có nhật ký Apply từ máy này. Theo [Chromium net_error_list.h](https://chromium.googlesource.com/chromium/src/+/main/net/base/net_error_list.h), -101 là CONNECTION_RESET và -105 là NAME_NOT_RESOLVED. Chưa xác định bên gây reset hoặc khẳng định DPI chỉ từ mã lỗi.

v1.2.1 sửa kiểm tra chỉ trang gốc và chấp nhận ngay HTTP chuyển hướng của v1.2. Tool thử thêm Community `/discussions/`, Profile `/my/` và theo chuyển hướng HTTPS Steam có giới hạn. Không đăng nhập trong phép thử; `/my/` chuyển tới login nên chưa chứng minh dữ liệu profile hoặc phiên Steam.

Kiểm tra chỉ đọc trên máy phát triển ngày 04/10 đạt Store, Community, Discussions, Profile→login và Help qua Windows; ba URL Community cũng đạt qua IP DoH, với TLS bình thường. Máy này là kết nối Viettel được người dùng xác nhận trước đó, không phải máy FPT của bạn họ. Không đổi DNS/hosts hoặc nạp driver trong lần đo. Vì vậy không thể dùng kết quả này để khẳng định đã sửa xong FPT.

## Tình trạng tìm được

VnExpress ngày 31/05/2024 đưa tin một số dịch vụ Steam bị chặn tại Việt Nam; đây là thông tin lịch sử, không phải phép đo toàn bộ ISP trong tháng 10/2026. [VnExpress](https://e.vnexpress.net/news/business/companies/gaming-platform-steam-blocked-in-vietnam-for-refusal-to-cooperate-with-authorities-4752391.html).

Báo cáo cộng đồng ngày 02–03/10/2026 cho thấy lỗi truy cập Store/Community và có người báo lỗi tải/cập nhật game. Có người dùng Viettel/VNPT cho biết Google DNS giúp truy cập; người khác dùng FPT báo cả 1.1.1.1 và 8.8.8.8 chưa đủ, phải dùng VPN. Cũng có người cùng ISP vẫn dùng được. Đây là lời kể của người dùng, chưa xác minh độc lập từng kết nối và không chứng minh một kiểu chặn duy nhất trên toàn quốc. [Báo cáo 02/10](https://www.reddit.com/r/VietnamGaming/comments/1wvmc0t/fpt_ch%E1%BA%B7n_steam/), [báo cáo 03/10](https://www.reddit.com/r/VietnamGaming/comments/1wwjuvf/sau_v%E1%BB%A5_pubg_nh%C3%A0_m%E1%BA%A1ng_b%E1%BA%AFt_%C4%91%E1%BA%A7u_ch%E1%BA%B7n_steam_ch%E1%BA%B7t_h%C6%A1n/).

Không tìm được trong lượt tra cứu này một phép đo có thẩm quyền và cập nhật cho từng tỉnh/ISP xác định đồng thời DNS, IP và DPI. Không suy ra nguyên nhân từ các suy đoán trong tiêu đề/bình luận.

## Kiểm tra chỉ đọc trên máy hiện tại

Người dùng xác nhận Viettel, Windows 11 và chưa thử Apply. Tool đã đọc được adapter Wi-Fi đang có default route IPv4 và xác minh tìm lại đúng adapter bằng GUID.

- DNS IPv4 hiện tại đã là Google: `8.8.8.8`, `8.8.4.4`, đặt thủ công.
- DNS IPv6 hiện tại cũng là Google: `2001:4860:4860::8888`, `2001:4860:4860::8844`.
- HTTPS theo phân giải Windows: Store chưa đạt; Community chưa đạt; Help đạt.
- Cùng buổi kiểm tra, lấy IP qua DoH rồi nối trực tiếp với chứng chỉ TLS/SNI bình thường: cả 9 hostname Steam/CDN phản hồi HTTPS, không nạp DPI.

Kết quả ở `dist/dns-read.txt` và `dist/network-check.txt`. Không đổi DNS, không sửa hosts, không nạp driver trong các phép thử này. Đây là kết quả tại thời điểm thử, có thể thay đổi.

**Suy luận:** điền lại Google DNS giống cấu hình sẵn có chưa đủ để bảo đảm sửa lỗi. Đường HTTPS tới Steam vẫn có thể hoạt động khi dùng IP từ DNS mã hóa, nên phương án DoH/hosts đáng thử trước DPI. Chưa kết luận DNS bị làm giả: khác biệt còn có thể do IP CDN được chọn, cache, tuyến IPv6 hoặc điều kiện kết nối. Các thử nghiệm này chưa xác nhận Steam client/đăng nhập/tải game.

## Các hướng xử lý

| Cách | Trường hợp có thể giúp | Giới hạn |
|---|---|---|
| Google DNS IPv4 `8.8.8.8 / 8.8.4.4` hoặc Cloudflare `1.1.1.1 / 1.0.0.1` | Resolver ISP lỗi hoặc trả kết quả không dùng được | Đặt IP resolver không tự bảo đảm DNS được mã hóa; không vượt mọi chặn IP/DPI |
| DNS-over-HTTPS | Tránh phụ thuộc truy vấn DNS thường qua ISP | Không tự thay tuyến dữ liệu HTTPS; Windows 11/Steam có thể dùng cơ chế DNS khác nhau |
| DoH → IP mới → vùng hosts Steam | Dùng IP Steam đã thử HTTPS mà không phải đổi cấu hình DNS của mọi ứng dụng | IP CDN đổi theo thời gian; chỉ áp dụng hostname đã biết |
| GoodbyeDPI | Một số kiểu DPI/SNI, tùy ISP và cấu hình TLS | Cần quyền quản trị, WinDivert và tiến trình chạy; không phải VPN |
| Mạng khác hoặc VPN/WARP ở chế độ tunnel | Đường kết nối bị chặn hoặc các cách DNS/DPI chưa đủ | Có thể đổi độ trễ, tốc độ; chế độ DNS-only khác tunnel |

Địa chỉ DNS được xác nhận từ [Google](https://developers.google.com/speed/public-dns/docs/using) và [Cloudflare](https://developers.cloudflare.com/1.1.1.1/setup/windows/). Cơ chế DPI: [GoodbyeDPI](https://github.com/ValdikSS/GoodbyeDPI). Phân biệt DNS-only/tunnel: [Cloudflare WARP](https://developers.cloudflare.com/warp-client/get-started/windows/).

Theo Steam Support, cũng cần kiểm tra tình trạng máy chủ, chất lượng kết nối, router và quy tắc firewall; không quy mọi lỗi -7 cho chặn ISP. Nếu Store truy cập được nhưng tải game chậm/lỗi, kiểm tra Download Region và cân nhắc Clear Download Cache. Đây là các bước chẩn đoán theo triệu chứng, không phải cách bảo đảm vượt chặn; Clear Download Cache có thể cần đăng nhập lại. Tool không tự xóa cache hoặc đổi vùng tài khoản. [Steam — mạng](https://help.steampowered.com/en/faqs/view/669A-2F68-D1D1-A5EC), [tải game](https://help.steampowered.com/en/faqs/view/5AC5-8056-E88F-F3FF), [cache](https://help.steampowered.com/en/faqs/view/6AD7-820D-8BE5-E51F).

Không tự tắt IPv6: Microsoft không khuyến nghị vô hiệu hóa IPv6 của Windows. Bản 1.2 chỉ thay DNS IPv4 và đọc DNS IPv6 để hiển thị/kiểm tra giữ nguyên. [Microsoft](https://learn.microsoft.com/en-us/troubleshoot/windows-server/networking/configure-ipv6-in-windows).

## Nếu muốn tự đổi DNS IPv4 bằng giao diện

Mở `Win+R` → `ncpa.cpl` → chuột phải Wi-Fi/Ethernet đang dùng → Properties → Internet Protocol Version 4 (TCP/IPv4) → Properties → Use the following DNS server addresses.

Chọn một cặp Google hoặc Cloudflare ở bảng trên. Chỉ thay ô DNS, giữ nguyên cấu hình IP/gateway. Ghi lại chế độ và giá trị DNS cũ trước khi đổi. Khi khôi phục, nếu trước đó DNS tự động thì chọn Obtain DNS server address automatically; nếu trước đó đặt tay thì trả đúng các giá trị cũ. Xóa DNS cache và thoát/mở lại Steam sau thay đổi. Windows 11 có thêm thiết lập DNS over HTTPS; điền hai địa chỉ IPv4 đơn thuần không đồng nghĩa đã bật DoH. [Cloudflare trên Windows](https://developers.cloudflare.com/1.1.1.1/setup/windows/).

Bản tool 1.2 dùng `netsh interface ipv4 ... dnsservers` để chỉ đổi DNS IPv4, không đổi địa chỉ IP, gateway hoặc DNS IPv6. Sao lưu cả chế độ DHCP/thủ công và nhận diện adapter bằng GUID khi khôi phục. [Microsoft netsh](https://learn.microsoft.com/en-us/windows-server/administration/windows-commands/netsh-interface).
