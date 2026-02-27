using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using WhatsAppCrm.Web.Entities;

namespace WhatsAppCrm.Web.Data;

public static class DatabaseSeeder
{
    private static readonly string[] Names =
    [
        "Ricardo Mendes", "Fernanda Albuquerque", "Carlos Eduardo Silva", "Patrícia Rocha", "Marcos Tavares",
        "Juliana Ferreira", "André Nascimento", "Camila Santana", "Roberto Gomes", "Luciana Barros",
        "Thiago Martins", "Daniela Correia", "Gustavo Peixoto", "Vanessa Lima", "Rodrigo Azevedo",
        "Beatriz Campos", "Leonardo Duarte", "Mariana Teixeira", "Paulo Henrique Costa", "Aline Ribeiro",
        "Fábio Monteiro", "Renata Cardoso", "Eduardo Nunes", "Cláudia Vieira", "Alexandre Borges",
        "Tatiana Freitas", "Diego Sampaio", "Isabela Andrade", "Márcio Cunha", "Priscila Ramos",
        "Bruno Cavalcanti", "Larissa Medeiros", "Sérgio Guimarães", "Natália Lopes", "Jorge Batista",
        "Carolina Pinto", "Rafael Moreira", "Amanda Fonseca", "Felipe Nogueira", "Gabriela Araújo",
        "Henrique Santos", "Simone Pereira", "Vinícius Dias", "Adriana Castro", "Leandro Oliveira",
        "Cristina Melo", "Rogério Souza", "Débora Barbosa", "Marcelo Reis", "Fernanda Torres"
    ];

    private static readonly string[] Tags = ["lead", "cliente", "vip", "inativo", "prospecto", "construtora", "locadora", "prestadora", "pme"];

    private static readonly string[] MessagesIn =
    [
        "Oi, vi o site de vocês. O Obraxs funciona para construtoras de médio porte?",
        "Quanto custa o plano para 15 usuários?",
        "Vocês integram com o sistema contábil que já usamos?",
        "Precisamos de controle de medição de obra. O sistema faz isso?",
        "Quero agendar uma demonstração para minha equipe.",
        "O sistema roda no celular? Meus engenheiros ficam no canteiro o dia todo.",
        "Hoje usamos tudo em planilha e está um caos. Vocês ajudam na migração?",
        "Qual o prazo de implantação do Obraxs?",
        "Temos 8 obras simultâneas. Consigo ver o financeiro de todas num dashboard?",
        "O módulo de compras faz cotação com fornecedores automaticamente?",
        "Como funciona o controle de contratos e aditivos?",
        "Preciso de relatório de rentabilidade por obra. Tem isso?",
        "Vocês têm clientes no segmento de locação de equipamentos?",
        "O sistema controla manutenção preventiva de equipamentos?",
        "Estamos comparando o Obraxs com o Sienge. Qual o diferencial?",
        "Podem enviar a proposta comercial por email?",
        "O Obraxs é seguro? Temos dados sensíveis de contratos.",
        "Quero entender melhor o módulo de tesouraria.",
        "Vocês fazem treinamento da equipe na implantação?",
        "Preciso de controle de estoque por obra. O sistema faz isso?"
    ];

    private static readonly string[] MessagesOut =
    [
        "Olá! O Obraxs foi feito especificamente para construção civil. Perfeito para médio porte!",
        "Para 15 usuários, o investimento fica em torno de R$ 2.800/mês com todos os módulos. Posso detalhar?",
        "Sim! Temos API aberta e integrações prontas com os principais sistemas contábeis do mercado.",
        "Com certeza! O módulo de Medições de Obra é um dos nossos destaques. Medição digital com aprovação online.",
        "Ótimo! A demo é personalizada e gratuita. Prefere de manhã ou à tarde?",
        "Sim! O Obraxs tem app mobile nativo. O engenheiro registra medição, aprova compra e consulta contrato pelo celular.",
        "Fazemos toda a migração assistida. Importamos dados das planilhas e treinamos a equipe. Sem perda de histórico!",
        "A implantação média leva 30 dias. Temos 4 etapas: Setup → Contratos → Operação → Análise.",
        "Exato! O dashboard executivo mostra visão consolidada de todas as obras, contratos e financeiro em tempo real.",
        "O fluxo é completo: solicitação → cotação automática → comparativo → pedido → recebimento → inventário.",
        "Controle total: proposta, aprovação, assinatura digital, aditivos com versionamento e alertas de vencimento.",
        "Temos relatórios de rentabilidade por obra, por contrato e por centro de custo. Exporta PDF e Excel.",
        "Sim! Atendemos construtoras, locadoras de equipamentos e prestadoras de serviços técnicos.",
        "O módulo de manutenção preventiva agenda automaticamente com base em horas de uso ou tempo. Alertas automáticos!",
        "O Obraxs é focado em contratos e operação de campo. Temos medição digital, manutenção preventiva e app mobile nativos.",
        "Claro! Me confirma o email que envio a proposta detalhada ainda hoje.",
        "Total segurança: backup automático, criptografia, controle de permissões por perfil e compliance LGPD.",
        "A Tesouraria inclui fluxo de caixa, contas a pagar/receber, conciliação bancária e dashboards financeiros.",
        "O treinamento é incluso! Capacitamos toda a equipe e acompanhamos os primeiros 90 dias de uso.",
        "Sim! Controle de estoque por obra com transferência entre canteiros, inventário e alertas de estoque mínimo."
    ];

    private static readonly string[] DealTitles =
    [
        "Implantação Obraxs - MRV Engenharia", "Migração ERP - Construtora Cyrela", "Módulo Financeiro - Locadora ABC",
        "Demo + Proposta - Tenda Construtora", "Treinamento Equipe - JBS Construções", "Plano Enterprise - Eztec",
        "Integração Contábil - Patriani", "App Mobile Setup - Helbor", "Módulo Compras - Direcional",
        "Controle Medições - Gafisa", "Manutenção Preventiva - Locadora XYZ", "Dashboard Financeiro - Even",
        "Consultoria Implantação - Lavvi", "Módulo Estoque - Mills Locação", "Tesouraria + Conciliação - Viver Inc.",
        "Gestão Contratos - Cury Construtora", "Módulo Orçamentos - Moura Dubeux", "Setup Completo - Pacaembu Const.",
        "POC 30 dias - Tecnisa", "Expansão Licenças - Tegra Inc.", "Suporte Premium - RNI Negócios",
        "Integração Bancária - Plano&Plano", "Controle de Obras - MPD Engenharia", "Módulo Cotações - Kallas Inc.",
        "App Canteiro - Método Engenharia", "Setup Multi-obra - Vitacon", "Centro de Custos - Mitre Realty",
        "Relatórios Gerenciais - Croma", "Modulo Inventário - Solaris Const.", "Piloto 3 Obras - Habitasul"
    ];

    private static readonly (string Name, string Category, string Content)[] TemplateContents =
    [
        ("Boas-vindas Obraxs", "utility", "Olá {{nome}}! Bem-vindo(a) ao Obraxs. Sua plataforma de gestão para construção civil está ativa. Qualquer dúvida, estamos aqui!"),
        ("Follow-up Demo", "marketing", "Oi {{nome}}, como foi a demonstração do Obraxs? Gostaria de agendar uma conversa para esclarecer dúvidas sobre os módulos?"),
        ("Proposta Enviada", "utility", "{{nome}}, a proposta #{{numero}} do Obraxs foi enviada para {{email}}. Inclui módulos, valores e cronograma de implantação."),
        ("Lembrete Demo", "utility", "Lembrete: sua demonstração do Obraxs com {{consultor}} está agendada para {{data}} às {{hora}}. Link: {{link}}"),
        ("Oferta Implantação", "marketing", "{{nome}}, até o final do mês: implantação com 50% de desconto + 3 meses de suporte premium grátis. Responda para saber mais!"),
        ("NPS Obraxs", "utility", "Oi {{nome}}! De 0 a 10, o quanto você recomendaria o Obraxs? Sua opinião nos ajuda a melhorar."),
        ("Reativação Lead", "marketing", "{{nome}}, vimos que você conheceu o Obraxs há um tempo. Temos novidades nos módulos de medição e manutenção. Bora conversar?"),
        ("Webinar Construção", "marketing", "{{nome}}, participe do webinar \"Gestão Digital de Obras com o Obraxs\" no dia {{data}}. Vagas limitadas! Inscreva-se."),
        ("Treinamento Agendado", "utility", "{{nome}}, o treinamento do Obraxs para sua equipe está confirmado para {{data}}. Duração: 2h. Link de acesso: {{link}}"),
        ("Suporte Ticket", "utility", "Seu chamado #{{ticket}} foi registrado no suporte Obraxs. Prazo de resposta: {{prazo}}h. Acompanhe em {{link}}.")
    ];

    private static readonly string[] FeedbackTexts =
    [
        "Cliente demonstrou alto interesse no módulo de medições",
        "Agendada demo para a equipe de engenharia",
        "Follow-up realizado, aguardando retorno sobre proposta",
        "Reunião produtiva, decisão prevista para próxima semana",
        "Solicitou proposta formal com valores detalhados",
        "Interesse em integração com sistema legado",
        "Realizou demo e ficou impressionado com app mobile",
        "Está comparando com concorrente, enviar diferencial",
        "Pediu referências de clientes no mesmo segmento",
        "CEO aprovou POC de 30 dias",
        "Equipe técnica validou requisitos",
        "Negociação de desconto para pagamento anual",
        "Treinamento da equipe agendado",
        "Contrato em revisão pelo jurídico",
        "Go-live previsto para próximo mês"
    ];

    private static readonly Random Rng = new();

    public static async Task SeedAsync(AppDbContext db)
    {
        // Delete all in FK order (new entities first)
        db.Conversions.RemoveRange(db.Conversions);
        db.CampaignSpendDailies.RemoveRange(db.CampaignSpendDailies);
        db.ContactFeedbacks.RemoveRange(db.ContactFeedbacks);
        db.CampaignMessages.RemoveRange(db.CampaignMessages);
        db.Campaigns.RemoveRange(db.Campaigns);
        db.Templates.RemoveRange(db.Templates);
        db.Messages.RemoveRange(db.Messages);
        db.Conversations.RemoveRange(db.Conversations);
        db.Deals.RemoveRange(db.Deals);
        db.Stages.RemoveRange(db.Stages);
        db.Pipelines.RemoveRange(db.Pipelines);
        db.Contacts.RemoveRange(db.Contacts);
        await db.SaveChangesAsync();

        // Templates
        foreach (var t in TemplateContents)
        {
            db.Templates.Add(new Template { Name = t.Name, Category = t.Category, Content = t.Content, Status = "approved" });
        }
        await db.SaveChangesAsync();

        // === Campaigns FIRST (needed for LeadCampaignId on contacts) ===
        var utmSources = new[] { "google_ads", "meta_ads", "linkedin_ads", "whatsapp", "referral", "organic" };

        // 3 existing WhatsApp campaigns
        var completedCampaign = new Campaign
        {
            Name = "Lançamento Módulo Medições 2.0",
            TemplateText = "Olá {{nome}}! O novo módulo de Medições de Obra do Obraxs está no ar. Medição digital com aprovação online. Agende uma demo gratuita!",
            Status = "completed",
            Platform = "whatsapp",
            UtmSource = "whatsapp",
            UtmMedium = "campaign",
            UtmCampaign = "medicoes_2_0",
            RateLimit = 20,
            StartedAt = RandomDate(30),
            CompletedAt = RandomDate(25)
        };
        db.Campaigns.Add(completedCampaign);

        var runningCampaign = new Campaign
        {
            Name = "Webinar: Gestão Digital de Obras",
            TemplateText = "{{nome}}, participe do webinar \"Elimine planilhas da sua construtora com o Obraxs\" no dia 15/03! Vagas limitadas. Responda SIM para inscrição.",
            Status = "running",
            Platform = "whatsapp",
            UtmSource = "whatsapp",
            UtmMedium = "campaign",
            UtmCampaign = "webinar_gestao_digital",
            RateLimit = 10,
            StartedAt = DateTime.UtcNow
        };
        db.Campaigns.Add(runningCampaign);

        var draftCampaign = new Campaign
        {
            Name = "Follow-up Leads Construção",
            TemplateText = "Oi {{nome}}, tudo bem? Você conheceu o Obraxs recentemente. Temos condições especiais para implantação este mês. Quer saber mais?",
            Status = "draft",
            Platform = "whatsapp",
            UtmSource = "whatsapp",
            UtmMedium = "campaign",
            UtmCampaign = "followup_leads",
            RateLimit = 30,
            AudienceFilter = JsonSerializer.Serialize(new { tags = new[] { "lead", "prospecto", "construtora" } })
        };
        db.Campaigns.Add(draftCampaign);

        // 3 NEW paid campaigns
        var googleAdsCampaign = new Campaign
        {
            Name = "Google Ads - Obraxs ERP Construção",
            TemplateText = "",
            Status = "completed",
            Platform = "google_ads",
            UtmSource = "google_ads",
            UtmMedium = "cpc",
            UtmCampaign = "erp_construcao_2026",
            ExternalId = "GA-12345678",
            StartedAt = RandomDate(60),
            CompletedAt = RandomDate(5)
        };
        db.Campaigns.Add(googleAdsCampaign);

        var metaAdsCampaign = new Campaign
        {
            Name = "Meta Ads - Gestão de Obras",
            TemplateText = "",
            Status = "completed",
            Platform = "meta_ads",
            UtmSource = "meta_ads",
            UtmMedium = "paid_social",
            UtmCampaign = "gestao_obras_q1",
            ExternalId = "23854123456789",
            StartedAt = RandomDate(45),
            CompletedAt = RandomDate(3)
        };
        db.Campaigns.Add(metaAdsCampaign);

        var linkedInCampaign = new Campaign
        {
            Name = "LinkedIn Ads - ERP Engenharia",
            TemplateText = "",
            Status = "running",
            Platform = "linkedin_ads",
            UtmSource = "linkedin_ads",
            UtmMedium = "sponsored",
            UtmCampaign = "erp_engenharia_b2b",
            ExternalId = "LI-98765432",
            StartedAt = RandomDate(30)
        };
        db.Campaigns.Add(linkedInCampaign);

        await db.SaveChangesAsync();

        var paidCampaigns = new[] { googleAdsCampaign, metaAdsCampaign, linkedInCampaign };
        var allCampaigns = new[] { completedCampaign, runningCampaign, draftCampaign, googleAdsCampaign, metaAdsCampaign, linkedInCampaign };

        // === Contacts (60% with UTM) ===
        var contacts = new List<Contact>();
        for (var i = 0; i < 50; i++)
        {
            var hasUtm = i < 30; // 60% with UTM source
            var utmSource = hasUtm ? utmSources[i % utmSources.Length] : null;
            Campaign? leadCampaign = null;

            if (hasUtm && utmSource is "google_ads" or "meta_ads" or "linkedin_ads")
            {
                leadCampaign = utmSource switch
                {
                    "google_ads" => googleAdsCampaign,
                    "meta_ads" => metaAdsCampaign,
                    "linkedin_ads" => linkedInCampaign,
                    _ => null
                };
            }

            var contact = new Contact
            {
                Name = Names[i],
                Phone = RandomPhone(),
                Email = $"{RemoveDiacritics(Names[i]).ToLowerInvariant().Replace(' ', '.')}@email.com",
                Tags = RandomTags(),
                OptedOut = i >= 47,
                OptOutAt = i >= 47 ? RandomDate(10) : null,
                UtmSource = utmSource,
                UtmMedium = hasUtm ? (utmSource is "google_ads" ? "cpc" : utmSource is "meta_ads" ? "paid_social" : utmSource is "linkedin_ads" ? "sponsored" : "campaign") : null,
                UtmCampaign = hasUtm ? (leadCampaign?.UtmCampaign ?? "organic_inbound") : null,
                LeadCampaignId = leadCampaign?.Id
            };
            db.Contacts.Add(contact);
            contacts.Add(contact);
        }
        await db.SaveChangesAsync();

        // Conversations + Messages
        var assignees = new[] { "Carlos", "Marina", "Roberto", null };
        var statuses = new[] { "sent", "delivered", "read" };
        for (var i = 0; i < 20; i++)
        {
            var status = i < 12 ? "open" : i < 17 ? "waiting" : "closed";
            var conv = new Conversation
            {
                ContactId = contacts[i].Id,
                Status = status,
                AssignedTo = assignees[Rng.Next(assignees.Length)],
                Channel = "whatsapp",
                UnreadCount = i < 12 ? Rng.Next(5) : 0
            };
            db.Conversations.Add(conv);
            await db.SaveChangesAsync();

            var msgCount = Rng.Next(5, 16);
            var lastMsgDate = RandomDate(14);
            for (var m = 0; m < msgCount; m++)
            {
                var isInbound = m % 2 == 0;
                lastMsgDate = lastMsgDate.AddMilliseconds(Rng.Next(3600000));
                db.Messages.Add(new Message
                {
                    ConversationId = conv.Id,
                    Direction = isInbound ? "inbound" : "outbound",
                    Content = isInbound
                        ? MessagesIn[Rng.Next(MessagesIn.Length)]
                        : MessagesOut[Rng.Next(MessagesOut.Length)],
                    Status = isInbound ? "read" : statuses[Rng.Next(statuses.Length)],
                    CreatedAt = lastMsgDate
                });
            }
            conv.LastMessageAt = lastMsgDate;
            await db.SaveChangesAsync();
        }

        // Campaign Messages for WhatsApp campaigns
        var campaignStatuses = new[] { "sent", "delivered", "read", "replied" };
        for (var i = 0; i < 15; i++)
        {
            db.CampaignMessages.Add(new CampaignMessage
            {
                CampaignId = completedCampaign.Id,
                ContactId = contacts[i].Id,
                Status = campaignStatuses[Rng.Next(campaignStatuses.Length)],
                SentAt = RandomDate(28),
                DeliveredAt = RandomDate(28),
                ReadAt = Rng.NextDouble() > 0.3 ? RandomDate(27) : null,
                RepliedAt = Rng.NextDouble() > 0.7 ? RandomDate(26) : null
            });
        }
        for (var i = 15; i < 35; i++)
        {
            db.CampaignMessages.Add(new CampaignMessage
            {
                CampaignId = runningCampaign.Id,
                ContactId = contacts[i % 47].Id,
                Status = i < 25 ? "delivered" : "pending",
                SentAt = i < 25 ? DateTime.UtcNow : null,
                DeliveredAt = i < 22 ? DateTime.UtcNow : null
            });
        }
        await db.SaveChangesAsync();

        // Pipeline + Stages
        var pipeline = new Pipeline { Name = "Pipeline Comercial Obraxs" };
        db.Pipelines.Add(pipeline);
        await db.SaveChangesAsync();

        var stageData = new (string Name, int Order, string Color)[]
        {
            ("Novo Lead", 0, "#94a3b8"),
            ("Demo Agendada", 1, "#60a5fa"),
            ("Qualificado", 2, "#a78bfa"),
            ("Proposta Enviada", 3, "#fbbf24"),
            ("Negociação", 4, "#f97316"),
            ("Fechado/Ganho", 5, "#22c55e")
        };

        var stages = new List<Stage>();
        foreach (var s in stageData)
        {
            var stage = new Stage { PipelineId = pipeline.Id, Name = s.Name, Order = s.Order, Color = s.Color };
            db.Stages.Add(stage);
            stages.Add(stage);
        }
        await db.SaveChangesAsync();

        // Deals — distribute across stages, some in "Fechado/Ganho"
        var deals = new List<Deal>();
        for (var i = 0; i < 30; i++)
        {
            var stageIdx = i < 8 ? 5 : Rng.Next(6); // First 8 deals are won
            var deal = new Deal
            {
                Title = DealTitles[i],
                ContactId = contacts[i % 30].Id,
                StageId = stages[stageIdx].Id,
                Value = Rng.Next(10000, 90000),
                Notes = i % 3 == 0 ? "Empresa demonstrou alto interesse nos módulos de medição e contratos" : null
            };
            db.Deals.Add(deal);
            deals.Add(deal);
        }
        await db.SaveChangesAsync();

        // === CampaignSpendDaily (~80 records, 30 days × 3 paid campaigns) ===
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        foreach (var campaign in paidCampaigns)
        {
            var baseDailySpend = campaign.Platform switch
            {
                "google_ads" => 150.0,
                "meta_ads" => 120.0,
                "linkedin_ads" => 200.0,
                _ => 100.0
            };

            for (var d = 0; d < 30; d++)
            {
                var date = today.AddDays(-29 + d);
                var variation = 0.7 + Rng.NextDouble() * 0.6; // 70%-130% variation
                db.CampaignSpendDailies.Add(new CampaignSpendDaily
                {
                    CampaignId = campaign.Id,
                    Date = date,
                    Amount = Math.Round(baseDailySpend * variation, 2)
                });
            }
        }
        await db.SaveChangesAsync();

        // === Conversions — for won deals (stage "Fechado/Ganho", order 5) ===
        var wonStage = stages.First(s => s.Order == 5);
        var wonDeals = deals.Where(d => d.StageId == wonStage.Id).ToList();
        foreach (var deal in wonDeals)
        {
            var contact = contacts.First(c => c.Id == deal.ContactId);
            db.Conversions.Add(new Conversion
            {
                ContactId = contact.Id,
                CampaignId = contact.LeadCampaignId, // link to campaign if UTM-attributed
                DealId = deal.Id,
                Value = deal.Value,
                ConvertedAt = RandomDate(20)
            });
        }
        await db.SaveChangesAsync();

        // === ContactFeedbacks (~30 for first 15 contacts) ===
        var feedbackAuthors = new[] { "Carlos (Vendas)", "Marina (CS)", "Roberto (Gerente)", "Ana (SDR)" };
        var feedbackTypes = new[] { "note", "call", "meeting", "task" };
        for (var i = 0; i < 15; i++)
        {
            var count = Rng.Next(1, 4); // 1-3 feedbacks per contact
            for (var f = 0; f < count; f++)
            {
                db.ContactFeedbacks.Add(new ContactFeedback
                {
                    ContactId = contacts[i].Id,
                    Author = feedbackAuthors[Rng.Next(feedbackAuthors.Length)],
                    Text = FeedbackTexts[Rng.Next(FeedbackTexts.Length)],
                    Type = feedbackTypes[Rng.Next(feedbackTypes.Length)],
                    CreatedAt = RandomDate(30)
                });
            }
        }
        await db.SaveChangesAsync();
    }

    private static string RandomPhone()
    {
        var ddds = new[] { 11, 21, 31, 41, 51, 61, 71, 81, 19, 27 };
        var ddd = ddds[Rng.Next(ddds.Length)];
        var num = Rng.Next(100000000, 999999999);
        return $"+55{ddd}9{num}";
    }

    private static string RandomTags()
    {
        var count = Rng.Next(1, 4);
        var shuffled = Tags.OrderBy(_ => Rng.Next()).Take(count);
        return JsonSerializer.Serialize(shuffled.ToArray());
    }

    private static DateTime RandomDate(int daysBack)
    {
        var d = DateTime.UtcNow;
        d = d.AddDays(-Rng.Next(daysBack));
        d = new DateTime(d.Year, d.Month, d.Day, Rng.Next(8, 20), Rng.Next(60), 0, DateTimeKind.Utc);
        return d;
    }

    private static string RemoveDiacritics(string text)
    {
        var normalized = text.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(normalized.Length);
        foreach (var c in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        }
        return sb.ToString().Normalize(NormalizationForm.FormC);
    }
}
