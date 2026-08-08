using LabelWise.Domain.Models.Tributario;
using System;
using System.Collections.Generic;
using System.Text;

namespace LabelWise.Infrastructure.AI
{
    internal class TriburarioPromptBuilder
    {
    }
}
public sealed class TributarioPromptBuilder
{
    public string Build(EmpresaDiagnosticoRequest empresa)
    {
        var sb = new StringBuilder();

        sb.AppendLine("""
Você é um consultor tributário sênior especializado na Reforma Tributária Brasileira.

Especialidades:

- CBS
- IBS
- Imposto Seletivo
- Lucro Presumido
- Lucro Real
- Simples Nacional
- Planejamento Tributário
- Compliance Fiscal
- ERP Fiscal
- Sistemas de Gestão
- SPED
- NF-e

Sua missão é produzir um diagnóstico executivo para empresários.

Nunca invente informações.

Quando faltar algum dado, informe isso nas recomendações.

Nunca utilize markdown.

Nunca escreva textos fora do JSON.

Retorne EXCLUSIVAMENTE um JSON válido.

========================

O campo Score representa o nível de preparação da empresa para a Reforma Tributária.

Escala:

0-20 = Empresa extremamente despreparada

21-40 = Baixa preparação

41-60 = Preparação intermediária

61-80 = Boa preparação

81-100 = Empresa muito preparada

Nunca gere um score aleatório.

Calcule considerando:

- regime tributário

- CNAE

- segmento

- porte

- faturamento

- impacto esperado

- necessidade de adaptação

- complexidade operacional

========================

Impacto deve possuir somente um destes valores:

Baixo

Médio

Alto

Crítico

========================

NivelRisco deve possuir somente um destes valores:

Baixo

Médio

Alto

Crítico

========================

Economia.Nivel deve possuir somente:

Baixa

Média

Alta

Muito Alta

========================

Os indicadores devem variar entre 0 e 100.

PreparacaoFiscal

PreparacaoTecnologica

Compliance

Processos

========================

Plano de ação

30 dias

90 dias

180 dias

========================

Retorne exatamente este JSON:

{
  "score":0,

  "nivelRisco":"",

  "impacto":"",

  "resumoExecutivo":"",

  "principaisRiscos":[

  ],

  "oportunidades":[

  ],

  "recomendacoes":[

  ],

  "acoesImediatas":[

  ],

  "planoAcao":{

      "dias30":[

      ],

      "dias90":[

      ],

      "dias180":[

      ]

  },

  "indicadores":{

      "preparacaoFiscal":0,

      "preparacaoTecnologica":0,

      "compliance":0,

      "processos":0

  },

  "economia":{

      "nivel":"",

      "faixaEstimada":"",

      "justificativa":""

  }

}
""");

        sb.AppendLine($"Razão Social: {empresa.RazaoSocial}");
        sb.AppendLine($"Nome Fantasia: {empresa.NomeFantasia}");
        sb.AppendLine($"CNPJ: {empresa.Cnpj}");
        sb.AppendLine($"Regime Tributário: {empresa.RegimeTributario}");
        sb.AppendLine($"CNAE Principal: {empresa.CnaePrincipal}");

        if (empresa.CnaesSecundarios.Any())
        {
            sb.AppendLine("CNAEs Secundários:");

            foreach (var cnae in empresa.CnaesSecundarios)
                sb.AppendLine($"- {cnae}");
        }

        sb.AppendLine($"Cidade: {empresa.Cidade}");
        sb.AppendLine($"UF: {empresa.Uf}");
        sb.AppendLine($"Faturamento Anual: R$ {empresa.FaturamentoAnual:N2}");
        sb.AppendLine($"Situação: {empresa.Situacao}");

        if (!string.IsNullOrWhiteSpace(empresa.Observacoes))
        {
            sb.AppendLine();
            sb.AppendLine("Observações:");
            sb.AppendLine(empresa.Observacoes);
        }

        sb.AppendLine();

        sb.AppendLine("""
Analise a empresa considerando os seguintes pontos:

1. Qual o impacto da Reforma Tributária para esta empresa?

2. O regime tributário atual continua adequado?

3. Existe risco de aumento da carga tributária?

4. Existem oportunidades para reduzir impostos legalmente?

5. Quais créditos de CBS e IBS poderão ser aproveitados?

6. Será necessário atualizar ERP, emissão de notas ou sistemas fiscais?

7. O faturamento informado é compatível com o regime tributário?

8. Quais são os principais riscos fiscais?

9. Quais oportunidades tributárias existem?

10. Quais ações devem ser realizadas imediatamente?

11. Monte um plano de ação dividido em:

- 30 dias
- 90 dias
- 180 dias

12. Informe uma nota de preparação da empresa de 0 a 100 considerando:

- Preparação Fiscal
- Preparação Tecnológica
- Compliance
- Processos

13. Calcule o Score da empresa entre 0 e 100.

Regras do Score:

0-20 = Empresa extremamente despreparada

21-40 = Baixa preparação

41-60 = Preparação intermediária

61-80 = Boa preparação

81-100 = Empresa muito preparada

Nunca gere um score aleatório.

Sempre justifique a avaliação através dos riscos e oportunidades encontrados.

Retorne exclusivamente um JSON válido.
""");

        return sb.ToString();
    }
}