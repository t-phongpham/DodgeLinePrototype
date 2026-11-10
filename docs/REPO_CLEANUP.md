# Dọn repo — 2026-11-09

**Cập nhật sau tích hợp leaderboard:** scene/data hiện được tổ chức theo cây mới ở [README](../README.md). Các đường dẫn trong bảng “Trước/Hiện tại” bên dưới ghi lại đợt cleanup trước khi đổi cây: menu hiện ở `Assets/Scenes/MainMenu.unity`, các màn ở `Assets/Scenes/Levels`, data ở `Assets/Data/Enemies`, `Weapons`, `Levels`. Build local chuyển sang `Builds/Windows`; Web ở `Builds/Web`. Việc chuyển mới giữ GUID và không đổi balance; hướng dẫn network/triển khai ở [NETWORK_AND_DEPLOYMENT.md](NETWORK_AND_DEPLOYMENT.md).

Đích sử dụng duy nhất hiện tại là campaign solo năm màn. Việc dọn này giữ gameplay, cân bằng và dữ liệu tiến trình người chơi; loại pipeline demo cũ đã bị campaign thay thế.

## Đã loại

- Scene SampleScene, Prototype_Graybox, Level2_Solo và hai scene Beta; balance/data Prototype/Beta không còn được campaign tham chiếu.
- DemoSceneGenerator, BetaSceneSetup, PrototypeHUD và StandaloneSmokeCheck cũ. CampaignBuilder/CampaignHUD/CampaignSmokeCheck là pipeline hiện hành.
- Welcome/tutorial, cấu hình package Learn đi kèm và scene template không dùng; các bản build Beta/Windows cũ.
- Log và kết quả kiểm thử lịch sử, gồm bản project clone tạm trong TestResults. Giữ kết quả lần kiểm tra hiện tại để xem lại khi cần.
- Tài liệu hướng dẫn demo/beta và thiết kế solo cũ đã được GDD/hướng dẫn hiện hành thay thế; ảnh sơ đồ GDD cũ không còn được tài liệu mới dùng.
- Project/solution IDE tự sinh ở root. Unity/IDE có thể tạo lại chúng; chúng nằm trong `.gitignore`.

Đợt xóa chính giải phóng **1,713,143,116 bytes**, khoảng1.71GB thập phân, trong38 nhóm đường dẫn. Nhật ký xóa nằm ở `TestResults/RepoCleanup-2026-11-09/deleted-files.json`. Cache mới do chạy Unity/test/build tạo ra không nằm trong con số đó.

## Đã chuyển và chuẩn hóa

| Trước | Hiện tại |
| --- | --- |
| `Assets/Art/Beta/Sprites` | `Assets/Art/Sprites` |
| Shape/material trong `Assets/Data/Prototype` | `Assets/Art/Common/Square.png`, `Circle.png`, `SpriteUnlit.mat` |
| PrototypeSettings / PrototypeWave / PrototypeSpawn | LevelSettings / WaveDefinition / WaveSpawn |
| StraightPath | EnemyPath |
| BetaArtSet | GameArtSet |
| GrayboxFactory | VisualFactory |

Asset được chuyển bằng Unity AssetDatabase; script giữ `.meta`/GUID khi đổi tên. Các khai báo `MovedFrom` còn tên cũ là metadata phục vụ migration, không phải module gameplay cũ. Scene/data được serialize lại bằng Unity, không sửa scene YAML thủ công.

CampaignBuilder tạo trực tiếp dữ liệu campaign khi thiếu, không còn gọi generator demo/beta. Loại lịch spawn legacy, routing hai scene thử và phím F1/F2 cũ. Test combat cần lịch ngắn dùng fixture trong test assembly; không cần giữ scene thử trong game. Các test artwork/collider hữu ích được chuyển sang campaign thay vì xóa toàn bộ kiểm tra cũ.

## Package

Bỏ11 dependency trực tiếp của template không dùng: `2d.animation`, `2d.aseprite`, `2d.psdimporter`, `2d.spriteshape`, `2d.tilemap`, `2d.tilemap.extras`, `2d.tooling`, `collab-proxy`, `learn.iet-framework`, `timeline`, `visualscripting` (tiền tố chung `com.unity.`). Unity tự giải lại lock file; không nâng phiên bản package còn dùng. Built-in modules được giữ.

Giữ URP, Input System, 2D Sprite (Sprite Editor import API), Unity Test Framework, uGUI và tích hợp IDE. Số package trong lock giảm từ71 xuống56.

## Giữ lại có chủ đích

Giữ concept gốc, art runtime, `.meta`, DebugCaptures và bản build campaign hiện tại. Giữ `Library`, `UserSettings`, `.vscode` vì vẫn hữu ích cho Unity/IDE trên máy này. Không xóa các file chỉ vì chúng bị `.gitignore` bỏ qua.

Các shape đơn giản, visual fallback, hai LineRenderer template bị disable của CampaignRoad và CampaignSmokeCheck vẫn có công dụng. Road template cung cấp dữ liệu để refresh; smoke check chỉ chạy khi bật cờ kiểm thử. Chúng không được coi là module lỗi thời.

## Kiểm tra sau dọn

**Validate Project** kiểm tra sáu scene, missing script, reference hình/balance, lịch spawn/path index, danh sách build và dependency; xuất snapshot thông số. Kết quả suite và bản Windows được ghi ở [TEST_REPORT.md](../TEST_REPORT.md). GDD mới mô tả trạng thái hiện tại, gồm cả những phần chưa hiện thực và giới hạn authoring.
