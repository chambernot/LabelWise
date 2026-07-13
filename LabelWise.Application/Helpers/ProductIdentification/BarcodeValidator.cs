using System.Text.RegularExpressions;

namespace LabelWise.Application.Helpers.ProductIdentification
{
    /// <summary>
    /// Helper para validação e normalização de códigos de barras.
    /// Suporta EAN-13, EAN-8, UPC-A, UPC-E.
    /// </summary>
    public static class BarcodeValidator
    {
        private static readonly Regex EAN13Regex = new Regex(@"^\d{13}$", RegexOptions.Compiled);
        private static readonly Regex EAN8Regex = new Regex(@"^\d{8}$", RegexOptions.Compiled);
        private static readonly Regex UPCARegex = new Regex(@"^\d{12}$", RegexOptions.Compiled);
        private static readonly Regex UPCERegex = new Regex(@"^\d{8}$", RegexOptions.Compiled);

        /// <summary>
        /// Valida se um código de barras é válido (formato e checksum).
        /// </summary>
        public static bool IsValid(string? barcode, out string? errorMessage)
        {
            errorMessage = null;

            if (string.IsNullOrWhiteSpace(barcode))
            {
                errorMessage = "Código de barras vazio ou nulo";
                return false;
            }

            barcode = barcode.Trim();

            // EAN-13
            if (EAN13Regex.IsMatch(barcode))
            {
                return ValidateCheckDigit(barcode, out errorMessage);
            }

            // EAN-8
            if (EAN8Regex.IsMatch(barcode))
            {
                return ValidateCheckDigit(barcode, out errorMessage);
            }

            // UPC-A (12 dígitos)
            if (UPCARegex.IsMatch(barcode))
            {
                return ValidateCheckDigit(barcode, out errorMessage);
            }

            // UPC-E (8 dígitos, mesmo formato que EAN-8)
            if (UPCERegex.IsMatch(barcode))
            {
                return ValidateCheckDigit(barcode, out errorMessage);
            }

            errorMessage = $"Formato de código de barras não suportado: {barcode.Length} dígitos";
            return false;
        }

        /// <summary>
        /// Normaliza um código de barras (remove espaços, hífens, etc.).
        /// </summary>
        public static string Normalize(string barcode)
        {
            if (string.IsNullOrWhiteSpace(barcode))
                return string.Empty;

            // Remove todos os caracteres não numéricos
            return new string(barcode.Where(char.IsDigit).ToArray());
        }

        /// <summary>
        /// Detecta o tipo de código de barras.
        /// </summary>
        public static string GetBarcodeType(string barcode)
        {
            barcode = Normalize(barcode);

            return barcode.Length switch
            {
                13 => "EAN-13",
                12 => "UPC-A",
                8 => "EAN-8/UPC-E",
                _ => "Unknown"
            };
        }

        /// <summary>
        /// Valida o dígito verificador usando algoritmo EAN/UPC.
        /// </summary>
        private static bool ValidateCheckDigit(string barcode, out string? errorMessage)
        {
            errorMessage = null;

            try
            {
                int checkDigit = int.Parse(barcode[^1..]);
                int calculatedCheckDigit = CalculateCheckDigit(barcode[..^1]);

                if (checkDigit != calculatedCheckDigit)
                {
                    errorMessage = $"Dígito verificador inválido: esperado {calculatedCheckDigit}, encontrado {checkDigit}";
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                errorMessage = $"Erro ao validar dígito verificador: {ex.Message}";
                return false;
            }
        }

        /// <summary>
        /// Calcula o dígito verificador para EAN/UPC.
        /// </summary>
        private static int CalculateCheckDigit(string barcodeWithoutCheckDigit)
        {
            int sum = 0;
            bool oddPosition = true;

            // Percorre de trás para frente
            for (int i = barcodeWithoutCheckDigit.Length - 1; i >= 0; i--)
            {
                int digit = int.Parse(barcodeWithoutCheckDigit[i].ToString());
                sum += oddPosition ? digit * 3 : digit;
                oddPosition = !oddPosition;
            }

            int remainder = sum % 10;
            return remainder == 0 ? 0 : 10 - remainder;
        }

        /// <summary>
        /// Converte UPC-A para EAN-13 (adiciona zero à esquerda).
        /// </summary>
        public static string ConvertUpcToEan(string upcBarcode)
        {
            upcBarcode = Normalize(upcBarcode);

            if (upcBarcode.Length == 12)
            {
                return "0" + upcBarcode;
            }

            return upcBarcode;
        }
    }
}
