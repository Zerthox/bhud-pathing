﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using BhModule.Community.Pathing.Content;
using Blish_HUD;
using Cronos;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using TmfLib;
using TmfLib.Prototype;

namespace BhModule.Community.Pathing.Utility {
    public static class AttributeParsingUtil {

        private static readonly Logger Logger = Logger.GetLogger(typeof(AttributeParsingUtil));

        private const char ATTRIBUTEVALUE_DELIMITER = ',';

        private static readonly CultureInfo _invariantCulture = CultureInfo.InvariantCulture;

        private static IEnumerable<string> SplitAttributeValue(this IAttribute attribute) => attribute.Value.Split(ATTRIBUTEVALUE_DELIMITER);

        private static T InternalGetValueAsEnum<T>(string attributeValue) where T : Enum {
            return EnumUtil.TryParseCacheEnum(attributeValue, out T value) ? value : default;
        }

        private static int InternalGetValueAsInt(string attributeValue, int @default = default) {
            return int.TryParse(attributeValue, NumberStyles.Any, _invariantCulture, out int value) ? value : @default;
        }

        private static float InternalGetValueAsFloat(string attributeValue, float @default = default) {
            return float.TryParse(attributeValue, NumberStyles.Any, _invariantCulture, out float value) ? value : @default;
        }

        internal static Guid InternalGetValueAsGuid(string attributeValue) {
            // Made robust to handle the proper GUIDs as generated by TacO and
            // those malformed or otherwise made up (which are not true GUIDs).
            byte[] rawGuid = null;

            try {
                if (attributeValue.Length % 4 == 0 && attributeValue.EndsWith("==")) {
                    // GUID appears to be properly formated.
                    rawGuid = Convert.FromBase64String(attributeValue);
                } else {
                    using var md5 = MD5.Create();
                    rawGuid = md5.ComputeHash(Encoding.UTF8.GetBytes(attributeValue));
                }
            } catch (Exception ex) {
                Logger.Debug(ex, $"Failed to parse value {attributeValue} as a GUID.");
            }

            return rawGuid?.Length == 16
                ? new Guid(rawGuid)
                : new Guid();
        }

        private static bool InternalGetValueAsBool(string attributeValue) {
            return InternalGetValueAsInt(attributeValue) > 0 || string.Equals(attributeValue, "true", StringComparison.OrdinalIgnoreCase);
        }

        public static string GetValueAsString(this IAttribute attribute) {
            return attribute.Value;
        }

        public static IEnumerable<string> GetValueAsStrings(this IAttribute attribute) {
            return SplitAttributeValue(attribute).Select(val => val.Trim());
        }

        public static int GetValueAsInt(this IAttribute attribute, int @default = default) {
            return InternalGetValueAsInt(attribute.Value, @default);
        }

        public static float GetValueAsFloat(this IAttribute attribute, float @default = default) {
            return InternalGetValueAsFloat(attribute.Value, @default);
        }

        public static bool GetValueAsBool(this IAttribute attribute) {
            return InternalGetValueAsBool(attribute.Value);
        }

        public static Guid GetValueAsGuid(this IAttribute attribute) {
            return InternalGetValueAsGuid(attribute.Value);
        }

        public static IEnumerable<int> GetValueAsInts(this IAttribute attribute) {
            return SplitAttributeValue(attribute).Select(attr => InternalGetValueAsInt(attr));
        }

        public static IEnumerable<float> GetValueAsFloats(this IAttribute attribute) {
            return SplitAttributeValue(attribute).Select(attr => InternalGetValueAsFloat(attr));
        }

        public static IEnumerable<Guid> GetValueAsGuids(this IAttribute attribute) {
            return SplitAttributeValue(attribute).Select(InternalGetValueAsGuid);
        }

        public static IEnumerable<bool> GetValueAsBools(this IAttribute attribute) {
            return SplitAttributeValue(attribute).Select(InternalGetValueAsBool);
        }

        public static async Task<(Texture2D Texture, Color Sample)> GetValueAsTextureAsync(this IAttribute attribute, TextureResourceManager resourceManager) {
            return await resourceManager.LoadTextureAsync(attribute.GetValueAsString());
        }

        public static Color GetValueAsColor(this IAttribute attribute, Color @default = default) {
            string attrValue = attribute.GetValueAsString().ToLowerInvariant();

            return attrValue switch {
                "white" => Color.White,
                "yellow" => Color.FromNonPremultiplied(255, 255, 0,  255),
                "red" => Color.FromNonPremultiplied(242,    13,  19, 255),
                "green" => Color.FromNonPremultiplied(85,   221, 85, 255),
                _ => ColorUtil.TryParseHex(attrValue, out var color) ? color : @default
            };
        }

        public static CronExpression GetValueAsCronExpression(this IAttribute attribute) {
            try {
                string strAttribute = attribute.GetValueAsString();
                int    segmentCount = strAttribute.Split(new [] {' '}, StringSplitOptions.RemoveEmptyEntries).Length;

                return segmentCount > 5 
                           ? CronExpression.Parse(attribute.GetValueAsString(), CronFormat.IncludeSeconds) 
                           : CronExpression.Parse(attribute.GetValueAsString());
            } catch (CronFormatException ex) {
                Logger.Warn(ex, "Failed to parse value {attributeValue} as a cron expression.", attribute.GetValueAsString());
            }

            return null;
        }

        public static T GetValueAsEnum<T>(this IAttribute attribute) where T : Enum {
            return InternalGetValueAsEnum<T>(attribute.Value);
        }

        public static IEnumerable<T> GetValueAsEnums<T>(this IAttribute attribute) where T : Enum {
            return SplitAttributeValue(attribute).Select(InternalGetValueAsEnum<T>);
        }

    }
}
