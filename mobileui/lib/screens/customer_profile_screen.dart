import 'package:flutter/material.dart';

import '../services/api_service.dart';

class CustomerProfileScreen extends StatefulWidget {
  final int maKhach;

  const CustomerProfileScreen({super.key, required this.maKhach});

  @override
  State<CustomerProfileScreen> createState() => _CustomerProfileScreenState();
}

class _CustomerProfileScreenState extends State<CustomerProfileScreen> {
  final _formKey = GlobalKey<FormState>();
  final _nameController = TextEditingController();
  final _emailController = TextEditingController();
  final _phoneController = TextEditingController();
  final _cccdController = TextEditingController();
  final _addressController = TextEditingController();

  bool _loading = true;
  bool _saving = false;
  String? _loadError;
  DateTime? _birthDate;
  String? _gender;

  @override
  void initState() {
    super.initState();
    _loadProfile();
  }

  @override
  void dispose() {
    _nameController.dispose();
    _emailController.dispose();
    _phoneController.dispose();
    _cccdController.dispose();
    _addressController.dispose();
    super.dispose();
  }

  Future<void> _loadProfile() async {
    setState(() {
      _loading = true;
      _loadError = null;
    });
    try {
      final profile = await ApiService.getCustomerProfile(widget.maKhach);
      if (!mounted) return;
      setState(() {
        _nameController.text =
            _profileValue(profile, 'hoTen', 'HoTen')?.toString() ?? '';
        _emailController.text =
            _profileValue(profile, 'email', 'Email')?.toString() ?? '';
        _phoneController.text =
            _profileValue(profile, 'soDienThoai', 'SoDienThoai')?.toString() ??
            '';
        _cccdController.text =
            _profileValue(profile, 'cccd', 'Cccd')?.toString() ?? '';
        _addressController.text =
            _profileValue(profile, 'diaChi', 'DiaChi')?.toString() ?? '';
        _birthDate = _parseDate(_profileValue(profile, 'ngaySinh', 'NgaySinh'));
        _gender = _profileValue(
          profile,
          'gioiTinh',
          'GioiTinh',
        )?.toString().trim();
        if (_gender?.isEmpty == true) _gender = null;
        _loading = false;
      });
    } catch (error) {
      if (!mounted) return;
      setState(() {
        _loadError = error.toString();
        _loading = false;
      });
    }
  }

  dynamic _profileValue(
    Map<String, dynamic> profile,
    String camel,
    String pascal,
  ) => profile[camel] ?? profile[pascal];

  DateTime? _parseDate(dynamic value) {
    if (value == null) return null;
    final date = DateTime.tryParse(value.toString());
    return date == null ? null : DateUtils.dateOnly(date);
  }

  Future<void> _selectBirthDate() async {
    final today = DateUtils.dateOnly(DateTime.now());
    final picked = await showDatePicker(
      context: context,
      initialDate:
          _birthDate ?? DateTime(today.year - 20, today.month, today.day),
      firstDate: DateTime(1900),
      lastDate: today,
      helpText: 'Chọn ngày sinh',
    );
    if (picked != null && mounted) {
      setState(() => _birthDate = DateUtils.dateOnly(picked));
    }
  }

  Future<void> _saveProfile() async {
    FocusManager.instance.primaryFocus?.unfocus();
    if (!_formKey.currentState!.validate()) return;

    setState(() => _saving = true);
    try {
      final result = await ApiService.updateCustomerProfile(
        customerId: widget.maKhach,
        profile: {
          'hoTen': _nameController.text.trim(),
          'email': _emailController.text.trim(),
          'soDienThoai': _phoneController.text.trim(),
          'cccd': _cccdController.text.trim(),
          'diaChi': _addressController.text.trim(),
          'ngaySinh': _birthDate == null ? null : _dateIso(_birthDate!),
          'gioiTinh': _gender ?? '',
        },
      );
      if (!mounted) return;
      setState(() => _saving = false);
      _showMessage(
        result['message']?.toString() ?? 'Cập nhật hồ sơ thất bại.',
        isError: result['success'] != true,
      );
      if (result['success'] == true) await _loadProfile();
    } catch (error) {
      if (!mounted) return;
      setState(() => _saving = false);
      _showMessage('Không thể cập nhật hồ sơ: $error', isError: true);
    }
  }

  Future<void> _showChangePasswordDialog() async {
    await showDialog<void>(
      context: context,
      builder: (context) => _ChangeCustomerPasswordDialog(
        maKhach: widget.maKhach,
        onMessage: (message, isError) {
          if (mounted) _showMessage(message, isError: isError);
        },
      ),
    );
  }

  String _dateIso(DateTime date) =>
      '${date.year.toString().padLeft(4, '0')}-'
      '${date.month.toString().padLeft(2, '0')}-'
      '${date.day.toString().padLeft(2, '0')}';

  String _dateLabel(DateTime? date) {
    if (date == null) return 'Chưa cập nhật';
    return '${date.day.toString().padLeft(2, '0')}/'
        '${date.month.toString().padLeft(2, '0')}/${date.year}';
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
    return Scaffold(
      backgroundColor: const Color(0xFFF5F7FC),
      appBar: AppBar(
        title: const Text(
          'Thông tin cá nhân',
          style: TextStyle(
            fontWeight: FontWeight.bold,
            color: Color(0xFF172554),
          ),
        ),
        backgroundColor: Colors.white,
        foregroundColor: const Color(0xFF172554),
        elevation: 0,
        actions: [
          IconButton(
            tooltip: 'Tải lại hồ sơ',
            onPressed: _loading ? null : _loadProfile,
            icon: const Icon(Icons.refresh),
          ),
        ],
      ),
      body: _loading
          ? const Center(child: CircularProgressIndicator())
          : _loadError != null
          ? _errorView()
          : _buildProfileForm(),
    );
  }

  Widget _errorView() => Center(
    child: Padding(
      padding: const EdgeInsets.all(24),
      child: Column(
        mainAxisSize: MainAxisSize.min,
        children: [
          const Icon(Icons.error_outline, color: Colors.redAccent, size: 44),
          const SizedBox(height: 12),
          Text(
            'Không tải được hồ sơ khách thuê.\n$_loadError',
            textAlign: TextAlign.center,
          ),
          const SizedBox(height: 12),
          OutlinedButton.icon(
            onPressed: _loadProfile,
            icon: const Icon(Icons.refresh),
            label: const Text('Thử lại'),
          ),
        ],
      ),
    ),
  );

  Widget _buildProfileForm() => Form(
    key: _formKey,
    child: ListView(
      padding: const EdgeInsets.all(16),
      children: [
        Container(
          padding: const EdgeInsets.all(18),
          decoration: BoxDecoration(
            color: Colors.white,
            borderRadius: BorderRadius.circular(18),
            border: Border.all(color: const Color(0xFFE2E8F0)),
          ),
          child: Row(
            children: [
              const CircleAvatar(
                radius: 28,
                backgroundColor: Color(0xFFEFF6FF),
                child: Icon(
                  Icons.person_outline,
                  color: Color(0xFF4F46E5),
                  size: 30,
                ),
              ),
              const SizedBox(width: 14),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      _nameController.text.trim().isEmpty
                          ? 'Khách thuê'
                          : _nameController.text.trim(),
                      style: const TextStyle(
                        color: Color(0xFF172554),
                        fontWeight: FontWeight.bold,
                        fontSize: 17,
                      ),
                    ),
                    const SizedBox(height: 4),
                    Text(
                      'Mã khách thuê #${widget.maKhach}',
                      style: const TextStyle(color: Color(0xFF64748B)),
                    ),
                  ],
                ),
              ),
            ],
          ),
        ),
        const SizedBox(height: 14),
        _section(
          title: 'Thông tin hồ sơ',
          icon: Icons.badge_outlined,
          children: [
            _textField(
              controller: _nameController,
              label: 'Họ và tên *',
              icon: Icons.person_outline,
              validator: (value) => value == null || value.trim().isEmpty
                  ? 'Vui lòng nhập họ và tên.'
                  : null,
            ),
            const SizedBox(height: 12),
            _textField(
              controller: _emailController,
              label: 'Email',
              icon: Icons.email_outlined,
              keyboardType: TextInputType.emailAddress,
              validator: (value) {
                final email = value?.trim() ?? '';
                if (email.isNotEmpty &&
                    !RegExp(r'^[^@\s]+@[^@\s]+\.[^@\s]+$').hasMatch(email)) {
                  return 'Email chưa đúng định dạng.';
                }
                return null;
              },
            ),
            const Padding(
              padding: EdgeInsets.only(top: 6),
              child: Align(
                alignment: Alignment.centerLeft,
                child: Text(
                  'Lưu ý: đổi email hồ sơ không tự đổi tên đăng nhập hiện tại.',
                  style: TextStyle(color: Color(0xFF64748B), fontSize: 12),
                ),
              ),
            ),
            const SizedBox(height: 12),
            _textField(
              controller: _phoneController,
              label: 'Số điện thoại',
              icon: Icons.phone_outlined,
              keyboardType: TextInputType.phone,
              validator: (value) {
                final phone = value?.trim() ?? '';
                if (phone.isNotEmpty &&
                    !RegExp(r'^[0-9+()\-\s]{8,16}$').hasMatch(phone)) {
                  return 'Số điện thoại chưa hợp lệ.';
                }
                return null;
              },
            ),
            const SizedBox(height: 12),
            _textField(
              controller: _cccdController,
              label: 'CCCD',
              icon: Icons.credit_card_outlined,
            ),
            const SizedBox(height: 12),
            DropdownButtonFormField<String>(
              initialValue: _gender,
              decoration: const InputDecoration(
                labelText: 'Giới tính',
                prefixIcon: Icon(Icons.wc_outlined),
                border: OutlineInputBorder(),
              ),
              items: const [
                DropdownMenuItem(value: 'Nam', child: Text('Nam')),
                DropdownMenuItem(value: 'Nữ', child: Text('Nữ')),
                DropdownMenuItem(value: 'Khác', child: Text('Khác')),
              ],
              onChanged: (value) => setState(() => _gender = value),
            ),
            const SizedBox(height: 12),
            OutlinedButton.icon(
              onPressed: _selectBirthDate,
              icon: const Icon(Icons.calendar_month_outlined),
              label: Align(
                alignment: Alignment.centerLeft,
                child: Text('Ngày sinh: ${_dateLabel(_birthDate)}'),
              ),
              style: OutlinedButton.styleFrom(
                padding: const EdgeInsets.symmetric(
                  horizontal: 12,
                  vertical: 16,
                ),
                alignment: Alignment.centerLeft,
              ),
            ),
            const SizedBox(height: 12),
            _textField(
              controller: _addressController,
              label: 'Địa chỉ',
              icon: Icons.home_outlined,
              maxLines: 2,
            ),
          ],
        ),
        const SizedBox(height: 14),
        OutlinedButton.icon(
          onPressed: _showChangePasswordDialog,
          icon: const Icon(Icons.lock_reset_outlined),
          label: const Text('Đổi mật khẩu'),
          style: OutlinedButton.styleFrom(
            minimumSize: const Size.fromHeight(50),
            foregroundColor: const Color(0xFF4F46E5),
          ),
        ),
        const SizedBox(height: 10),
        FilledButton.icon(
          onPressed: _saving ? null : _saveProfile,
          icon: _saving
              ? const SizedBox(
                  width: 18,
                  height: 18,
                  child: CircularProgressIndicator(strokeWidth: 2),
                )
              : const Icon(Icons.save_outlined),
          label: Text(_saving ? 'Đang lưu...' : 'Lưu thông tin cá nhân'),
          style: FilledButton.styleFrom(minimumSize: const Size.fromHeight(50)),
        ),
      ],
    ),
  );

  Widget _section({
    required String title,
    required IconData icon,
    required List<Widget> children,
  }) => Container(
    padding: const EdgeInsets.all(16),
    decoration: BoxDecoration(
      color: Colors.white,
      borderRadius: BorderRadius.circular(18),
      border: Border.all(color: const Color(0xFFE2E8F0)),
    ),
    child: Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Row(
          children: [
            Icon(icon, color: const Color(0xFF4F46E5)),
            const SizedBox(width: 8),
            Text(
              title,
              style: const TextStyle(
                color: Color(0xFF172554),
                fontWeight: FontWeight.bold,
              ),
            ),
          ],
        ),
        const SizedBox(height: 16),
        ...children,
      ],
    ),
  );

  Widget _textField({
    required TextEditingController controller,
    required String label,
    required IconData icon,
    TextInputType? keyboardType,
    String? Function(String?)? validator,
    int maxLines = 1,
  }) => TextFormField(
    controller: controller,
    keyboardType: keyboardType,
    validator: validator,
    maxLines: maxLines,
    decoration: InputDecoration(
      labelText: label,
      prefixIcon: Icon(icon),
      border: const OutlineInputBorder(),
    ),
  );
}

class _ChangeCustomerPasswordDialog extends StatefulWidget {
  final int maKhach;
  final void Function(String message, bool isError) onMessage;

  const _ChangeCustomerPasswordDialog({
    required this.maKhach,
    required this.onMessage,
  });

  @override
  State<_ChangeCustomerPasswordDialog> createState() =>
      _ChangeCustomerPasswordDialogState();
}

class _ChangeCustomerPasswordDialogState
    extends State<_ChangeCustomerPasswordDialog> {
  final _formKey = GlobalKey<FormState>();
  final _currentController = TextEditingController();
  final _newController = TextEditingController();
  final _confirmController = TextEditingController();
  bool _saving = false;
  bool _obscureCurrent = true;
  bool _obscureNew = true;
  bool _obscureConfirm = true;

  @override
  void dispose() {
    _currentController.dispose();
    _newController.dispose();
    _confirmController.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    if (!_formKey.currentState!.validate()) return;
    setState(() => _saving = true);
    try {
      final result = await ApiService.changeCustomerPassword(
        customerId: widget.maKhach,
        currentPassword: _currentController.text,
        newPassword: _newController.text,
      );
      if (!mounted) return;
      setState(() => _saving = false);
      if (result['success'] == true) {
        Navigator.pop(context);
        widget.onMessage(
          result['message']?.toString() ?? 'Đổi mật khẩu thành công.',
          false,
        );
      } else {
        widget.onMessage(
          result['message']?.toString() ?? 'Đổi mật khẩu thất bại.',
          true,
        );
      }
    } catch (error) {
      if (!mounted) return;
      setState(() => _saving = false);
      widget.onMessage('Không thể đổi mật khẩu: $error', true);
    }
  }

  @override
  Widget build(BuildContext context) => AlertDialog(
    title: const Text('Đổi mật khẩu'),
    content: Form(
      key: _formKey,
      child: SingleChildScrollView(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            _passwordField(
              controller: _currentController,
              label: 'Mật khẩu hiện tại',
              obscure: _obscureCurrent,
              onToggle: () =>
                  setState(() => _obscureCurrent = !_obscureCurrent),
              validator: (value) => value == null || value.isEmpty
                  ? 'Vui lòng nhập mật khẩu hiện tại.'
                  : null,
            ),
            const SizedBox(height: 12),
            _passwordField(
              controller: _newController,
              label: 'Mật khẩu mới',
              obscure: _obscureNew,
              onToggle: () => setState(() => _obscureNew = !_obscureNew),
              validator: (value) {
                if (value == null || value.length < 6) {
                  return 'Mật khẩu mới tối thiểu 6 ký tự.';
                }
                if (value == _currentController.text) {
                  return 'Mật khẩu mới phải khác mật khẩu hiện tại.';
                }
                return null;
              },
            ),
            const SizedBox(height: 12),
            _passwordField(
              controller: _confirmController,
              label: 'Nhập lại mật khẩu mới',
              obscure: _obscureConfirm,
              onToggle: () =>
                  setState(() => _obscureConfirm = !_obscureConfirm),
              validator: (value) => value != _newController.text
                  ? 'Mật khẩu nhập lại không khớp.'
                  : null,
            ),
          ],
        ),
      ),
    ),
    actions: [
      TextButton(
        onPressed: _saving ? null : () => Navigator.pop(context),
        child: const Text('Hủy'),
      ),
      FilledButton(
        onPressed: _saving ? null : _submit,
        child: _saving
            ? const SizedBox(
                width: 18,
                height: 18,
                child: CircularProgressIndicator(strokeWidth: 2),
              )
            : const Text('Cập nhật mật khẩu'),
      ),
    ],
  );

  Widget _passwordField({
    required TextEditingController controller,
    required String label,
    required bool obscure,
    required VoidCallback onToggle,
    required String? Function(String?) validator,
  }) => TextFormField(
    controller: controller,
    obscureText: obscure,
    validator: validator,
    decoration: InputDecoration(
      labelText: label,
      border: const OutlineInputBorder(),
      suffixIcon: IconButton(
        onPressed: onToggle,
        icon: Icon(
          obscure ? Icons.visibility_outlined : Icons.visibility_off_outlined,
        ),
      ),
    ),
  );
}
