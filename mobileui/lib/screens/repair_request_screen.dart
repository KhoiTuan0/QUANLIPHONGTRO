import 'package:flutter/material.dart';
import '../services/api_service.dart';

class RepairRequestScreen extends StatefulWidget {
  final int maKhach;

  const RepairRequestScreen({super.key, required this.maKhach});

  @override
  State<RepairRequestScreen> createState() => _RepairRequestScreenState();
}

class _RepairRequestScreenState extends State<RepairRequestScreen>
    with SingleTickerProviderStateMixin {
  late final TabController _tabController;
  final _contentController = TextEditingController();
  DateTime _requestedDate = DateTime.now();
  List<Map<String, dynamic>> _contracts = [];
  List<Map<String, dynamic>> _historyRequests = [];
  int? _selectedContractId;
  bool _isLoadingContracts = true;
  bool _isLoadingHistory = true;
  bool _isSubmitting = false;
  int? _confirmingRequestId;
  int? _deletingRequestId;
  String? _contractError;
  String? _historyError;

  @override
  void initState() {
    super.initState();
    _tabController = TabController(length: 2, vsync: this);
    _loadContracts();
    _loadHistory();
  }

  @override
  void dispose() {
    _tabController.dispose();
    _contentController.dispose();
    super.dispose();
  }

  Future<void> _loadContracts() async {
    if (mounted) {
      setState(() {
        _isLoadingContracts = true;
        _contractError = null;
      });
    }
    try {
      final data = await ApiService.getContractsByCustomer(widget.maKhach);
      final contracts = data
          .whereType<Map>()
          .map((item) => Map<String, dynamic>.from(item))
          .where(_isActiveContract)
          .toList();
      if (!mounted) return;
      setState(() {
        _contracts = contracts;
        if (!contracts.any(
          (contract) =>
              _parseInt(contract['maHopDong'] ?? contract['MaHopDong']) ==
              _selectedContractId,
        )) {
          _selectedContractId = contracts.isEmpty
              ? null
              : _parseInt(
                  contracts.first['maHopDong'] ?? contracts.first['MaHopDong'],
                );
        }
        _isLoadingContracts = false;
      });
    } catch (error) {
      if (!mounted) return;
      setState(() {
        _contractError = error.toString();
        _isLoadingContracts = false;
      });
    }
  }

  Future<void> _loadHistory() async {
    if (mounted) {
      setState(() {
        _isLoadingHistory = true;
        _historyError = null;
      });
    }
    try {
      final data = await ApiService.getRepairRequestsByCustomer(widget.maKhach);
      if (!mounted) return;
      setState(() {
        _historyRequests = data
            .whereType<Map>()
            .map((item) => Map<String, dynamic>.from(item))
            .toList();
        _isLoadingHistory = false;
      });
    } catch (error) {
      if (!mounted) return;
      setState(() {
        _historyError = error.toString();
        _isLoadingHistory = false;
      });
    }
  }

  Future<void> _submitRequest() async {
    final content = _contentController.text.trim();
    if (content.isEmpty) {
      _showSnackBar('Vui lòng nhập nội dung cần sửa chữa.', isError: true);
      return;
    }
    if (_selectedContractId == null) {
      _showSnackBar(
        'Không tìm thấy hợp đồng đang hiệu lực để gửi yêu cầu.',
        isError: true,
      );
      return;
    }

    setState(() => _isSubmitting = true);
    final result = await ApiService.sendRepairRequest({
      'maHopDong': _selectedContractId,
      'moTa': content,
      'ngayYeuCau': _requestedDate.toIso8601String(),
    });
    if (!mounted) return;
    setState(() => _isSubmitting = false);

    if (result['success'] == true) {
      _contentController.clear();
      await _loadHistory();
      if (!mounted) return;
      _tabController.animateTo(1);
      _showSnackBar(
        result['message']?.toString() ?? 'Đã gửi yêu cầu sửa chữa.',
      );
    } else {
      _showSnackBar(
        result['message']?.toString() ?? 'Gửi yêu cầu sửa chữa thất bại.',
        isError: true,
      );
    }
  }

  Future<void> _confirmCompleted(int requestId) async {
    setState(() => _confirmingRequestId = requestId);
    final result = await ApiService.confirmRepairRequest(requestId);
    if (!mounted) return;
    setState(() => _confirmingRequestId = null);
    if (result['success'] == true) {
      _showSnackBar(
        result['message']?.toString() ?? 'Đã xác nhận hoàn thành sửa chữa.',
      );
      await _loadHistory();
    } else {
      _showSnackBar(
        result['message']?.toString() ?? 'Không thể xác nhận yêu cầu sửa chữa.',
        isError: true,
      );
    }
  }

  Future<void> _cancelRequest(int requestId) async {
    final shouldCancel = await showDialog<bool>(
      context: context,
      builder: (context) => AlertDialog(
        title: const Text('Hủy yêu cầu sửa chữa?'),
        content: const Text(
          'Yêu cầu đang chờ tiếp nhận sẽ bị xóa khỏi hệ thống.',
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(context, false),
            child: const Text('Giữ lại'),
          ),
          FilledButton(
            onPressed: () => Navigator.pop(context, true),
            child: const Text('Hủy yêu cầu'),
          ),
        ],
      ),
    );
    if (shouldCancel != true || !mounted) return;

    setState(() => _deletingRequestId = requestId);
    final result = await ApiService.deleteRepairRequest(requestId);
    if (!mounted) return;
    setState(() => _deletingRequestId = null);
    if (result['success'] == true) {
      _showSnackBar(result['message']?.toString() ?? 'Đã hủy yêu cầu.');
      await _loadHistory();
    } else {
      _showSnackBar(
        result['message']?.toString() ?? 'Không thể hủy yêu cầu.',
        isError: true,
      );
    }
  }

  Future<void> _selectRequestedDate() async {
    final today = DateUtils.dateOnly(DateTime.now());
    final selected = await showDatePicker(
      context: context,
      initialDate: _requestedDate.isBefore(today) ? today : _requestedDate,
      firstDate: today,
      lastDate: today.add(const Duration(days: 365)),
    );
    if (selected != null && mounted) {
      setState(() => _requestedDate = selected);
    }
  }

  void _showSnackBar(String message, {bool isError = false}) {
    ScaffoldMessenger.of(context).showSnackBar(
      SnackBar(
        content: Text(message),
        backgroundColor: isError ? Colors.redAccent : Colors.green,
        behavior: SnackBarBehavior.floating,
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: const Color(0xFFF5F7FC),
      appBar: AppBar(
        title: const Text(
          'Báo Cáo & Xử Lý Sửa Chữa',
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
        bottom: PreferredSize(
          preferredSize: const Size.fromHeight(48),
          child: TabBar(
            controller: _tabController,
            indicatorColor: const Color(0xFF4F46E5),
            labelColor: const Color(0xFF4F46E5),
            unselectedLabelColor: const Color(0xFF64748B),
            tabs: const [
              Tab(
                icon: Icon(Icons.add_comment_rounded, size: 20),
                text: 'Gửi yêu cầu mới',
              ),
              Tab(
                icon: Icon(Icons.history_rounded, size: 20),
                text: 'Lịch sử & Tiến độ',
              ),
            ],
          ),
        ),
      ),
      body: TabBarView(
        controller: _tabController,
        children: [_buildNewRequestTab(), _buildHistoryTab()],
      ),
    );
  }

  Widget _buildNewRequestTab() {
    final selectedContract = _contracts.where(
      (contract) =>
          _parseInt(contract['maHopDong'] ?? contract['MaHopDong']) ==
          _selectedContractId,
    );
    final roomId = selectedContract.isEmpty
        ? null
        : selectedContract.first['maPhong'] ??
              selectedContract.first['MaPhong'];

    return ListView(
      padding: const EdgeInsets.all(20),
      children: [
        _buildNotice(
          icon: Icons.info_outline,
          text:
              'Chọn hợp đồng thuê, mô tả sự cố và ngày mong muốn sửa chữa. '
              'Chủ trọ sẽ tiếp nhận yêu cầu.',
        ),
        const SizedBox(height: 16),
        if (_isLoadingContracts)
          const Center(child: CircularProgressIndicator())
        else if (_contractError != null)
          _buildLoadError(
            message: 'Không tải được hợp đồng.\n$_contractError',
            onRetry: _loadContracts,
          )
        else if (_contracts.isEmpty)
          _buildNotice(
            icon: Icons.meeting_room_outlined,
            text: 'Bạn chưa có hợp đồng đang hiệu lực để báo sửa chữa.',
            color: const Color(0xFFFFF7ED),
          )
        else ...[
          _buildFormCard(
            children: [
              const Text(
                'Hợp đồng / phòng *',
                style: TextStyle(fontWeight: FontWeight.bold),
              ),
              const SizedBox(height: 8),
              DropdownButtonFormField<int>(
                initialValue: _selectedContractId,
                isExpanded: true,
                decoration: _inputDecoration(
                  'Chọn hợp đồng',
                  Icons.description_outlined,
                ),
                items: _contracts
                    .map((contract) {
                      final contractId = _parseInt(
                        contract['maHopDong'] ?? contract['MaHopDong'],
                      );
                      if (contractId == null) return null;
                      final contractRoom =
                          contract['maPhong'] ?? contract['MaPhong'] ?? '—';
                      return DropdownMenuItem<int>(
                        value: contractId,
                        child: Text(
                          'Hợp đồng #$contractId · Phòng $contractRoom',
                          overflow: TextOverflow.ellipsis,
                        ),
                      );
                    })
                    .whereType<DropdownMenuItem<int>>()
                    .toList(),
                onChanged: (value) =>
                    setState(() => _selectedContractId = value),
              ),
              if (roomId != null) ...[
                const SizedBox(height: 8),
                Text(
                  'Yêu cầu sẽ được gửi cho phòng $roomId.',
                  style: const TextStyle(
                    color: Color(0xFF64748B),
                    fontSize: 12,
                  ),
                ),
              ],
              const SizedBox(height: 20),
              const Text(
                'Mô tả chi tiết sự cố *',
                style: TextStyle(fontWeight: FontWeight.bold),
              ),
              const SizedBox(height: 8),
              TextField(
                controller: _contentController,
                maxLines: 4,
                maxLength: 1000,
                decoration: _inputDecoration(
                  'Ví dụ: Điều hòa không lạnh, rò rỉ nước...',
                  Icons.edit_note_rounded,
                ),
              ),
              const SizedBox(height: 12),
              const Text(
                'Ngày mong muốn sửa chữa',
                style: TextStyle(fontWeight: FontWeight.bold),
              ),
              const SizedBox(height: 8),
              OutlinedButton.icon(
                onPressed: _selectRequestedDate,
                icon: const Icon(Icons.calendar_today_outlined),
                label: Text(_formatDate(_requestedDate)),
                style: OutlinedButton.styleFrom(
                  alignment: Alignment.centerLeft,
                  padding: const EdgeInsets.symmetric(
                    horizontal: 14,
                    vertical: 14,
                  ),
                ),
              ),
            ],
          ),
          const SizedBox(height: 20),
          SizedBox(
            height: 50,
            child: ElevatedButton(
              onPressed: _isSubmitting ? null : _submitRequest,
              style: ElevatedButton.styleFrom(
                backgroundColor: const Color(0xFF4F46E5),
                foregroundColor: Colors.white,
                shape: RoundedRectangleBorder(
                  borderRadius: BorderRadius.circular(12),
                ),
                elevation: 0,
              ),
              child: _isSubmitting
                  ? const SizedBox(
                      width: 22,
                      height: 22,
                      child: CircularProgressIndicator(
                        color: Colors.white,
                        strokeWidth: 2,
                      ),
                    )
                  : const Text(
                      'GỬI YÊU CẦU SỬA CHỮA',
                      style: TextStyle(fontWeight: FontWeight.bold),
                    ),
            ),
          ),
        ],
      ],
    );
  }

  Widget _buildHistoryTab() {
    if (_isLoadingHistory) {
      return const Center(child: CircularProgressIndicator());
    }
    if (_historyError != null) {
      return _buildLoadError(
        message: 'Không tải được lịch sử sửa chữa.\n$_historyError',
        onRetry: _loadHistory,
      );
    }
    if (_historyRequests.isEmpty) {
      return RefreshIndicator(
        onRefresh: _loadHistory,
        child: ListView(
          children: [
            SizedBox(
              height: MediaQuery.sizeOf(context).height * 0.5,
              child: const Center(
                child: Text(
                  'Chưa có lịch sử báo cáo sửa chữa nào.',
                  style: TextStyle(color: Color(0xFF64748B)),
                ),
              ),
            ),
          ],
        ),
      );
    }

    return RefreshIndicator(
      color: const Color(0xFF4F46E5),
      onRefresh: _loadHistory,
      child: ListView.builder(
        padding: const EdgeInsets.all(16),
        itemCount: _historyRequests.length,
        itemBuilder: (context, index) =>
            _buildHistoryCard(_historyRequests[index], index),
      ),
    );
  }

  Widget _buildHistoryCard(Map<String, dynamic> request, int index) {
    final requestId =
        _parseInt(request['maYeuCau'] ?? request['MaYeuCau']) ?? 0;
    final status =
        (request['trangThai'] ?? request['TrangThai'] ?? 'Chưa cập nhật')
            .toString();
    final normalizedStatus = status.trim().toLowerCase();
    final confirmed = _parseBool(
      request['khachXacNhan'] ?? request['KhachXacNhan'],
    );
    final isCompleted =
        normalizedStatus.contains('hoàn thành') ||
        normalizedStatus.contains('hoan thanh');
    final isWaiting = normalizedStatus.contains('chờ tiếp nhận') ||
        normalizedStatus.contains('cho tiep nhan');
    final statusColor = _repairStatusColor(normalizedStatus);
    final roomName =
        (request['tenPhong'] ??
                request['TenPhong'] ??
                request['maPhong'] ??
                request['MaPhong'] ??
                '—')
            .toString();
    final content =
        (request['noiDung'] ?? request['NoiDung'] ?? 'Không có mô tả')
            .toString();
    final cost = _parseDouble(request['chiPhi'] ?? request['ChiPhi']);
    final requestedDate = request['ngayBao'] ?? request['NgayBao'];
    final completedDate = request['ngayHoanThanh'] ?? request['NgayHoanThanh'];

    return Card(
      elevation: 0,
      margin: const EdgeInsets.only(bottom: 12),
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.circular(16),
        side: const BorderSide(color: Color(0xFFE2E8F0)),
      ),
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              children: [
                Expanded(
                  child: Text(
                    'Yêu cầu #${requestId > 0 ? requestId : index + 1}',
                    style: const TextStyle(
                      fontWeight: FontWeight.bold,
                      color: Color(0xFF4F46E5),
                      fontSize: 15,
                    ),
                  ),
                ),
                _statusPill(status, statusColor),
              ],
            ),
            const Divider(height: 22),
            Text(
              content,
              style: const TextStyle(color: Color(0xFF172554), fontSize: 14),
            ),
            const SizedBox(height: 12),
            _detailLine(Icons.meeting_room_outlined, 'Phòng', roomName),
            _detailLine(
              Icons.calendar_today_outlined,
              'Ngày báo',
              _formatDateValue(requestedDate),
            ),
            if (completedDate != null)
              _detailLine(
                Icons.event_available_outlined,
                'Ngày hoàn thành',
                _formatDateValue(completedDate),
              ),
            if (cost != null)
              _detailLine(
                Icons.payments_outlined,
                'Chi phí',
                '${_formatMoney(cost)} đ',
                valueColor: Colors.deepOrange,
              ),
            if (isCompleted && !confirmed && requestId > 0) ...[
              const SizedBox(height: 12),
              SizedBox(
                width: double.infinity,
                child: ElevatedButton.icon(
                  onPressed: _confirmingRequestId == requestId
                      ? null
                      : () => _confirmCompleted(requestId),
                  icon: _confirmingRequestId == requestId
                      ? const SizedBox(
                          width: 16,
                          height: 16,
                          child: CircularProgressIndicator(
                            strokeWidth: 2,
                            color: Colors.white,
                          ),
                        )
                      : const Icon(Icons.check_circle_outline_rounded),
                  label: const Text('Xác nhận đã sửa xong'),
                  style: ElevatedButton.styleFrom(
                    backgroundColor: Colors.green,
                    foregroundColor: Colors.white,
                  ),
                ),
              ),
            ] else if (confirmed) ...[
              const SizedBox(height: 10),
              const Row(
                children: [
                  Icon(Icons.check_circle, color: Colors.green, size: 16),
                  SizedBox(width: 6),
                  Text(
                    'Đã xác nhận hoàn tất',
                    style: TextStyle(
                      color: Colors.green,
                      fontWeight: FontWeight.w600,
                    ),
                  ),
                ],
              ),
            ],
            if (isWaiting && requestId > 0) ...[
              const SizedBox(height: 8),
              Align(
                alignment: Alignment.centerRight,
                child: TextButton.icon(
                  onPressed: _deletingRequestId == requestId
                      ? null
                      : () => _cancelRequest(requestId),
                  icon: _deletingRequestId == requestId
                      ? const SizedBox(
                          width: 14,
                          height: 14,
                          child: CircularProgressIndicator(strokeWidth: 2),
                        )
                      : const Icon(Icons.delete_outline),
                  label: const Text('Hủy yêu cầu'),
                  style: TextButton.styleFrom(
                    foregroundColor: Colors.redAccent,
                  ),
                ),
              ),
            ],
          ],
        ),
      ),
    );
  }

  Widget _buildNotice({
    required IconData icon,
    required String text,
    Color color = const Color(0xFFEFF6FF),
  }) {
    return Container(
      padding: const EdgeInsets.all(16),
      decoration: BoxDecoration(
        color: color,
        borderRadius: BorderRadius.circular(14),
      ),
      child: Row(
        children: [
          Icon(icon, color: const Color(0xFF4F46E5)),
          const SizedBox(width: 12),
          Expanded(child: Text(text)),
        ],
      ),
    );
  }

  Widget _buildFormCard({required List<Widget> children}) {
    return Container(
      padding: const EdgeInsets.all(20),
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(20),
        border: Border.all(color: const Color(0xFFE2E8F0)),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: children,
      ),
    );
  }

  Widget _buildLoadError({
    required String message,
    required Future<void> Function() onRetry,
  }) {
    return Center(
      child: Padding(
        padding: const EdgeInsets.all(24),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            const Icon(Icons.error_outline, color: Colors.redAccent, size: 44),
            const SizedBox(height: 12),
            Text(message, textAlign: TextAlign.center),
            const SizedBox(height: 12),
            OutlinedButton.icon(
              onPressed: onRetry,
              icon: const Icon(Icons.refresh),
              label: const Text('Thử tải lại'),
            ),
          ],
        ),
      ),
    );
  }

  InputDecoration _inputDecoration(String hint, IconData icon) {
    return InputDecoration(
      hintText: hint,
      prefixIcon: Icon(icon, color: const Color(0xFF4F46E5), size: 20),
      border: OutlineInputBorder(borderRadius: BorderRadius.circular(12)),
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

  Widget _detailLine(
    IconData icon,
    String label,
    String value, {
    Color valueColor = const Color(0xFF64748B),
  }) {
    return Padding(
      padding: const EdgeInsets.only(bottom: 7),
      child: Row(
        children: [
          Icon(icon, color: const Color(0xFF64748B), size: 16),
          const SizedBox(width: 8),
          Text(
            '$label: ',
            style: const TextStyle(color: Color(0xFF64748B), fontSize: 12),
          ),
          Expanded(
            child: Text(
              value,
              textAlign: TextAlign.end,
              style: TextStyle(
                color: valueColor,
                fontSize: 12,
                fontWeight: FontWeight.w600,
              ),
            ),
          ),
        ],
      ),
    );
  }

  Widget _statusPill(String status, Color color) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 5),
      decoration: BoxDecoration(
        color: color.withValues(alpha: 0.1),
        borderRadius: BorderRadius.circular(20),
      ),
      child: Text(
        status,
        style: TextStyle(
          color: color,
          fontWeight: FontWeight.bold,
          fontSize: 11,
        ),
      ),
    );
  }
}

bool _isActiveContract(Map<String, dynamic> contract) {
  final status = (contract['trangThai'] ?? contract['TrangThai'] ?? '')
      .toString()
      .trim()
      .toLowerCase();
  return !status.contains('thanh lý') &&
      !status.contains('thanh ly') &&
      !status.contains('kết thúc') &&
      !status.contains('ket thuc') &&
      !status.contains('hết hạn') &&
      !status.contains('het han') &&
      status != 'terminated' &&
      status != 'cancelled';
}

Color _repairStatusColor(String status) {
  if (status.contains('hoàn thành') || status.contains('hoan thanh')) {
    return Colors.green;
  }
  if (status.contains('đang') || status.contains('dang')) {
    return Colors.blue;
  }
  if (status.contains('chờ') || status.contains('cho')) {
    return Colors.deepPurple;
  }
  return Colors.orange;
}

int? _parseInt(dynamic value) => int.tryParse(value?.toString() ?? '');

double? _parseDouble(dynamic value) =>
    value is num ? value.toDouble() : double.tryParse(value?.toString() ?? '');

bool _parseBool(dynamic value) =>
    value == true || value?.toString().toLowerCase() == 'true';

String _formatDate(DateTime date) =>
    '${date.day.toString().padLeft(2, '0')}/'
    '${date.month.toString().padLeft(2, '0')}/${date.year}';

String _formatDateValue(dynamic value) {
  if (value == null) return 'Chưa cập nhật';
  final parsed = DateTime.tryParse(value.toString());
  return parsed == null ? value.toString() : _formatDate(parsed);
}

String _formatMoney(double amount) =>
    amount.round().toString().replaceAllMapped(
      RegExp(r'(\d)(?=(\d{3})+(?!\d))'),
      (match) => '${match[1]},',
    );
