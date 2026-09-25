using System.Windows;
using WpfScreenHelper;
using System.Windows.Media;

namespace Hub.Services.Windowing;

public static class WindowPositionService
{
    private const double MaxHeightScreenPercentage = 0.5;

    public static void CenterOnCurrentMonitor(Window window)
    {
        var cursorPosition = MouseHelper.MousePosition;
        var screen = Screen.FromPoint(cursorPosition);
        var area = screen.WorkingArea;

        var dpiScale = VisualTreeHelper.GetDpi(window);

        var areaLeft = area.Left / dpiScale.DpiScaleX;
        var areaTop = area.Top / dpiScale.DpiScaleY;
        var areaWidth = area.Width / dpiScale.DpiScaleX;
        var areaHeight = area.Height / dpiScale.DpiScaleY;

        window.MaxHeight = areaHeight * MaxHeightScreenPercentage;

        window.Left = areaLeft + (areaWidth - window.Width) / 2;
        window.Top = areaTop + (areaHeight - window.Height) / 2;
    }
}
