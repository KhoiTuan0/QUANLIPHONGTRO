import 'package:flutter/material.dart';
import '../services/api_service.dart';

class CustomerDetailScreen extends StatefulWidget {
  final Map<String, dynamic> customer;

  const CustomerDetailScreen({super.key, required this.customer});

  @override
  State<CustomerDetailScreen> createState() => _CustomerDetailScreenState();
}

class _CustomerDetailScreenState extends State<CustomerDetailScreen> {
  bool _isLoadingContracts = true;
  List<dynamic> _customerContracts = [];

  @override
  void initState() {
    super.initState();
    _loadCustomerContracts();
  }

  void _loadCustomerContracts() async {
    final maKhach = widget.customer['maKhach'] ?? widget.customer['customerId'] ?? 0;
    try {
      final contracts = await ApiService.getContractsByCustomer(maKhach);
      if (mounted) {
        setState(() {
          _customerContracts = contracts;
          _isLoadingContracts = false;
        });
      }
    } catch (e) {
      print('Lỗi tải hợp đồng của khách: $e');
      if (mounted) setState(() => _isLoadingContracts = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final c = widget.customer;
    final hoTen = c['hoTen'] ?? c['fullName'] ?? 'Khách thuê';
    final sdt = c['soDienThoai'] ?? c['phone'] ?? 'Chưa cập nhật';
    final cccd = c['cccd'] ?? 'Chưa cập nhật';
    final email = c['email'] ?? 'Chưa cập nhật';
    final diaChi = c['diaChi'] ?? 'Chưa cập nhật';
    final gioiTinh = c['gioiTinh'] ?? 'Chưa cập nhật';
    final ngaySinh = c['ngaySinh'] != null ? c['ngaySinh'].toString().split('T')[0] : 'Chưa cập nhật';

    return Scaffold(
      backgroundColor: const Color(0xFFF5F7FC),
      appBar: AppBar(
        title: Text(
          'Hồ Sơ: $hoTen',
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
            // Thẻ thông tin cá nhân khách thuê
            Container(
              padding: const EdgeInsets.all(20.0),
              decoration: BoxDecoration(
                color: Colors.white,
                borderRadius: BorderRadius.circular(20),
                border: Border.all(color: const Color(0xFFE2E8F0)),
              ),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Row(
                    children: [
                      Container(
                        width: 56,
                        height: 56,
                        decoration: BoxDecoration(
                          color: const Color(0xFF4F46E5).withOpacity(0.1),
                          borderRadius: BorderRadius.circular(16),
                        ),
                        child: const Icon(Icons.person_rounded, color: Color(0xFF4F46E5), size: 30),
                      ),
                      const SizedBox(width: 16),
                      Expanded(
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: [
                            Text(
                              hoTen,
                              style: const TextStyle(fontWeight: FontWeight.bold, fontSize: 18, color: Color(0xFF172554)),
                            ),
                            const SizedBox(height: 4),
                            Text(
                              'Giới tính: $gioiTinh • Ngày sinh: $ngaySinh',
                              style: const TextStyle(color: Color(0xFF64748B), fontSize: 13),
                            ),
                          ],
                        ),
                      ),
                    ],
                  ),
                  const Divider(height: 32, color: Color(0xFFE2E8F0)),
                  _buildInfoRow(Icons.phone_outlined, 'Số điện thoại', sdt),
                  const SizedBox(height: 14),
                  _buildInfoRow(Icons.badge_outlined, 'Số CCCD / CMND', cccd),
                  const SizedBox(height: 14),
                  _buildInfoRow(Icons.email_outlined, 'Địa chỉ Email', email),
                  const SizedBox(height: 14),
                  _buildInfoRow(Icons.location_on_outlined, 'Địa chỉ thường trú', diaChi),
                ],
              ),
            ),
            const SizedBox(height: 24),

            // Tiêu đề phần phòng đang thuê
            const Text(
              'Phòng Trọ Đang Thuê & Hợp Đồng',
              style: TextStyle(fontWeight: FontWeight.bold, fontSize: 16, color: Color(0xFF172554)),
            ),
            const SizedBox(height: 12),

            // Nội dung hiển thị phòng đang ở
            _isLoadingContracts
                ? const Center(child: Padding(padding: EdgeInsets.all(20.0), child: CircularProgressIndicator(color: Color(0xFF4F46E5))))
                : _customerContracts.isEmpty
                    ? Container(
                        padding: const EdgeInsets.all(20),
                        decoration: BoxDecoration(
                          color: Colors.white,
                          borderRadius: BorderRadius.circular(16),
                          border: Border.all(color: const Color(0xFFE2E8F0)),
                        ),
                        child: const Center(
                          child: Text(
                            'Khách hàng này hiện không thuê phòng nào (hoặc chưa có hợp đồng).',
                            style: TextStyle(color: Color(0xFF64748B), fontSize: 14),
                            textAlign: TextAlign.center,
                          ),
                        ),
                      )
                    : Column(
                        children: _customerContracts.map((contract) {
                          final maHd = contract['maHopDong'] ?? contract['contractId'] ?? 0;
                          final tenPhong = contract['tenPhong'] ?? contract['roomNumber'] ?? contract['maPhong'] ?? 'Phòng';
                          final giaThue = contract['giaThueThang'] ?? contract['basePrice'] ?? 0;
                          final ngayBatDau = contract['ngayBatDau'] != null ? contract['ngayBatDau'].toString().split('T')[0] : '';
                          final trangThaiHd = contract['trangThai'] ?? 'Đang hiệu lực';

                          return Container(
                            margin: const EdgeInsets.only(bottom: 12),
                            padding: const EdgeInsets.all(16),
                            decoration: BoxDecoration(
                              color: Colors.white,
                              borderRadius: BorderRadius.circular(16),
                              border: Border.all(color: const Color(0xFFE2E8F0)),
                            ),
                            child: Column(
                              crossAxisAlignment: CrossAxisAlignment.start,
                              children: [
                                Row(
                                  mainAxisAlignment: MainAxisAlignment.spaceBetween,
                                  children: [
                                    Text(
                                      'Phòng: $tenPhong',
                                      style: const TextStyle(fontWeight: FontWeight.bold, fontSize: 16, color: Color(0xFF4F46E5)),
                                    ),
                                    Container(
                                      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
                                      decoration: BoxDecoration(
                                        color: Colors.green.withOpacity(0.1),
                                        borderRadius: BorderRadius.circular(20),
                                      ),
                                      child: Text(
                                        trangThaiHd,
                                        style: const TextStyle(color: Colors.green, fontWeight: FontWeight.bold, fontSize: 12),
                                      ),
                                    ),
                                  ],
                                ),
                                const Divider(height: 20, color: Color(0xFFE2E8F0)),
                                Text('Mã hợp đồng: #$maHd', style: const TextStyle(fontSize: 13, color: Color(0xFF172554))),
                                const SizedBox(height: 4),
                                Text('Giá thuê: $giaThue VNĐ/tháng', style: const TextStyle(fontSize: 13, color: Color(0xFF172554))),
                                const SizedBox(height: 4),
                                Text('Ngày bắt đầu: $ngayBatDau', style: const TextStyle(fontSize: 13, color: Color(0xFF64748B))),
                              ],
                            ),
                          );
                        }).toList(),
                      ),
          ],
        ),
      ),
    );
  }

  Widget _buildInfoRow(IconData icon, String label, String value) {
    return Row(
      children: [
        Icon(icon, color: const Color(0xFF4F46E5), size: 20),
        const SizedBox(width: 12),
        SizedBox(
          width: 130,
          child: Text(label, style: const TextStyle(color: Color(0xFF64748B), fontSize: 14)),
        ),
        Expanded(
          child: Text(
            value,
            style: const TextStyle(fontWeight: FontWeight.w600, fontSize: 14, color: Color(0xFF172554)),
            textAlign: TextAlign.right,
          ),
        ),
      ],
    );
  }
}