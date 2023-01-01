﻿using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using BhModule.Community.Pathing.Utility;
using Blish_HUD.Contexts;
using Microsoft.Xna.Framework;
using TmfLib.Prototype;

namespace BhModule.Community.Pathing.Behavior.Filter {
    public class FestivalFilter : IBehavior, ICanFilter {

        public const string PRIMARY_ATTR_NAME = "festival";

        public ObservableCollection<FestivalContext.Festival> AllowedFestivals { get; }

        private bool _isFiltered;

        public bool IsFiltered() => _isFiltered;

        public FestivalFilter(IEnumerable<FestivalContext.Festival> allowedFestivals) : this(allowedFestivals.ToArray()) { /* NOOP */ }

        public FestivalFilter(params FestivalContext.Festival[] allowedFestivals) {
            this.AllowedFestivals = new ObservableCollection<FestivalContext.Festival>(allowedFestivals);

            this.AllowedFestivals.CollectionChanged += AllowedFestivalsOnCollectionChanged;

            UpdateFiltered();
        }

        private void UpdateFiltered() {
            _isFiltered = !this.AllowedFestivals.Any(f => f.IsActive());
        }

        public string FilterReason() {
            return $"Hidden because the {string.Join(", ", AllowedFestivals.Select(festival => festival.DisplayName))} festival isn't active.";
        }

        private void AllowedFestivalsOnCollectionChanged(object sender, NotifyCollectionChangedEventArgs e) {
            UpdateFiltered();
        }

        public static IBehavior BuildFromAttributes(AttributeCollection attributes) {
            return attributes.TryGetAttribute(PRIMARY_ATTR_NAME, out var attribute)
                       ? new FestivalFilter(attribute.GetValueAsStrings().Select(FestivalContext.Festival.FromName))
                       : null;

        }

        public void Update(GameTime gameTime) { /* NOOP */ }

        public void Unload() { /* NOOP */ }

    }
}
