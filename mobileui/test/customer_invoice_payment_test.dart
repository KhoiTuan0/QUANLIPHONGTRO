import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:mobileui/screens/customer_invoice_screen.dart';
import 'package:mobileui/services/api_service.dart';

void main() {
  test('ghi nhận thanh toán qua đúng API thu tiền hóa đơn', () async {
    http.Request? capturedRequest;
    final client = MockClient((request) async {
      capturedRequest = request;
      return http.Response(
        jsonEncode({
          'success': true,
          'message': "Đã thu đủ, hóa đơn chuyển sang 'Đã thanh toán'.",
          'data': {
            'maHoaDon': 123,
            'daThanhToan': 1250000,
            'conLai': 0,
            'trangThai': 'Đã thanh toán',
          },
        }),
        200,
        headers: {'content-type': 'application/json'},
      );
    });

    final response = await ApiService.collectInvoicePayment(
      invoiceId: 123,
      amount: 1250000,
      paymentMethod: 'Mô phỏng thanh toán',
      note: 'Kiểm thử',
      client: client,
    );

    expect(capturedRequest?.method, 'PUT');
    expect(capturedRequest?.url.path, '/api/HoaDon/123/thu-tien');
    expect(jsonDecode(capturedRequest!.body), {
      'soTienThu': 1250000,
      'phuongThuc': 'Mô phỏng thanh toán',
      'ghiChu': 'Kiểm thử',
    });
    expect(response['success'], isTrue);
    expect((response['data'] as Map)['conLai'], 0);
  });

  testWidgets('hiển thị kết quả thanh toán API và trả về trạng thái hóa đơn', (
    tester,
  ) async {
    const updatedInvoice = {
      'maHoaDon': 123,
      'tongTien': 1250000,
      'daThanhToan': 1250000,
      'conLai': 0,
      'trangThai': 'Đã thanh toán',
    };
    Map<String, dynamic>? returnedInvoice;

    await tester.pumpWidget(
      MaterialApp(
        home: Builder(
          builder: (context) => Scaffold(
            body: Center(
              child: ElevatedButton(
                onPressed: () async {
                  returnedInvoice = await Navigator.push<Map<String, dynamic>>(
                    context,
                    MaterialPageRoute(
                      builder: (context) => const PaymentSuccessScreen(
                        invoice: updatedInvoice,
                        invoiceId: '123',
                        amount: 1250000,
                        message: 'Đã thu đủ, hóa đơn đã thanh toán.',
                      ),
                    ),
                  );
                },
                child: const Text('Mở kết quả'),
              ),
            ),
          ),
        ),
      ),
    );

    await tester.tap(find.text('Mở kết quả'));
    await tester.pumpAndSettle();
    expect(find.text('Thanh toán thành công'), findsOneWidget);
    expect(find.text('Đã thu đủ, hóa đơn đã thanh toán.'), findsOneWidget);

    await tester.tap(find.text('QUAY LẠI HÓA ĐƠN'));
    await tester.pumpAndSettle();
    expect(returnedInvoice?['trangThai'], 'Đã thanh toán');
    expect(returnedInvoice?['conLai'], 0);
  });
}
