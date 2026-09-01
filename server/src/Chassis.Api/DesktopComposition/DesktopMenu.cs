using ElectronNET.API;
using ElectronNET.API.Entities;

namespace Chassis.Api;

/// <summary>
/// The native application menu. Deliberately small for the template — roles only,
/// no custom handlers yet. Product menus (and the "Check for updates" item that
/// pairs with §8) get added here, never in <c>Program.cs</c>.
/// </summary>
internal static class DesktopMenu
{
    public static void Install()
    {
        MenuItem[] menu =
        [
            new()
            {
                Label = "Chassis",
                Submenu =
                [
                    new() { Role = MenuRole.reload, Label = "Reload" },
                    new() { Role = MenuRole.forceReload, Label = "Force Reload" },
                    new() { Role = MenuRole.toggleDevTools, Label = "Toggle Developer Tools" },
                    new() { Type = MenuType.separator },
                    new() { Role = MenuRole.quit, Label = "Quit Chassis" },
                ],
            },
            new()
            {
                Label = "Edit",
                Submenu =
                [
                    new() { Role = MenuRole.undo, Label = "Undo" },
                    new() { Role = MenuRole.redo, Label = "Redo" },
                    new() { Type = MenuType.separator },
                    new() { Role = MenuRole.cut, Label = "Cut" },
                    new() { Role = MenuRole.copy, Label = "Copy" },
                    new() { Role = MenuRole.paste, Label = "Paste" },
                    new() { Role = MenuRole.selectAll, Label = "Select All" },
                ],
            },
            new()
            {
                Label = "View",
                Submenu =
                [
                    new() { Role = MenuRole.resetZoom, Label = "Actual Size" },
                    new() { Role = MenuRole.zoomIn, Label = "Zoom In" },
                    new() { Role = MenuRole.zoomOut, Label = "Zoom Out" },
                    new() { Type = MenuType.separator },
                    new() { Role = MenuRole.togglefullscreen, Label = "Toggle Full Screen" },
                ],
            },
        ];

        Electron.Menu.SetApplicationMenu(menu);
    }
}
