import 'package:flutter_test/flutter_test.dart';
import 'package:mobileui/services/api_service.dart';

// Opt-in only. Credentials come from an ignored fixture on a disposable SQL copy.
// flutter test test/live_api_test.dart --dart-define=QLPT_LIVE_TEST=true
//   --dart-define=API_BASE_URL=http://127.0.0.1:5187/api
//   --dart-define-from-file=<private fixture.json>
void main() {
  test('Mobile session and scoped endpoints against live API/SQL', () async {
    const tenant = String.fromEnvironment('tenant');
    const password = String.fromEnvironment('password');
    const other = String.fromEnvironment('other');
    const admin = String.fromEnvironment('admin');
    final ownId = int.parse(const String.fromEnvironment('customerId'));
    final otherId = int.parse(const String.fromEnvironment('otherCustomerId'));
    final login = await ApiService.login(tenant, password);
    expect(login['success'], isTrue);
    expect(login['maKhach'], ownId);
    final profile = await ApiService.getCustomerProfile(ownId);
    expect(profile['maKhach'], ownId);
    await expectLater(ApiService.getCustomerProfile(otherId), throwsException);
    await ApiService.getCustomerMeterReadings(ownId);
    await ApiService.getCustomerServiceHistory(ownId);
    await ApiService.getCustomerNotifications(ownId);
    await expectLater(ApiService.getCustomerNotifications(otherId), throwsException);
    await ApiService.getContractExtensionRequests();
    final invoices = await ApiService.getInvoicesForCustomer(ownId);
    expect(invoices, isNotEmpty);
    final invoiceId = (invoices.first as Map)['maHoaDon'] as int;
    final pdf = await ApiService.downloadInvoicePdf(invoiceId);
    expect(String.fromCharCodes(pdf.take(5)), '%PDF-');
    expect(await ApiService.sendMessage({
      'customerIdentity': ownId, 'content': 'Kiểm thử tích hợp trên bản sao CSDL',
      'isAdmin': true,
    }), isTrue);
    final messages = await ApiService.getMessages(ownId);
    expect(messages.any((entry) => entry is Map &&
        entry['content'] == 'Kiểm thử tích hợp trên bản sao CSDL' &&
        entry['isAdmin'] == false), isTrue,
        reason: 'The server must derive sender role despite forged isAdmin');
    await expectLater(ApiService.getMessages(otherId), throwsException);
    await ApiService.logout();
    await expectLater(ApiService.getCustomerProfile(ownId), throwsException);
    expect((await ApiService.login(other, password))['success'], isTrue);
    await expectLater(ApiService.getMessages(ownId), throwsException);
    await expectLater(ApiService.downloadInvoicePdf(invoiceId), throwsException);
    await ApiService.logout();
    expect((await ApiService.login(admin, password))['success'], isTrue);
    await ApiService.getMessages(ownId);
    await ApiService.logout();
  }, skip: !const bool.fromEnvironment('QLPT_LIVE_TEST'),
     timeout: const Timeout(Duration(minutes: 2)));
}
