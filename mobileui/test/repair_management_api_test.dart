import 'dart:convert';

import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:mobileui/services/api_service.dart';

void main() {
  test('chủ trọ cập nhật yêu cầu qua PUT /SuaChua/{id}', () async {
    http.Request? capturedRequest;
    final client = MockClient((request) async {
      capturedRequest = request;
      return http.Response(
        jsonEncode({
          'success': true,
          'message': 'Cập nhật thành công',
          'data': {'maYeuCau': 7, 'trangThai': 'Đang xử lý', 'chiPhi': 0},
        }),
        200,
        headers: {'content-type': 'application/json'},
      );
    });

    final result = await ApiService.updateRepairRequest(7, {
      'trangThai': 'Đang xử lý',
    }, client: client);

    expect(capturedRequest?.method, 'PUT');
    expect(capturedRequest?.url.path, '/api/SuaChua/7');
    expect(jsonDecode(capturedRequest!.body), {'trangThai': 'Đang xử lý'});
    expect(result['success'], isTrue);
    expect(result['message'], 'Cập nhật thành công');
  });

  test('không báo cập nhật thành công khi API từ chối yêu cầu', () async {
    final client = MockClient(
      (_) async => http.Response(
        jsonEncode({'success': false, 'message': 'Không tìm thấy yêu cầu'}),
        404,
        headers: {'content-type': 'application/json'},
      ),
    );

    final result = await ApiService.updateRepairRequest(999, {
      'trangThai': 'Đang xử lý',
    }, client: client);

    expect(result['success'], isFalse);
    expect(result['message'], 'Không tìm thấy yêu cầu');
  });
}
