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

        // Templates (categoria Utilidade, sem variáveis) usados quando a pessoa não falou com o bot nas últimas 24 h
        public string TemplateLembreteAplicacao { get; set; } = "glp1_lembrete_aplicacao";
        public string TemplateAlertaIngestao { get; set; } = "glp1_alerta_ingestao";
    }
}
