import 'package:flutter/material.dart';
import 'package:printing/printing.dart';
import '../services/api_service.dart';
import 'customer_history_screen.dart';

class CustomerInvoiceScreen extends StatefulWidget {
  final int maKhach;
  const CustomerInvoiceScreen({super.key, required this.maKhach});

  @override
  State<CustomerInvoiceScreen> createState() => _CustomerInvoiceScreenState();
}

class _CustomerInvoiceScreenState extends State<CustomerInvoiceScreen> {
  List<dynamic> _invoices = [];
  bool _isLoading = true;
  String? _loadError;

  @override
  void initState() {
    super.initState();
    _loadInvoices();
  }

  Future<void> _loadInvoices() async {
    if (mounted) {
      setState(() {
        _isLoading = true;
        _loadError = null;
      });
    }
    try {
      final invoices = await ApiService.getInvoicesByCustomer(widget.maKhach);
      if (!mounted) return;
      setState(() {
        _invoices = invoices;
        _isLoading = false;
      });
    } catch (error) {
      if (!mounted) return;
      setState(() {
        _loadError = error.toString();
        _isLoading = false;
      });
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: const Color(0xFFF5F7FC),
      appBar: AppBar(
        title: const Text(
          'Hóa Đơn Hàng Tháng',
          style: TextStyle(
            fontWeight: FontWeight.bold,
            fontSize: 18,
            color: Color(0xFF172554),
          ),
        ),
        backgroundColor: Colors.white,
        foregroundColor: const Color(0xFF172554),
        elevation: 0,
        centerTitle: true,
        actions: [
          IconButton(
            tooltip: 'Lịch sử thanh toán và sử dụng',
            icon: const Icon(Icons.history_rounded),
            onPressed: () {
              Navigator.push(
                context,
                MaterialPageRoute(
                  builder: (context) =>
                      CustomerHistoryScreen(maKhach: widget.maKhach),
                ),
              );
            },
          ),
        ],
      ),
      body: _isLoading
          ? const Center(
              child: CircularProgressIndicator(color: Color(0xFF4F46E5)),
            )
          : _loadError != null
          ? Center(
              child: Padding(
                padding: const EdgeInsets.all(24),
                child: Column(
                  mainAxisAlignment: MainAxisAlignment.center,
                  children: [
                    const Icon(
                      Icons.error_outline,
                      color: Colors.redAccent,
                      size: 48,
                    ),
                    const SizedBox(height: 12),
                    Text(
                      'Không tải được hóa đơn.\n$_loadError',
                      textAlign: TextAlign.center,
                      style: const TextStyle(color: Color(0xFF64748B)),
                    ),
                    const SizedBox(height: 16),
                    ElevatedButton.icon(
                      onPressed: _loadInvoices,
                      icon: const Icon(Icons.refresh),
                      label: const Text('Thử lại'),
                    ),
                  ],
                ),
              ),
            )
          : _invoices.isEmpty
          ? Center(
              child: Column(
                mainAxisAlignment: MainAxisAlignment.center,
                children: [
                  Icon(
                    Icons.receipt_long_outlined,
                    size: 64,
                    color: Colors.grey.shade400,
                  ),
                  const SizedBox(height: 12),
                  const Text(
                    'Không có hóa đơn nào.',
                    style: TextStyle(color: Color(0xFF64748B), fontSize: 16),
                  ),
                ],
              ),
            )
          : RefreshIndicator(
              color: const Color(0xFF4F46E5),
              onRefresh: () async => _loadInvoices(),
              child: ListView.builder(
                padding: const EdgeInsets.all(16),
                itemCount: _invoices.length,
                itemBuilder: (context, index) {
                  final inv = Map<String, dynamic>.from(
                    _invoices[index] as Map,
                  );
                  final bool isPaid = _isInvoicePaid(inv);
                  final int thang =
                      int.tryParse(
                        (inv['thang'] ?? inv['Thang'] ?? '').toString(),
                      ) ??
                      1;
                  final int nam =
                      int.tryParse(
                        (inv['nam'] ?? inv['Nam'] ?? '').toString(),
                      ) ??
                      DateTime.now().year;
                  final double tongTien = _invoiceAmount(
                    inv,
                    'tongTien',
                    'TongTien',
                  );
                  final double conLai = _invoiceRemaining(inv);
                  final String status = _invoiceStatus(inv);

                  return Card(
                    elevation: 0,
                    margin: const EdgeInsets.only(bottom: 12),
                    shape: RoundedRectangleBorder(
                      borderRadius: BorderRadius.circular(16),
                      side: const BorderSide(color: Color(0xFFE2E8F0)),
                    ),
                    child: InkWell(
                      borderRadius: BorderRadius.circular(16),
                      onTap: () async {
                        final result =
                            await Navigator.push<Map<String, dynamic>>(
                              context,
                              MaterialPageRoute(
                                builder: (context) => InvoiceDetailScreen(
                                  invoice: Map<String, dynamic>.from(inv),
                                ),
                              ),
                            );
                        if (result != null && mounted) {
                          setState(() => _invoices[index] = result);
                        }
                      },
                      child: Padding(
                        padding: const EdgeInsets.all(16.0),
                        child: Row(
                          children: [
                            Container(
                              width: 50,
                              height: 50,
                              decoration: BoxDecoration(
                                color: (isPaid ? Colors.green : Colors.orange)
                                    .withValues(alpha: 0.1),
                                borderRadius: BorderRadius.circular(12),
                              ),
                              child: Icon(
                                Icons.receipt_rounded,
                                color: isPaid ? Colors.green : Colors.orange,
                                size: 26,
                              ),
                            ),
                            const SizedBox(width: 16),
                            Expanded(
                              child: Column(
                                crossAxisAlignment: CrossAxisAlignment.start,
                                children: [
                                  Text(
                                    'Hóa đơn tháng $thang/$nam',
                                    style: const TextStyle(
                                      fontWeight: FontWeight.bold,
                                      fontSize: 16,
                                      color: Color(0xFF172554),
                                    ),
                                  ),
                                  const SizedBox(height: 4),
                                  Text(
                                    isPaid
                                        ? 'Đã thanh toán: ${_formatPaymentAmount(tongTien)}'
                                        : 'Còn phải trả: ${_formatPaymentAmount(conLai)}',
                                    style: const TextStyle(
                                      color: Color(0xFF4F46E5),
                                      fontWeight: FontWeight.bold,
                                      fontSize: 14,
                                    ),
                                  ),
                                  const SizedBox(height: 8),
                                  Container(
                                    padding: const EdgeInsets.symmetric(
                                      horizontal: 10,
                                      vertical: 4,
                                    ),
                                    decoration: BoxDecoration(
                                      color:
                                          (isPaid
                                                  ? Colors.green
                                                  : Colors.orange)
                                              .withValues(alpha: 0.1),
                                      borderRadius: BorderRadius.circular(20),
                                    ),
                                    child: Text(
                                      status,
                                      style: TextStyle(
                                        color: isPaid
                                            ? Colors.green
                                            : Colors.orange.shade800,
                                        fontWeight: FontWeight.bold,
                                        fontSize: 11,
                                      ),
                                    ),
                                  ),
                                ],
                              ),
                            ),
                            const Icon(
                              Icons.arrow_forward_ios_rounded,
                              size: 16,
                              color: Color(0xFF94A3B8),
                            ),
                          ],
                        ),
                      ),
                    ),
                  );
                },
              ),
            ),
    );
  }
}

// Màn hình chi tiết hóa đơn & quét mã QR thanh toán
class InvoiceDetailScreen extends StatelessWidget {
  final Map<String, dynamic> invoice;

  const InvoiceDetailScreen({super.key, required this.invoice});

  @override
  Widget build(BuildContext context) {
    final double tongTien = _invoiceAmount(invoice, 'tongTien', 'TongTien');
    final double daThanhToan = _invoiceAmount(
      invoice,
      'daThanhToan',
      'DaThanhToan',
    );
    final double conLai = _invoiceRemaining(invoice);
    final String trangThai = _invoiceStatus(invoice);
    final bool isPaid = _isInvoicePaid(invoice);
    final int thang =
        int.tryParse(
          (invoice['thang'] ?? invoice['Thang'])?.toString() ?? '',
        ) ??
        1;
    final int nam =
        int.tryParse((invoice['nam'] ?? invoice['Nam'])?.toString() ?? '') ??
        DateTime.now().year;
    final rawDueDate = invoice['hanThanhToan'] ?? invoice['HanThanhToan'];
    final String hanThanhToan = _formatInvoiceDate(rawDueDate);
    final invoiceItems = _invoiceItems(invoice);
    final violations = invoiceItems
        .where(
          (item) =>
              _invoiceItemDescription(item).toLowerCase().contains('vi phạm') ||
              item.containsKey('maViPham') ||
              item.containsKey('MaViPham'),
        )
        .toList();
    final penaltyTotal = _invoicePenaltyTotal(invoice, violations);
    final invoiceId = int.tryParse((invoice['maHoaDon'] ?? invoice['MaHoaDon'] ?? '').toString());

    const bankId = 'VCB';
    const accountNo = '1234567890';
    const accountName = 'CHU TRO';
    String qrUrl =
        'https://img.vietqr.io/image/$bankId-$accountNo-compact2.png?amount=${conLai.round()}&addInfo=Thanh%20toan%20HD%20thang%20$thang%20nam%20$nam&accountName=$accountName';

    return Scaffold(
      backgroundColor: const Color(0xFFF5F7FC),
      appBar: AppBar(
        title: Text(
          'Chi Tiết Hóa Đơn Tháng $thang/$nam',
          style: const TextStyle(
            fontWeight: FontWeight.bold,
            fontSize: 18,
            color: Color(0xFF172554),
          ),
        ),
        backgroundColor: Colors.white,
        foregroundColor: const Color(0xFF172554),
        elevation: 0,
        centerTitle: true,
      ),
      body: SingleChildScrollView(
        padding: const EdgeInsets.all(20.0),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            // PDF được lấy từ API backend, kèm cookie đăng nhập.
            if (invoiceId != null && invoiceId > 0) ...[
              _InvoicePdfActions(invoiceId: invoiceId),
              const SizedBox(height: 18),
            ],
            // Thẻ thông tin hóa đơn
            Container(
              padding: const EdgeInsets.all(20),
              decoration: BoxDecoration(
                color: Colors.white,
                borderRadius: BorderRadius.circular(20),
                border: Border.all(color: const Color(0xFFE2E8F0)),
              ),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Row(
                    mainAxisAlignment: MainAxisAlignment.spaceBetween,
                    children: [
                      Text(
                        'Kỳ hóa đơn: Tháng $thang/$nam',
                        style: const TextStyle(
                          fontSize: 16,
                          fontWeight: FontWeight.bold,
                          color: Color(0xFF172554),
                        ),
                      ),
                      Container(
                        padding: const EdgeInsets.symmetric(
                          horizontal: 10,
                          vertical: 4,
                        ),
                        decoration: BoxDecoration(
                          color: (isPaid ? Colors.green : Colors.orange)
                              .withValues(alpha: 0.1),
                          borderRadius: BorderRadius.circular(20),
                        ),
                        child: Text(
                          trangThai,
                          style: TextStyle(
                            color: isPaid
                                ? Colors.green
                                : Colors.orange.shade800,
                            fontWeight: FontWeight.bold,
                            fontSize: 11,
                          ),
                        ),
                      ),
                    ],
                  ),
                  const Divider(height: 28, color: Color(0xFFE2E8F0)),
                  Row(
                    mainAxisAlignment: MainAxisAlignment.spaceBetween,
                    children: [
                      const Text(
                        'Hạn thanh toán:',
                        style: TextStyle(
                          color: Color(0xFF64748B),
                          fontSize: 14,
                        ),
                      ),
                      Text(
                        hanThanhToan,
                        style: const TextStyle(
                          fontWeight: FontWeight.bold,
                          fontSize: 14,
                          color: Color(0xFF172554),
                        ),
                      ),
                    ],
                  ),
                  if (invoiceItems.isNotEmpty ||
                      penaltyTotal > 0 ||
                      violations.isNotEmpty) ...[
                    const SizedBox(height: 16),
                    const Text(
                      'Chi tiết các khoản',
                      style: TextStyle(
                        fontSize: 14,
                        fontWeight: FontWeight.bold,
                        color: Color(0xFF172554),
                      ),
                    ),
                    const SizedBox(height: 8),
                    if (invoiceItems.isNotEmpty)
                      ...invoiceItems.map((item) {
                        final isViolation =
                            _invoiceItemDescription(
                              item,
                            ).toLowerCase().contains('vi phạm') ||
                            item.containsKey('maViPham') ||
                            item.containsKey('MaViPham');
                        return Padding(
                          padding: const EdgeInsets.only(bottom: 8),
                          child: Row(
                            crossAxisAlignment: CrossAxisAlignment.start,
                            children: [
                              Icon(
                                isViolation
                                    ? Icons.report_gmailerrorred_outlined
                                    : Icons.receipt_outlined,
                                color: isViolation
                                    ? Colors.deepOrange
                                    : const Color(0xFF64748B),
                                size: 18,
                              ),
                              const SizedBox(width: 8),
                              Expanded(
                                child: Text(
                                  _invoiceItemDescription(item),
                                  style: const TextStyle(
                                    color: Color(0xFF475569),
                                    fontSize: 13,
                                  ),
                                ),
                              ),
                              const SizedBox(width: 8),
                              Text(
                                '${_invoiceViolationAmount(item).toStringAsFixed(0)} đ',
                                style: TextStyle(
                                  color: isViolation
                                      ? Colors.deepOrange
                                      : const Color(0xFF172554),
                                  fontWeight: FontWeight.w600,
                                ),
                              ),
                            ],
                          ),
                        );
                      }),
                    if (penaltyTotal > 0)
                      _invoiceAmountRow(
                        'Tổng tiền phạt',
                        '${penaltyTotal.toStringAsFixed(0)} đ',
                        valueColor: Colors.deepOrange,
                      ),
                    if (violations.isNotEmpty)
                      const Text(
                        'Khoản phạt được tính chung trong tổng hóa đơn do hệ thống trả về.',
                        style: TextStyle(
                          color: Color(0xFF64748B),
                          fontSize: 12,
                        ),
                      ),
                  ],
                  const SizedBox(height: 12),
                  Row(
                    mainAxisAlignment: MainAxisAlignment.spaceBetween,
                    children: [
                      const Text(
                        'Tổng tiền thanh toán:',
                        style: TextStyle(
                          fontSize: 15,
                          fontWeight: FontWeight.bold,
                          color: Color(0xFF172554),
                        ),
                      ),
                      Text(
                        _formatPaymentAmount(tongTien),
                        style: const TextStyle(
                          fontSize: 18,
                          fontWeight: FontWeight.bold,
                          color: Colors.redAccent,
                        ),
                      ),
                    ],
                  ),
                  const SizedBox(height: 12),
                  _invoiceAmountRow(
                    'Đã thanh toán',
                    _formatPaymentAmount(daThanhToan),
                    valueColor: Colors.green,
                  ),
                  _invoiceAmountRow(
                    'Còn phải trả',
                    _formatPaymentAmount(conLai),
                    valueColor: conLai > 0 ? Colors.redAccent : Colors.green,
                  ),
                ],
              ),
            ),
            const SizedBox(height: 24),

            if (isPaid)
              Container(
                padding: const EdgeInsets.all(20),
                decoration: BoxDecoration(
                  color: Colors.green.withValues(alpha: 0.08),
                  borderRadius: BorderRadius.circular(16),
                  border: Border.all(
                    color: Colors.green.withValues(alpha: 0.2),
                  ),
                ),
                child: const Row(
                  children: [
                    Icon(Icons.check_circle, color: Colors.green, size: 28),
                    SizedBox(width: 12),
                    Expanded(
                      child: Text(
                        'Hóa đơn này đã được thanh toán thành công.',
                        style: TextStyle(
                          color: Color(0xFF166534),
                          fontWeight: FontWeight.w600,
                        ),
                      ),
                    ),
                  ],
                ),
              )
            else ...[
              const Text(
                'Quét mã QR để thanh toán (Ngân hàng / MoMo):',
                style: TextStyle(
                  fontSize: 15,
                  fontWeight: FontWeight.bold,
                  color: Color(0xFF172554),
                ),
              ),
              const SizedBox(height: 12),
              Center(
                child: Container(
                  padding: const EdgeInsets.all(16),
                  decoration: BoxDecoration(
                    color: Colors.white,
                    borderRadius: BorderRadius.circular(20),
                    border: Border.all(color: const Color(0xFFE2E8F0)),
                  ),
                  child: Column(
                    children: [
                      ClipRRect(
                        borderRadius: BorderRadius.circular(12),
                        child: Image.network(
                          qrUrl,
                          height: 200,
                          width: 200,
                          loadingBuilder: (context, child, loadingProgress) {
                            if (loadingProgress == null) return child;
                            return const SizedBox(
                              height: 200,
                              width: 200,
                              child: Center(
                                child: CircularProgressIndicator(
                                  color: Color(0xFF4F46E5),
                                ),
                              ),
                            );
                          },
                          errorBuilder: (context, error, stackTrace) =>
                              const Text(
                                'Không thể tải mã QR.',
                                style: TextStyle(color: Colors.red),
                              ),
                        ),
                      ),
                      const SizedBox(height: 12),
                      const Text(
                        'Sử dụng App Ngân hàng hoặc MoMo để quét',
                        style: TextStyle(
                          color: Color(0xFF64748B),
                          fontSize: 12,
                        ),
                      ),
                    ],
                  ),
                ),
              ),
              const SizedBox(height: 12),
              const Text(
                'Mô phỏng thanh toán sẽ ghi nhận khoản thu vào hóa đơn trên hệ thống. '
                'Không có giao dịch ngân hàng hoặc trừ tiền thật.',
                textAlign: TextAlign.center,
                style: TextStyle(color: Color(0xFF64748B), fontSize: 12),
              ),
              const SizedBox(height: 16),
              _PaymentConfirmationButton(invoice: invoice, amountDue: conLai),
            ],
          ],
        ),
      ),
    );
  }
}


// Hai thao tác PDF: xem trước trong ứng dụng và mở bảng lưu/chia sẻ.
class _InvoicePdfActions extends StatefulWidget {
  final int invoiceId;
  const _InvoicePdfActions({required this.invoiceId});

  @override
  State<_InvoicePdfActions> createState() => _InvoicePdfActionsState();
}

class _InvoicePdfActionsState extends State<_InvoicePdfActions> {
  bool _busy = false;

  Future<void> _handlePdf({required bool preview}) async {
    if (_busy) return;
    setState(() => _busy = true);
    try {
      final bytes = await ApiService.downloadInvoicePdf(widget.invoiceId);
      if (!mounted) return;
      final filename = 'HoaDon_${widget.invoiceId}.pdf';
      if (preview) {
        await Navigator.of(context).push<void>(
          MaterialPageRoute(
            builder: (_) => Scaffold(
              appBar: AppBar(title: Text('Hóa đơn #${widget.invoiceId}')),
              body: PdfPreview(
                build: (_) async => bytes,
                pdfFileName: filename,
                allowPrinting: true,
                allowSharing: true,
              ),
            ),
          ),
        );
      } else {
        // Android/iOS mở bảng chia sẻ, có thể chọn ứng dụng lưu file.
        await Printing.sharePdf(bytes: bytes, filename: filename);
      }
    } catch (error) {
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: Text('Không thể xử lý PDF: $error'),
          backgroundColor: Colors.redAccent,
        ),
      );
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        OutlinedButton.icon(
          onPressed: _busy ? null : () => _handlePdf(preview: true),
          icon: const Icon(Icons.picture_as_pdf_outlined),
          label: const Text('Xem PDF hóa đơn'),
        ),
        const SizedBox(height: 8),
        FilledButton.icon(
          onPressed: _busy ? null : () => _handlePdf(preview: false),
          icon: _busy
              ? const SizedBox(
                  width: 18,
                  height: 18,
                  child: CircularProgressIndicator(strokeWidth: 2),
                )
              : const Icon(Icons.download_rounded),
          label: Text(_busy ? 'Đang tải PDF...' : 'Lưu / chia sẻ PDF'),
        ),
      ],
    );
  }
}

List<Map<String, dynamic>> _invoiceItems(Map<String, dynamic> invoice) {
  // Ưu tiên các dòng chi tiết hóa đơn; fallback cho API cũ trả riêng vi phạm.
  for (final key in [
    'ChiTietHoaDons',
    'ChiTietHoaDon',
    'ChiTiet',
    'chiTietHoaDons',
    'chiTietHoaDon',
    'chiTiet',
    'lineItems',
    'details',
    'chiTietViPham',
    'cacViPham',
    'violations',
    'penalties',
  ]) {
    final value = invoice[key];
    if (value is List) {
      return value
          .whereType<Map>()
          .map((item) => Map<String, dynamic>.from(item))
          .toList();
    }
  }
  return [];
}

double _invoicePenaltyTotal(
  Map<String, dynamic> invoice,
  List<Map<String, dynamic>> violations,
) {
  for (final key in [
    'tongTienPhat',
    'TongTienPhat',
    'tienPhat',
    'TienPhat',
    'totalFine',
    'fineAmount',
  ]) {
    final value = num.tryParse(invoice[key]?.toString() ?? '');
    if (value != null) return value.toDouble();
  }
  return violations.fold<double>(
    0,
    (total, item) => total + _invoiceViolationAmount(item),
  );
}

double _invoiceViolationAmount(Map<String, dynamic> violation) {
  for (final key in [
    'tienPhat',
    'TienPhat',
    'fineAmount',
    'FineAmount',
    'amount',
    'Amount',
    'thanhTien',
    'ThanhTien',
  ]) {
    final value = num.tryParse(violation[key]?.toString() ?? '');
    if (value != null) return value.toDouble();
  }
  return 0;
}

String _invoiceItemDescription(Map<String, dynamic> item) {
  for (final key in [
    'noiDung',
    'NoiDung',
    'moTa',
    'MoTa',
    'description',
    'Description',
    'content',
    'Content',
  ]) {
    final value = item[key]?.toString().trim();
    if (value != null && value.isNotEmpty) return value;
  }
  return 'Khoản thu';
}

Widget _invoiceAmountRow(
  String title,
  String amount, {
  Color valueColor = const Color(0xFF172554),
}) {
  return Padding(
    padding: const EdgeInsets.only(bottom: 8),
    child: Row(
      mainAxisAlignment: MainAxisAlignment.spaceBetween,
      children: [
        Text(title, style: const TextStyle(color: Color(0xFF475569))),
        Text(
          amount,
          style: TextStyle(color: valueColor, fontWeight: FontWeight.w600),
        ),
      ],
    ),
  );
}

class _PaymentConfirmationButton extends StatefulWidget {
  final Map<String, dynamic> invoice;
  final double amountDue;

  const _PaymentConfirmationButton({
    required this.invoice,
    required this.amountDue,
  });

  @override
  State<_PaymentConfirmationButton> createState() =>
      _PaymentConfirmationButtonState();
}

class _PaymentConfirmationButtonState
    extends State<_PaymentConfirmationButton> {
  bool _isProcessing = false;

  Future<void> _simulatePayment() async {
    final invoiceId = int.tryParse(
      (widget.invoice['maHoaDon'] ?? widget.invoice['MaHoaDon'] ?? '')
          .toString(),
    );
    if (invoiceId == null || widget.amountDue <= 0) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(
          content: Text('Hóa đơn thiếu mã hợp lệ hoặc không còn công nợ.'),
          backgroundColor: Colors.redAccent,
          behavior: SnackBarBehavior.floating,
        ),
      );
      return;
    }

    setState(() => _isProcessing = true);
    final response = await ApiService.collectInvoicePayment(
      invoiceId: invoiceId,
      amount: widget.amountDue,
      paymentMethod: 'Mô phỏng thanh toán',
      note: 'Khách xác nhận thanh toán mô phỏng trên ứng dụng.',
    );
    if (!mounted) return;
    setState(() => _isProcessing = false);

    if (response['success'] != true) {
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: Text(
            response['message']?.toString() ?? 'Thanh toán thất bại.',
          ),
          backgroundColor: Colors.redAccent,
          behavior: SnackBarBehavior.floating,
        ),
      );
      return;
    }

    final paidInvoice = Map<String, dynamic>.from(widget.invoice);
    final paidData = response['data'] is Map
        ? Map<String, dynamic>.from(response['data'] as Map)
        : <String, dynamic>{};
    final paidTotal =
        _invoiceAmount(widget.invoice, 'daThanhToan', 'DaThanhToan') +
        widget.amountDue;
    _setInvoiceValue(
      paidInvoice,
      'daThanhToan',
      'DaThanhToan',
      paidData['daThanhToan'] ?? paidData['DaThanhToan'] ?? paidTotal,
    );
    _setInvoiceValue(
      paidInvoice,
      'conLai',
      'ConLai',
      paidData['conLai'] ?? paidData['ConLai'] ?? 0,
    );
    _setInvoiceValue(
      paidInvoice,
      'trangThai',
      'TrangThai',
      paidData['trangThai'] ?? paidData['TrangThai'] ?? 'Đã thanh toán',
    );
    final successMessage =
        response['message']?.toString() ?? 'Đã ghi nhận thanh toán.';
    final result = await Navigator.push<Map<String, dynamic>>(
      context,
      MaterialPageRoute(
        builder: (context) => PaymentSuccessScreen(
          invoice: paidInvoice,
          invoiceId: invoiceId.toString(),
          amount: widget.amountDue,
          message: successMessage,
        ),
      ),
    );
    if (result != null && mounted) {
      Navigator.pop(context, result);
    } else if (mounted) {
      setState(() => _isProcessing = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    return SizedBox(
      height: 50,
      child: ElevatedButton(
        style: ElevatedButton.styleFrom(
          backgroundColor: const Color(0xFF4F46E5),
          foregroundColor: Colors.white,
          shape: RoundedRectangleBorder(
            borderRadius: BorderRadius.circular(12),
          ),
          elevation: 0,
        ),
        onPressed: _isProcessing ? null : _simulatePayment,
        child: _isProcessing
            ? const SizedBox(
                height: 20,
                width: 20,
                child: CircularProgressIndicator(
                  strokeWidth: 2,
                  color: Colors.white,
                ),
              )
            : const Text(
                'Thanh toán ngay',
                style: TextStyle(fontSize: 15, fontWeight: FontWeight.bold),
              ),
      ),
    );
  }
}

class PaymentSuccessScreen extends StatelessWidget {
  final Map<String, dynamic> invoice;
  final String invoiceId;
  final double amount;
  final String message;

  const PaymentSuccessScreen({
    super.key,
    required this.invoice,
    required this.invoiceId,
    required this.amount,
    required this.message,
  });

  @override
  Widget build(BuildContext context) {
    return PopScope<Map<String, dynamic>>(
      canPop: false,
      onPopInvokedWithResult: (didPop, result) {
        if (!didPop) Navigator.pop(context, invoice);
      },
      child: Scaffold(
        backgroundColor: const Color(0xFFEEF5FF),
        appBar: AppBar(
          title: const Text('Kết quả thanh toán'),
          centerTitle: true,
          backgroundColor: const Color(0xFFEEF5FF),
          foregroundColor: const Color(0xFF172554),
          elevation: 0,
          leading: IconButton(
            tooltip: 'Quay lại hóa đơn',
            icon: const Icon(Icons.arrow_back),
            onPressed: () => Navigator.pop(context, invoice),
          ),
        ),
        body: Center(
          child: SingleChildScrollView(
            padding: const EdgeInsets.all(24),
            child: Container(
              width: 420,
              padding: const EdgeInsets.fromLTRB(24, 28, 24, 24),
              decoration: BoxDecoration(
                color: Colors.white,
                borderRadius: BorderRadius.circular(16),
                boxShadow: [
                  BoxShadow(
                    color: const Color(0xFF172554).withValues(alpha: 0.08),
                    blurRadius: 24,
                    offset: const Offset(0, 8),
                  ),
                ],
              ),
              child: Column(
                mainAxisSize: MainAxisSize.min,
                children: [
                  Container(
                    width: 64,
                    height: 64,
                    decoration: const BoxDecoration(
                      color: Color(0xFF16B82E),
                      shape: BoxShape.circle,
                    ),
                    child: const Icon(
                      Icons.check_rounded,
                      color: Colors.white,
                      size: 42,
                    ),
                  ),
                  const SizedBox(height: 20),
                  const Text(
                    'Thanh toán thành công',
                    textAlign: TextAlign.center,
                    style: TextStyle(
                      fontWeight: FontWeight.bold,
                      fontSize: 20,
                      color: Color(0xFF172554),
                    ),
                  ),
                  const SizedBox(height: 12),
                  Text(
                    'Hóa đơn #$invoiceId',
                    textAlign: TextAlign.center,
                    style: const TextStyle(color: Color(0xFF475569)),
                  ),
                  const SizedBox(height: 8),
                  Text(
                    'Số tiền: ${_formatPaymentAmount(amount)}',
                    style: const TextStyle(
                      color: Color(0xFF16A34A),
                      fontWeight: FontWeight.bold,
                    ),
                  ),
                  const SizedBox(height: 8),
                  Text(
                    message,
                    textAlign: TextAlign.center,
                    style: const TextStyle(
                      color: Color(0xFF64748B),
                      fontSize: 12,
                    ),
                  ),
                  const SizedBox(height: 8),
                  const Text(
                    'Đây là thanh toán mô phỏng được ghi nhận qua API. '
                    'Không có giao dịch ngân hàng hoặc trừ tiền thật.',
                    textAlign: TextAlign.center,
                    style: TextStyle(color: Color(0xFF64748B), fontSize: 12),
                  ),
                  const SizedBox(height: 24),
                  SizedBox(
                    width: double.infinity,
                    height: 48,
                    child: ElevatedButton(
                      style: ElevatedButton.styleFrom(
                        backgroundColor: const Color(0xFF0788D1),
                        foregroundColor: Colors.white,
                        elevation: 0,
                        shape: RoundedRectangleBorder(
                          borderRadius: BorderRadius.circular(8),
                        ),
                      ),
                      onPressed: () => Navigator.pop(context, invoice),
                      child: const Text(
                        'QUAY LẠI HÓA ĐƠN',
                        style: TextStyle(fontWeight: FontWeight.bold),
                      ),
                    ),
                  ),
                ],
              ),
            ),
          ),
        ),
      ),
    );
  }
}

double _invoiceAmount(
  Map<String, dynamic> invoice,
  String camelCaseKey,
  String pascalCaseKey,
) =>
    (num.tryParse(
              (invoice[camelCaseKey] ?? invoice[pascalCaseKey])?.toString() ??
                  '',
            ) ??
            0)
        .toDouble();

double _invoiceRemaining(Map<String, dynamic> invoice) {
  final total = _invoiceAmount(invoice, 'tongTien', 'TongTien');
  final paid = _invoiceAmount(invoice, 'daThanhToan', 'DaThanhToan');
  final reportedRemaining = invoice['conLai'] ?? invoice['ConLai'];
  return reportedRemaining == null
      ? (total - paid).clamp(0, double.infinity).toDouble()
      : (num.tryParse(reportedRemaining.toString()) ?? total - paid)
            .clamp(0, double.infinity)
            .toDouble();
}

String _invoiceStatus(Map<String, dynamic> invoice) {
  final status =
      (invoice['trangThai'] ?? invoice['TrangThai'])?.toString().trim() ?? '';
  if (status.isNotEmpty) return status;
  if (_invoiceRemaining(invoice) <= 0) return 'Đã thanh toán';
  if (_invoiceAmount(invoice, 'daThanhToan', 'DaThanhToan') > 0) {
    return 'Thanh toán một phần';
  }
  return 'Chưa thanh toán';
}

bool _isInvoicePaid(Map<String, dynamic> invoice) {
  if (invoice['conLai'] != null || invoice['ConLai'] != null) {
    return _invoiceRemaining(invoice) <= 0;
  }
  final status = _invoiceStatus(invoice).toLowerCase();
  return status.contains('đã thanh toán') ||
      status.contains('đã thu đủ') ||
      status == 'paid';
}

void _setInvoiceValue(
  Map<String, dynamic> invoice,
  String camelCaseKey,
  String pascalCaseKey,
  dynamic value,
) {
  final key = invoice.containsKey(pascalCaseKey) ? pascalCaseKey : camelCaseKey;
  invoice[key] = value;
}

String _formatInvoiceDate(dynamic value) {
  if (value == null) return 'N/A';
  final date = DateTime.tryParse(value.toString());
  if (date == null) return value.toString();
  return '${date.day.toString().padLeft(2, '0')}/'
      '${date.month.toString().padLeft(2, '0')}/${date.year}';
}

String _formatPaymentAmount(double amount) =>
    '${amount.round().toString().replaceAllMapped(RegExp(r'(\d)(?=(\d{3})+(?!\d))'), (match) => '${match[1]},')} đ';
