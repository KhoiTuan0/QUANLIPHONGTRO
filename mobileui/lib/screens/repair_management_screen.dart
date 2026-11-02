import 'package:flutter/material.dart';
import '../services/api_service.dart';

class RepairManagementScreen extends StatefulWidget {
  const RepairManagementScreen({super.key});

  @override
  State<RepairManagementScreen> createState() => _RepairManagementScreenState();
}

class _RepairManagementScreenState extends State<RepairManagementScreen> {
  static const _allStatuses = 'Tất cả';
  static const _statuses = [
    _allStatuses,
    'Chờ tiếp nhận',
    'Đang xử lý',
    'Đã hoàn thành',
  ];

  List<Map<String, dynamic>> _requests = [];
  bool _isLoading = true;
  String? _loadError;
  String _selectedStatus = _allStatuses;
  final Set<int> _updatingIds = {};

  @override
  void initState() {
    super.initState();
    _loadRequests();
  }

  Future<void> _loadRequests() async {
    if (mounted) {
      setState(() {
        _isLoading = true;
        _loadError = null;
      });
    }
    try {
      final data = await ApiService.getRepairRequests();
      if (!mounted) return;
      final requests = data
          .whereType<Map>()
          .map((item) => Map<String, dynamic>.from(item))
          .toList();
      requests.sort((left, right) {
        final leftDate = _parseDate(_value(left, 'ngayBao', 'NgayBao'));
        final rightDate = _parseDate(_value(right, 'ngayBao', 'NgayBao'));
        return (rightDate ?? DateTime(0)).compareTo(leftDate ?? DateTime(0));
      });
      setState(() {
        _requests = requests;
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

  Future<void> _acceptRequest(Map<String, dynamic> request) async {
    final id = _requestId(request);
    if (id == null) {
      _showMessage(
        'Yêu cầu thiếu mã hợp lệ, không thể cập nhật.',
        isError: true,
      );
      return;
    }
    await _saveRequest(id, {'trangThai': 'Đang xử lý'});
  }

  Future<void> _editRequest(Map<String, dynamic> request) async {
    final id = _requestId(request);
    if (id == null) {
      _showMessage(
        'Yêu cầu thiếu mã hợp lệ, không thể cập nhật.',
        isError: true,
      );
      return;
    }
    final result = await showDialog<Map<String, dynamic>>(
      context: context,
      builder: (context) => _RepairUpdateDialog(request: request),
    );
    if (result == null || !mounted) return;
    await _saveRequest(id, result);
  }

  Future<void> _saveRequest(int requestId, Map<String, dynamic> updates) async {
    setState(() => _updatingIds.add(requestId));
    final result = await ApiService.updateRepairRequest(requestId, updates);
    if (!mounted) return;
    setState(() => _updatingIds.remove(requestId));
    if (result['success'] == true) {
      _showMessage(
        result['message']?.toString() ?? 'Đã cập nhật yêu cầu sửa chữa.',
      );
      await _loadRequests();
    } else {
      _showMessage(
        result['message']?.toString() ?? 'Cập nhật yêu cầu thất bại.',
        isError: true,
      );
    }
  }

  void _showMessage(String message, {bool isError = false}) {
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
    final filteredRequests = _selectedStatus == _allStatuses
        ? _requests
        : _requests
              .where(
                (request) =>
                    _requestStatus(request).toLowerCase() ==
                    _selectedStatus.toLowerCase(),
              )
              .toList();

    return Scaffold(
      backgroundColor: const Color(0xFFF5F7FC),
      appBar: AppBar(
        title: const Text(
          'Yêu cầu sửa chữa',
          style: TextStyle(
            color: Color(0xFF172554),
            fontWeight: FontWeight.bold,
          ),
        ),
        backgroundColor: Colors.white,
        foregroundColor: const Color(0xFF172554),
        elevation: 0,
        actions: [
          IconButton(
            tooltip: 'Tải lại',
            onPressed: _loadRequests,
            icon: const Icon(Icons.refresh),
          ),
        ],
      ),
      body: Column(
        children: [
          Padding(
            padding: const EdgeInsets.fromLTRB(16, 12, 16, 8),
            child: DropdownButtonFormField<String>(
              initialValue: _selectedStatus,
              decoration: const InputDecoration(
                labelText: 'Lọc theo trạng thái',
                prefixIcon: Icon(Icons.filter_list),
                border: OutlineInputBorder(),
                filled: true,
                fillColor: Colors.white,
              ),
              items: _statuses
                  .map(
                    (status) =>
                        DropdownMenuItem(value: status, child: Text(status)),
                  )
                  .toList(),
              onChanged: (value) {
                if (value != null) setState(() => _selectedStatus = value);
              },
            ),
          ),
          Expanded(child: _buildBody(filteredRequests)),
        ],
      ),
    );
  }

  Widget _buildBody(List<Map<String, dynamic>> requests) {
    if (_isLoading) {
      return const Center(child: CircularProgressIndicator());
    }
    if (_loadError != null) {
      return _buildMessage(
        icon: Icons.error_outline,
        message: 'Không tải được yêu cầu.\n$_loadError',
        actionLabel: 'Thử lại',
        onAction: _loadRequests,
      );
    }
    if (requests.isEmpty) {
      return RefreshIndicator(
        onRefresh: _loadRequests,
        child: ListView(
          children: const [
            SizedBox(
              height: 300,
              child: Center(
                child: Text(
                  'Không có yêu cầu sửa chữa phù hợp.',
                  style: TextStyle(color: Color(0xFF64748B)),
                ),
              ),
            ),
          ],
        ),
      );
    }

    return RefreshIndicator(
      onRefresh: _loadRequests,
      child: ListView.builder(
        padding: const EdgeInsets.all(16),
        itemCount: requests.length,
        itemBuilder: (context, index) => _buildRequestCard(requests[index]),
      ),
    );
  }

  Widget _buildMessage({
    required IconData icon,
    required String message,
    required String actionLabel,
    required VoidCallback onAction,
  }) {
    return Center(
      child: Padding(
        padding: const EdgeInsets.all(24),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            Icon(icon, size: 44, color: Colors.redAccent),
            const SizedBox(height: 12),
            Text(message, textAlign: TextAlign.center),
            const SizedBox(height: 12),
            OutlinedButton.icon(
              onPressed: onAction,
              icon: const Icon(Icons.refresh),
              label: Text(actionLabel),
            ),
          ],
        ),
      ),
    );
  }

  Widget _buildRequestCard(Map<String, dynamic> request) {
    final id = _requestId(request);
    final status = _requestStatus(request);
    final normalizedStatus = status.trim().toLowerCase();
    final waiting =
        normalizedStatus == 'chờ tiếp nhận' ||
        normalizedStatus == 'cho tiep nhan';
    final completed =
        normalizedStatus == 'đã hoàn thành' ||
        normalizedStatus == 'da hoan thanh';
    final statusColor = _statusColor(normalizedStatus);
    final customerId = _value(request, 'maKhach', 'MaKhach') ?? '—';
    final roomName =
        _value(request, 'tenPhong', 'TenPhong') ??
        _value(request, 'maPhong', 'MaPhong') ??
        '—';
    final requestDate = _value(request, 'ngayBao', 'NgayBao');
    final completionDate = _value(request, 'ngayHoanThanh', 'NgayHoanThanh');
    final cost = _parseMoney(_value(request, 'chiPhi', 'ChiPhi'));
    final confirmed = _parseBool(
      _value(request, 'khachXacNhan', 'KhachXacNhan'),
    );
    final busy = id != null && _updatingIds.contains(id);

    return Card(
      margin: const EdgeInsets.only(bottom: 12),
      elevation: 0,
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
                    'Yêu cầu #${id ?? '—'}',
                    style: const TextStyle(
                      color: Color(0xFF4F46E5),
                      fontWeight: FontWeight.bold,
                      fontSize: 16,
                    ),
                  ),
                ),
                _statusChip(status, statusColor),
              ],
            ),
            const Divider(height: 24),
            Text(
              (_value(request, 'noiDung', 'NoiDung') ?? 'Không có mô tả')
                  .toString(),
              style: const TextStyle(color: Color(0xFF172554)),
            ),
            const SizedBox(height: 12),
            _infoRow(Icons.person_outline, 'Mã khách', customerId.toString()),
            _infoRow(Icons.meeting_room_outlined, 'Phòng', roomName.toString()),
            _infoRow(
              Icons.calendar_today_outlined,
              'Ngày báo',
              _formatDate(requestDate),
            ),
            if (completionDate != null)
              _infoRow(
                Icons.event_available_outlined,
                'Ngày hoàn thành',
                _formatDate(completionDate),
              ),
            if (cost != null)
              _infoRow(
                Icons.payments_outlined,
                'Chi phí',
                '${_formatMoney(cost)} đ',
                valueColor: Colors.deepOrange,
              ),
            if (confirmed)
              const Padding(
                padding: EdgeInsets.only(top: 8),
                child: Row(
                  children: [
                    Icon(Icons.verified, color: Colors.green, size: 17),
                    SizedBox(width: 6),
                    Text(
                      'Khách đã xác nhận hoàn tất',
                      style: TextStyle(
                        color: Colors.green,
                        fontWeight: FontWeight.w600,
                        fontSize: 12,
                      ),
                    ),
                  ],
                ),
              ),
            if (id != null && !completed) ...[
              const SizedBox(height: 12),
              Row(
                children: [
                  if (waiting)
                    Expanded(
                      child: OutlinedButton.icon(
                        onPressed: busy ? null : () => _acceptRequest(request),
                        icon: busy
                            ? const SizedBox(
                                width: 16,
                                height: 16,
                                child: CircularProgressIndicator(
                                  strokeWidth: 2,
                                ),
                              )
                            : const Icon(Icons.playlist_add_check),
                        label: const Text('Tiếp nhận'),
                      ),
                    ),
                  if (waiting) const SizedBox(width: 8),
                  Expanded(
                    child: ElevatedButton.icon(
                      onPressed: busy ? null : () => _editRequest(request),
                      icon: const Icon(Icons.edit_outlined),
                      label: const Text('Cập nhật xử lý'),
                      style: ElevatedButton.styleFrom(
                        backgroundColor: const Color(0xFF4F46E5),
                        foregroundColor: Colors.white,
                      ),
                    ),
                  ),
                ],
              ),
            ] else if (completed) ...[
              const SizedBox(height: 12),
              SizedBox(
                width: double.infinity,
                child: OutlinedButton.icon(
                  onPressed: busy ? null : () => _editRequest(request),
                  icon: const Icon(Icons.receipt_long_outlined),
                  label: const Text('Xem chi tiết yêu cầu'),
                ),
              ),
            ],
          ],
        ),
      ),
    );
  }

  Widget _statusChip(String status, Color color) {
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
          fontSize: 11,
          fontWeight: FontWeight.bold,
        ),
      ),
    );
  }

  Widget _infoRow(
    IconData icon,
    String label,
    String value, {
    Color valueColor = const Color(0xFF64748B),
  }) {
    return Padding(
      padding: const EdgeInsets.only(bottom: 6),
      child: Row(
        children: [
          Icon(icon, size: 16, color: const Color(0xFF64748B)),
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
}

class _RepairUpdateDialog extends StatefulWidget {
  final Map<String, dynamic> request;

  const _RepairUpdateDialog({required this.request});

  @override
  State<_RepairUpdateDialog> createState() => _RepairUpdateDialogState();
}

class _RepairUpdateDialogState extends State<_RepairUpdateDialog> {
  static const _repairStatuses = [
    'Chờ tiếp nhận',
    'Đang xử lý',
    'Đã hoàn thành',
  ];

  late String _status;
  late final TextEditingController _costController;
  DateTime? _completionDate;
  String? _validationError;

  @override
  void initState() {
    super.initState();
    _status = _requestStatus(widget.request);
    if (!_repairStatuses.contains(_status)) {
      _status = _repairStatuses.first;
    }
    final existingCost =
        _value(widget.request, 'chiPhi', 'ChiPhi')?.toString() ?? '';
    _costController = TextEditingController(text: existingCost);
    _completionDate = _parseDate(
      _value(widget.request, 'ngayHoanThanh', 'NgayHoanThanh'),
    );
  }

  @override
  void dispose() {
    _costController.dispose();
    super.dispose();
  }

  Future<void> _pickCompletionDate() async {
    final today = DateUtils.dateOnly(DateTime.now());
    final initialDate = _completionDate ?? today;
    final date = await showDatePicker(
      context: context,
      initialDate: initialDate,
      firstDate: DateTime(2000),
      lastDate: DateTime(2100, 12, 31),
    );
    if (date != null && mounted) setState(() => _completionDate = date);
  }

  void _submit() {
    final rawCost = _costController.text.trim();
    final cost = rawCost.isEmpty ? 0.0 : double.tryParse(rawCost);
    if (cost == null || cost < 0) {
      setState(() => _validationError = 'Chi phí phải là số không âm.');
      return;
    }
    if (_status == 'Đã hoàn thành' && _completionDate == null) {
      setState(() => _completionDate = DateTime.now());
    }

    final updates = <String, dynamic>{'trangThai': _status, 'chiPhi': cost};
    if (_status == 'Đã hoàn thành') {
      updates['ngayHoanThanh'] = (_completionDate ?? DateTime.now())
          .toIso8601String();
    }
    Navigator.pop(context, updates);
  }

  @override
  Widget build(BuildContext context) {
    final isAlreadyCompleted =
        _requestStatus(widget.request) == 'Đã hoàn thành';
    return AlertDialog(
      title: Text(
        isAlreadyCompleted
            ? 'Thông tin sửa chữa'
            : 'Cập nhật yêu cầu #${_requestId(widget.request) ?? ''}',
      ),
      content: SingleChildScrollView(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            DropdownButtonFormField<String>(
              initialValue: _status,
              decoration: const InputDecoration(
                labelText: 'Trạng thái xử lý',
                border: OutlineInputBorder(),
              ),
              items: _repairStatuses
                  .map(
                    (status) =>
                        DropdownMenuItem(value: status, child: Text(status)),
                  )
                  .toList(),
              onChanged: isAlreadyCompleted
                  ? null
                  : (value) {
                      if (value != null) setState(() => _status = value);
                    },
            ),
            const SizedBox(height: 16),
            TextField(
              controller: _costController,
              enabled: !isAlreadyCompleted,
              keyboardType: const TextInputType.numberWithOptions(
                decimal: true,
              ),
              decoration: const InputDecoration(
                labelText: 'Chi phí (đ)',
                hintText: 'Nhập 0 nếu không phát sinh',
                border: OutlineInputBorder(),
              ),
            ),
            if (_status == 'Đã hoàn thành') ...[
              const SizedBox(height: 12),
              OutlinedButton.icon(
                onPressed: isAlreadyCompleted ? null : _pickCompletionDate,
                icon: const Icon(Icons.calendar_today_outlined),
                label: Text(
                  _completionDate == null
                      ? 'Chọn ngày hoàn thành'
                      : _formatDate(_completionDate),
                ),
              ),
            ],
            if (_validationError != null) ...[
              const SizedBox(height: 8),
              Text(
                _validationError!,
                style: const TextStyle(color: Colors.redAccent),
              ),
            ],
            if (!isAlreadyCompleted) ...[
              const SizedBox(height: 12),
              const Text(
                'Khi chuyển sang “Đã hoàn thành”, API sẽ ghi chi phí phát sinh '
                'vào sổ chi nếu có.',
                style: TextStyle(color: Color(0xFF64748B), fontSize: 12),
              ),
            ],
          ],
        ),
      ),
      actions: [
        TextButton(
          onPressed: () => Navigator.pop(context),
          child: const Text('Đóng'),
        ),
        if (!isAlreadyCompleted)
          FilledButton(onPressed: _submit, child: const Text('Lưu cập nhật')),
      ],
    );
  }
}

dynamic _value(Map<String, dynamic> map, String camelCase, String pascalCase) =>
    map[camelCase] ?? map[pascalCase];

int? _requestId(Map<String, dynamic> request) =>
    int.tryParse((_value(request, 'maYeuCau', 'MaYeuCau') ?? '').toString());

String _requestStatus(Map<String, dynamic> request) =>
    (_value(request, 'trangThai', 'TrangThai') ?? 'Chờ tiếp nhận').toString();

DateTime? _parseDate(dynamic value) =>
    value == null ? null : DateTime.tryParse(value.toString());

String _formatDate(dynamic value) {
  final date = value is DateTime ? value : _parseDate(value);
  if (date == null) return 'Chưa cập nhật';
  return '${date.day.toString().padLeft(2, '0')}/'
      '${date.month.toString().padLeft(2, '0')}/${date.year}';
}

double? _parseMoney(dynamic value) =>
    value is num ? value.toDouble() : double.tryParse(value?.toString() ?? '');

bool _parseBool(dynamic value) =>
    value == true || value?.toString().toLowerCase() == 'true';

String _formatMoney(double value) => value.round().toString().replaceAllMapped(
  RegExp(r'(\d)(?=(\d{3})+(?!\d))'),
  (match) => '${match[1]},',
);

Color _statusColor(String status) {
  if (status.contains('hoàn thành') || status.contains('hoan thanh')) {
    return Colors.green;
  }
  if (status.contains('đang xử lý') || status.contains('dang xu ly')) {
    return Colors.blue;
  }
  if (status.contains('chờ tiếp nhận') || status.contains('cho tiep nhan')) {
    return Colors.deepPurple;
  }
  return Colors.orange;
}
