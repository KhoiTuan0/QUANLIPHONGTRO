import 'dart:convert';

import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:mobileui/services/api_service.dart';

void main() {
  test('lấy chỉ số điện nước và chỉ giữ các phòng thuộc khách', () async {
    final requestedPaths = <String>[];
    final client = MockClient((request) async {
      requestedPaths.add(request.url.path);
      if (request.url.path == '/api/HopDong/customer/5') {
        return _jsonResponse([
          {'maHopDong': 10, 'maPhong': 2},
        ]);
      }
      if (request.url.path == '/api/ChiSoDienNuoc') {
        return _jsonResponse([
          {
            'maChiSo': 1,
            'maPhong': 2,
            'loaiChiSo': 'Điện',
            'thang': 8,
            'nam': 2026,
          },
          {
            'maChiSo': 2,
            'maPhong': 99,
            'loaiChiSo': 'Nước',
            'thang': 8,
            'nam': 2026,
          },
        ]);
      }
      return http.Response('', 404);
    });

    final readings = await ApiService.getCustomerMeterReadings(
      5,
      client: client,
    );

    expect(readings, hasLength(1));
    expect((readings.single as Map)['maPhong'], 2);
    expect(
      requestedPaths,
      containsAll(['/api/HopDong/customer/5', '/api/ChiSoDienNuoc']),
    );
  });

  test('lấy lịch sử dịch vụ theo từng hợp đồng của khách', () async {
    final client = MockClient((request) async {
      if (request.url.path == '/api/HopDong/customer/5') {
        return _jsonResponse([
          {'maHopDong': 10, 'maPhong': 2},
          {'maHopDong': 11, 'maPhong': 3},
        ]);
      }
      if (request.url.path == '/api/DangKyDichVu') {
        final contractId = request.url.queryParameters['maHopDong'];
        return _jsonResponse([
          {
            'maDangKy': contractId == '10' ? 1 : 2,
            'maHopDong': int.parse(contractId!),
            'tenDichVu': 'Internet',
            'trangThai': 'Đang sử dụng',
          },
        ]);
      }
      return http.Response('', 404);
    });

    final services = await ApiService.getCustomerServiceHistory(
      5,
      client: client,
    );

    expect(services, hasLength(2));
    expect(
      services.map((entry) => (entry as Map)['maHopDong']),
      containsAll([10, 11]),
    );
    expect((services.first as Map)['maPhong'], isNotNull);
  });

  test(
    'lịch sử thanh toán tổng hợp hóa đơn qua các hợp đồng của khách',
    () async {
      final client = MockClient((request) async {
        if (request.url.path == '/api/HopDong/customer/5') {
          return _jsonResponse([
            {'maHopDong': 10, 'maPhong': 2},
          ]);
        }
        if (request.url.path == '/api/HoaDon/hop-dong/10') {
          return _jsonResponse([
            {
              'maHoaDon': 30,
              'thang': 8,
              'nam': 2026,
              'tongTien': 1000000,
              'daThanhToan': 400000,
              'conLai': 600000,
              'trangThai': 'Thanh toán một phần',
            },
          ]);
        }
        return http.Response('', 404);
      });

      final invoices = await ApiService.getInvoicesForCustomer(
        5,
        client: client,
      );

      expect(invoices, hasLength(1));
      expect((invoices.single as Map)['daThanhToan'], 400000);
      expect((invoices.single as Map)['conLai'], 600000);
      expect((invoices.single as Map)['maPhong'], 2);
    },
  );
}

http.Response _jsonResponse(Object body) => http.Response(
  jsonEncode(body),
  200,
  headers: {'content-type': 'application/json'},
);
