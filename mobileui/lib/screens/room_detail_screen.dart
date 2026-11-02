import 'package:flutter/material.dart';
import 'create_contract_screen.dart';
import 'create_booking_screen.dart';

class RoomDetailScreen extends StatefulWidget {
  final Map<String, dynamic> room;

  const RoomDetailScreen({super.key, required this.room});

  @override
  State<RoomDetailScreen> createState() => _RoomDetailScreenState();
}

class _RoomDetailScreenState extends State<RoomDetailScreen> {
  Color _getStatusColor(String? status) {
    switch (status) {
      case 'Trống': return Colors.green;
      case 'Đang thuê': return Colors.blueAccent;
      case 'Đã đặt cọc': return Colors.orange;
      case 'Bảo trì': return Colors.redAccent;
      default: return Colors.grey;
    }
  }

  @override
  Widget build(BuildContext context) {
    final room = widget.room;
    final tenPhong = room['tenPhong'] ?? room['roomNumber'] ?? 'Phòng';
    final tang = room['tang'] ?? room['floor'] ?? 1;
    final giaThue = room['giaThue'] ?? room['basePrice'] ?? 0;
    final trangThai = room['trangThai'] ?? room['status'] ?? 'Trống';
    final moTa = room['moTa'] ?? 'Không có mô tả';
    final statusColor = _getStatusColor(trangThai);

    return Scaffold(
      backgroundColor: const Color(0xFFF5F7FC),
      appBar: AppBar(
        title: Text(
          'Chi Tiết $tenPhong',
          style: const TextStyle(fontWeight: FontWeight.bold, fontSize: 18, color: Color(0xFF172554)),
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
            // Thẻ thông tin chi tiết phòng
            Container(
              padding: const EdgeInsets.all(20.0),
              decoration: BoxDecoration(
                color: Colors.white,
                borderRadius: BorderRadius.circular(20),
                border: Border.all(color: const Color(0xFFE2E8F0)),
                boxShadow: [
                  BoxShadow(
                    color: Colors.black.withOpacity(0.02),
                    blurRadius: 10,
                    offset: const Offset(0, 4),
                  ),
                ],
              ),
              child: Column(
                children: [
                  Row(
                    mainAxisAlignment: MainAxisAlignment.spaceBetween,
                    children: [
                      Text(
                        tenPhong,
                        style: const TextStyle(fontSize: 22, fontWeight: FontWeight.bold, color: Color(0xFF172554)),
                      ),
                      Container(
                        padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 6),
                        decoration: BoxDecoration(
                          color: statusColor.withOpacity(0.1),
                          borderRadius: BorderRadius.circular(20),
                        ),
                        child: Text(
                          trangThai,
                          style: TextStyle(color: statusColor, fontWeight: FontWeight.bold, fontSize: 13),
                        ),
                      ),
                    ],
                  ),
                  const Divider(height: 32, color: Color(0xFFE2E8F0)),
                  _buildInfoRow(Icons.layers_outlined, 'Tầng số', 'Tầng $tang'),
                  const SizedBox(height: 16),
                  _buildInfoRow(Icons.description_outlined, 'Mô tả tiện ích', moTa),
                  const SizedBox(height: 16),
                  _buildInfoRow(
                    Icons.payments_outlined,
                    'Giá thuê hàng tháng',
                    '$giaThue VNĐ',
                    isHighlight: true,
                  ),
                ],
              ),
            ),
            const SizedBox(height: 24),

            // Khu vực xử lý nghiệp vụ theo trạng thái thực tế
            if (trangThai == 'Trống') ...[
              SizedBox(
                height: 50,
                child: ElevatedButton.icon(
                  style: ElevatedButton.styleFrom(
                    backgroundColor: const Color(0xFF4F46E5),
                    foregroundColor: Colors.white,
                    shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(12)),
                    elevation: 0,
                  ),
                  icon: const Icon(Icons.add_task_rounded, size: 20),
                  label: const Text(
                    'TẠO HỢP ĐỒNG (NHẬN PHÒNG)',
                    style: TextStyle(fontWeight: FontWeight.bold, fontSize: 15),
                  ),
                  onPressed: () async {
                    final result = await Navigator.push(
                      context,
                      MaterialPageRoute(
                        builder: (context) => CreateContractScreen(room: room),
                      ),
                    );
                    if (result == true && mounted) {
                      Navigator.pop(context, true);
                    }
                  },
                ),
              ),
              const SizedBox(height: 12),
              SizedBox(
                height: 50,
                child: OutlinedButton.icon(
                  style: OutlinedButton.styleFrom(
                    side: const BorderSide(color: Colors.orange, width: 1.5),
                    shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(12)),
                    backgroundColor: Colors.white,
                  ),
                  icon: const Icon(Icons.bookmark_add_outlined, color: Colors.orange, size: 20),
                  label: const Text(
                    'NHẬN ĐẶT CỌC GIỮ PHÒNG',
                    style: TextStyle(color: Colors.orange, fontWeight: FontWeight.bold, fontSize: 15),
                  ),
                  onPressed: () async {
                    final result = await Navigator.push(
                      context,
                      MaterialPageRoute(
                        builder: (context) => CreateBookingScreen(room: room),
                      ),
                    );
                    if (result == true && mounted) {
                      Navigator.pop(context, true);
                    }
                  },
                ),
              ),
            ] else if (trangThai == 'Đang thuê') ...[
              Container(
                padding: const EdgeInsets.all(16),
                decoration: BoxDecoration(
                  color: Colors.blue.shade50,
                  borderRadius: BorderRadius.circular(12),
                  border: Border.all(color: Colors.blue.shade200),
                ),
                child: const Row(
                  children: [
                    Icon(Icons.info_outline_rounded, color: Color(0xFF4F46E5)),
                    SizedBox(width: 12),
                    Expanded(
                      child: Text(
                        'Phòng này đang có khách thuê ở. Để thanh lý trả phòng, vui lòng quản lý trực tiếp thông qua danh sách hợp đồng.',
                        style: TextStyle(color: Color(0xFF1E3A8A), fontSize: 13),
                      ),
                    ),
                  ],
                ),
              ),
            ] else if (trangThai == 'Đã đặt cọc') ...[
              Container(
                padding: const EdgeInsets.all(16),
                decoration: BoxDecoration(
                  color: Colors.orange.shade50,
                  borderRadius: BorderRadius.circular(12),
                  border: Border.all(color: Colors.orange.shade200),
                ),
                child: const Row(
                  children: [
                    Icon(Icons.bookmark_rounded, color: Colors.orange),
                    SizedBox(width: 12),
                    Expanded(
                      child: Text(
                        'Phòng này hiện đã được khách đặt cọc giữ chỗ chờ ngày dọn vào.',
                        style: TextStyle(color: Color(0xFF9A3412), fontSize: 13),
                      ),
                    ),
                  ],
                ),
              ),
            ],
          ],
        ),
      ),
    );
  }

  Widget _buildInfoRow(IconData icon, String label, String value, {bool isHighlight = false}) {
    return Row(
      children: [
        Icon(icon, color: const Color(0xFF4F46E5), size: 22),
        const SizedBox(width: 12),
        Expanded(
          child: Text(
            label,
            style: const TextStyle(color: Color(0xFF64748B), fontSize: 14),
          ),
        ),
        Text(
          value,
          style: TextStyle(
            fontWeight: FontWeight.bold,
            fontSize: 15,
            color: isHighlight ? const Color(0xFF4F46E5) : const Color(0xFF172554),
          ),
        ),
      ],
    );
  }
}