using System;
using System.Text.RegularExpressions;

namespace LabelWise.Infrastructure.Helpers
{
    public static class ImageFormatHelper
    {
        public static string DetectMimeTypeFromBase64(string base64)
        {
            if (string.IsNullOrWhiteSpace(base64))
            {
                return "image/jpeg";
            }

            var cleaned = StripDataUrlPrefix(base64);

            return cleaned switch
            {
                _ when cleaned.StartsWith("/9j/", StringComparison.Ordinal) => "image/jpeg",
                _ when cleaned.StartsWith("iVBOR", StringComparison.Ordinal) => "image/png",
                _ when cleaned.StartsWith("UklG", StringComparison.Ordinal) => "image/webp",
                _ when cleaned.StartsWith("R0lGOD", StringComparison.Ordinal) => "image/gif",
                _ => "image/jpeg"
            };
        }

        public static string NormalizeBase64Image(string base64)
        {
            if (string.IsNullOrWhiteSpace(base64))
            {
                return string.Empty;
            }

            var cleaned = StripDataUrlPrefix(base64);
            cleaned = cleaned.Trim();

            var mime = DetectMimeTypeFromBase64(cleaned);

            return $"data:{mime};base64,{cleaned}";
        }

        private static string StripDataUrlPrefix(string value)
        {
            var v = value.Trim();

            // Removes prefixes like:
            // data:image/jpeg;base64,
            // image/jpeg;base64,
            // data:...;base64,
            // ...;base64,
            v = Regex.Replace(v, @"^data:[^;]+;base64,", string.Empty, RegexOptions.IgnoreCase);
            v = Regex.Replace(v, @"^[^,]+;base64,", string.Empty, RegexOptions.IgnoreCase);

            return v;
        }
    }
}
