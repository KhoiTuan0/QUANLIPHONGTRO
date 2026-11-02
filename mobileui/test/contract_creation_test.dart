import 'dart:convert';

import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:mobileui/services/api_service.dart';

void main() {
  test('loads rooms for the contract room selector', () async {
    final client = MockClient((request) async {
      expect(request.url.path, '/api/Phong');
      return http.Response(
        jsonEncode([
          {'maPhong': 8, 'tenPhong': 'Phòng A101', 'trangThai': 'Trống'},
          {'maPhong': 9, 'tenPhong': 'Phòng A102', 'trangThai': 'Đang thuê'},
        ]),
        200,
        headers: {'content-type': 'application/json'},
      );
    });

    final rooms = await ApiService.getRoomsForContractCreation(client: client);

    expect(rooms, hasLength(2));
    expect((rooms.first as Map)['trangThai'], 'Trống');
  });

  test('posts contract data using the API model property names', () async {
    http.Request? capturedRequest;
    final client = MockClient((request) async {
      capturedRequest = request;
      return http.Response(
        jsonEncode({'success': true, 'message': 'Tạo hợp đồng thành công.'}),
        201,
        headers: {'content-type': 'application/json'},
      );
    });

    final result = await ApiService.createContract({
      'maPhong': 8,
      'maKhachDaiDien': 21,
      'ngayBatDau': '2026-10-11',
      'ngayKetThuc': '2027-10-10',
      'giaThueThang': 2600000,
      'tienCoc': 2600000,
      'trangThai': 'Active',
    }, client: client);

    expect(capturedRequest?.method, 'POST');
    expect(capturedRequest?.url.path, '/api/HopDong');
    expect(jsonDecode(capturedRequest!.body), {
      'maPhong': 8,
      'maKhachDaiDien': 21,
      'ngayBatDau': '2026-10-11',
      'ngayKetThuc': '2027-10-10',
      'giaThueThang': 2600000,
      'tienCoc': 2600000,
      'trangThai': 'Active',
    });
    expect(result['success'], isTrue);
    expect(result['message'], 'Tạo hợp đồng thành công.');
  });

  test('does not report success when the API rejects a contract', () async {
    final client = MockClient((request) async {
      return http.Response(
        jsonEncode({'success': false, 'message': 'Phòng đã có hợp đồng.'}),
        400,
        headers: {'content-type': 'application/json'},
      );
    });

    final result = await ApiService.createContract({
      'maPhong': 8,
    }, client: client);

    expect(result['success'], isFalse);
    expect(result['message'], 'Phòng đã có hợp đồng.');
  });
}
