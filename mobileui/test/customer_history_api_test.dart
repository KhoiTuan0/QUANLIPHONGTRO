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
      if (request.url.path == '/api/KhachThue/5/lich-su/dien-nuoc') {
        return _jsonResponse([
          {
            'maChiSo': 1,
            'tenPhong': 'A102',
            'loaiChiSo': 'Điện',
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
    expect((readings.single as Map)['tenPhong'], 'A102');
    expect(
      requestedPaths,
      ['/api/KhachThue/5/lich-su/dien-nuoc'],
    );
  });

  test('lấy lịch sử dịch vụ theo từng hợp đồng của khách', () async {
    final client = MockClient((request) async {
      if (request.url.path == '/api/KhachThue/5/lich-su/dich-vu') {
        return _jsonResponse([
          {
            'maDangKy': 1,
            'maHopDong': 10,
            'tenDichVu': 'Internet',
            'tenPhong': 'A102',
            'donGia': 100000,
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

    expect(services, hasLength(1));
    expect(
      services.map((entry) => (entry as Map)['maHopDong']),
      [10],
    );
    expect((services.first as Map)['tenPhong'], 'A102');
    expect((services.first as Map)['donGia'], 100000);
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
