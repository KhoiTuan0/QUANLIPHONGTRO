import 'dart:async';
import 'package:flutter/material.dart';
import '../services/api_service.dart';
import 'login_screen.dart';
import 'room_list_screen.dart';
import 'contract_list_screen.dart';
import 'booking_list_screen.dart';
import 'customer_list_screen.dart';
import 'room_type_screen.dart';
import 'vi_pham_screen.dart';
import 'thong_ke_screen.dart';
import 'repair_management_screen.dart';
import 'rent_billing_screen.dart';
import 'cancel_booking_management_screen.dart';
import 'checkout_request_management_screen.dart';
import 'contract_extension_management_screen.dart'; // MỚI

class MainScreen extends StatefulWidget {
  const MainScreen({super.key});

  @override
  State<MainScreen> createState() => _MainScreenState();
}

class _MainScreenState extends State<MainScreen> {
  int _currentIndex = 0;

  final List<Widget> _screens = const [
    RoomListScreen(),
    ContractListScreen(),
    BookingListScreen(),
    CustomerListScreen(),
  ];

  Future<void> _logout() async {
    FocusManager.instance.primaryFocus?.unfocus();
    final shouldLogout = await showDialog<bool>(
      context: context,
      builder: (context) => AlertDialog(
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(16)),
        title: const Text('Xác nhận đăng xuất', style: TextStyle(fontWeight: FontWeight.bold)),
        content: const Text('Bạn có chắc chắn muốn đăng xuất khỏi hệ thống quản lý phòng trọ?'),
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
    unawaited(ApiService.logout());
    Navigator.pushAndRemoveUntil(
      context,
      MaterialPageRoute(builder: (context) => const LoginScreen()),
      (route) => false,
    );
  }

  void _openTool(String value) {
    final Widget destination = switch (value) {
      'violations' => const ViPhamScreen(),
      'reports' => const ThongKeScreen(),
      'repairs' => const RepairManagementScreen(),
      'rent-billing' => const RentBillingScreen(),
      'cancel-requests' => const CancelBookingManagementScreen(),
      'checkout-requests' => CheckoutRequestManagementScreen(),
      'extension-requests' => const ContractExtensionManagementScreen(), // MỚI
      _ => const RoomTypeScreen(),
    };
    Navigator.push(context, MaterialPageRoute(builder: (_) => destination));
  }

  @override
  Widget build(BuildContext context) {
    const primary = Color(0xFF4F46E5);
    const ink = Color(0xFF172554);
    return Scaffold(
      backgroundColor: const Color(0xFFF5F7FC),
      appBar: AppBar(
        toolbarHeight: 70,
        backgroundColor: Colors.white,
        surfaceTintColor: Colors.transparent,
        elevation: 0,
        titleSpacing: 8,
        leadingWidth: 60,
        leading: Padding(
          padding: const EdgeInsets.only(left: 12, top: 11, bottom: 11),
          child: PopupMenuButton<String>(
            tooltip: 'Mở danh mục quản lý',
            offset: const Offset(0, 50),
            shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(20)),
            color: Colors.white,
            onSelected: _openTool,
            itemBuilder: (_) => const [
              PopupMenuItem(value: 'room-types', child: _MenuEntry(Icons.category_rounded, 'Loại phòng', Color(0xFF6366F1))),
              PopupMenuItem(value: 'rent-billing', child: _MenuEntry(Icons.receipt_long_rounded, 'Lập tiền thuê', Color(0xFF0D9488))),
              PopupMenuItem(value: 'repairs', child: _MenuEntry(Icons.handyman_rounded, 'Yêu cầu sửa chữa', Color(0xFFF59E0B))),
              PopupMenuItem(value: 'cancel-requests', child: _MenuEntry(Icons.event_busy_rounded, 'Yêu cầu hủy đặt phòng', Color(0xFFDC2626))),
              PopupMenuItem(value: 'checkout-requests', child: _MenuEntry(Icons.meeting_room_outlined, 'Yêu cầu trả phòng', Color(0xFFEA580C))),
              PopupMenuItem(value: 'extension-requests', child: _MenuEntry(Icons.event_repeat_rounded, 'Yêu cầu gia hạn hợp đồng', Color(0xFF4F46E5))), // MỚI
              PopupMenuItem(value: 'violations', child: _MenuEntry(Icons.gpp_maybe_rounded, 'Vi phạm nội quy', Color(0xFFEF4444))),
              PopupMenuItem(value: 'reports', child: _MenuEntry(Icons.insights_rounded, 'Báo cáo thống kê', Color(0xFF8B5CF6))),
            ],
            child: Container(
              decoration: BoxDecoration(
                color: const Color(0xFFEEF2FF),
                borderRadius: BorderRadius.circular(14),
                border: Border.all(color: const Color(0xFFC7D2FE)),
              ),
              child: const Icon(Icons.grid_view_rounded, color: primary),
            ),
          ),
        ),
        title: const Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text('Nhà Trọ Của Tôi 🏡', style: TextStyle(fontSize: 18, fontWeight: FontWeight.w800, color: ink)),
            Text('Quản lý dễ dàng mỗi ngày', style: TextStyle(fontSize: 11, color: Color(0xFF64748B))),
          ],
        ),
        actions: [
          IconButton(
            tooltip: 'Đăng xuất',
            onPressed: _logout,
            icon: const Icon(Icons.logout_rounded, color: Color(0xFFEF4444)),
          ),
          const SizedBox(width: 6),
        ],
        bottom: PreferredSize(
          preferredSize: const Size.fromHeight(1),
          child: Container(height: 1, color: const Color(0xFFE8EDFF)),
        ),
      ),
      body: IndexedStack(index: _currentIndex, children: _screens),
      bottomNavigationBar: SafeArea(
        top: false,
        child: Container(
          decoration: const BoxDecoration(
            color: Colors.white,
            border: Border(top: BorderSide(color: Color(0xFFE4E9FA))),
            boxShadow: [BoxShadow(color: Color(0x100F172A), blurRadius: 18, offset: Offset(0, -4))],
          ),
          child: NavigationBar(
            height: 72,
            backgroundColor: Colors.white,
            indicatorColor: const Color(0xFFE0E7FF),
            labelBehavior: NavigationDestinationLabelBehavior.alwaysShow,
            selectedIndex: _currentIndex,
            onDestinationSelected: (index) => setState(() => _currentIndex = index),
            destinations: const [
              NavigationDestination(icon: Icon(Icons.home_outlined), selectedIcon: Icon(Icons.home_rounded, color: primary), label: 'Phòng trọ'),
              NavigationDestination(icon: Icon(Icons.description_outlined), selectedIcon: Icon(Icons.description_rounded, color: primary), label: 'Hợp đồng'),
              NavigationDestination(icon: Icon(Icons.event_available_outlined), selectedIcon: Icon(Icons.event_available_rounded, color: primary), label: 'Đặt chỗ'),
              NavigationDestination(icon: Icon(Icons.people_outline_rounded), selectedIcon: Icon(Icons.people_rounded, color: primary), label: 'Khách thuê'),
            ],
          ),
        ),
      ),
    );
  }
}

class _MenuEntry extends StatelessWidget {
  final IconData icon;
  final String label;
  final Color color;
  const _MenuEntry(this.icon, this.label, this.color);

  @override
  Widget build(BuildContext context) => Row(
    children: [
      Container(
        padding: const EdgeInsets.all(9),
        decoration: BoxDecoration(color: color.withValues(alpha: 0.10), borderRadius: BorderRadius.circular(11)),
        child: Icon(icon, color: color, size: 21),
      ),
      const SizedBox(width: 12),
      Text(label, style: const TextStyle(fontWeight: FontWeight.w600, fontSize: 14)),
    ],
  );
}
