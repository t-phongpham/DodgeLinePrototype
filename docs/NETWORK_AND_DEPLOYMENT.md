# Leaderboard, GitHub và bản Web

Game hiện chỉ có một chiến dịch 5 màn. Module network là **leaderboard dùng Unity Gaming Services qua HTTPS/JSON**; không đồng bộ combat giữa các máy. Windows và Web dùng chung các bảng điểm, nhưng mỗi thiết bị/browser có danh tính ẩn danh riêng.

## 1. Hoàn tất cấu hình Unity Cloud

Bộ luật hiện tại là **v2: chỉ xây tháp trong vòng vàng**. Dùng5 ID v2 dưới đây. Nếu đã tạo bảng v1, giữ nó làm dữ liệu cũ; không dùng bảng đó cho bản sửa này. Token/danh tính và tiến trình local giữ nguyên, còn outbox điểm tách theo rulesVersion để không gửi kết quả đặt tự do cũ lên v2.

Client đã có code gửi/đọc điểm, giữ điểm chưa gửi, retry và UI. Lúc kiểm tra ngày 2026-11-10, đăng nhập ẩn danh của project đã thành công nhưng cả 5 leaderboard dưới đây trả404. Cần tạo chúng trong Dashboard trước khi xác nhận gửi/đọc điểm trên dịch vụ thật.

1. Mở [Unity Dashboard](https://cloud.unity.com/), đăng nhập tài khoản có quyền project **DodgeLinePrototype** trong organization **phongphmthanh**.
2. Kiểm tra Project ID là **af360e97-8619-400d-8287-97c7d0b04335**, environment **production**.
3. Trong **Development > Products > Leaderboards**, tạo 5 bảng với **ID** chính xác bên dưới. Display name có thể đặt dễ đọc; code dùng ID.

| Màn | Leaderboard ID |
| --- | --- |
| 1 | `dodge-line-level-1-v2` |
| 2 | `dodge-line-level-2-v2` |
| 3 | `dodge-line-level-3-v2` |
| 4 | `dodge-line-level-4-v2` |
| 5 | `dodge-line-level-5-v2` |

4. Mỗi bảng dùng **Sort: Descending** và **Update: Keep Best**. Không bật buckets, tiers hoặc lịch reset cho bản đầu.
5. Giữ cấu hình cho phép player đã đăng nhập đọc bảng và ghi điểm của chính mình. Nếu project đã đặt Access Control riêng, bảo đảm quyền này; không đặt service-account/admin key vào game.
6. Mở game bình thường, chọn **Online leaderboard**, nhập tên rồi **Save name**. Trước khi có điểm, bảng trống là bình thường. Thắng màn1, kiểm tra thông báo gửi điểm thành công và hạng cá nhân.
7. Chơi lại điểm thấp hơn: điểm tốt nhất trên server phải giữ nguyên. Tắt mạng rồi thắng: local progress vẫn lưu, kết quả vào hàng đợi. Có mạng lại, mở bảng và bấm **Refresh / send pending**.

File client: `Assets/Resources/LeaderboardConfig.json`. Project ID và tên bảng là thông tin cấu hình công khai, không phải admin secret. Nếu giữ đúng ID ở trên, tạo bảng trong Dashboard không cần build lại. Đổi project/environment/ID trong JSON thì cần build lại Windows/Web.

Nếu dùng project Unity Cloud khác, cập nhật Project ID/environment trong JSON và tạo 5 bảng trong project đó. Không cần cài SDK Leaderboards vì client dùng `UnityWebRequest` trực tiếp. API tham khảo: [Authentication](https://docs.unity.com/en-us/services-web-apis/client-auth), [Leaderboards](https://docs.unity.com/en-us/oas-leaderboards/1.0.0).

## 2. Chơi và kiểm tra bản Windows local

- Mở `Builds/Windows/DodgeLine.exe`. Mở bình thường dùng Unity Cloud trong JSON; mất mạng vẫn chơi solo được.
- Giữ EXE cùng `DodgeLine_Data`, `MonoBleedingEdge`, `UnityPlayer.dll` và các file Unity đi kèm. Không copy riêng một EXE.
- Trong Unity: **Tools > Dodge Line > Build > Windows (Local Testing)** để cập nhật bản này. Đây là Development Build.
- Scene để mở trong Editor: `Assets/Scenes/MainMenu.unity`. Không cần Generate lại.

Để tự thử module HTTP mà chưa cấu hình Dashboard, có server **kiểm thử local** không cần npm package. Máy cần Node.js 24 trở lên. Từ PowerShell tại root project:

```powershell
node tools/leaderboard-fixture.mjs
```

Giữ terminal đó mở. Trong terminal khác:

```powershell
& .\Builds\Windows\DodgeLine.exe -dodgeLineLeaderboardEndpoint http://127.0.0.1:8787
```

Cờ này chỉ có trong Editor/Development Build, chỉ nhận địa chỉ loopback và dùng vùng lưu token/điểm riêng. Đóng rồi mở lại EXE với cùng cờ để thử giữ danh tính. Tắt server để thử mất mạng; bật lại rồi gửi pending. Fixture giữ dữ liệu trong RAM nên restart server xóa bảng test. Đây không phải backend để deploy và không chứng minh dữ liệu đã lên Unity Cloud.

Kiểm tra Node:

```powershell
node --test tools/leaderboard-fixture.test.mjs
```

Trong **Window > General > Test Runner**, EditMode kiểm tra dữ liệu/outbox; PlayMode có các test HTTP tự khởi động fixture bằng Node. Nếu máy chưa cài Node, nhóm HTTP báo skipped với lý do rõ ràng; gameplay tests vẫn tách riêng.

## 3. Xuất và mở bản Web trên máy

1. Trong Unity Hub, kiểm tra Editor6000.6.7f1 có **Web Build Support**. Máy hiện tại đã có module này.
2. Lưu scene, dừng Play. Chọn **Tools > Dodge Line > Build > Web**. Unity có thể mất vài phút khi đổi target lần đầu.
3. Đầu ra ở `Builds/Web/index.html`. Build dùng template `Assets/WebGLTemplates/DodgeLine`, gzip và decompression fallback để host tĩnh không cần cấu hình riêng Content-Encoding. Không double-click index.html bằng `file://`.
4. Từ root project chạy:

```powershell
node tools/serve-web.mjs
```

5. Mở **http://127.0.0.1:8080/** bằng Chrome/Edge desktop. Thử menu, màn1, chuột/phím và nút leaderboard. Bản Web dùng Unity Cloud; fixture local không được tự động đưa vào bản Web Release.
6. Save tiến trình của Web nằm trong dữ liệu trình duyệt/origin đó. Nó không tự lấy save của EXE. Windows và Web vẫn cạnh tranh trên cùng bảng điểm online sau khi Dashboard đã cấu hình.

Build lại Windows khi muốn tiếp tục kiểm tra EXE mới; Web và Windows là hai thư mục riêng. [Unity Web Player settings](https://docs.unity.com/en-us/engine/6000.6/manual/platform-specific/webgl/develop/class-player-settings-web-gl) mô tả compression/fallback. Request khác origin chịu luật CORS; tham khảo [Unity Web networking](https://docs.unity.com/en-us/engine/6000.6/manual/platform-specific/webgl/develop/networking).

## 4. Đưa source lên GitHub

Máy lúc kiểm tra chưa có lệnh `git`; chưa tạo repository hoặc push thay bạn. Cách dễ dùng là cài **GitHub Desktop**, đăng nhập tài khoản GitHub của bạn.

1. Trong GitHub Desktop chọn **File > Add local repository**, trỏ tới `C:\DodgeLinePrototype`.
2. Nếu báo thư mục chưa phải Git repository, chọn **create a repository here**. Kiểm tra đường dẫn cuối cùng vẫn là `C:\DodgeLinePrototype`, không tạo thêm một project lồng bên trong. Nếu hộp thoại hỏi Name/Local path, dùng Name=`DodgeLinePrototype`, Local path=`C:\`.
3. Giữ `.gitignore` có sẵn. Xem danh sách thay đổi: cần có `Assets` cùng `.meta`, `Packages`, `ProjectSettings`, `docs`, `tools`, `.github`, README và báo cáo test. `Library`, `UserSettings`, `Builds`, `TestResults`, log và project IDE tự sinh được bỏ qua.
4. Commit, ví dụ **Add single-player game and leaderboard module**. Chọn **Publish repository**. Dùng nhánh **main** cho workflow đã chuẩn bị; nếu dùng tên khác thì sửa `branches` trong workflow.
5. Đối với cách Pages dưới đây, dùng repository public. Đầu ra Windows không nằm trong Git; có thể zip cả `Builds/Windows` để gửi người chơi hoặc đính kèm GitHub Release. Bỏ thư mục `*_BackUpThisFolder_ButDontShipItWithYourGame` khi đóng gói.

Không dùng thao tác kéo cả project lên trang upload GitHub vì sẽ khó giữ đúng ignore, cấu trúc và cập nhật. File PNG hiện tại đều dưới giới hạn100MiB/file; Web build cần kiểm tra lại nếu sau này thêm asset lớn. [Giới hạn file GitHub](https://docs.github.com/en/repositories/working-with-files/managing-large-files/about-large-files-on-github).

## 5. Đưa game lên GitHub Pages

Đã có `.github/workflows/pages.yml`. Workflow **chỉ publish Web build đã tạo trên máy**, không tự mở Unity/compile project trên GitHub và không cần Unity license secret.

1. Sau khi build Web thành công, từ root project chạy:

```powershell
powershell -ExecutionPolicy Bypass -File tools/Prepare-Web.ps1
```

2. Script tạo/cập nhật `web/` từ `Builds/Web`. Script chỉ thay thư mục xuất bản `web`, không sửa Assets hay source build. Review rồi commit `web/` cùng workflow trong GitHub Desktop; **Push origin**.
3. Trên repository GitHub, mở **Settings > Pages**, chọn **Source: GitHub Actions**.
4. Mở tab **Actions**, chọn **Publish Dodge Line Web**, bấm **Run workflow** trên nhánh main nếu lần push đầu chưa deploy. Sau này push thay đổi `web/**` trên main sẽ tự chạy.
5. Khi job deploy xanh, mở URL ở deployment/Settings > Pages, thường dạng `https://TEN_GITHUB.github.io/TEN_REPO/`.
6. Thử lại leaderboard trên URL thật. Thắng bằng Windows rồi xem bảng ở Web; dùng tên khác ở Web để phân biệt hai danh tính.

Mỗi lần đổi code/art/config: **Build Web → Prepare-Web → commit web/ → push**. Chỉ push source C# không làm game trên Pages tự cập nhật. [Hướng dẫn GitHub Pages Actions](https://docs.github.com/en/pages/getting-started-with-github-pages/using-custom-workflows-with-github-pages).

## 6. Demo module network cho giảng viên

1. Mở bảng online để thấy request tải Top 10 và hạng.
2. Thắng màn1: điểm theo công thức game được gửi và hiện trong bảng, không nhập điểm bằng tay.
3. Mở trên browser/thiết bị khác, thấy cùng bảng nhưng danh tính riêng.
4. Gửi kết quả thấp hơn, điểm best vẫn giữ.
5. Tắt mạng: solo vẫn chơi và có pending; có mạng lại bấm gửi, hàng đợi được xóa sau khi server xác nhận.

Có thể mở F12 > Network ở Web để xem GET/POST và HTTP status. Không đưa token Authorization/session token lên slide hoặc commit log đăng nhập. Module này dùng server lưu/rank điểm thật; chỉ host HTML không thay phần đó.

## 7. Khi gặp lỗi

| Hiện tượng | Kiểm tra |
| --- | --- |
| `Leaderboard is not set up yet` /404 | Đúng project, environment production và đủ 5 ID; kiểm tra Dashboard |
| Sign-in refused /401/403 | Quyền project/service, anonymous auth và Access Control; không tự đổi sang một player mới để che lỗi |
| Too many requests /429 | Chờ rồi Retry; client có đọc Retry-After và không gửi lặp liên tục |
| Pending còn sau khi đóng menu | Bình thường khi chưa gửi được; mở bảng và Refresh để thử lại |
| Web không tải | Dùng HTTP server, kiểm tra đủ file Build/template; Console F12 có lỗi404 hoặc decompression nào |
| Web cũ sau khi push | Xem Actions đã deploy commit mới chưa, hard refresh/kiểm tra cache |
| GitHub Actions không thấy index.html | Chạy Prepare-Web và commit `web/`, không chỉ commit Assets |

Khi cần gửi lỗi: ghi Windows/Web, URL hoặc scene, thao tác, thông báo và stack trace đầu tiên. Báo cáo đã chạy nằm ở [TEST_REPORT.md](../TEST_REPORT.md). Chưa coi endpoint fixture hoặc build thành công là bằng chứng leaderboard thật đã hoạt động sau Dashboard setup.
