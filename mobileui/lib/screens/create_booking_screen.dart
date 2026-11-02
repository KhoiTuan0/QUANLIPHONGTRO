import 'package:flutter/material.dart';
import '../services/api_service.dart';

class CreateBookingScreen extends StatefulWidget {
  final Map<String, dynamic> room;

  const CreateBookingScreen({super.key, required this.room});

  @override
  State<CreateBookingScreen> createState() => _CreateBookingScreenState();
}

class _CreateBookingScreenState extends State<CreateBookingScreen> {
  final _amountController = TextEditingController();
  final _noteController = TextEditingController();

  List<dynamic> _customers = [];
  int? _selectedCustomerId;
  DateTime _expectedDate = DateTime.now().add(const Duration(days: 3));
  bool _isLoading = false;

  @override
  void initState() {
    super.initState();
    _loadCustomers();
    final initialPrice =
        widget.room['basePrice'] ?? widget.room['giaThue'] ?? 0;
    _amountController.text = initialPrice.toString();
  }

  int? _positiveId(dynamic value) {
    final parsed = value is num
        ? value.toInt()
        : int.tryParse(value?.toString().trim() ?? '');
    return parsed != null && parsed > 0 ? parsed : null;
  }

  @override
  void dispose() {
    _amountController.dispose();
    _noteController.dispose();
    super.dispose();
  }

  void _loadCustomers() async {
    try {
      final list = await ApiService.getCustomers();
      if (mounted) {
        setState(() {
          _customers = list;
          if (_customers.isNotEmpty) {
            _selectedCustomerId = _positiveId(
              _customers.first['maKhach'] ?? _customers.first['customerId'],
            );
          }
        });
      }
    } catch (e) {
      print('Lỗi tải khách hàng: $e');
    }
  }

  void _handleCreateBooking() async {
    if (_selectedCustomerId == null) {
      _showSnackBar(
        'Chưa có khách hàng! Vui lòng tạo khách hàng trước.',
        isError: true,
      );
      return;
    }

    final depositAmount = double.tryParse(_amountController.text.trim());
    if (depositAmount == null || depositAmount <= 0) {
      _showSnackBar('Số tiền cọc phải lớn hơn 0!', isError: true);
      return;
    }

    // MaPhong là khóa chính trong SQL Server. Không dùng roomId=0.
    final maPhong = _positiveId(widget.room['maPhong']) ??
        _positiveId(widget.room['MaPhong']) ??
        _positiveId(widget.room['roomId']);
    if (maPhong == null) {
      debugPrint('Không xác định được MaPhong: ${widget.room}');
      _showSnackBar(
        'Không xác định được mã phòng. Hãy quay lại danh sách và chọn phòng lại.',
        isError: true,
      );
      return;
    }

    setState(() => _isLoading = true);

    try {
      String formattedExpectedDate = _expectedDate.toIso8601String().split(
        'T',
      )[0];
      String formattedToday = DateTime.now().toIso8601String().split('T')[0];

      final data = {
        'roomId': maPhong,
        'maPhong': maPhong,
        'customerId': _selectedCustomerId,
        'depositAmount': depositAmount,
        'bookingDate': formattedToday,
        'expectedDate': formattedExpectedDate,
        'expectedCheckIn': formattedExpectedDate,
        // Lưu đúng nhãn trạng thái đang dùng trong dữ liệu quản lý đặt phòng.
        'status': 'Đã đặt cọc',
        'note': _noteController.text.trim(),
      };

      debugPrint('Tạo đặt cọc - MaPhong: $maPhong; dữ liệu: $data');
      final result = await ApiService.createBooking(data);

      if (mounted) {
        if (result['success'] == true) {
          // Backend tạo lượt giữ chỗ và cập nhật trạng thái phòng trong cùng transaction.
          _showSnackBar('Đặt cọc giữ phòng thành công.', isError: false);
          Navigator.pop(context, true);
        } else {
          _showSnackBar(
            (result['message'] ?? 'Đặt cọc thất bại!').toString(),
            isError: true,
          );
        }
      }
    } catch (e) {
      if (mounted) {
        _showSnackBar('Lỗi Server: $e', isError: true);
      }
    } finally {
      if (mounted) setState(() => _isLoading = false);
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

  @override
  Widget build(BuildContext context) {
    final roomNumber =
        widget.room['roomNumber'] ?? widget.room['tenPhong'] ?? '';

    return Scaffold(
      backgroundColor: const Color(0xFFF5F7FC),
      appBar: AppBar(
        title: Text(
          'Đặt Cọc Giữ $roomNumber',
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
                  const Text(
                    'Chọn khách hàng đặt cọc *',
                    style: TextStyle(
                      fontWeight: FontWeight.bold,
                      fontSize: 14,
                      color: Color(0xFF172554),
                    ),
                  ),
                  const SizedBox(height: 8),
                  _customers.isEmpty
                      ? const Text(
                          '❌ Chưa có khách hàng nào trong hệ thống!',
                          style: TextStyle(
                            color: Colors.redAccent,
                            fontWeight: FontWeight.bold,
                            fontSize: 13,
                          ),
                        )
                      : DropdownButtonFormField<int>(
                          initialValue: _selectedCustomerId,
                          isExpanded: true,
                          decoration: _inputDecoration(
                            '',
                            Icons.person_outline,
                          ),
                          items: _customers.map<DropdownMenuItem<int>>((c) {
                            final id = _positiveId(c['maKhach'] ?? c['customerId']);
                            final name = c['fullName'] ?? c['hoTen'] ?? 'Khách';
                            final phone = c['phone'] ?? c['soDienThoai'] ?? '';
                            return DropdownMenuItem<int>(
                              value: id,
                              child: Text(
                                '$name - $phone',
                                overflow: TextOverflow.ellipsis,
                              ),
                            );
                          }).toList(),
                          onChanged: (val) =>
                              setState(() => _selectedCustomerId = val),
                        ),
                  const SizedBox(height: 20),

                  const Text(
                    'Số tiền đặt cọc (VNĐ) *',
                    style: TextStyle(
                      fontWeight: FontWeight.bold,
                      fontSize: 14,
                      color: Color(0xFF172554),
                    ),
                  ),
                  const SizedBox(height: 8),
                  TextField(
                    controller: _amountController,
                    keyboardType: TextInputType.number,
                    decoration: _inputDecoration(
                      'Nhập số tiền cọc',
                      Icons.payments_outlined,
                    ),
                  ),
                  const SizedBox(height: 20),

                  const Text(
                    'Ngày dự kiến nhận phòng *',
                    style: TextStyle(
                      fontWeight: FontWeight.bold,
                      fontSize: 14,
                      color: Color(0xFF172554),
                    ),
                  ),
                  const SizedBox(height: 8),
                  InkWell(
                    onTap: () async {
                      final picked = await showDatePicker(
                        context: context,
                        initialDate: _expectedDate,
                        firstDate: DateTime.now(),
                        lastDate: DateTime.now().add(const Duration(days: 90)),
                      );
                      if (picked != null) {
                        setState(() => _expectedDate = picked);
                      }
                    },
                    child: Container(
                      padding: const EdgeInsets.symmetric(
                        horizontal: 16,
                        vertical: 14,
                      ),
                      decoration: BoxDecoration(
                        color: const Color(0xFFF5F7FC),
                        border: Border.all(color: const Color(0xFFCBD5E1)),
                        borderRadius: BorderRadius.circular(12),
                      ),
                      child: Row(
                        mainAxisAlignment: MainAxisAlignment.spaceBetween,
                        children: [
                          Text(
                            '${_expectedDate.day}/${_expectedDate.month}/${_expectedDate.year}',
                            style: const TextStyle(
                              fontSize: 15,
                              color: Color(0xFF172554),
                            ),
                          ),
                          const Icon(
                            Icons.calendar_today_rounded,
                            color: Color(0xFF4F46E5),
                            size: 20,
                          ),
                        ],
                      ),
                    ),
                  ),
                  const SizedBox(height: 20),

                  const Text(
                    'Ghi chú đặt cọc',
                    style: TextStyle(
                      fontWeight: FontWeight.bold,
                      fontSize: 14,
                      color: Color(0xFF172554),
                    ),
                  ),
                  const SizedBox(height: 8),
                  TextField(
                    controller: _noteController,
                    maxLines: 2,
                    decoration: _inputDecoration(
                      'VD: Cọc giữ phòng trong 3 ngày...',
                      Icons.note_alt_outlined,
                    ),
                  ),
                ],
              ),
            ),
            const SizedBox(height: 24),
            SizedBox(
              height: 50,
              child: ElevatedButton(
                style: ElevatedButton.styleFrom(
                  backgroundColor: Colors.orange,
                  foregroundColor: Colors.white,
                  shape: RoundedRectangleBorder(
                    borderRadius: BorderRadius.circular(12),
                  ),
                  elevation: 0,
                ),
                onPressed: _isLoading ? null : _handleCreateBooking,
                child: _isLoading
                    ? const SizedBox(
                        width: 24,
                        height: 24,
                        child: CircularProgressIndicator(
                          color: Colors.white,
                          strokeWidth: 2,
                        ),
                      )
                    : const Text(
                        'XÁC NHẬN ĐẶT CỌC',
                        style: TextStyle(
                          fontSize: 15,
                          fontWeight: FontWeight.bold,
                        ),
                      ),
              ),
            ),
          ],
        ),
      ),
    );
  }

  InputDecoration _inputDecoration(String hint, IconData icon) {
    return InputDecoration(
      hintText: hint,
      hintStyle: const TextStyle(color: Color(0xFF94A3B8), fontSize: 13),
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
}
