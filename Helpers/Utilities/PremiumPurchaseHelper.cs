using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Serilog;
using TextureSwapper.Core;
using MessageBox = Wpf.Ui.Controls.MessageBox;
using MessageBoxResult = Wpf.Ui.Controls.MessageBoxResult;

namespace TextureSwapper.Helpers.Utilities
{
    public static class PremiumPurchaseHelper
    {
        public static string DiscordServerInviteUrl { get; set; } = Constants.DiscordInviteUrl;
        public const string DiscordContactUsername = Constants.DiscordContactUsername;

        public static async Task PromptPurchaseOptionsAsync(Window? owner = null)
        {
            try
            {
                MessageBox messageBox = new()
                {
                    Title = "Get TextureSwapper Premium",
                    Content = "Buy Directly via Discord (Recommended)\n\n"
                        + "Join the Discord server and open a ticket (or DM 'noizy3345') to discuss details.\n"
                        + "Instant key generation & delivery directly from the developer\n\n"
                        + "Automated Web Store\n\n"
                        + "Instant checkout via Gumroad (~$6.28 / 5.61€ including regional VAT/taxes).",
                    PrimaryButtonText = "Join Discord Server",
                    SecondaryButtonText = "Open Gumroad",
                    CloseButtonText = "Cancel",
                    MaxWidth = 520,
                };

                Window? targetOwner = owner
                    ?? Application.Current?.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
                    ?? Application.Current?.MainWindow;

                if (targetOwner != null)
                {
                    messageBox.Owner = targetOwner;
                }

                MessageBoxResult result = await messageBox.ShowDialogAsync();

                if (result == MessageBoxResult.Primary)
                {
                    try
                    {
                        Clipboard.SetText(DiscordContactUsername);
                    }
                    catch (Exception clipEx)
                    {
                        Log.Warning(clipEx, "Failed to copy Discord username to clipboard.");
                    }

                    string targetUrl = !string.IsNullOrWhiteSpace(DiscordServerInviteUrl)
                        ? DiscordServerInviteUrl
                        : "https://discord.com/app";

                    try
                    {
                        _ = Process.Start(new ProcessStartInfo
                        {
                            FileName = targetUrl,
                            UseShellExecute = true
                        });
                    }
                    catch (Exception procEx)
                    {
                        Log.Warning(procEx, "Failed to launch Discord URL.");
                    }
                }
                else if (result == MessageBoxResult.Secondary)
                {
                    OpenGumroad();
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to display Premium purchase options dialog, falling back to Gumroad.");
                OpenGumroad();
            }
        }

        public static void OpenGumroad()
        {
            try
            {
                _ = Process.Start(
                    new ProcessStartInfo
                    {
                        FileName = Constants.GumroadBuyUrl,
                        UseShellExecute = true,
                    }
                );
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Direct browser start failed for Gumroad, attempting cmd fallback.");
                try
                {
                    _ = Process.Start(
                        new ProcessStartInfo
                        {
                            FileName = "cmd",
                            Arguments = $"/c start {Constants.GumroadBuyUrl}",
                            CreateNoWindow = true,
                            UseShellExecute = false,
                        }
                    );
                }
                catch (Exception fallbackEx)
                {
                    Log.Error(fallbackEx, "Failed to open Gumroad URL via fallback.");
                }
            }
        }
    }
}
