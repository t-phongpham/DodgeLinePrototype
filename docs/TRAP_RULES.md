# Luật Trap hiện tại — 2026-11-09

Luật này đang được dùng trong năm màn campaign. Không cần Generate lại scene. Sau khi thay code, bản EXE phải được build lại để nhận thay đổi.

Từ bộ luật ô xây v2, Bow/Cannon chỉ dùng vòng vàng TowerBuildPad. **Trap không dùng vòng vàng**; luật ô đường, slow và lượt kích hoạt bên dưới không đổi.

## Được phép

- Đặt Trap trên ô đường hợp lệ dù đang có enemy Advance, Alert hoặc Evade ở đó.
- Đặt trên các ô đường cuối gần căn cứ, trước vùng tiếp xúc đánh base.
- Trap mới có thể kích hoạt lên enemy đang overlap ở bước Physics2D tiếp theo, thông qua Enter/Stay. Không thêm thời gian lắp đặt hoặc arming delay.
- Các waypoint thẳng hàng dùng chung lưới Trap và điều kiện kích hoạt. Marker bắt đầu approach không tự tạo góc cua hoặc làm enemy mất eligibility khi bước qua marker.

## Vẫn bị cấm / không kích hoạt

- Ngoài đường, ngay spawn hoặc vùng sát góc cua thật.
- Đè footprint căn cứ hay footprint một contact slot, kể cả slot đang trống.
- Đặt thêm Trap cùng ô đã có Trap.
- Xây trong pause/kết quả hoặc không đủ energy.
- Enemy Dead, AttackBase, khác path hoặc thuộc đoạn không tương đương không kích hoạt Trap. AttackBase được loại trước khi ghi nhận enemy ID hoặc tiêu lượt; nhịp đánh base không bị slow.

Góc thật vẫn chừa ít nhất1 unit sau góc và0.6 unit trước góc, tính theo ô snap của dải đường. Chỉ các đoạn cùng hướng, thẳng hàng và liên tiếp mới tương đương; hai đoạn song song cách nhau hoặc đi qua một góc khác không được gộp. Trigger chỉ làm chậm enemy trên path tương ứng.

Footprint logic để đặt Trap có bán kính0.25; trigger slow vẫn0.8. Kiểm tra base/contact dùng footprint nhỏ, không dùng toàn vùng slow để cấm xây. Vòng tác dụng có thể phủ qua enemy đang đánh base nhưng chúng bị bỏ qua, giữ lại lượt cho enemy đang di chuyển.

Giá35, slow50% trong2 giây, tối đa6 enemy khác nhau. Mỗi enemy tiêu tối đa một lượt trên một Trap; Trap khác refresh thời gian slow, không nhân chồng tốc độ. Không sửa thông số vũ khí, lính hoặc lịch wave trong thay đổi này.

## Thử nhanh trong Unity

1. Mở `Assets/Scenes/MainMenu.unity`, Play rồi chọn Forest Road. Hoặc mở trực tiếp Level1 để kiểm tra.
2. Nhấn3, chọn ô giữa đường đang có Runner đi qua và click. Preview hợp lệ phải cho đặt; sau bước physics enemy chậm lại và Trap còn5/6 lượt. Ảnh Trap nằm dưới chân enemy nên có thể bị enemy che một phần.
3. Thử đặt trên đường cuối quanh `(5,0)` hoặc `(6,0)` ở màn1. Đây là các ô mới được mở. Căn cứ và các contact slot vẫn không cho đặt đè; preview hiển thị lý do từ chối.
4. Đặt Trap trước khi bắt đầu wave, gần đường vào base; quan sát enemy đi vào approach vẫn nhận slow khi đúng dải thẳng. Trong các màn hai tuyến, bẫy không tác động tuyến còn lại.
5. Cho enemy tới base rồi đặt Trap tại ô hợp lệ có vùng tác dụng phủ tới nó: enemy đang đứng đánh không được tiêu lượt, không được đánh chậm hơn.
6. Đặt lại tại ô đã có Trap, ngay spawn, ngoài đường hoặc sát góc thật: từ chối và không mất tiền.

Các ca test mới kiểm tra bằng physics thật: đặt dưới Runner/Wisp đang lùi, kích hoạt qua marker approach, giữ cấm base/contact, bỏ qua attacker đang overlap và giữ wind-up/attack interval. Kết quả thực tế được ghi trong [TEST_REPORT.md](../TEST_REPORT.md); các bước chuột/phím ở trên dành cho người chơi tự xác nhận cảm giác thao tác.
