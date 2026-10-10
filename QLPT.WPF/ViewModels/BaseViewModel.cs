using CommunityToolkit.Mvvm.ComponentModel;

namespace QLPT.WPF.ViewModels;

public abstract partial class BaseViewModel : ObservableObject
{
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _errorMessage = "";

    // Đánh dấu "đang ở bên trong một RunSafeAsync". AsyncLocal chảy theo chuỗi await
    // của chính thao tác đó, nhưng một cú click mới bắt đầu chuỗi riêng nên không thấy cờ.
    private static readonly AsyncLocal<bool> _dangChay = new();

    protected async Task RunSafeAsync(Func<Task> action)
    {
        // Lời gọi lồng nhau (vd: xóa xong gọi LoadAsync bên trong cùng khối) chạy luôn,
        // lỗi để vòng ngoài bắt. Trước đây nó vấp chốt IsBusy bên dưới và bị bỏ qua
        // âm thầm, nên bảng không tự làm mới sau khi xóa / đổi trạng thái.
        if (_dangChay.Value)
        {
            await action();
            return;
        }

        if (IsBusy) return; // chặn bấm đúp khi một thao tác khác đang chạy
        IsBusy = true;
        ErrorMessage = "";
        _dangChay.Value = true;
        try { await action(); }
        catch (Exception ex) { ErrorMessage = ex.Message; }
        finally
        {
            _dangChay.Value = false;
            IsBusy = false;
        }
    }
}
