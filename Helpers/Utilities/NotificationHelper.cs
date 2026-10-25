using System.Windows;
using Wpf.Ui.Controls;

namespace TextureSwapper.Helpers.Utilities
{
    public class NotificationState
    {
        public int CurrentId { get; set; }
    }

    public static class NotificationHelper
    {
        public static async Task ShowToastAsync(
            InfoBar infoBar,
            NotificationState state,
            string title,
            string message,
            ControlAppearance appearance,
            int durationMs = 3500
        )
        {
            if (
                infoBar == null
                || Application.Current?.Dispatcher == null
                || Application.Current.Dispatcher.HasShutdownStarted
            )
            {
                return;
            }

            int notificationId = 0;
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                state.CurrentId++;
                notificationId = state.CurrentId;

                infoBar.Title = title;
                infoBar.Message = message;
                infoBar.Severity = appearance switch
                {
                    ControlAppearance.Primary => InfoBarSeverity.Informational,
                    ControlAppearance.Success => InfoBarSeverity.Success,
                    ControlAppearance.Danger => InfoBarSeverity.Error,
                    ControlAppearance.Caution => InfoBarSeverity.Warning,
                    ControlAppearance.Info => InfoBarSeverity.Informational,
                    _ => InfoBarSeverity.Informational,
                };

                infoBar.IsOpen = true;
            });

            await Task.Delay(durationMs);

            if (
                Application.Current?.Dispatcher == null
                || Application.Current.Dispatcher.HasShutdownStarted
            )
            {
                return;
            }

            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                if (state.CurrentId == notificationId)
                {
                    infoBar.IsOpen = false;
                }
            });
        }
    }
}
