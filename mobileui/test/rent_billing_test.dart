import 'dart:async';
import 'dart:convert';
import 'dart:typed_data';

import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:mobileui/services/api_service.dart';
import 'package:mobileui/utils/rent_calculator.dart';

void main() {
  group('calculateProratedRent', () {
    test('uses actual days in the calendar month and includes both dates', () {
      final amount = calculateProratedRent(
        monthlyRent: 3000000,
        from: DateTime(2026, 2, 1),
        through: DateTime(2026, 2, 14),
      );

      expect(amount, 1500000);
    });

    group('calculateMeterCharge', () {
      test('calculates usage times unit price and rounds to whole VND', () {
        expect(
          calculateMeterCharge(
            oldReading: 13500,
            currentReading: 13627.4,
            unitPrice: 3500,
          ),
          445900,
        );
      });

      test('rejects a current reading lower than the old reading', () {
        expect(
          () => calculateMeterCharge(
            oldReading: 100,
            currentReading: 99,
            unitPrice: 3500,
          ),
          throwsArgumentError,
        );
      });
    });

    test('rounds to whole VND and rejects ranges crossing a month', () {
      expect(
        calculateProratedRent(
          monthlyRent: 1000000,
          from: DateTime(2026, 4, 1),
          through: DateTime(2026, 4, 1),
        ),
        33333,
      );
      expect(
        () => calculateProratedRent(
          monthlyRent: 1000000,
          from: DateTime(2026, 4, 30),
          through: DateTime(2026, 5, 1),
        ),
        throwsArgumentError,
      );
    });
  });

  test('posts a manual invoice using the agreed HoaDon DTO fields', () async {
    http.Request? capturedRequest;
    final client = MockClient((request) async {
      capturedRequest = request;
      return http.Response(
        jsonEncode({
          'success': true,
          'message': 'Đã lập hóa đơn.',
          'data': {'maHoaDon': 88, 'tongTien': 1500000},
        }),
        200,
        headers: {'content-type': 'application/json'},
      );
    });

    final result = await ApiService.createManualInvoice(
      contractId: 12,
      month: 2,
      year: 2026,
      total: 1500000,
      note: 'Tiền thuê 14 ngày',
      client: client,
    );

    expect(capturedRequest?.method, 'POST');
    expect(capturedRequest?.url.path, '/api/HoaDon');
    expect(jsonDecode(capturedRequest!.body), {
      'maHopDong': 12,
      'thang': 2,
      'nam': 2026,
      'tongTien': 1500000,
      'ghiChu': 'Tiền thuê 14 ngày',
    });
    expect(result['success'], isTrue);
  });

  test('loads pending fines for the selected contract', () async {
    http.Request? capturedRequest;
    final client = MockClient((request) async {
      capturedRequest = request;
      return http.Response(
        jsonEncode([
          {
            'maViPham': 9,
            'noiDung': 'Gây ồn sau giờ quy định',
            'tienPhat': 250000,
          },
        ]),
        200,
        headers: {'content-type': 'application/json'},
      );
    });

    final violations = await ApiService.getPendingInvoiceViolations(
      contractId: 12,
      client: client,
    );

    expect(
      capturedRequest?.url.path,
      '/api/HoaDon/hop-dong/12/vi-pham-chua-tinh',
    );
    expect(violations, hasLength(1));
    expect(violations.single['maViPham'], 9);
    expect(violations.single['tienPhat'], 250000);
  });

  test(
    'shows the server message when the contract already has an invoice',
    () async {
      final client = MockClient((request) async {
        return http.Response(
          jsonEncode({
            'success': false,
            'message': 'Hóa đơn tháng 2/2026 của hợp đồng #12 đã tồn tại.',
          }),
          400,
          headers: {'content-type': 'application/json'},
        );
      });

      final result = await ApiService.createManualInvoice(
        contractId: 12,
        month: 2,
        year: 2026,
        total: 1500000,
        note: 'Tiền thuê 14 ngày',
        client: client,
      );

      expect(result['success'], isFalse);
      expect(result['message'], contains('đã tồn tại'));
    },
  );

  test(
    'uploads meter photo to OCR service and reads its returned value',
    () async {
      http.Request? capturedRequest;
      final client = MockClient((request) async {
        capturedRequest = request;
        return http.Response(
          jsonEncode({'chiSoMoi': 13627.4, 'tieuThu': 127.4}),
          200,
          headers: {'content-type': 'application/json'},
        );
      });

      final result = await ApiService.recognizeElectricMeter(
        imageBytes: Uint8List.fromList([1, 2, 3, 4]),
        filename: 'meter.jpg',
        roomId: 7,
        oldReading: 13500,
        month: 9,
        year: 2026,
        client: client,
      );

      expect(capturedRequest?.method, 'POST');
      expect(capturedRequest?.url.toString(), ApiService.meterOcrUrl);
      expect(
        capturedRequest?.headers['content-type'],
        startsWith('multipart/form-data'),
      );
      expect(capturedRequest?.body, contains('name="maPhong"'));
      expect(capturedRequest?.body, contains('name="chiSoCu"'));
      expect(capturedRequest?.body, contains('filename="meter.jpg"'));
      expect(result['success'], isTrue);
      expect(result['chiSoMoi'], 13627.4);
      expect(result['tieuThu'], closeTo(127.4, 0.001));
    },
  );

  test('rejects OCR readings below the previous meter value', () async {
    final client = MockClient(
      (_) async => http.Response(
        jsonEncode({'chiSoMoi': 120.0}),
        200,
        headers: {'content-type': 'application/json'},
      ),
    );

    final result = await ApiService.recognizeElectricMeter(
      imageBytes: Uint8List.fromList([1]),
      filename: 'meter.jpg',
      roomId: 7,
      oldReading: 135,
      month: 9,
      year: 2026,
      client: client,
    );

    expect(result['success'], isFalse);
    expect(result['message'], contains('nhỏ hơn chỉ số cũ'));
  });

  test('returns an error when OCR response exceeds its timeout', () async {
    final pendingResponse = Completer<http.Response>();
    final client = MockClient((_) => pendingResponse.future);

    final result = await ApiService.recognizeElectricMeter(
      imageBytes: Uint8List.fromList([1]),
      filename: 'meter.jpg',
      roomId: 7,
      oldReading: 135,
      month: 9,
      year: 2026,
      client: client,
      timeout: const Duration(milliseconds: 10),
    );

    expect(result['success'], isFalse);
    expect(result['message'], contains('không phản hồi'));
    pendingResponse.complete(http.Response('', 200));
  });

  test('aborts OCR request when its abort trigger completes', () async {
    final abortTrigger = Completer<void>();
    final client = MockClient((request) async {
      abortTrigger.complete();
      await abortTrigger.future;
      throw http.RequestAbortedException(request.url);
    });

    final result = await ApiService.recognizeElectricMeter(
      imageBytes: Uint8List.fromList([1]),
      filename: 'meter.jpg',
      roomId: 7,
      oldReading: 135,
      month: 9,
      year: 2026,
      client: client,
      abortTrigger: abortTrigger.future,
    );

    expect(result['success'], isFalse);
    expect(result['message'], contains('Đã dừng'));
  });

  test('loads customers from the KhachThue endpoint for name lookup', () async {
    http.Request? capturedRequest;
    final client = MockClient((request) async {
      capturedRequest = request;
      return http.Response(
        jsonEncode([
          {'maKhach': 7, 'hoTen': 'Nguyễn Văn An'},
        ]),
        200,
        headers: {'content-type': 'application/json'},
      );
    });

    final customers = await ApiService.getCustomersForRentBilling(
      client: client,
    );

    expect(capturedRequest?.url.path, '/api/KhachThue');
    expect((customers.single as Map)['hoTen'], 'Nguyễn Văn An');
  });
}
