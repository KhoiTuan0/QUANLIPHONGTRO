import 'package:flutter/material.dart';
import '../services/api_service.dart';

class RegisterScreen extends StatefulWidget {
  const RegisterScreen({super.key});

  @override
  State<RegisterScreen> createState() => _RegisterScreenState();
}

class _RegisterScreenState extends State<RegisterScreen> {
  final _usernameController = TextEditingController();
  final _passwordController = TextEditingController();
  final _fullNameController = TextEditingController();
  final _emailController = TextEditingController();
  final _phoneController = TextEditingController();
  final _cccdController = TextEditingController();
  final _addressController = TextEditingController();

  bool _isLoading = false;
  bool _obscurePassword = true;

  void _handleRegister() async {
    // Kiểm tra các trường bắt buộc theo nghiệp vụ hệ thống
    if (_usernameController.text.trim().isEmpty ||
        _passwordController.text.trim().isEmpty ||
        _fullNameController.text.trim().isEmpty) {
      _showSnackBar('Vui lòng nhập Tên đăng nhập, Mật khẩu và Họ tên!', isError: true);
      return;
    }

    setState(() => _isLoading = true);

    try {
      final data = {
        'tenDangNhap': _usernameController.text.trim(),
        'matKhau': _passwordController.text.trim(), // Backend sẽ xử lý mã hóa mật khẩu
        'hoTen': _fullNameController.text.trim(),
        'email': _emailController.text.trim(),
        'soDienThoai': _phoneController.text.trim(),
        'vaiTro': 'Customer', // Mặc định đăng ký từ app di động là khách hàng/người thuê trọ
        'cccd': _cccdController.text.trim(),
        'diaChi': _addressController.text.trim(),
      };

      bool success = await ApiService.register(data);

      if (success && mounted) {
        _showSnackBar('Đăng ký tài khoản thành công! Vui lòng đăng nhập.', isError: false);
        Navigator.pop(context);
      } else if (mounted) {
        _showSnackBar('Đăng ký thất bại! Tên đăng nhập có thể đã tồn tại.', isError: true);
      }
    } catch (e) {
      if (mounted) {
        _showSnackBar('Lỗi kết nối Server: $e', isError: true);
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
    return Scaffold(
      backgroundColor: const Color(0xFFF5F7FC),
      appBar: AppBar(
        title: const Text('Đăng Ký Tài Khoản', style: TextStyle(fontWeight: FontWeight.bold, fontSize: 18)),
        backgroundColor: Colors.white,
        foregroundColor: const Color(0xFF172554),
        elevation: 0,
        centerTitle: true,
      ),
      body: SafeArea(
        child: SingleChildScrollView(
          padding: const EdgeInsets.all(24.0),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              // Tiêu đề nhỏ mô tả
              const Text(
                'Tạo tài khoản khách thuê mới',
                style: TextStyle(fontSize: 22, fontWeight: FontWeight.bold, color: Color(0xFF172554)),
              ),
              const SizedBox(height: 6),
              const Text(
                'Điền thông tin cá nhân chính xác để chủ trọ dễ dàng quản lý hợp đồng và hóa đơn.',
                style: TextStyle(fontSize: 14, color: Color(0xFF64748B)),
              ),
              const SizedBox(height: 24),

              // Thẻ chứa form đăng ký
              Container(
                padding: const EdgeInsets.all(20),
                decoration: BoxDecoration(
                  color: Colors.white,
                  borderRadius: BorderRadius.circular(20),
                  boxShadow: [
                    BoxShadow(
                      color: Colors.black.withOpacity(0.04),
                      blurRadius: 15,
                      offset: const Offset(0, 5),
                    ),
                  ],
                  border: Border.all(color: const Color(0xFFE2E8F0)),
                ),
                child: Column(
                  children: [
                    _buildTextField(
                      controller: _usernameController,
                      label: 'Tên đăng nhập *',
                      icon: Icons.person_outline,
                    ),
                    const SizedBox(height: 16),
                    _buildTextField(
                      controller: _passwordController,
                      label: 'Mật khẩu *',
                      icon: Icons.lock_outline,
                      obscureText: _obscurePassword,
                      suffixIcon: IconButton(
                        icon: Icon(
                          _obscurePassword ? Icons.visibility_off_outlined : Icons.visibility_outlined,
                          color: const Color(0xFF64748B),
                        ),
                        onPressed: () => setState(() => _obscurePassword = !_obscurePassword),
                      ),
                    ),
                    const SizedBox(height: 16),
                    _buildTextField(
                      controller: _fullNameController,
                      label: 'Họ và tên *',
                      icon: Icons.badge_outlined,
                    ),
                    const SizedBox(height: 16),
                    _buildTextField(
                      controller: _emailController,
                      label: 'Email',
                      icon: Icons.email_outlined,
                      keyboardType: TextInputType.emailAddress,
                    ),
                    const SizedBox(height: 16),
                    _buildTextField(
                      controller: _phoneController,
                      label: 'Số điện thoại',
                      icon: Icons.phone_outlined,
                      keyboardType: TextInputType.phone,
                    ),
                    const SizedBox(height: 16),
                    _buildTextField(
                      controller: _cccdController,
                      label: 'Số CCCD',
                      icon: Icons.card_membership_outlined,
                      keyboardType: TextInputType.number,
                    ),
                    const SizedBox(height: 16),
                    _buildTextField(
                      controller: _addressController,
                      label: 'Địa chỉ thường trú',
                      icon: Icons.home_outlined,
                    ),
                  ],
                ),
              ),
              const SizedBox(height: 28),

              // Nút Đăng ký
              SizedBox(
                height: 50,
                child: ElevatedButton(
                  style: ElevatedButton.styleFrom(
                    backgroundColor: const Color(0xFF4F46E5),
                    foregroundColor: Colors.white,
                    shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(12)),
                    elevation: 0,
                  ),
                  onPressed: _isLoading ? null : _handleRegister,
                  child: _isLoading
                      ? const SizedBox(
                          width: 24,
                          height: 24,
                          child: CircularProgressIndicator(color: Colors.white, strokeWidth: 2),
                        )
                      : const Text('ĐĂNG KÝ NGAY', style: TextStyle(fontSize: 16, fontWeight: FontWeight.bold)),
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }

  // Widget hỗ trợ tạo TextField đồng bộ style
  Widget _buildTextField({
    required TextEditingController controller,
    required String label,
    required IconData icon,
    bool obscureText = false,
    TextInputType keyboardType = TextInputType.text,
    Widget? suffixIcon,
  }) {
    return TextField(
      controller: controller,
      obscureText: obscureText,
      keyboardType: keyboardType,
      decoration: InputDecoration(
        labelText: label,
        labelStyle: const TextStyle(color: Color(0xFF64748B), fontSize: 14),
        prefixIcon: Icon(icon, color: const Color(0xFF4F46E5)),
        suffixIcon: suffixIcon,
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
        contentPadding: const EdgeInsets.symmetric(horizontal: 16, vertical: 14),
      ),
    );
  }
}