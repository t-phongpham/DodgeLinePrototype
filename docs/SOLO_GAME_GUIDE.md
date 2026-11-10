# Chơi và phát triển bản solo hoàn chỉnh

**Cập nhật 2026-11-10:** tháp bắt buộc dùng ô TowerBuildPad. Các scene đã được nâng cấp sẵn; mở MainMenu và Play, không cần Generate. Màn3 chỉnh ô trên xuống0.1 unit, các màn vẫn thắng được bằng chiến thuật mua hợp lệ. Xem [TEST_REPORT.md](../TEST_REPORT.md) để biết phạm vi đã thử.

**Trap đã được nới luật đặt theo yêu cầu ngày 2026-11-09:** xem [TRAP_RULES.md](TRAP_RULES.md) để thử đặt dưới enemy và gần căn cứ. Không cần tạo lại scene.

Ngày 2026-11-09 — Unity **6000.6.7f1**. Điểm vào game là **MainMenu**. Chiến dịch có năm màn, mở khóa nối tiếp, sao/điểm lưu cục bộ, xây/nâng/bán, pause, thắng/thua và retry. Module network là leaderboard online; hướng dẫn Dashboard, EXE local, Web và GitHub ở [NETWORK_AND_DEPLOYMENT.md](NETWORK_AND_DEPLOYMENT.md). Boss và âm thanh nằm ngoài phạm vi. Chất lượng phát hành cuối cần người thật chơi thử.

## Chạy ngay trong Unity

1. Mở `C:\DodgeLinePrototype` bằng Unity Hub với Editor6000.6.7f1. Chờ import/compile xong.
2. Ngoài Play Mode, vào **Assets > Scenes** và double-click **MainMenu**.
3. Mở **Window > General > Console** và tab **Game**, chọn tỉ lệ **16:9**. Nhấn Play.
4. Chọn **1. Forest Road** trong menu. Các màn sau mở khi bạn thắng màn trước. Nút **How to play** giải thích điều khiển.
5. `1` chọn Bow, `2` chọn Cannon từ màn2, `3` chọn Trap. Click trái đặt; click phải/Esc hủy chế độ đặt. **Bow/Cannon chỉ đặt trong vòng vàng còn trống**, click trong vòng sẽ snap về tâm; mỗi vòng chứa một tháp và không chia loại vũ khí. Đất trống ngoài vòng vẫn bị từ chối. Trap dùng luật riêng trên đường: được đặt dưới enemy đang di chuyển và tại ô cuối hợp lệ gần base, không chồng base/contact. AttackBase không tiêu lượt Trap hoặc bị giảm nhịp đánh.
6. Với màn1, đặt Bow quanh `(-1,1.5)` và `(5,1.5)`. Hủy chế độ đặt, click Bow đầu và nhấn `U` nâng L2, rồi `Space` bắt đầu. Tiếp tục dùng tiền kiếm được để thêm/nâng tháp; bấm Space khi bước sang Preparation của wave tiếp.
7. Click một tháp khi không ở chế độ đặt để xem damage/range/level. `U` nâng tối đa L3, `X` bán trả70% tổng đã trả. Trap không nâng cấp, có thể bán khi vẫn còn trên bản đồ.
8. `P` pause; `Esc` pause khi không đang đặt công trình. Resume tiếp tục đúng trận. `R` chơi lại màn hiện tại từ HP20/energy150. Chọn **Campaign menu** ở pause/kết quả để về menu.
9. Thắng hết wave: **Next stage** đi tiếp. Thắng màn5 hoàn tất chiến dịch. Retry hoặc menu vẫn có để chơi lại. Sao/điểm/mở khóa được giữ; tiền/tower/base HP không chuyển giữa các màn.

Menu và kết quả thắng có **Online leaderboard**. Nhập tên, Save name, chọn màn để xem Top 10/hạng. Thắng sẽ lưu local trước rồi gửi điểm; lỗi mạng giữ pending và không ngăn Next stage/Retry. Tạo bảng trong Dashboard theo hướng dẫn network trước khi xác nhận bảng online thật.

Khi trong Editor, **Quit game** không đóng Play Mode thay cho nút Play của Unity; hãy bấm Play lần nữa để dừng. Thay đổi object/data thử nghiệm trong lúc Play cần được quản lý cẩn thận: Transform của runtime entity không được lưu thành bố cục màn; ScriptableObject asset có thể bị sửa thật trong Play, vì vậy chỉnh asset ngoài Play.

Nếu scene chưa có: **Tools > Dodge Line > Create Game Scenes**. Lệnh tạo phần còn thiếu và đăng ký sáu scene vào build; giữ các scene/data campaign đã tồn tại. Nó không phải nút áp lại mặc định vào màn bạn vừa chỉnh. Repo hiện chỉ giữ pipeline campaign; các scene demo/beta cũ đã được loại.

## Chơi không cần Unity

Mở **Builds > Windows > DodgeLine.exe**. Chương trình vào menu và cho chơi cả chiến dịch. Từ Unity, tạo lại bản này bằng **Tools > Dodge Line > Build > Windows (Local Testing)**.

Lệnh tự động hiện xuất **Development Build** để giữ khả năng chẩn đoán và kiểm thử bản chạy. Khi muốn xuất bản thường: **File > Build Profiles**, chọn Windows, đặt Scene List theo thứ tự `MainMenu`, `Level1`…`Level5`, tắt **Development Build**, rồi **Build** vào thư mục mới. Bản thường đó cần được chạy thử lại riêng; chưa được coi là đã kiểm chứng chỉ vì bản Development đã qua test.

Gửi **thư mục `Builds/Windows`** cho người chơi, gồm EXE, `DodgeLine_Data` và DLL đi kèm; bỏ thư mục `*_BackUpThisFolder_ButDontShipItWithYourGame` nếu Unity sinh ra vì đó là dữ liệu debug dành cho người phát triển. Người nhận không cần mở project Unity. Không chỉ gửi riêng EXE. Game lưu tiến trình bằng PlayerPrefs của ứng dụng trên máy đó; hiện chưa có cloud save, profile nhiều người hoặc nút xóa tiến trình.

Tham khảo chính thức: [Unity — Build Profiles](https://docs.unity.com/en-us/engine/6000.0/manual/building-and-publishing/build-settings) và [Unity — PlayerPrefs](https://docs.unity.com/en-us/engine/6000.0/script-reference/unityengine/playerprefs).

## Nội dung nằm ở đâu

| Bạn muốn sửa | Nơi sửa |
| --- | --- |
| Menu và liên kết 5 màn | `Assets/Scenes/MainMenu.unity`, component CampaignMenu |
| Bản đồ / route / base | `Assets/Scenes/Levels/Level1.unity` tới Level5 |
| Tên, lời hướng dẫn, thứ tự mua của chiến thuật test và điểm Trap gợi ý | `Assets/Data/Levels/LevelN/Stage.asset` |
| Ô bắt buộc để đặt Bow/Cannon | Trong scene: `Dodge Line > Tower build pads > Build pad NN`, component TowerBuildPad |
| Wave, thời gian chuẩn bị, energy, multiplier | `Assets/Data/Levels/LevelN/Settings.asset` |
| HP/tốc/armor/CPA/charges | `Assets/Data/Enemies`: Runner, RetreatWisp, IronGuard |
| Giá/damage/range/nâng cấp/projectile | `Assets/Data/Weapons`: ThornBow, DustCannon |
| Hình và kích thước enemy/vũ khí | `Assets/Data/GameArt.asset` |
| Năm nền, base alpha, texture đường | `Assets/Art/Campaign` |
| Quy tắc và bố trí từng màn | [GDD.md](GDD.md) |
| Project/environment/bảng leaderboard | `Assets/Resources/LeaderboardConfig.json` |

Năm màn dùng chung EnemyData/WeaponData/GameArtSet trong `Assets/Data`. Chỉnh một asset dùng chung sẽ ảnh hưởng mọi màn tham chiếu nó. Settings/wave/multiplier riêng cho từng màn. Sprite runtime nằm ở `Assets/Art/Sprites`; shape/material ở `Assets/Art/Common`. Muốn có biến thể riêng, tạo asset/PNG mới rồi gán vào Settings hoặc GameArt tương ứng.

## Tự chỉnh một map trong Editor

1. Dừng Play, mở scene campaign muốn chỉnh và Ctrl+S trước khi bắt đầu.
2. Chọn **Enemy Route 1/2** dưới **Dodge Line**. Trong component **EnemyPath**, mở **Waypoints**. Component hỗ trợ cả đường thẳng và nhiều đoạn.
3. Chỉnh tọa độ các điểm giữa bằng world units. `Start` phải khớp điểm0. Giữ `Path Id` riêng cho mỗi tuyến. Giữ `width=1` nếu chưa muốn retest kích thước đường. Các route không nên cắt nhau, chồng lên nhau hoặc tự giao; chưa có hệ merge ưu tiên riêng.
4. **Giữ nguyên hai điểm cuối** `(5.5,y)` và `(6.55,y)`, `approachStartX=5.5`, cũng như base `(7.6,0)`, collider size `(1.6,3.6)` trong bước chỉnh đầu tiên. Chúng kết nối với hệ contact slot. Hai route dùng y dương/âm như hiện tại để chia contact trên/dưới. Đừng chỉ kéo Transform của object Route: waypoint là tọa độ world, Transform không phải dữ liệu navigation.
5. Chạy **Tools > Dodge Line > Refresh Roads In Open Scene** rồi Ctrl+S. Hình đường được vẽ lại từ chính waypoint vừa sửa. Luật đặt tháp dùng khoảng cách tới từng đoạn, không dùng hình chữ nhật bao cả đường chữ S.
6. Bẫy snap vào các điểm cách1 unit trên mỗi dải thẳng. Các waypoint thẳng hàng được gộp cho lưới Trap và điều kiện kích hoạt; marker approach không tạo khoảng cấm mới. Ở góc thật vẫn loại vùng0.6 unit trước góc và yêu cầu ít nhất1 unit sau góc; đoạn quá ngắn không có ô trap. Đoạn thẳng cuối được dùng tới endpoint, sau đó PlacementSystem loại footprint base và contact slot riêng. Enemy tự đổi SegmentId theo route; trap cho phép các segment thẳng hàng tương đương trên cùng path, không cho kích hoạt xuyên góc hoặc từ path khác.
7. Sau đổi route, chọn **Tower build pads > Build pad NN**. Di chuyển Transform theo X/Y để đổi ô; vòng LineRenderer dùng local-space nên đi cùng tâm xét đặt. Giữ Scale=(1,1,1), không xoay/nghiêng pad hoặc root. Muốn thêm ô, Duplicate một pad rồi đặt ở chỗ hợp lệ; xóa/disable pad thì chỗ đó không còn được xây. Mọi ô phải đủ đất cho footprint Cannon0.45, không chồng đường/base/approach/obstacle. Sau khi sửa, cập nhật `Stage.suggestedBows`/`suggestedCannons` nếu chúng trỏ tới ô đã dời; các mảng này chỉ là thứ tự mua của test bot, không cấp quyền đặt. `Validate Project` kiểm tra cả hai phần. Không chỉ thêm một tọa độ trong Stage rồi coi đó là ô đã có.
8. Thử lại bằng Play: tower ngoài đường, trap trên đường, lùi qua góc, queue tại base và thắng/thua. Nâng độ khó bằng wave sau khi đường hoạt động ổn.

Nền **Painted environment (visual only)** có SpriteRenderer ở order=-10. Đường có edge=-4, surface=-3. Base ở1, trap2, tháp3, enemy5, HP7/8, cảnh báo8, đạn10, preview12. Dùng trật tự này khi thêm đồ trang trí. Màu sắc/texture phong cảnh không tự quyết định vùng cấm xây.

Base **Forest Outpost - 20 HP** có BoxCollider2D, BaseController và child artwork. Hình được fit vào footprint vật lý; base flash không thay scale ảnh. Nếu thay base art, giữ mép tường trái nhìn thấy ở mép collider; nếu hình và hitbox lệch nhau, cảm giác chạm base sẽ sai. Không thêm collider quanh từng chi tiết mái/đèn. Chỉnh hình riêng, giữ collider trên object gốc.

Hiện mặc định không có chướng ngại gameplay ở giữa bản đồ. Nếu thêm đá chặn bắn: tạo GameObject với BoxCollider2D trigger và ProjectileBlocker, đồng thời khai báo đúng Rect đó trong `PlacementSystem.obstacles`. Hai phần cần khớp vì mask đặt tháp hiện là dữ liệu Rect riêng. Cây/đá chỉ trang trí thì không cần collider/Rect.

## Chỉnh wave có chủ đích

Mở Settings của màn, vào **Waves > Spawns**. Enemy là data asset, Delay là giây sau lần spawn thực tế trước, Path Index bắt đầu từ0. Entry đầu tiên tính Delay từ lúc bắt đầu wave. `Waves` là nguồn lịch spawn duy nhất; không còn mảng Wave legacy.

Giữ ba nhịp chính: Wisp trước có khoảng lùi → nghỉ → Guard và nhóm Runner. Không đặt Runner tốc1.6 sát ngay sau Wisp tốc1.3 trừ khi bạn chủ ý tạo wave hạn chế retreat. Không gán Path Index1 cho màn chỉ có một tuyến. Sau mỗi lần sửa, ghi HP còn lại, lúc enemy bắt đầu đánh base, số tiền và tháp đã mua; thay một nhóm số liệu mỗi lượt chơi.

Để tạo biến thể trong phạm vi năm màn: giữ menu/scene names, đổi tuyến giữa, các ô TowerBuildPad, sắc thái background và lịch wave, rồi kiểm tra lại chiến thuật thắng với đúng các ô đó. Muốn thêm màn6 phải mở rộng giới hạn mở khóa/scene routing/menu/build list trong code; không chỉ thêm một asset rồi tự xuất hiện.

## Từ bản solo này đến bản phát hành

Vòng chơi và nội dung đã tách khỏi graybox. Các bước tiếp theo chủ yếu là sản xuất và kiểm chứng:

1. Chơi cả năm màn bằng chuột/phím thật, từ fresh save, thử cả sai lầm và chiến thuật khác. Mời vài người chưa biết game chơi; quan sát chỗ họ không hiểu nút, terrain hoặc Wisp.
2. Chỉnh wave/economy dựa trên kết quả. Chiến thuật tự động thắng chứng minh có đường thắng hợp lệ, không chứng minh game có độ khó hay cảm giác cuối cùng phù hợp.
3. Đồng bộ góc nhìn và nét vẽ enemy/vũ khí; thêm sprite-sheet walk/attack/death nếu cần. Bản hiện tại giữ sprite tĩnh với flash/tint, chưa có hoạt ảnh nhân vật đầy đủ.
4. Hoàn thiện font/biểu tượng, hướng dẫn ngắn theo wave, lựa chọn độ phân giải và độ đọc HUD. UI hiện là IMGUI cho mouse/keyboard, chưa có controller/touch hay menu accessibility đầy đủ.
5. Build và thử trên máy Windows khác, kiểm tra save/retry/resume, quyền phân phối art và bản credits. Tài liệu nguồn art: [ART_PROVENANCE.md](ART_PROVENANCE.md). Kiểm tra leaderboard trên hai danh tính sau Dashboard setup; Web cần được kiểm tra lại trên URL host thật.

Kiểm tra reference: ngoài Play, lưu scene rồi chạy **Tools > Dodge Line > Validate Project**. Công cụ mở lần lượt sáu scene, kiểm tra script/reference/wave/build list, xuất snapshot số liệu vào `TestResults/ProjectValidation`, rồi mở MainMenu. Kiểm tra này không thay thế chơi thử.

Test tự động: **Window > General > Test Runner**, chạy EditMode và PlayMode. Kết quả đã chạy bằng công cụ nằm ở [TEST_REPORT.md](../TEST_REPORT.md). Khi lỗi, mở Console, chọn dòng đỏ đầu tiên, copy cả message + stack trace, ghi scene/wave/thao tác rồi gửi vào chat. Với lỗi hình, đặt ảnh Game và Inspector vào DebugCaptures.
