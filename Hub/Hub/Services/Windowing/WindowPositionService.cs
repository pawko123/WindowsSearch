using System.Windows;
using System.Windows.Forms;

namespace Hub.Services.Windowing;

public static class WindowPositionService
{
    public static void CenterOnCurrentMonitor(Window window)
    {
        var cursorPosition = Cursor.Position;
        var screen = Screen.FromPoint(cursorPosition);
        var area = screen.WorkingArea;

        window.Left = area.Left + (area.Width - window.Width) / 2;
        window.Top = area.Top + (area.Height - window.Height) / 2;
    }
}
