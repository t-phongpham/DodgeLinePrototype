# Cập nhật Dodge Line trên GitHub Pages

## Thay đổi giao diện Web — 2026-11-10

- README giới thiệu game, luật chơi, điều khiển, chiến dịch và leaderboard.
- Quit game trên Web mở hộp thoại xác nhận riêng của website. No hoặc Esc quay lại menu. Yes gọi `window.close()` trực tiếp từ click người dùng.
- Trình duyệt có thể từ chối đóng tab, nhất là tab mở thủ công và có lịch sử điều hướng. Khi đó Unity player được dừng bằng `Quit()` và hiện Game closed / Play again. Không thể bảo đảm website được phép đóng mọi tab. Tham khảo [MDN Window.close](https://developer.mozilla.org/en-US/docs/Web/API/Window/close).
- Chuột phải trong khung game vẫn gửi input đến Unity để hủy xây/bỏ chọn; chặn hành vi mặc định của menu và nút phải. Chuột phải ngoài khung game vẫn dùng menu trình duyệt.
- Windows giữ Application.Quit; không cần bản xác nhận HTML.

Bridge WebGL nằm ở `Assets/Plugins/WebGL/DodgeLineBrowser.jslib`; HTML và JavaScript ở `Assets/WebGLTemplates/DodgeLine/`. File trong `web/` là đầu ra build, không sửa trực tiếp vì lần build sau sẽ ghi đè.

### Theo dõi lỗi Edge tiếp diễn — Web 2026.11.11.1

Người dùng xác nhận đúng URL Pages, footer2026.11.10.2 và InPrivate vẫn có menu. Chưa xác định nguyên nhân; không coi profile sạch/headless đạt là bằng chứng lỗi trên tab người dùng đã hết.

Bản2026.11.11.1 bổ sung preventDefault cho **right mousedown/mouseup/auxclick** trong khung game, ngoài contextmenu. Không stopPropagation; không cancel pointerdown vì có thể làm mất compatibility mouse events. Unity vẫn phải nhận chuột phải để bỏ chọn. Không chặn nút trái hoặc nút giữa.

Nếu menu vẫn xuất hiện sau khi deploy bản mới, mở:

<https://t-phongpham.github.io/DodgeLinePrototype/?inputDebug=1>

Chọn Bow ở màn1, click phải ở giữa khung game, đóng menu trình duyệt nếu có. Cuộn xuống **Right-click check**, bấm **Copy report**, dán vào chat. Panel ghi phiên bản, URL không query, user-agent và tối đa16 sự kiện gần nhất: target, loại event, trusted/cancelable/defaultPrevented, nút chuột và modifier. Không ghi token, PlayerPrefs, tên hay điểm người chơi; không tự gửi báo cáo qua mạng. Bỏ `?inputDebug=1` để trở về giao diện thường. Không cần F12, nhập code vào Console hoặc xóa save.

Tên commit cho bản bổ sung này:

```text
Guard Edge right mouse defaults and add opt-in input diagnostics
```

Đợt này chỉ cần commit các file đang thay đổi rồi Push origin; `web/` đã được chuẩn bị sau build. Kết quả và giới hạn kiểm thử nằm trong [TEST_REPORT.md](../TEST_REPORT.md).

### Bản sửa chuột phải tiếp theo — Web 2026.11.10.2

Chặn contextmenu ở capture phase trên window và trực tiếp trên canvas; HTML cũng có oncontextmenu dự phòng. Không chặn pointer/mouse down/up, nên input bỏ chọn của Unity vẫn nhận bình thường. Phạm vi chặn vẫn chỉ là khung game/canvas. JavaScript có query phiên bản để tránh dùng lại script cũ sau deploy; số **Web 2026.11.10.2** hiện ở footer.

Website đã phát đúng file của lượt sửa đầu khi người dùng báo vẫn có menu; điều này không chứng minh tab đang mở của người dùng đã tải lại file đó. Kiểm tra Edge với profile sạch ở chế độ thường và fullscreen chưa tái hiện lỗi của ảnh. Không kết luận nguyên nhân là cache, fullscreen hoặc extension nếu chưa kiểm tra tab thực tế.

Sau khi push bản này, đóng tab game cũ, mở lại URL Pages và kiểm tra footer có **Web 2026.11.10.2**. Nếu chưa có, chờ Actions xanh rồi Ctrl+Shift+R. Nếu vẫn có menu, thử InPrivate/Incognito không bật extension và gửi ảnh footer, URL, tên/phiên bản trình duyệt. Không cần xóa dữ liệu website hoặc tiến trình đang lưu. Cửa sổ riêng tư dùng vùng lưu khác nên có thể chỉ mở màn1, phù hợp để thử chuột phải.

Tên commit cho bản sửa này:

```text
Fix Web context menu capture and add fullscreen input checks
```

## Commit bản đã chuẩn bị

1. Mở **GitHub Desktop**, chọn repository **DodgeLinePrototype**, kiểm tra **Current branch: main** và đường dẫn `C:\DodgeLinePrototype`.
2. Trong **Changes**, xem diff. Giữ các thay đổi source, `.meta` mới, README/docs, công cụ kiểm tra và toàn bộ thay đổi `web/`. Các file build nhị phân trong `web/Build` là cần thiết cho website.
3. Không thêm `Library`, `Temp`, `Builds`, `TestResults` hoặc project IDE tự sinh. `.gitignore` đã loại chúng; không dùng force add.
4. Điền **Summary**:

   ```text
   Improve Web quit flow, right-click controls and game README
   ```

5. **Description** có thể dùng:

   ```text
   Add a player-facing README and Web update instructions.
   Confirm Web quit, request tab close, and stop Unity when browser closing is blocked.
   Preserve right-click deselection without opening the browser context menu.
   Include the rebuilt Web player and browser regression checks.
   ```

6. Bấm **Commit to main**, rồi **Push origin**. Nếu Desktop yêu cầu Fetch/Pull trước, lấy thay đổi remote và kiểm tra conflict trước khi push; không force push.
7. Trên GitHub mở **Actions → Publish Dodge Line Web**. Push thay đổi `web/` trên main sẽ tự chạy workflow. Chờ job deploy xanh.
8. Mở URL Pages từ **Settings → Pages → Visit site**. Thử Quit/No, chuột phải và Quit/Yes. Nếu còn bản cũ, kiểm tra Actions đã deploy đúng commit rồi dùng **Ctrl+Shift+R**.

Không bấm Configure để tạo workflow mới: `.github/workflows/pages.yml` đã có sẵn. Source của Pages phải giữ **GitHub Actions**.

## Những lần sửa tiếp theo

1. Lưu scene và dừng Play trong Unity.
2. Chọn **Tools → Dodge Line → Build → Web**, đợi build thành công.
3. Mở PowerShell tại `C:\DodgeLinePrototype` và chạy:

   ```powershell
   powershell -ExecutionPolicy Bypass -File tools/Prepare-Web.ps1
   ```

4. Review, commit và push như trên. Chỉ commit source C# chưa làm bản game trên website đổi theo.

Đợt này bản Web được chuẩn bị trong workspace; kết quả kiểm tra thực tế nằm ở [TEST_REPORT.md](../TEST_REPORT.md). Không cần build lại nếu không sửa thêm sau bản đã chuẩn bị. Windows là đầu ra riêng và chỉ cần build Windows khi bạn muốn cập nhật EXE.
