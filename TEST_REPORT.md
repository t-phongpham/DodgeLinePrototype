# Kiểm thử ô xây tháp v2 — 2026-11-10

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
