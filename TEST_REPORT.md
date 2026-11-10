# Kiểm thử Dodge Line

## Bổ sung chặn menu chuột phải — Web 2026.11.10.2

Người dùng báo menu trình duyệt vẫn hiện trong khung thường dù HUD đã hiện Placement cancelled. Đã GET `index.html` và `browser-controls.js` từ Pages thật: cả hai trả200 và SHA256 khớp bản sửa Web UX đã commit. Điều này xác nhận file đang được host; không chứng minh tab đang mở của người dùng đã tải lại chúng. Edge profile sạch với code cũ đạt cả chuột phải thường/fullscreen (`TestResults/RightClick-2026-11-10/EdgeBefore`); chưa tái hiện đúng lỗi trong profile người dùng và chưa kết luận nguyên nhân.

Bản mới chặn contextmenu ở window capture và trực tiếp canvas, thêm HTML oncontextmenu dự phòng, giữ toàn bộ pointer/mouse down/up cho Unity. Có query phiên bản cho JavaScript và footer **Web 2026.11.10.2** để nhận biết tab cũ. Không đổi C#, gameplay, map hoặc dữ liệu leaderboard.

Build Web bằng Unity6000.6.7f1 thành công, Editor exit0 (`TestResults/RightClick-2026-11-10/build-web.log`), không thấy C# error/warning hoặc shader compilation error. Đã Prepare-Web; 7 file,44,065,790 bytes, SHA256 của toàn bộ `web/` khớp `Builds/Web` (`publication-check.json`). Chưa commit/push/deploy bản bổ sung thay người dùng.

Kiểm tra thực tế bản build qua localhost bằng Chrome và Edge headless/SwiftShader/CDP, profile test riêng không dùng extension người dùng:

- Chuột phải thật trên canvas ở chế độ thường bị preventDefault; hủy xây rồi click ô trống không mua thêm hoặc mất energy.
- Vào fullscreen bằng nút thật: fullscreenElement=unity-canvas, parent vẫn là game; chuột phải tiếp tục bỏ chọn và chặn contextmenu. Không quy lỗi của người dùng cho reparent/fullscreen.
- Giả lập một handler trên canvas gọi stopImmediatePropagation cho contextmenu: capture vẫn chặn default trước khi propagation bị dừng.
- DOM contextmenu ngoài khung game không bị chặn; đã xem ảnh footer có đúng phiên bản mới.
- Smoke menu/leaderboard/ô xây/combat/pause/retry và Quit/No/Esc/đóng player/Play again/đóng tab script được giữ trong công cụ kiểm tra.

Kết quả Chrome ở `ChromeAfter/browser-check.json`, Edge ở `EdgeFinal/browser-check.json` trong thư mục kết quả trên: cả hai exit0, các assertion đều đạt, không uncaught JS/WASM exception. Lượt `EdgeAfter` chạy song song ban đầu so hash energy khi Preparation đã hết, income làm101→105 và gây false failure. Đã cô lập lại tình huống chuột phải bằng Retry và mua tháp thật trong Preparation mới, không thay đổi kinh tế runtime.

Chưa thử trên tab/profile/extension của người dùng hoặc deploy bản mới lên Pages. Không chạy lại EditMode/PlayMode, build Windows hoặc toàn bộ5 màn vì không đổi gameplay. Hướng dẫn nhận biết phiên bản, commit và thử InPrivate nằm ở [WEB_UPDATE_GUIDE.md](docs/WEB_UPDATE_GUIDE.md).

## Web UX — 2026-11-10

Thêm README cho người chơi, hộp thoại Quit Web qua bridge `.jslib`, dừng Unity nếu trình duyệt chặn đóng tab, nút Play again, và chặn contextmenu riêng trong vùng game. Không đổi luật chiến đấu, chỉ số, map hoặc phiên bản leaderboard v2. Windows giữ Application.Quit; đợt này không build lại EXE.

Đã build Web Release bằng Unity6000.6.7f1 trên project gốc: **Build Finished, Result: Success**, Editor thoát code0; log `TestResults/WebUX-2026-11-10/build-web.log`. Không thấy C# error/warning hoặc shader compilation error trong log. Node kiểm tra cú pháp JavaScript thành công.

Chrome headless/SwiftShader chạy chính bản build qua HTTP localhost, sử dụng input chuột/phím CDP. Kết quả cuối: `TestResults/WebUX-2026-11-10/Browser2/browser-check.json`, process exit0, không uncaught JS/WASM exception.

| Hành vi kiểm tra | Kết quả |
| --- | --- |
| Nút Quit thực trong Unity → IL2CPP → jslib → hộp thoại HTML | Đạt |
| No và Esc hủy Quit, game tiếp tục và canvas nhận lại focus | Đạt |
| Chuột phải trên canvas không mở menu trình duyệt | Đạt, contextmenu đã bị preventDefault |
| Hủy chế độ xây bằng chuột phải rồi click ô hợp lệ khác | Không xây thêm, energy giữ nguyên |
| Chọn tháp rồi chuột phải bỏ chọn | Đã xem ảnh range/HUD được bỏ chọn |
| Menu chuột phải bên ngoài vùng game | Không bị chặn, kiểm tra bằng DOM event |
| Yes khi tab có lịch sử và Chrome từ chối đóng | Unity Quit hoàn tất, canvas ẩn, Game closed hiển thị |
| Play again sau khi đóng game | Tải lại Unity và menu thành công |
| Yes trong tab mở bằng window.open | Tab thật biến mất khỏi danh sách Chrome targets |
| Menu/leaderboard overlay, luật pad, mua tháp, bắt đầu wave, pause/retry | Đạt smoke check |

Đã xem ảnh hộp thoại và trạng thái đóng; bản sao dễ xem ở `DebugCaptures/Web_QuitConfirmation.png`, `Web_GameClosed.png`, `Web_RightClickDeselected.png`. Lượt kiểm tra ban đầu dùng hash ảnh vùng energy bị ảnh hưởng bởi vòng preview vẽ đè HUD; đã sửa công cụ để đưa chuột ra khỏi map trước khi so sánh rồi chạy lại đạt. Đây không phải lỗi mất energy do chuột phải.

Lượt này Authentication thật200, GET bảng màn1 v2 thật200; GET entry của tài khoản kiểm thử mới404 vì chưa có điểm là bình thường. Không POST điểm test lên Cloud; chưa kiểm tra lại các bảng2–5 hoặc gửi điểm trên dịch vụ thật. Các ghi nhận404 ở đợt ô xây phía dưới là kết quả lịch sử, không phải trạng thái mới của bảng màn1.

Đã chạy Prepare-Web để cập nhật `web/` từ bản build mới và đối chiếu file. Chưa commit/push/deploy thay người dùng. Chưa chạy trên URL Pages công khai với thay đổi mới, Firefox/Edge/Safari, fullscreen hoặc thiết bị cảm ứng. Không chạy lại EditMode/PlayMode hay chơi hết5 màn trong đợt Web UX; các kết quả gameplay bên dưới thuộc đợt trước.

## Lịch sử: ô xây tháp v2 — 2026-11-10

Sửa yêu cầu chỉ được đặt Bow/Cannon trong các vòng chỉ định của map. Vòng cũ chỉ là hình gợi ý; PlacementSystem trước đây kiểm tra đất/đường và cho đặt tự do. Đợt này biến chúng thành TowerBuildPad thật, click trong vòng snap về tâm, mỗi ô một tháp, dùng chung loại vũ khí và bán để giải phóng ô. Trap giữ luật ô đường riêng.

Môi trường: Unity6000.6.7f1, Windows, Node24.15.0. Kết quả đợt bắt đầu2026-11-09 và hoàn tất ngày2026-11-10 nằm ở `TestResults/BuildPads-2026-11-09` (ignored). Hướng dẫn game: [SOLO_GAME_GUIDE.md](docs/SOLO_GAME_GUIDE.md); thiết kế: [GDD.md](docs/GDD.md).

## Scene và dữ liệu

Đã chạy Editor UpgradeBuildPads trên project gốc, tạo pad/LineRenderer bằng Unity API và save5 scene, giữ GUID. Không sửa scene YAML thủ công. Pad transform là nguồn vị trí của cả marker và placement; xóa/disable pad thì vị trí đó không được xây. Không có fallback đặt tự do trong game hoặc fixture.

| Màn | Số ô chung | Chỉnh bố cục |
| --- | ---: | --- |
| 1 | 5 | Giữ tâm vòng cũ |
| 2 | 8 | Giữ tâm vòng cũ |
| 3 | 6 | Gộp vòng trùng; dời ô(1.5,2.8) xuống(1.5,2.7) để Cannon không vượt mép terrain |
| 4 | 8 | Giữ tâm vòng cũ |
| 5 | 7 | Giữ tâm vòng cũ |

Không đổi route, wave, chỉ số weapon/enemy, kinh tế hoặc cơ chế Trap. Các vòng dùng chung Bow/Cannon; Cannon vẫn mở từ màn2. Stage.suggestedBows/Cannons chỉ là thứ tự mua của bot kiểm thử, không là quyền đặt.

## Automated tests đã hoàn tất

| Kiểm tra | Kết quả | Bằng chứng |
| --- | --- | --- |
| Editor nâng cấp scene | Thành công, exit0 | `upgrade.log` |
| ProjectValidation | Pad tồn tại, không trùng, đủ đất cho cả hai tháp; đúng reference/scene/wave/config | `validate.log`, `TestResults/ProjectValidation/mechanics.json` |
| EditMode đầy đủ | **34/34**, 0 failed/skipped | `editmode.xml` |
| PlayMode đầy đủ | **59/59**, 0 failed/skipped | `playmode.xml` |
| Node HTTP protocol suite | **1/1**; thêm assertion tách bảng v1/v2 | `node --test tools/leaderboard-fixture.test.mjs` |

Sáu ca pad mới kiểm tra: đất hợp lệ ngoài ô không được xây/không trừ tiền; snap click lệch tâm và biên/ngoài biên; một ô dùng chung, cấm chồng, bán rồi dùng lại; dời/disable/không có pad không mở lại free placement; pad đặt sai không vượt được cấm đường/base/approach/obstacle/bounds; mọi ô ở cả5 màn dùng được Bow/Cannon theo unlock và Trap không cần pad.

Các regression projectile, CPA/retreat/hit khi lùi, spacing, contact base, Trap, nâng/bán, pause/retry và network trước đó vẫn đạt. Hai combat fixture cần vị trí tháp riêng đã tạo pad fixture rõ ràng; runtime không được nới luật cho test. Ca network mới xác nhận bảng/outbox v1 không trộn sang v2 và session/name vẫn giữ.

## Khả năng thắng bằng ô xây

PlayMode đã thắng đủ5 màn bằng giao dịch/tiền thật theo bot hiện hành, không cấp tiền, sửa HP hoặc ép Victory. Bot chỉ dùng ô có marker. HP còn **20/17/13/16/18**; do đó chưa cần đổi route/wave hoặc hạ chỉ số để bù luật mới.

| Màn | Wave | HP | Kills | Energy còn | Combat giây |
| --- | ---: | ---: | ---: | ---: | ---: |
| 1 | 6 | 20 | 68 | 278 | 150.10 |
| 2 | 8 | 17 | 166 | 1248 | 275.39 |
| 3 | 8 | 13 | 163 | 80 | 340.15 |
| 4 | 9 | 16 | 232 | 1595 | 314.43 |
| 5 | 10 | 18 | 301 | 2392 | 411.59 |

Đây là một chiến thuật thắng hợp lệ, không chứng minh mọi lựa chọn tháp hoặc độ khó cho người mới đã tối ưu.

## Windows và Web

Đã build lại **Windows Development x64** thành công (`build-windows.log`, exit0). Chạy chính `Builds/Windows/DodgeLine.exe` qua hai lượt: online với HTTP fixture loopback và offline với endpoint không có server. Cả hai process exit0, mỗi lượt5 chiến thắng mua hợp lệ +5 thất bại không xây, kiểm tra retry/unlock/sao/menu và tiếp xúc base thật.

Native checker đã kiểm tra riêng từng màn: đất trống hợp lệ nhưng không có pad bị từ chối cho cả hai loại tháp và không mất tiền; click lệch tâm được snap đúng; toàn bộ tháp của chiến thuật thắng đều ở tâm pad. Các chỉ số thắng khớp bảng PlayMode phía trên. `Online/campaign-smoke.txt` có POST/GET điểm thực tế trên5 bảng fixture v2 và phục hồi identity; `Offline/campaign-smoke.txt` giữ đủ5 kết quả pending sau khi tạo lại client. Đây không phải điểm đã lên bảng Cloud.

Probe bằng EXE mới (`CloudProbe/cloud-probe.json`) xác nhận anonymous auth thật thành công nhưng bảng v2 chưa tạo trả404. Native không ghi runtime error/exception/assert; mỗi lượt camera capture vẫn có22 cảnh báo URP về các shader post-processing bị strip/không hỗ trợ. Không có C# warning/error hoặc shader compilation error trong log build Windows.

Đã build lại **Web Release** thành công (`build-web.log`, exit0), không có C# warning/error hoặc shader compilation error. Chrome headless/SwiftShader đã chạy chính Web build mới qua HTTP localhost và input chuột/phím CDP. `Browser/browser-check.json` ghi loaded=true, overlayBlocksStageClick=true, unmarkedGroundDidNotSpend=true, padPlacementSpentEnergy=true và không có uncaught JS/WASM exception.

Đã xem ảnh xác nhận: đất trống ngoài ô có ghost đỏ/thông báo từ chối, energy vẫn150 (`04b-unmarked-ground-rejected.png`); click lệch tâm bên trong ô tạo Bow tại tâm và energy còn100 (`04d-pad-placement.png`); hủy preview rồi bắt đầu wave thấy đạn bay (`05-combat.png`). Menu, leaderboard, pause/retry cũng được chạy lại. Web gọi Authentication thật200 và bảng v2 chưa tạo404; không có điểm giả hiển thị thay cho bảng online.

Đã chạy lại Prepare-Web, thư mục `web/` khớp bản build cuối, tổng44,159,336 bytes. Windows và Web/Pages staging cùng dùng luật ô xây v2. Chưa push/publish công khai. Ảnh kiểm tra trực tiếp được lưu ở DebugCaptures/BuildPads_OutsideRejected.png, BuildPads_CenteredTower.png và Game_WebCombat.png.

## Leaderboard và dữ liệu cũ

Luật đặt tự do và chỉ đặt pad có điều kiện đạt điểm khác nhau. Resource config chuyển sang `rulesVersion=v2` và5 ID `dodge-line-level-1-v2`…`dodge-line-level-5-v2`. Outbox tách phiên bản; session/name và unlock/stars/best local cũ giữ nguyên. Best local cũ không tự trở thành entry online v2. Người chơi cần thắng bằng bản v2 để gửi kết quả mới.

Đã kiểm tra dịch vụ thật ngày2026-11-10: anonymous sign-in thành công, cả5 bảng v2 trả404. Chưa tạo bảng Cloud hoặc publish GitHub/Pages thay người dùng. Hướng dẫn [NETWORK_AND_DEPLOYMENT.md](docs/NETWORK_AND_DEPLOYMENT.md) đã đổi sang ID v2. Không gửi điểm test lên bảng Cloud; fixture HTTP dùng loopback và namespace riêng.

## Chưa xác nhận

Chưa có người thật chơi hết5 màn bằng chuột/phím, chưa tối ưu balance/đo FPS, chưa thử clean Windows PC hoặc URL Pages công khai. Chưa gửi/đọc điểm thành công trên Cloud vì bảng chưa tạo. Server vẫn tin score client, không có anti-cheat/server replay. Boss/audio/hoạt ảnh sprite đầy đủ ngoài phạm vi.

Để tự kiểm tra, mở `Assets/Scenes/MainMenu.unity` rồi Play. Thử chọn Bow và click đất trống ngoài vòng: preview đỏ, không có tháp và không trừ tiền. Click lệch tâm bên trong vòng: tháp vào đúng tâm. Thử đặt thêm Cannon cùng ô, bán tháp rồi xây lại. Trap vẫn đặt trên đường. Khi có lỗi, gửi Console message/stack trace đầu tiên kèm scene/wave/thao tác.
