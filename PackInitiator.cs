﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BhModule.Community.Pathing.State;
using BhModule.Community.Pathing.UI.Controls;
using Blish_HUD;
using Blish_HUD.Controls;
using Microsoft.Xna.Framework;
using TmfLib;
using TmfLib.Reader;

namespace BhModule.Community.Pathing {
    public class PackInitiator : IUpdatable {

        private static readonly Logger Logger = Logger.GetLogger<PackInitiator>();

        private readonly string            _watchPath;
        private readonly PathingModule     _module;
        private readonly IProgress<string> _loadingIndicator;

        private readonly IRootPackState _packState;

        private readonly SafeList<Pack> _packs = new();

        private SharedPackCollection _sharedPackCollection;
        
        private readonly PackReaderSettings _packReaderSettings;

        public bool IsLoading { get; private set; }

        public IRootPackState PackState => _packState;

        private int  _lastMap   = -1;

        public PackInitiator(string watchPath, PathingModule module, IProgress<string> loadingIndicator) {
            _watchPath        = watchPath;
            _module           = module;
            _loadingIndicator = loadingIndicator;

            _packReaderSettings = new PackReaderSettings();
            _packReaderSettings.VenderPrefixes.Add("bh-"); // Support Blish HUD specific categories/markers/trails/attributes.

            _packState = new SharedPackState(_module);
        }

        public void ReloadPacks() {
            if (_packState.CurrentMapId < 0 || this.IsLoading) return;

            _lastMap = -1;

            LoadMapFromEachPackInBackground(_packState.CurrentMapId);
        }

        public IEnumerable<ContextMenuStripItem> GetPackMenuItems() {
            // All Markers
            bool isAnyMarkers = !this.IsLoading
                             && _sharedPackCollection != null
                             && _sharedPackCollection.Categories != null
                             && _sharedPackCollection.Categories.Any(category => !string.IsNullOrWhiteSpace(category.DisplayName));

            var allMarkers = new ContextMenuStripItem() {
                Text = "All Markers", // TODO: Localize "All Markers"
                CanCheck = true,
                Checked = _module.Settings.GlobalPathablesEnabled.Value,
                Submenu = isAnyMarkers
                    ? new CategoryContextMenuStrip(_packState, _sharedPackCollection.Categories, false)
                    : null
            };

            allMarkers.CheckedChanged += (_, e) => {
                _module.Settings.GlobalPathablesEnabled.Value = e.Checked;
            };

            // Scripts
            if (_module.Settings.ScriptsEnabled.Value && _module.ScriptEngine.Global != null && _module.ScriptEngine.Global.Menu.Menus.Any()) {
                yield return _module.ScriptEngine.Global.Menu.BuildMenu();
            }

            // Reload Markers
            var reloadMarkers = new ContextMenuStripItem() {
                Text    = "Reload Markers", // TODO: Localize "Reload Markers"
                Enabled = !this.IsLoading && _packState.CurrentMapId > 0
            };

            reloadMarkers.Click += (_, _) => {
                ReloadPacks();
            };

            // Unload Markers
            var unloadMarkers = new ContextMenuStripItem() {
                Text    = "Unload Markers", // TODO: Localize "Unload Markers"
                Enabled = !this.IsLoading && _packState.CurrentMapId > 0
            };

            unloadMarkers.Click += async (_, _) => {
                if (_packState.CurrentMapId < 0) return;

                await UnloadStateAndCollection();
            };
            
            yield return allMarkers;
            yield return reloadMarkers;
            yield return unloadMarkers;
        }

        public async Task Init() {
            await _packState.Load();
            await LoadAllPacks();
        }

        public async Task LoadUnpackedPackFiles(string unpackedDir) {
            var newPack = Pack.FromDirectoryMarkerPack(unpackedDir);
            
            _packs.Add(newPack);
        }

        public async Task LoadPackedPackFiles(IEnumerable<string> zipPackFiles) {
            foreach (string packArchive in zipPackFiles) {
                try {
                    var newPack = Pack.FromArchivedMarkerPack(packArchive);
                    _packs.Add(newPack);
                } catch (InvalidDataException) {
                    Logger.Warn($"Pack {packArchive} appears to be corrupt.  Please remove it and download it again.");
                } catch (Exception ex) {
                    Logger.Warn(ex, $"Pack {packArchive} failed to load.");
                }
            }
        }

        public async Task LoadPack(Pack pack) {
            if (pack != null) {
                _packs.Add(pack);
            }
        }

        public void UnloadPackByName(string packName) {
            foreach (var pack in _packs.ToArray()) {
                if (string.Equals(packName, pack.Name, StringComparison.OrdinalIgnoreCase)) {
                    _packs.Remove(pack);
                    break;
                }
            }
        }

        private async Task UnloadStateAndCollection() {
            _sharedPackCollection?.Unload();
            await _packState.Unload();
        }

        private async Task LoadAllPacks() {
            // Load from base and advanced markers paths
            foreach (string markerDir in (_packState.UserResourceStates.Advanced.MarkerLoadPaths ?? Array.Empty<string>()).Concat(new [] {_watchPath})) {
                await LoadPackedPackFiles(Directory.GetFiles(markerDir, "*.zip",  SearchOption.AllDirectories));
                await LoadPackedPackFiles(Directory.GetFiles(markerDir, "*.taco", SearchOption.AllDirectories));
                await LoadUnpackedPackFiles(markerDir);
            }

            // If the module loads at launch, this can end up firing twice.
            // If the module loads after launch (manually enabled), we need this to populate the current map.
            if (GameService.Gw2Mumble.CurrentMap.Id != default) {
                LoadMapFromEachPackInBackground(_packState.CurrentMapId = GameService.Gw2Mumble.CurrentMap.Id);
            }

            GameService.Gw2Mumble.CurrentMap.MapChanged += OnMapChanged;
        }

        private async Task PrepareState(int mapId) {
            await UnloadStateAndCollection();

            _sharedPackCollection = new SharedPackCollection();
        }

        private void LoadMapFromEachPackInBackground(int mapId) {
            lock (this) {
                if (mapId == _lastMap) {
                    return;
                };
                _lastMap = mapId;
            }

            var thread = new Thread(async () => await LoadMapFromEachPack(mapId)) {
                IsBackground = true
            };

            thread.Start();
        }

        private async Task LoadMapFromEachPack(int mapId) {
            this.IsLoading = true;

            var loadTimer = Stopwatch.StartNew();

            _packState.Module.ScriptEngine.Reset();

            var packTimings = new List<(Pack Pack, long LoadDuration)>();

            // TODO: Localize the loading messages.
            _loadingIndicator.Report("Loading marker packs...");

            await PrepareState(mapId);

            var packs = _packs.ToArray();

            foreach (var pack in packs) {
                try {
                    var packTimer = Stopwatch.StartNew();

                    _loadingIndicator.Report($"Loading {pack.Name}...");
                    await pack.LoadMapAsync(mapId, _sharedPackCollection, _packReaderSettings);

                    packTimings.Add((pack, packTimer.ElapsedMilliseconds));
                } catch (FileNotFoundException e) {
                    Logger.Warn("Pack file '{packPath}' failed to load because it could not be found.", e.FileName);
                    _packs.Remove(pack);
                } catch (Exception e) {
                    Logger.Warn(e, $"Loading pack '{pack.Name}' failed.");
                }
            }

            _loadingIndicator.Report("Finalizing marker collection...");

            try {
                await _packState.LoadPackCollection(_sharedPackCollection);
            } catch (Exception e) {
                Logger.Warn(e, $"Finalizing packs failed.");
                _packState?.Unload();
            }

            // We only load scripts if they're enabled.
            if (_packState.Module.Settings.ScriptsEnabled.Value) {
                _loadingIndicator.Report("Loading scripts...");

                foreach (var pack in packs) {
                    await _packState.Module.ScriptEngine.LoadScript("pack.lua", pack.ResourceManager);
                }
            }

            foreach (var pack in packs) {
                pack.ReleaseLocks();
            }
            
            _loadingIndicator.Report(null);

            this.IsLoading = false;

            Logger.Info($"Finished loading packs {string.Join(", ", packTimings.Select(p => $"{(p.Pack.ManifestedPack ? "+" : "-")}{p.Pack.Name}[{p.LoadDuration}ms]"))} in {loadTimer.ElapsedMilliseconds}ms for map {mapId}.");
        }

        private void OnMapChanged(object sender, ValueEventArgs<int> e) {
            if (e.Value == _packState.CurrentMapId) return;

            _packState.CurrentMapId = e.Value;

            LoadMapFromEachPackInBackground(e.Value);
        }
        
        public void Update(GameTime gameTime) {
            _packState.Update(gameTime);
        }

        public void Unload() {
            GameService.Gw2Mumble.CurrentMap.MapChanged -= OnMapChanged;

            _packState.Unload();
        }

    }
}
