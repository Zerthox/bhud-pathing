﻿using Blish_HUD;
using Blish_HUD.Modules;
using Blish_HUD.Modules.Managers;
using Blish_HUD.Settings;
using Microsoft.Xna.Framework;
using System;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Threading.Tasks;
using BhModule.Community.Pathing.UI.Common;
using BhModule.Community.Pathing.UI.Controls;
using BhModule.Community.Pathing.UI.Views;
using Blish_HUD.Controls;
using Blish_HUD.Settings.UI.Views;
using Microsoft.Xna.Framework.Input;

namespace BhModule.Community.Pathing {
    [Export(typeof(Module))]
    public class PathingModule : Module {

        private static readonly Logger Logger = Logger.GetLogger<PathingModule>();

        #region Service Managers
        internal SettingsManager    SettingsManager    => this.ModuleParameters.SettingsManager;
        internal ContentsManager    ContentsManager    => this.ModuleParameters.ContentsManager;
        internal DirectoriesManager DirectoriesManager => this.ModuleParameters.DirectoriesManager;
        internal Gw2ApiManager      Gw2ApiManager      => this.ModuleParameters.Gw2ApiManager;
        #endregion

        internal static PathingModule Instance { get; private set; }

        private CornerIcon       _pathingIcon;
        public  ContextMenuStrip _pathingContextMenuStrip;

        private ModuleSettings _moduleSettings;

        [ImportingConstructor]
        public PathingModule([Import("ModuleParameters")] ModuleParameters moduleParameters) : base(moduleParameters) {
            Instance = this;
        }

        protected override void DefineSettings(SettingCollection settings) {
            _moduleSettings = new ModuleSettings(settings);
        }

        protected override void Initialize() {
            _pathingIcon = new CornerIcon() {
                IconName = Strings.General_UiName,
                Icon     = ContentsManager.GetTexture(@"png\pathing-icon.png"),
                Priority = Strings.General_UiName.GetHashCode()
            };

            _pathingContextMenuStrip = new ContextMenuStrip();

            var newWindow = new TabbedWindow2(ContentsManager.GetTexture(@"png\controls\156006.png"),
                                              new Rectangle(35, 36, 900, 640),
                                              new Rectangle(95, 42, 783 + 38, 572 + 20)) {
                Title    = Strings.General_UiName,
                Parent   = GameService.Graphics.SpriteScreen,
                Location = new Point(100, 100),
                Emblem   = this.ContentsManager.GetTexture(@"png\controls\1615829.png")
            };

            Logger.Info(newWindow.HeightSizingMode.ToString());

            newWindow.Tabs.Add(new Tab(ContentsManager.GetTexture(@"png\156740+155150.png"), () => new SettingsView(_moduleSettings.PackSettings),    Strings.Window_MainSettingsTab));
            newWindow.Tabs.Add(new Tab(ContentsManager.GetTexture(@"png\157123+155150.png"), () => new SettingsView(_moduleSettings.MapSettings),     Strings.Window_MapSettingsTab));
            newWindow.Tabs.Add(new Tab(ContentsManager.GetTexture(@"png\156734+155150.png"), () => new SettingsView(_moduleSettings.KeyBindSettings), Strings.Window_KeyBindSettingsTab));

            #if SHOWINDEV
                newWindow.Tabs.Add(new Tab(ContentsManager.GetTexture(@"png\156909.png"), () => new PackRepoView(), Strings.Window_DownloadMarkerPacks));
            #endif

            _pathingIcon.Menu = _pathingContextMenuStrip;

            _pathingIcon.Click += delegate {
                if (GameService.Input.Keyboard.ActiveModifiers.HasFlag(ModifierKeys.Ctrl)) {
                    _moduleSettings.GlobalPathablesEnabled.Value = !_moduleSettings.GlobalPathablesEnabled.Value;
                } else {
                    newWindow.ToggleWindow();
                }
            };
        }

        private PackInitiator _watcher;

        protected override async Task LoadAsync() {
            var sw = Stopwatch.StartNew();
            _watcher = new PackInitiator(DirectoriesManager.GetFullDirectoryPath("markers"), _moduleSettings);
            await _watcher.Init();
            sw.Stop();
            Logger.Debug($"Took {sw.ElapsedMilliseconds} ms to complete loading...");
        }

        protected override void OnModuleLoaded(EventArgs e) {

            // Base handler must be called
            base.OnModuleLoaded(e);
        }

        protected override void Update(GameTime gameTime) {
            _watcher?.Update(gameTime);
        }

        /// <inheritdoc />
        protected override void Unload() {
            _watcher?.Unload();
            _pathingIcon?.Dispose();
            _pathingContextMenuStrip.Dispose();

            Instance = null;
        }

    }

}
