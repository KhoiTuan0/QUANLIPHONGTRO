import 'package:flutter/material.dart';
import '../services/api_service.dart';
import 'customer_detail_screen.dart';

class CustomerListScreen extends StatefulWidget {
  const CustomerListScreen({super.key});

  @override
  State<CustomerListScreen> createState() => _CustomerListScreenState();
}

class _CustomerListScreenState extends State<CustomerListScreen> {
  late Future<List<dynamic>> _customersFuture;

  @override
  void initState() {
    super.initState();
    _customersFuture = ApiService.getCustomers();
  }

  void _loadData() {
    setState(() {
      _customersFuture = ApiService.getCustomers();
    });
  }

  Future<void> _showAddDialog() async {
    final hoTenCtrl = TextEditingController();
    final sdtCtrl = TextEditingController();
    final cccdCtrl = TextEditingController();
    final emailCtrl = TextEditingController();
    final diaChiCtrl = TextEditingController();
    var saving = false;

    Map<String, dynamic>? createdAccount;
    try {
      createdAccount = await showDialog<Map<String, dynamic>>(
        context: context,
        builder: (dialogContext) => StatefulBuilder(
          builder: (dialogContext, setDialogState) {
            return AlertDialog(
              shape: RoundedRectangleBorder(
                borderRadius: BorderRadius.circular(20),
              ),
              title: const Text(
                'Thêm khách hàng mới',
                style: TextStyle(
                  fontWeight: FontWeight.bold,
                  fontSize: 18,
                  color: Color(0xFF172554),
                ),
              ),
              content: SingleChildScrollView(
                child: Column(
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    TextField(
                      controller: hoTenCtrl,
                      textCapitalization: TextCapitalization.words,
                      decoration: _dialogInputDecoration(
                        'Họ và tên *',
                        Icons.person_outline,
                      ),
                    ),
                    const SizedBox(height: 12),
                    TextField(
                      controller: sdtCtrl,
                      keyboardType: TextInputType.phone,
                      decoration: _dialogInputDecoration(
                        'Số điện thoại *',
                        Icons.phone_outlined,
                      ),
                    ),
                    const SizedBox(height: 12),
                    TextField(
                      controller: cccdCtrl,
                      keyboardType: TextInputType.number,
                      decoration: _dialogInputDecoration(
                        'Số CCCD / CMND',
                        Icons.badge_outlined,
                      ),
                    ),
                    const SizedBox(height: 12),
                    TextField(
                      controller: emailCtrl,
                      keyboardType: TextInputType.emailAddress,
                      autofillHints: const [AutofillHints.email],
                      decoration: _dialogInputDecoration(
                        'Email đăng nhập *',
                        Icons.email_outlined,
                      ),
                    ),
                    const SizedBox(height: 8),
                    const Align(
                      alignment: Alignment.centerLeft,
                      child: Text(
                        'Email sẽ là tên đăng nhập và mật khẩu khởi tạo. '
                        'Khách nên đổi mật khẩu sau lần đăng nhập đầu tiên.',
                        style: TextStyle(
                          color: Color(0xFF64748B),
                          fontSize: 12,
                        ),
                      ),
                    ),
                    const SizedBox(height: 12),
                    TextField(
                      controller: diaChiCtrl,
                      decoration: _dialogInputDecoration(
                        'Địa chỉ thường trú',
                        Icons.location_on_outlined,
                      ),
                    ),
                  ],
                ),
              ),
              actions: [
                TextButton(
                  onPressed: saving ? null : () => Navigator.pop(dialogContext),
                  child: const Text(
                    'Hủy',
                    style: TextStyle(color: Color(0xFF64748B)),
                  ),
                ),
                ElevatedButton(
                  style: ElevatedButton.styleFrom(
                    backgroundColor: const Color(0xFF4F46E5),
                    foregroundColor: Colors.white,
                    elevation: 0,
                    shape: RoundedRectangleBorder(
                      borderRadius: BorderRadius.circular(10),
                    ),
                  ),
                  onPressed: saving
                      ? null
                      : () async {
                          final email = emailCtrl.text.trim().toLowerCase();
                          if (hoTenCtrl.text.trim().isEmpty ||
                              sdtCtrl.text.trim().isEmpty) {
                            _showSnackBar(
                              'Vui lòng nhập họ tên và số điện thoại.',
                              isError: true,
                            );
                            return;
                          }
                          if (!RegExp(
                            r'^[^@\s]+@[^@\s]+\.[^@\s]+$',
                          ).hasMatch(email)) {
                            _showSnackBar(
                              'Vui lòng nhập địa chỉ email hợp lệ.',
                              isError: true,
                            );
                            return;
                          }
                          if (email.length < 6) {
                            _showSnackBar(
                              'Email phải có ít nhất 6 ký tự để dùng làm mật khẩu khởi tạo.',
                              isError: true,
                            );
                            return;
                          }

                          setDialogState(() => saving = true);
                          final result =
                              await ApiService.registerCustomerForLandlord(
                                email: email,
                                fullName: hoTenCtrl.text,
                                phone: sdtCtrl.text,
                                cccd: cccdCtrl.text,
                                address: diaChiCtrl.text,
                              );
                          if (!mounted || !dialogContext.mounted) return;
                          if (result['success'] == true) {
                            Navigator.pop(dialogContext, result);
                          } else {
                            setDialogState(() => saving = false);
                            _showSnackBar(
                              result['message']?.toString() ??
                                  'Không thể tạo khách hàng.',
                              isError: true,
                            );
                          }
                        },
                  child: saving
                      ? const SizedBox(
                          width: 18,
                          height: 18,
                          child: CircularProgressIndicator(strokeWidth: 2),
                        )
                      : const Text(
                          'Tạo khách và tài khoản',
                          style: TextStyle(fontWeight: FontWeight.bold),
                        ),
                ),
              ],
            );
          },
        ),
      );

      if (!mounted || createdAccount == null) return;
      _loadData();
      await showDialog<void>(
        context: context,
        builder: (dialogContext) => AlertDialog(
          title: const Text('Đã tạo khách và tài khoản'),
          content: SelectableText(
            'Tên đăng nhập: ${createdAccount!['username']}\n'
            'Mật khẩu khởi tạo: ${createdAccount['initialPassword']}\n\n'
            'Hãy gửi thông tin này riêng cho khách và nhắc khách đổi mật khẩu '
            'sau khi đăng nhập.',
          ),
          actions: [
            FilledButton(
              onPressed: () => Navigator.pop(dialogContext),
              child: const Text('Đã hiểu'),
            ),
          ],
        ),
      );
    } finally {
      hoTenCtrl.dispose();
      sdtCtrl.dispose();
      cccdCtrl.dispose();
      emailCtrl.dispose();
      diaChiCtrl.dispose();
    }
  }

  void _showSnackBar(String message, {bool isError = false}) {
    ScaffoldMessenger.of(context).showSnackBar(
      SnackBar(
        content: Text(message),
        backgroundColor: isError ? Colors.redAccent : Colors.green,
        behavior: SnackBarBehavior.floating,
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(10)),
      ),
    );
  }

  InputDecoration _dialogInputDecoration(String label, IconData icon) {
    return InputDecoration(
      labelText: label,
      labelStyle: const TextStyle(color: Color(0xFF64748B), fontSize: 13),
      prefixIcon: Icon(icon, color: const Color(0xFF4F46E5), size: 20),
      border: OutlineInputBorder(
        borderRadius: BorderRadius.circular(12),
        borderSide: const BorderSide(color: Color(0xFFCBD5E1)),
      ),
      enabledBorder: OutlineInputBorder(
        borderRadius: BorderRadius.circular(12),
        borderSide: const BorderSide(color: Color(0xFFCBD5E1)),
      ),
      focusedBorder: OutlineInputBorder(
        borderRadius: BorderRadius.circular(12),
        borderSide: const BorderSide(color: Color(0xFF4F46E5), width: 2),
      ),
      filled: true,
      fillColor: const Color(0xFFF5F7FC),
      contentPadding: const EdgeInsets.symmetric(horizontal: 12, vertical: 12),
    );
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: const Color(0xFFF5F7FC),
      floatingActionButton: FloatingActionButton.extended(
        backgroundColor: const Color(0xFF4F46E5),
        foregroundColor: Colors.white,
        elevation: 2,
        onPressed: _showAddDialog,
        icon: const Icon(Icons.person_add_rounded),
        label: const Text(
          'Thêm khách',
          style: TextStyle(fontWeight: FontWeight.bold),
        ),
      ),
      body: FutureBuilder<List<dynamic>>(
        future: _customersFuture,
        builder: (context, snapshot) {
          if (snapshot.connectionState == ConnectionState.waiting) {
            return const Center(
              child: CircularProgressIndicator(color: Color(0xFF4F46E5)),
            );
          }
          if (snapshot.hasError) {
            return Center(
              child: Text(
                'Lỗi tải dữ liệu: ${snapshot.error}',
                style: const TextStyle(color: Colors.red),
              ),
            );
          }

          final list = snapshot.data ?? [];
          if (list.isEmpty) {
            return Center(
              child: Column(
                mainAxisAlignment: MainAxisAlignment.center,
                children: [
                  Icon(
                    Icons.people_outline_rounded,
                    size: 64,
                    color: Colors.grey.shade400,
                  ),
                  const SizedBox(height: 12),
                  const Text(
                    'Chưa có khách hàng nào trong hệ thống',
                    style: TextStyle(color: Color(0xFF64748B), fontSize: 16),
                  ),
                ],
              ),
            );
          }

          return RefreshIndicator(
            color: const Color(0xFF4F46E5),
            onRefresh: () async => _loadData(),
            child: ListView.builder(
              padding: const EdgeInsets.all(16),
              itemCount: list.length,
              itemBuilder: (context, index) {
                final customer = list[index];
                final hoTen = customer['hoTen'] ?? customer['fullName'] ?? '';
                final sdt =
                    customer['soDienThoai'] ?? customer['phone'] ?? 'Chưa có';
                final cccd = customer['cccd'] ?? 'Chưa có';

                return Card(
                  elevation: 0,
                  margin: const EdgeInsets.only(bottom: 12),
                  shape: RoundedRectangleBorder(
                    borderRadius: BorderRadius.circular(16),
                    side: const BorderSide(color: Color(0xFFE2E8F0)),
                  ),
                  child: InkWell(
                    borderRadius: BorderRadius.circular(16),
                    onTap: () {
                      Navigator.push(
                        context,
                        MaterialPageRoute(
                          builder: (context) =>
                              CustomerDetailScreen(customer: customer),
                        ),
                      );
                    },
                    child: Padding(
                      padding: const EdgeInsets.all(16.0),
                      child: Row(
                        children: [
                          Container(
                            width: 50,
                            height: 50,
                            decoration: BoxDecoration(
                              color: const Color(0xFF4F46E5).withOpacity(0.1),
                              borderRadius: BorderRadius.circular(12),
                            ),
                            child: const Icon(
                              Icons.person_rounded,
                              color: Color(0xFF4F46E5),
                              size: 26,
                            ),
                          ),
                          const SizedBox(width: 16),
                          Expanded(
                            child: Column(
                              crossAxisAlignment: CrossAxisAlignment.start,
                              children: [
                                Text(
                                  hoTen,
                                  style: const TextStyle(
                                    fontWeight: FontWeight.bold,
                                    fontSize: 16,
                                    color: Color(0xFF172554),
                                  ),
                                ),
                                const SizedBox(height: 4),
                                Text(
                                  'SĐT: $sdt',
                                  style: const TextStyle(
                                    color: Color(0xFF64748B),
                                    fontSize: 13,
                                  ),
                                ),
                                const SizedBox(height: 2),
                                Text(
                                  'CCCD: $cccd',
                                  style: const TextStyle(
                                    color: Color(0xFF64748B),
                                    fontSize: 13,
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
          );
        },
      ),
    );
  }
}
