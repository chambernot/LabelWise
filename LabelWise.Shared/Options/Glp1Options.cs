namespace LabelWise.Shared.Options
{
    // Seção "Glp1" da configuração. Enquanto o modo estiver em teste, só os telefones de AllowedPhones veem algo diferente.
    public class Glp1Options
    {
        public const string SectionName = "Glp1";

        public string[] AllowedPhones { get; set; } = Array.Empty<string>();
        public decimal ProteinGPerKg { get; set; } = 1.2m;
        public int ProteinMinG { get; set; } = 80;
        public int ProteinMaxG { get; set; } = 120;
        public int CalorieFloorKcal { get; set; } = 1200;
        public int WaterMlPerKg { get; set; } = 35;
        public string ConsentVersion { get; set; } = "glp1-v1";
    }
}
