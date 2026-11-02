import 'package:flutter/material.dart';
import '../services/api_service.dart';

class CustomerContractScreen extends StatefulWidget {
  final int maKhach; // Nhận mã khách từ lúc đăng nhập thành công

  const CustomerContractScreen({super.key, required this.maKhach});

  @override
  State<CustomerContractScreen> createState() => _CustomerContractScreenState();
}

class _CustomerContractScreenState extends State<CustomerContractScreen> {
  List<dynamic> _contracts = [];
  bool _isLoading = true;

  @override
  void initState() {
    super.initState();
    _loadContracts();
  }

  Future<void> _loadContracts() async {
    if (mounted) setState(() => _isLoading = true);
    final data = await ApiService.getContractsByCustomer(widget.maKhach);
    if (!mounted) return;
    setState(() {
      _contracts = data;
      _isLoading = false;
    });
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: const Color(0xFFF5F7FC),
      appBar: AppBar(
        title: const Text(
          'Hợp Đồng Thuê Trọ Của Tôi',
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
      ),
      body: _isLoading
          ? const Center(
              child: CircularProgressIndicator(color: Color(0xFF4F46E5)),
            )
          : _contracts.isEmpty
          ? Center(
              child: Column(
                mainAxisAlignment: MainAxisAlignment.center,
                children: [
                  Icon(
                    Icons.description_outlined,
                    size: 64,
                    color: Colors.grey.shade400,
                  ),
                  const SizedBox(height: 12),
                  const Text(
                    'Bạn chưa có hợp đồng thuê phòng nào.',
                    style: TextStyle(color: Color(0xFF64748B), fontSize: 16),
                  ),
                  const SizedBox(height: 16),
                  ElevatedButton.icon(
                    onPressed: _loadContracts,
                    icon: const Icon(Icons.refresh_rounded, size: 18),
                    label: const Text(
                      'Tải lại',
                      style: TextStyle(fontWeight: FontWeight.bold),
                    ),
                    style: ElevatedButton.styleFrom(
                      backgroundColor: const Color(0xFF4F46E5),
                      foregroundColor: Colors.white,
                      elevation: 0,
                      shape: RoundedRectangleBorder(
                        borderRadius: BorderRadius.circular(10),
                      ),
                    ),
                  ),
                ],
              ),
            )
          : RefreshIndicator(
              color: const Color(0xFF4F46E5),
              onRefresh: () async => _loadContracts(),
              child: ListView.builder(
                padding: const EdgeInsets.all(16),
                itemCount: _contracts.length,
                itemBuilder: (context, index) {
                  final item = Map<String, dynamic>.from(
                    _contracts[index] as Map,
                  );
                  final maHopDong = item['maHopDong'] ?? index + 1;
                  final maPhong = item['maPhong'];
                  final trangThai =
                      item['trangThai']?.toString() ?? 'Chưa cập nhật';
                  final statusNormalized = trangThai.toLowerCase();
                  final isEnded =
                      statusNormalized.contains('thanh lý') ||
                      statusNormalized.contains('kết thúc') ||
                      statusNormalized.contains('hết hạn');
                  final statusColor = isEnded
                      ? Colors.orange.shade800
                      : Colors.green;
                  return Card(
                    elevation: 0,
                    margin: const EdgeInsets.only(bottom: 12),
                    shape: RoundedRectangleBorder(
                      borderRadius: BorderRadius.circular(16),
                      side: const BorderSide(color: Color(0xFFE2E8F0)),
                    ),
                    child: Padding(
                      padding: const EdgeInsets.all(16.0),
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Row(
                            mainAxisAlignment: MainAxisAlignment.spaceBetween,
                            children: [
                              Text(
                                'Hợp đồng #$maHopDong',
                                style: const TextStyle(
                                  fontSize: 16,
                                  fontWeight: FontWeight.bold,
                                  color: Color(0xFF4F46E5),
                                ),
                              ),
                              Container(
                                padding: const EdgeInsets.symmetric(
                                  horizontal: 10,
                                  vertical: 4,
                                ),
                                decoration: BoxDecoration(
                                  color: statusColor.withValues(alpha: 0.1),
                                  borderRadius: BorderRadius.circular(20),
                                ),
                                child: Text(
                                  trangThai,
                                  style: TextStyle(
                                    color: statusColor,
                                    fontWeight: FontWeight.bold,
                                    fontSize: 11,
                                  ),
                                ),
                              ),
                            ],
                          ),
                          const Divider(height: 24, color: Color(0xFFE2E8F0)),
                          _buildInfoRow(
                            Icons.meeting_room_outlined,
                            'Mã phòng:',
                            maPhong?.toString() ?? 'Chưa cập nhật',
                          ),
                          const SizedBox(height: 10),
                          _buildInfoRow(
                            Icons.payments_outlined,
                            'Tiền thuê hàng tháng:',
                            '${_formatContractMoney(item['giaThueThang'])} đ',
                          ),
                          const SizedBox(height: 10),
                          _buildInfoRow(
                            Icons.account_balance_wallet_outlined,
                            'Tiền cọc:',
                            '${_formatContractMoney(item['tienCoc'])} đ',
                          ),
                          const SizedBox(height: 10),
                          _buildInfoRow(
                            Icons.date_range_rounded,
                            'Thời hạn:',
                            '${_formatContractDate(item['ngayBatDau'])} → ${_formatContractDate(item['ngayKetThuc'])}',
                          ),
                          const SizedBox(height: 16),
                          SizedBox(
                            width: double.infinity,
                            height: 42,
                            child: OutlinedButton.icon(
                              style: OutlinedButton.styleFrom(
                                foregroundColor: const Color(0xFF4F46E5),
                                side: const BorderSide(
                                  color: Color(0xFF4F46E5),
                                ),
                                shape: RoundedRectangleBorder(
                                  borderRadius: BorderRadius.circular(10),
                                ),
                                backgroundColor: Colors.white,
                              ),
                              onPressed: () {
                                _showContractDetailModal(context, item);
                              },
                              icon: const Icon(
                                Icons.info_outline_rounded,
                                size: 18,
                              ),
                              label: const Text(
                                'Xem chi tiết hợp đồng',
                                style: TextStyle(
                                  fontWeight: FontWeight.bold,
                                  fontSize: 13,
                                ),
                              ),
                            ),
                          ),
                        ],
                      ),
                    ),
                  );
                },
              ),
            ),
    );
  }

  Widget _buildInfoRow(IconData icon, String label, String value) {
    return Row(
      children: [
        Icon(icon, size: 18, color: const Color(0xFF4F46E5)),
        const SizedBox(width: 10),
        Text(
          label,
          style: const TextStyle(color: Color(0xFF64748B), fontSize: 13),
        ),
        const SizedBox(width: 8),
        Expanded(
          child: Text(
            value,
            style: const TextStyle(
              fontWeight: FontWeight.bold,
              fontSize: 13,
              color: Color(0xFF172554),
            ),
            textAlign: TextAlign.right,
          ),
        ),
      ],
    );
  }

  // Hộp thoại hiển thị chi tiết đầy đủ của hợp đồng
  void _showContractDetailModal(BuildContext context, dynamic contract) {
    showDialog(
      context: context,
      builder: (context) => AlertDialog(
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(20)),
        title: Text(
          'Chi Tiết Hợp Đồng #${contract['maHopDong']}',
          style: const TextStyle(fontWeight: FontWeight.bold, fontSize: 18),
        ),
        content: SingleChildScrollView(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            mainAxisSize: MainAxisSize.min,
            children: [
              _modalRow(
                'Mã phòng:',
                contract['maPhong']?.toString() ?? 'Chưa cập nhật',
              ),
              const SizedBox(height: 10),
              _modalRow(
                'Mã hợp đồng:',
                contract['maHopDong']?.toString() ?? 'Chưa cập nhật',
              ),
              const SizedBox(height: 10),
              _modalRow(
                'Trạng thái:',
                contract['trangThai']?.toString() ?? 'Chưa cập nhật',
              ),
              const SizedBox(height: 10),
              _modalRow(
                'Giá thuê hàng tháng:',
                '${_formatContractMoney(contract['giaThueThang'])} VNĐ',
              ),
              const SizedBox(height: 10),
              _modalRow(
                'Tiền đặt cọc:',
                '${_formatContractMoney(contract['tienCoc'])} VNĐ',
              ),
              const SizedBox(height: 10),
              _modalRow(
                'Ngày bắt đầu:',
                _formatContractDate(contract['ngayBatDau']),
              ),
              const SizedBox(height: 10),
              _modalRow(
                'Ngày kết thúc:',
                _formatContractDate(contract['ngayKetThuc']),
              ),
            ],
          ),
        ),
        actions: [
          ElevatedButton(
            style: ElevatedButton.styleFrom(
              backgroundColor: const Color(0xFF4F46E5),
              foregroundColor: Colors.white,
              elevation: 0,
              shape: RoundedRectangleBorder(
                borderRadius: BorderRadius.circular(8),
              ),
            ),
            onPressed: () => Navigator.pop(context),
            child: const Text(
              'Đóng',
              style: TextStyle(fontWeight: FontWeight.bold),
            ),
          ),
        ],
      ),
    );
  }

  Widget _modalRow(String label, String value) {
    return Row(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        SizedBox(
          width: 120,
          child: Text(
            label,
            style: const TextStyle(
              color: Color(0xFF64748B),
              fontSize: 13,
              fontWeight: FontWeight.w500,
            ),
          ),
        ),
        Expanded(
          child: Text(
            value,
            style: const TextStyle(
              fontWeight: FontWeight.bold,
              fontSize: 13,
              color: Color(0xFF172554),
            ),
          ),
        ),
      ],
    );
  }
}

String _formatContractMoney(dynamic value) {
  final amount = num.tryParse(value?.toString() ?? '');
  if (amount == null) return 'Chưa cập nhật';
  return amount.round().toString().replaceAllMapped(
    RegExp(r'(\d)(?=(\d{3})+(?!\d))'),
    (match) => '${match[1]},',
  );
}

String _formatContractDate(dynamic value) {
  if (value == null) return 'Chưa cập nhật';
  final date = DateTime.tryParse(value.toString());
  if (date == null) return value.toString();
  final day = date.day.toString().padLeft(2, '0');
  final month = date.month.toString().padLeft(2, '0');
  return '$day/$month/${date.year}';
}
