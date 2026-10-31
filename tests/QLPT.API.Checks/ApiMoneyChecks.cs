using Microsoft.AspNetCore.Mvc;
using QLPT.API.Controllers;
using QLPT.API.Models;
using QLPT.API.Services;

public static class ApiMoneyChecks
{
    public static async Task<int> RunAsync()
    {
        int checks = 0;
        void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
            checks++;
        }
        var start = new DateOnly(2026, 9, 1);
        HopDong Contract(decimal? price = 2_500_000) => new()
        {
            MaHopDong = 1, MaPhong = 2, GiaThueThang = price, NgayBatDau = start,
            NgayKetThuc = new DateOnly(2026, 11, 30), TrangThai = NghiepVu.HopDong.HieuLuc
        };
        DichVu Service(int id) => new()
        {
            MaDichVu = id, TenDichVu = id switch { 1 => "Điện", 2 => "Nước", _ => "Internet" },
            DonGia = id switch { 1 => 3500, 2 => 20000, _ => 100000 }
        };
        DangKyDichVu Registration(int id, decimal? price = null, DateOnly? beginning = null, DateOnly? ending = null, int number = 1) => new()
        {
            MaDangKy = number, MaHopDong = 1, MaDichVu = id, MaDichVuNavigation = Service(id),
            DonGiaApDung = price ?? Service(id).DonGia, NgayBatDau = beginning ?? start,
            NgayKetThuc = ending, TrangThai = ending.HasValue ? NghiepVu.DichVu.DaKetThuc : NghiepVu.DichVu.DangSuDung
        };
        ChiSoDienNuoc Reading(string type, decimal previous, decimal current) => new()
        {
            MaPhong = 2, LoaiChiSo = type, Thang = 9, Nam = 2026, ChiSoCu = previous, ChiSoMoi = current
        };
        List<DangKyDichVu> Registrations(DangKyDichVu? internet = null) =>
            [Registration(1), Registration(2), internet ?? Registration(3)];
        HoaDonBuilder.KetQua Bill(IEnumerable<DangKyDichVu>? registrations = null, IEnumerable<ChiSoDienNuoc>? readings = null,
            HopDong? contract = null, decimal? fallbackPrice = null, decimal? electricPrice = null, decimal? waterPrice = null,
            int month = 9, int year = 2026) => HoaDonBuilder.TinhTuDuLieu(contract ?? Contract(), month, year,
                registrations ?? Registrations(), [Service(1), Service(2), Service(3)],
                readings ?? [Reading("Điện", 100, 198), Reading("Nước", 20, 23)], fallbackPrice, electricPrice, waterPrice);
        void Expect(HoaDonBuilder.KetQua bill, decimal total, int rows, string scenario)
        {
            Check(bill.Loi == null, $"{scenario}: {bill.Loi}");
            Check(bill.TongTien == total, $"{scenario}: expected {total}, got {bill.TongTien}.");
            Check(bill.ChiTiet.Count == rows, $"{scenario}: expected {rows} invoice lines, got {bill.ChiTiet.Count}.");
        }

        // Agreed rent 2.5m + 98 kWh * 3500 + 3 m³ * 20000 + Internet 100k.
        Expect(Bill(), 3_003_000, 4, "ordinary monthly invoice");
        Expect(Bill(Registrations(Registration(3, beginning: new DateOnly(2026, 11, 1)))), 2_903_000, 3, "future service must not be billed early");
        Expect(Bill(Registrations(Registration(3, beginning: new DateOnly(2026, 8, 1), ending: new DateOnly(2026, 10, 1)))),
            3_003_000, 4, "a stopped service must remain in the earlier month when it was used");
        Expect(Bill(Registrations(Registration(3, beginning: new DateOnly(2026, 8, 1), ending: new DateOnly(2026, 8, 31)))),
            2_903_000, 3, "a service stopped before the period must be excluded");
        Expect(Bill(Registrations(Registration(3, beginning: new DateOnly(2026, 9, 30)))), 3_003_000, 4, "last-day start is within the monthly flat-fee period");
        Expect(Bill(Registrations(Registration(3, beginning: new DateOnly(2026, 8, 1), ending: start))),
            3_003_000, 4, "first-day stop retains use in the month");
        var restarted = Registrations(Registration(3, ending: new DateOnly(2026, 9, 14), number: 3));
        restarted.Add(Registration(3, price: 120_000, beginning: new DateOnly(2026, 9, 15), number: 4));
        var restartBill = Bill(restarted);
        Expect(restartBill, 3_023_000, 4, "stop/restart must not duplicate one monthly Internet fee");
        Check(restartBill.ChiTiet.Count(c => c.NoiDung == "Phí Internet") == 1, "One service must produce one monthly flat-fee line.");
        Expect(Bill(Registrations(Registration(3, price: 0))), 2_903_000, 3, "a free service must not become the catalogue price");
        Expect(Bill(electricPrice: 4500), 3_101_000, 4, "explicit electric tariff");
        Expect(Bill(waterPrice: 25000), 3_018_000, 4, "explicit water tariff");
        Expect(Bill(readings: [Reading("Điện", 100, 100), Reading("Nước", 20, 20)]), 2_600_000, 4, "zero consumption is billed as zero utilities");
        Expect(Bill(contract: Contract(null), fallbackPrice: 2_500_000), 3_003_000, 4, "legacy contract price uses the room fallback");
        Expect(Bill(registrations: [], readings: []), 2_500_000, 1, "rent-only invoice without registered services");

        var foreign = Registration(3, price: 999_999, number: 99); foreign.MaHopDong = 99;
        var scoped = Registrations(); scoped.Add(foreign);
        var foreignMeter = Reading("Điện", 0, 999_999); foreignMeter.MaPhong = 99;
        Expect(Bill(scoped, [foreignMeter, Reading("Điện", 100, 198), Reading("Nước", 20, 23)]),
            3_003_000, 4, "other contracts and rooms must not affect the bill");

        Check(Bill(readings: [Reading("Điện", 100, 198)]).Loi?.Contains("Nước") == true, "Missing registered water readings must block issuing an incomplete invoice.");
        Check(Bill(readings: [Reading("Điện", 100, 99), Reading("Nước", 20, 23)]).Loi != null, "A decreasing meter must not silently become zero consumption.");
        Check(Bill(readings: [Reading("Điện", -1, 198), Reading("Nước", 20, 23)]).Loi != null, "A negative old reading must be rejected.");
        Check(Bill(Registrations(Registration(3, price: -1))).Loi != null, "A negative fixed-service price must not be silently skipped.");
        Check(Bill(electricPrice: -1).Loi != null, "Negative electric override must be rejected.");
        Check(Bill(contract: Contract(0)).Loi != null, "A zero agreed rent requires correction before billing.");
        Check(Bill(contract: Contract(null)).Loi != null, "Missing contract and room prices must not yield a free invoice.");
        Check(Bill(readings: [Reading("Điện", 0, decimal.MaxValue), Reading("Nước", 20, 23)]).Loi != null, "Multiplication overflow must be a business error.");
        Check(Bill(contract: Contract(decimal.MaxValue)).Loi != null, "Invoice rows outside decimal(18,2) storage must be rejected.");
        var stoppedWithoutDate = Registration(3); stoppedWithoutDate.TrangThai = NghiepVu.DichVu.DaKetThuc;
        Check(Bill(Registrations(stoppedWithoutDate)).Loi != null, "A stopped service without an end date must not be charged indefinitely.");
        Check(Bill(Registrations(Registration(3, beginning: new DateOnly(2026, 9, 10), ending: new DateOnly(2026, 9, 9)))).Loi != null,
            "Invalid service date order must be reported.");
        Check(Bill(month: 8).Loi != null, "Do not issue a bill before the contract month.");
        Check(Bill(month: 12).Loi != null, "Do not issue a bill after the contract month.");
        Check(Bill(month: 0).Loi != null && Bill(year: 10000).Loi != null, "Invalid calendar periods must return a business error.");
        Check((await HoaDonBuilder.TinhAsync(null!, Contract(), 0, 2026)).Loi != null, "Invalid periods must be rejected before database access.");
        Check((await HoaDonBuilder.LapAsync(null!, Contract(), 9, int.MaxValue)).Loi != null, "An oversized year must not reach SQL or throw a DateOnly exception.");

        var registrationApi = new DangKyDichVuController(null!);
        foreach (var invalid in new[]
        {
            new DangKyDichVu(),
            new DangKyDichVu { MaHopDong = 1, MaDichVu = 2, DonGiaApDung = -1 },
            new DangKyDichVu { MaHopDong = 1, MaDichVu = 2, DonGiaApDung = 1.123m },
            new DangKyDichVu { MaHopDong = 1, MaDichVu = 2, DonGiaApDung = decimal.MaxValue },
            new DangKyDichVu { MaHopDong = 1, MaDichVu = 2, NgayBatDau = DateOnly.MinValue }
        })
            Check(await registrationApi.Create(invalid) is BadRequestObjectResult, "Invalid service registration input must be rejected before database access.");
        Console.WriteLine($"PASS {checks} invoice amount, service-period and registration checks without database access.");
        return checks;
    }
}
