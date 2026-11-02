import 'dart:async';
import 'package:flutter/material.dart';
import 'login_screen.dart';
import '../services/api_service.dart';

import 'customer_contract_screen.dart';
import 'customer_invoice_screen.dart';
import 'repair_request_screen.dart';
import 'customer_chat_screen.dart';
import 'customer_notification_screen.dart';
import 'checkout_request_screen.dart';
import 'customer_profile_screen.dart';
import 'customer_booking_list_screen.dart';
import 'customer_contract_extension_screen.dart'; // MỚI
import '../services/push_notification_service.dart';

class CustomerHomeScreen extends StatefulWidget {
  final int maKhach;
  const CustomerHomeScreen({super.key, required this.maKhach});

  @override
  State<CustomerHomeScreen> createState() => _CustomerHomeScreenState();
}

class _CustomerHomeScreenState extends State<CustomerHomeScreen> {
  int _currentIndex = 0;
  late final List<Widget> _customerScreens;
  int _unreadHint = 0;

  void _onNewMessage() {
    if (!mounted) return;
    setState(() => _unreadHint++);
  }

  void _onOpenMessages() {
    if (!mounted) return;
    setState(() => _unreadHint = 0);
    Navigator.push(context, MaterialPageRoute(
      builder: (_) => CustomerChatScreen(maKhach: widget.maKhach),
    ));
  }

  void _openMyBookings() {
    Navigator.push(context, MaterialPageRoute(
      builder: (_) => CustomerBookingListScreen(maKhach: widget.maKhach),
    ));
  }

  // MỚI: mở trang gửi yêu cầu gia hạn, không thay đổi các tab hiện có.
  void _openContractExtension() {
    Navigator.push(context, MaterialPageRoute(
      builder: (_) => CustomerContractExtensionScreen(maKhach: widget.maKhach),
    ));
  }

  @override
  void initState() {
    super.initState();
    PushNotificationService.messageVersion.addListener(_onNewMessage);
    PushNotificationService.openMessagesVersion.addListener(_onOpenMessages);
    unawaited(PushNotificationService.registerCustomer(widget.maKhach));
    _customerScreens = [
      CustomerContractScreen(maKhach: widget.maKhach),
      CustomerInvoiceScreen(maKhach: widget.maKhach),
      RepairRequestScreen(maKhach: widget.maKhach),
      CustomerNotificationScreen(maKhach: widget.maKhach),
      CheckoutRequestScreen(maKhach: widget.maKhach),
    ];
  }

  @override
  void dispose() {
    PushNotificationService.messageVersion.removeListener(_onNewMessage);
    PushNotificationService.openMessagesVersion.removeListener(_onOpenMessages);
    super.dispose();
  }

  Future<void> _logout() async {
    FocusManager.instance.primaryFocus?.unfocus();
    final shouldLogout = await showDialog<bool>(
      context: context,
      builder: (context) => AlertDialog(
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(16)),
        title: const Text('Xác nhận đăng xuất', style: TextStyle(fontWeight: FontWeight.bold)),
        content: const Text('Bạn có chắc chắn muốn đăng xuất khỏi khu vực khách hàng?'),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(context, false),
            child: const Text('Hủy', style: TextStyle(color: Color(0xFF64748B))),
          ),
          ElevatedButton(
            style: ElevatedButton.styleFrom(
              backgroundColor: Colors.redAccent,
              foregroundColor: Colors.white,
              elevation: 0,
              shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(8)),
            ),
            onPressed: () => Navigator.pop(context, true),
            child: const Text('Đăng xuất'),
          ),
        ],
      ),
    );
    if (shouldLogout != true || !mounted) return;
    await PushNotificationService.clearCustomer();
    if (!mounted) return;
    unawaited(ApiService.logout());
    Navigator.pushAndRemoveUntil(
      context,
      MaterialPageRoute(builder: (context) => const LoginScreen()),
      (route) => false,
    );
  }

  static const _purple = Color(0xFF5848EA);
  static const _ink = Color(0xFF1B2552);

  Widget _quickAction({
    required IconData icon,
    required String label,
    required VoidCallback onTap,
    Color color = _purple,
  }) {
    return Expanded(
      child: InkWell(
        onTap: onTap,
        borderRadius: BorderRadius.circular(16),
        child: Container(
          padding: const EdgeInsets.symmetric(vertical: 14, horizontal: 6),
          decoration: BoxDecoration(
            color: Colors.white,
            borderRadius: BorderRadius.circular(16),
            border: Border.all(color: const Color(0xFFE8E8F6)),
          ),
          child: Column(
            children: [
              Container(
                width: 40,
                height: 40,
                decoration: BoxDecoration(
                  color: color.withValues(alpha: 0.09),
                  borderRadius: BorderRadius.circular(12),
                ),
                child: Icon(icon, color: color, size: 21),
              ),
              const SizedBox(height: 8),
              Text(
                label,
                maxLines: 1,
                overflow: TextOverflow.ellipsis,
                style: const TextStyle(fontSize: 11, fontWeight: FontWeight.w700, color: _ink),
              ),
            ],
          ),
        ),
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    const labels = [
      'Hợp đồng của tôi', 'Thanh toán', 'Yêu cầu sửa chữa',
      'Thông báo từ chủ trọ', 'Trả phòng',
    ];
    return Scaffold(
      backgroundColor: const Color(0xFFF5F6FC),
      body: SafeArea(
        child: Column(
          children: [
            Container(
              width: double.infinity,
              decoration: const BoxDecoration(
                gradient: LinearGradient(
                  begin: Alignment.topLeft,
                  end: Alignment.bottomRight,
                  colors: [Color(0xFF6557F5), Color(0xFF4338CA)],
                ),
                borderRadius: BorderRadius.vertical(bottom: Radius.circular(28)),
              ),
              child: Padding(
                padding: const EdgeInsets.fromLTRB(18, 16, 12, 20),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Row(
                      children: [
                        Container(
                          padding: const EdgeInsets.all(10),
                          decoration: BoxDecoration(
                            color: Colors.white.withValues(alpha: 0.18),
                            borderRadius: BorderRadius.circular(14),
                          ),
                          child: const Icon(Icons.home_work_rounded, color: Colors.white, size: 24),
                        ),
                        const SizedBox(width: 12),
                        Expanded(
                          child: Column(
                            crossAxisAlignment: CrossAxisAlignment.start,
                            children: [
                              Text('KHÔNG GIAN CỦA BẠN',
                                style: TextStyle(color: Colors.white.withValues(alpha: 0.76),
                                  fontSize: 10, letterSpacing: 1.2, fontWeight: FontWeight.w700)),
                              const SizedBox(height: 3),
                              Text('Xin chào, khách hàng #${widget.maKhach}',
                                maxLines: 1, overflow: TextOverflow.ellipsis,
                                style: const TextStyle(color: Colors.white,
                                  fontSize: 17, fontWeight: FontWeight.w800)),
                            ],
                          ),
                        ),
                        IconButton(
                          tooltip: 'Thông tin cá nhân',
                          onPressed: () => Navigator.push(context, MaterialPageRoute(
                            builder: (_) => CustomerProfileScreen(maKhach: widget.maKhach))),
                          icon: const Icon(Icons.account_circle_outlined, color: Colors.white),
                        ),
                        IconButton(
                          tooltip: 'Đăng xuất',
                          onPressed: _logout,
                          icon: const Icon(Icons.logout_rounded, color: Colors.white),
                        ),
                      ],
                    ),
                    const SizedBox(height: 18),
                    const Text('Quản lý phòng trọ thật dễ dàng ✨',
                      style: TextStyle(fontSize: 14, fontWeight: FontWeight.w700, color: Colors.white)),
                    const SizedBox(height: 4),
                    Text('Theo dõi hợp đồng, hóa đơn và yêu cầu của bạn.',
                      style: TextStyle(color: Colors.white.withValues(alpha: 0.82), fontSize: 12)),
                  ],
                ),
              ),
            ),
            Padding(
              padding: const EdgeInsets.fromLTRB(16, 14, 16, 12),
              child: Row(
                children: [
                  _quickAction(icon: Icons.calendar_month_rounded, label: 'Đặt phòng', onTap: _openMyBookings),
                  const SizedBox(width: 8),
                  _quickAction(icon: Icons.event_repeat_rounded, label: 'Gia hạn', onTap: _openContractExtension), // MỚI
                  const SizedBox(width: 8),
                  _quickAction(icon: Icons.receipt_long_rounded, label: 'Hóa đơn',
                    onTap: () => setState(() => _currentIndex = 1)),
                  const SizedBox(width: 8),
                  _quickAction(icon: Icons.forum_rounded, label: 'Tin nhắn', onTap: _onOpenMessages),
                ],
              ),
            ),
            if (_unreadHint > 0)
              Padding(
                padding: const EdgeInsets.fromLTRB(16, 0, 16, 10),
                child: Material(
                  color: const Color(0xFFE8F7ED),
                  borderRadius: BorderRadius.circular(14),
                  child: InkWell(
                    borderRadius: BorderRadius.circular(14),
                    onTap: _onOpenMessages,
                    child: Padding(
                      padding: const EdgeInsets.all(12),
                      child: Row(children: [
                        const Icon(Icons.mark_chat_unread_rounded, color: Color(0xFF16803C)),
                        const SizedBox(width: 10),
                        Expanded(child: Text('Bạn có $_unreadHint tin nhắn mới. Nhấn để xem.',
                          style: const TextStyle(color: Color(0xFF166534), fontWeight: FontWeight.w600, fontSize: 12))),
                        const Icon(Icons.chevron_right, color: Color(0xFF16803C)),
                      ]),
                    ),
                  ),
                ),
              ),
            Padding(
              padding: const EdgeInsets.fromLTRB(18, 2, 18, 10),
              child: Row(children: [
                Container(width: 4, height: 22,
                  decoration: BoxDecoration(color: _purple, borderRadius: BorderRadius.circular(5))),
                const SizedBox(width: 9),
                Expanded(child: Text(labels[_currentIndex],
                  style: const TextStyle(fontSize: 17, fontWeight: FontWeight.w800, color: _ink))),
              ]),
            ),
            Expanded(child: IndexedStack(index: _currentIndex, children: _customerScreens)),
          ],
        ),
      ),
      bottomNavigationBar: NavigationBar(
        height: 70,
        backgroundColor: Colors.white,
        indicatorColor: const Color(0xFFECEAFF),
        labelBehavior: NavigationDestinationLabelBehavior.alwaysShow,
        selectedIndex: _currentIndex,
        onDestinationSelected: (index) => setState(() => _currentIndex = index),
        destinations: [
          const NavigationDestination(icon: Icon(Icons.description_outlined),
            selectedIcon: Icon(Icons.description_rounded, color: _purple), label: 'Hợp đồng'),
          const NavigationDestination(icon: Icon(Icons.receipt_long_outlined),
            selectedIcon: Icon(Icons.receipt_long_rounded, color: _purple), label: 'Thanh toán'),
          const NavigationDestination(icon: Icon(Icons.handyman_outlined),
            selectedIcon: Icon(Icons.handyman_rounded, color: _purple), label: 'Sửa chữa'),
          NavigationDestination(
            icon: Badge(isLabelVisible: _unreadHint > 0,
              label: Text('$_unreadHint'), child: const Icon(Icons.notifications_outlined)),
            selectedIcon: const Icon(Icons.notifications_rounded, color: _purple), label: 'Thông báo'),
          const NavigationDestination(icon: Icon(Icons.meeting_room_outlined),
            selectedIcon: Icon(Icons.meeting_room_rounded, color: _purple), label: 'Trả phòng'),
        ],
      ),
    );
  }
}
