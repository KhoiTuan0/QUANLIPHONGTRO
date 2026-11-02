import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

import 'package:mobileui/main.dart'; // Đảm bảo import đúng gói project của bạn

void main() {
  testWidgets('Kiểm tra khởi chạy ứng dụng Quản Lý Phòng Trọ', (WidgetTester tester) async {
    // Build ứng dụng với widget gốc thực tế là QuanLyPhongTroApp
    await tester.pumpWidget(const QuanLyPhongTroApp());

    // Kiểm tra xem màn hình Đăng nhập (hoặc tiêu đề ứng dụng) có xuất hiện hay không
    expect(find.text('Quản Lý Phòng Trọ'), findsWidgets);
  });
}