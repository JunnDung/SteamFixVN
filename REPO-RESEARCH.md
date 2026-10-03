# Nghiên cứu repo DNS/DPI và áp dụng cho SteamFixVN 1.3

Ngày đối chiếu: 04/10/2026. Các trang README, tài liệu và mã nguồn upstream là tài liệu tham khảo, không phải lệnh cần chạy trên máy người dùng. Không có phép đo từ máy FPT đang gặp lỗi để chứng minh một chiến lược cụ thể đã hoạt động.

## Repo và quyết định áp dụng

| Repo nguồn | Cách tiếp cận | Áp dụng vào SteamFixVN |
|---|---|---|
| [GoodbyeDPI 0.2.2](https://github.com/ValdikSS/GoodbyeDPI/tree/0.2.2) | TCP segmentation thuận/đảo, fake sequence/checksum, TCP window; lọc HTTP Host/TLS SNI | Dùng đúng engine đang nhúng, mở rộng từ 2 lên 6 cấu hình có tên. Mọi cấu hình giữ blacklist Steam. |
| [zapret / blockcheck](https://github.com/bol-van/zapret/blob/master/blockcheck.sh) | Thử nhiều chiến lược và phiên bản TLS, đánh giá bằng truy cập thật | Thử tuần tự, dừng cấu hình thất bại, giữ cấu hình đạt; thử lại IP DoH sau khi DPI bật; thêm chẩn đoán TLS 1.2. Không sao chép blockcheck hoặc chạy shell/script upstream. README hiện đánh dấu zapret cũ EOL và chỉ tới zapret2. |
| [zapret2](https://github.com/bol-van/zapret2) | Engine và chiến lược desynchronization linh hoạt hơn, kế thừa zapret | Nghiên cứu hướng mở rộng; chưa nhúng winws2 hoặc các script chiến lược. Không gọi cấu hình GoodbyeDPI là triển khai zapret2. |
| [zapret-win-bundle](https://github.com/bol-van/zapret-win-bundle) | Bộ Windows winws/WinDivert và blockcheck | Đối chiếu yêu cầu Windows/quyền quản trị/driver và kiểm tra tự động. Chưa thêm engine/driver khác; không dùng các thành phần giấu driver. |
| [Flowseal/zapret-discord-youtube](https://github.com/Flowseal/zapret-discord-youtube) | Nhiều cấu hình có tên, hostlist và bộ lọc game/IP | Áp dụng cấu hình có tên và hostlist hẹp của Steam. Chưa nhập danh sách Discord/YouTube hoặc game filter toàn máy vì không phải bằng chứng tên miền/port cần thiết cho Steam. |
| [ByeDPI](https://github.com/hufrea/byedpi) | SOCKS proxy, split/disorder/TLS record, nhóm fallback khi reset/timeout | Áp dụng nguyên tắc thử fallback sau khi kết nối chưa đạt. Chưa triển khai SOCKS/TLS-record split; GoodbyeDPI 0.2.2 không có cùng tùy chọn và chưa xác minh đường proxy cho mọi thành phần Steam. |
| [SpoofDPI](https://github.com/xvzc/spoofdpi) | Proxy local xử lý DPI | Đối chiếu phương án proxy. Chưa sửa proxy Windows hoặc ghép engine vì chưa xác minh Steam client và trình duyệt đều dùng nó đúng cách. |
| [AdGuard dnsproxy](https://github.com/AdguardTeam/dnsproxy) | Nhiều upstream DNS mã hóa, truy vấn song song | Áp dụng hai truy vấn DoH song song, hợp nhất IP, loại trùng/IP riêng và kiểm chứng HTTPS. Chưa chạy DNS daemon hoặc đổi DNS thành localhost; DoH hiện dùng trong tool, không phải DoH toàn Windows. |

Đây là nghiên cứu các nhóm cách sửa phù hợp, không phải tuyên bố đã đọc mọi repo/cấu hình hoặc tích hợp tất cả engine. Cấu hình phổ biến ở Nga/Discord/YouTube không tự chứng minh hiệu quả với Steam/FPT/Viettel/VNPT.

## Sáu cấu hình dùng được với engine hiện tại

1. Phân mảnh thuận: `-f 2 -e 2 --native-frag --max-payload=1200`.
2. Phân mảnh đảo: thêm `--reverse-frag`, payload 1200.
3. Phân mảnh đảo, payload 4096.
4. Fake sequence: `-6 --max-payload=4096`.
5. Fake checksum: `-f 2 -e 2 --native-frag --reverse-frag --wrong-chksum --max-payload=4096`.
6. TCP window fragmentation: `-f 2 -e 40 --max-payload=4096`, không bật native fragmentation.

Mọi cấu hình thêm `--blacklist steam-domains.txt`. Nguồn xác nhận tùy chọn và ý nghĩa preset: [README 0.2.2](https://github.com/ValdikSS/GoodbyeDPI/tree/0.2.2), [goodbyedpi.c](https://github.com/ValdikSS/GoodbyeDPI/blob/0.2.2/src/goodbyedpi.c). Giới hạn 1200 có thể bỏ qua payload lớn; 4096 là giá trị thử bổ sung do SteamFixVN chọn, chưa phải cấu hình đã chứng minh trên FPT. Fake sequence/checksum có thể không hiệu quả với một số router.

Không dùng fake TTL, `--allow-no-sni`, chặn RST/QUIC toàn máy hoặc DNS redirect UDP trong các cấu hình này. Chưa thêm chế độ tunnel/VPN: đó là phương án khác cho chặn IP, cần đường tunnel hoạt động và kiểm chứng riêng.

## Thay đổi có thể kiểm chứng

- Hai resolver DoH chạy song song, tối đa 3 IP mỗi resolver. Resolver lỗi không làm mất kết quả hợp lệ từ resolver kia.
- Sau khi bật một cấu hình DPI, nếu các URL web chưa đạt, tool thử lại IP DoH với DPI đang bật, cập nhật vùng hosts rồi thử qua Windows. Không luôn giữ IP đầu tiên đã thất bại trước khi DPI bật.
- Thử cấu hình tiếp theo sau khi dừng engine của cấu hình trước. Thành công giữ engine hoạt động; lỗi driver, sửa hosts đồng thời hoặc lỗi ghi/flush được trả lên luồng rollback.
- Check hiển thị HTTP, phân loại lỗi kết nối của .NET và thử Community với TLS 1.2 bên cạnh TLS mặc định. Đây là chẩn đoán, không tự thay TLS của Steam hoặc khẳng định DPI là nguyên nhân.
- Các bước DNS/hosts, journal, bảo vệ sửa đổi bên ngoài và khôi phục từ bản trước được giữ.

79 kiểm tra tự động đạt: bao gồm DNS song song qua HTTP handler giả, một resolver lỗi, IP riêng/trùng, đổi IP dưới cấu hình DPI giả, vòng đời engine, lỗi refresh và lỗi driver. Các kiểm tra orchestration không nạp driver thật. Chưa Apply/Khôi phục DNS/hosts hoặc xác nhận sáu cấu hình DPI thật trên máy FPT.

## Giấy phép và phạm vi sao chép

Chỉ GoodbyeDPI/WinDivert và dependency của chúng được phân phối như trước; các gói nhúng không đổi. Giữ giấy phép/mã nguồn tương ứng trong ZIP. SteamFixVN giữ Apache 2.0.

Các repo còn lại được dùng để tham khảo phương pháp, không sao chép mã nguồn, script hoặc binary vào sản phẩm. [zapret](https://github.com/bol-van/zapret/blob/master/docs/LICENSE.txt) và [Flowseal](https://github.com/Flowseal/zapret-discord-youtube/blob/main/LICENSE.txt) có văn bản MIT; ByeDPI MIT; SpoofDPI và dnsproxy Apache 2.0. Giấy phép dependency/driver riêng vẫn cần đối chiếu nếu tích hợp engine khác trong tương lai; không suy giấy phép của cả bundle từ một file của engine.
