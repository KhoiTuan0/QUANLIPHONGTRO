import 'dart:convert';

import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:mobileui/services/api_service.dart';

void main() {
  test(
    'registers a landlord-created customer with email credentials',
    () async {
      http.Request? capturedRequest;
      final client = MockClient((request) async {
        capturedRequest = request;
        return http.Response(
          jsonEncode({'message': 'Đăng ký thành công'}),
          200,
          headers: {'content-type': 'application/json'},
        );
      });

      final result = await ApiService.registerCustomerForLandlord(
        email: ' An@example.com ',
        fullName: 'Nguyễn Văn An',
        phone: '0901234567',
        cccd: '012345678901',
        address: 'Hà Nội',
        client: client,
      );

      expect(capturedRequest?.method, 'POST');
      expect(capturedRequest?.url.path, '/api/Auth/register');
      expect(jsonDecode(capturedRequest!.body), {
        'tenDangNhap': 'an@example.com',
        'matKhau': 'an@example.com',
        'hoTen': 'Nguyễn Văn An',
        'email': 'an@example.com',
        'soDienThoai': '0901234567',
        'vaiTro': 'Customer',
        'cccd': '012345678901',
        'diaChi': 'Hà Nội',
      });
      expect(result['success'], isTrue);
      expect(result['username'], 'an@example.com');
      expect(result['initialPassword'], 'an@example.com');
    },
  );

  test(
    'shows API duplicate account errors when registering customer',
    () async {
      final client = MockClient(
        (_) async => http.Response(
          jsonEncode({'message': 'Tên đăng nhập đã tồn tại!'}),
          400,
          headers: {'content-type': 'application/json'},
        ),
      );

      final result = await ApiService.registerCustomerForLandlord(
        email: 'an@example.com',
        fullName: 'Nguyễn Văn An',
        phone: '0901234567',
        client: client,
      );

      expect(result['success'], isFalse);
      expect(result['message'], 'Tên đăng nhập đã tồn tại!');
    },
  );

  test('loads customer profile by maKhach', () async {
    http.Request? capturedRequest;
    final client = MockClient((request) async {
      capturedRequest = request;
      return http.Response(
        jsonEncode({
          'maKhach': 4,
          'hoTen': 'Nguyễn Văn An',
          'email': 'an@example.com',
        }),
        200,
        headers: {'content-type': 'application/json'},
      );
    });

    final profile = await ApiService.getCustomerProfile(4, client: client);

    expect(capturedRequest?.method, 'GET');
    expect(capturedRequest?.url.path, '/api/KhachThue/4');
    expect(profile['hoTen'], 'Nguyễn Văn An');
  });

  test('updates only allowed profile fields through KhachThue DTO', () async {
    http.Request? capturedRequest;
    final client = MockClient((request) async {
      capturedRequest = request;
      return http.Response(
        jsonEncode({
          'success': true,
          'message': 'Cập nhật khách thuê thành công',
        }),
        200,
        headers: {'content-type': 'application/json'},
      );
    });

    final result = await ApiService.updateCustomerProfile(
      customerId: 4,
      profile: {
        'hoTen': 'Nguyễn Văn An',
        'cccd': '012345678901',
        'ngaySinh': '1998-05-20',
        'gioiTinh': 'Nam',
        'soDienThoai': '0901234567',
        'email': 'an@example.com',
        'diaChi': 'Hà Nội',
      },
      client: client,
    );

    expect(capturedRequest?.method, 'PUT');
    expect(capturedRequest?.url.path, '/api/KhachThue/4');
    expect(jsonDecode(capturedRequest!.body), {
      'hoTen': 'Nguyễn Văn An',
      'cccd': '012345678901',
      'ngaySinh': '1998-05-20',
      'gioiTinh': 'Nam',
      'soDienThoai': '0901234567',
      'email': 'an@example.com',
      'diaChi': 'Hà Nội',
    });
    expect(result['success'], isTrue);
  });

  test('posts current and new passwords to the agreed auth endpoint', () async {
    http.Request? capturedRequest;
    final client = MockClient((request) async {
      capturedRequest = request;
      return http.Response(
        jsonEncode({'success': true, 'message': 'Đổi mật khẩu thành công.'}),
        200,
        headers: {'content-type': 'application/json'},
      );
    });

    final result = await ApiService.changeCustomerPassword(
      customerId: 4,
      currentPassword: 'old-password',
      newPassword: 'new-password',
      client: client,
    );

    expect(capturedRequest?.method, 'POST');
    expect(capturedRequest?.url.path, '/api/Auth/change-password');
    expect(jsonDecode(capturedRequest!.body), {
      'maKhach': 4,
      'matKhauHienTai': 'old-password',
      'matKhauMoi': 'new-password',
    });
    expect(result['success'], isTrue);
  });

  test('explains when the backend has no password change route yet', () async {
    final client = MockClient((_) async => http.Response('Not Found', 404));

    final result = await ApiService.changeCustomerPassword(
      customerId: 4,
      currentPassword: 'old-password',
      newPassword: 'new-password',
      client: client,
    );

    expect(result['success'], isFalse);
    expect(result['message'], contains('/Auth/change-password'));
  });
}
