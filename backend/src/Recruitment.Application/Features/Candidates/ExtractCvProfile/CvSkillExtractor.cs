using System.Text.RegularExpressions;

namespace Recruitment.Application.Features.Candidates.ExtractCvProfile;

/// <summary>
/// CV metninden yetenekleri (teknoloji, araç, yöntem) bulur. İki kaynak kullanılır: bilinen yetenek
/// listesiyle metinde geçen adlar ve "Yetenekler / Skills" gibi bir bölümdeki maddeler. Tahmine
/// dayanır; sonuç adayın etiketlerine önerilir, kullanıcı düzeltebilir.
/// </summary>
internal static class CvSkillExtractor
{
    public const int MaxSkills = 15;

    private const int MaxSectionLines = 12;
    private const int MaxSkillLength = 30;
    private const int MaxSkillWords = 4;

    // Gösterilecek ad ve metinde aranacak yazılışlar (büyük/küçük harf fark etmez).
    // Tek harfli ve çok genel adlar (C, R, Go) yanlış eşleşeceği için yalnızca bölüm maddesi olarak okunur.
    private static readonly (string Name, string[] Aliases)[] Known =
    [
        ("C#", ["C#", "C Sharp"]), ("C++", ["C++"]), ("Java", ["Java"]), ("Kotlin", ["Kotlin"]), ("Swift", ["Swift"]),
        ("Python", ["Python"]), ("JavaScript", ["JavaScript", "ECMAScript"]), ("TypeScript", ["TypeScript"]),
        ("PHP", ["PHP"]), ("Ruby", ["Ruby"]), ("Rust", ["Rust"]), ("Golang", ["Golang"]), ("Scala", ["Scala"]),
        ("Dart", ["Dart"]), ("Flutter", ["Flutter"]), ("React", ["React", "React.js", "ReactJS"]), ("React Native", ["React Native"]),
        ("Angular", ["Angular", "AngularJS"]), ("Vue.js", ["Vue", "Vue.js", "VueJS"]), ("Next.js", ["Next.js", "NextJS"]),
        ("Node.js", ["Node.js", "NodeJS", "Node js"]), ("Express", ["Express.js", "ExpressJS"]), ("jQuery", ["jQuery"]),
        ("HTML", ["HTML", "HTML5"]), ("CSS", ["CSS", "CSS3"]), ("Sass", ["Sass", "SCSS"]), ("Tailwind CSS", ["Tailwind"]),
        ("Bootstrap", ["Bootstrap"]), ("Redux", ["Redux"]), (".NET", [".NET", "dotnet", "DotNet"]), (".NET Core", [".NET Core", "dotnet core"]),
        ("ASP.NET", ["ASP.NET", "ASP.NET MVC"]), ("ASP.NET Core", ["ASP.NET Core"]), ("Entity Framework", ["Entity Framework", "EF Core"]),
        ("Dapper", ["Dapper"]), ("LINQ", ["LINQ"]), ("Blazor", ["Blazor"]), ("WPF", ["WPF"]), ("WinForms", ["WinForms", "Windows Forms"]),
        ("Spring Boot", ["Spring Boot", "SpringBoot"]), ("Spring", ["Spring Framework"]), ("Hibernate", ["Hibernate"]), ("Django", ["Django"]),
        ("Flask", ["Flask"]), ("FastAPI", ["FastAPI"]), ("Laravel", ["Laravel"]), ("Symfony", ["Symfony"]),
        ("SQL", ["SQL"]), ("T-SQL", ["T-SQL", "TSQL"]), ("PL/SQL", ["PL/SQL", "PLSQL"]), ("PostgreSQL", ["PostgreSQL", "Postgres"]),
        ("PostGIS", ["PostGIS"]), ("MySQL", ["MySQL"]), ("MariaDB", ["MariaDB"]), ("MS SQL Server", ["MSSQL", "MS SQL", "SQL Server"]),
        ("Oracle", ["Oracle"]), ("SQLite", ["SQLite"]), ("MongoDB", ["MongoDB", "Mongo DB"]), ("Redis", ["Redis"]),
        ("Elasticsearch", ["Elasticsearch", "Elastic Search"]), ("Cassandra", ["Cassandra"]), ("Neo4j", ["Neo4j"]),
        ("RabbitMQ", ["RabbitMQ"]), ("Kafka", ["Kafka"]), ("gRPC", ["gRPC"]), ("GraphQL", ["GraphQL"]), ("REST API", ["REST API", "RESTful"]),
        ("SignalR", ["SignalR"]), ("WebSocket", ["WebSocket", "WebSockets"]), ("Microservices", ["Microservices", "Mikroservis", "Mikro servis"]),
        ("Docker", ["Docker"]), ("Kubernetes", ["Kubernetes", "K8s"]), ("Helm", ["Helm"]), ("Linux", ["Linux", "Ubuntu"]),
        ("Nginx", ["Nginx"]), ("IIS", ["IIS"]), ("Git", ["Git"]), ("GitHub", ["GitHub"]), ("GitLab", ["GitLab"]), ("Bitbucket", ["Bitbucket"]),
        ("Azure DevOps", ["Azure DevOps", "TFS"]), ("CI/CD", ["CI/CD", "CI CD"]), ("Jenkins", ["Jenkins"]), ("Terraform", ["Terraform"]),
        ("Ansible", ["Ansible"]), ("AWS", ["AWS", "Amazon Web Services"]), ("Azure", ["Azure"]), ("Google Cloud", ["Google Cloud", "GCP"]),
        ("Jira", ["Jira"]), ("Confluence", ["Confluence"]), ("Postman", ["Postman"]), ("Swagger", ["Swagger", "OpenAPI"]),
        ("Scrum", ["Scrum"]), ("Agile", ["Agile", "Çevik"]), ("Kanban", ["Kanban"]), ("TDD", ["TDD"]), ("DDD", ["Domain Driven Design"]),
        ("Clean Architecture", ["Clean Architecture"]), ("SOLID", ["SOLID"]), ("Design Patterns", ["Design Patterns", "Tasarım Desenleri"]),
        ("xUnit", ["xUnit"]), ("NUnit", ["NUnit"]), ("Jest", ["Jest"]), ("Selenium", ["Selenium"]), ("Cypress", ["Cypress"]),
        ("QGIS", ["QGIS"]), ("ArcGIS", ["ArcGIS"]), ("GIS", ["GIS", "CBS", "Coğrafi Bilgi Sistemleri"]), ("OpenLayers", ["OpenLayers"]),
        ("Leaflet", ["Leaflet"]), ("GeoServer", ["GeoServer"]), ("Mapbox", ["Mapbox"]),
        ("Machine Learning", ["Machine Learning", "Makine Öğrenmesi", "Makine Öğrenimi"]), ("Deep Learning", ["Deep Learning", "Derin Öğrenme"]),
        ("TensorFlow", ["TensorFlow"]), ("PyTorch", ["PyTorch"]), ("Pandas", ["Pandas"]), ("NumPy", ["NumPy"]),
        ("Power BI", ["Power BI", "PowerBI"]), ("Tableau", ["Tableau"]), ("Excel", ["MS Excel", "Microsoft Excel"]), ("Figma", ["Figma"]),
        ("Unity", ["Unity"]), ("Unreal Engine", ["Unreal Engine"]), ("Android", ["Android"]), ("iOS", ["iOS"]),
        ("Hangfire", ["Hangfire"]), ("Serilog", ["Serilog"]), ("MediatR", ["MediatR"]), ("AutoMapper", ["AutoMapper"]),
        ("OAuth", ["OAuth", "OAuth2"]), ("JWT", ["JWT"]), ("LDAP", ["LDAP"]), ("Keycloak", ["Keycloak"]),
    ];

    private static readonly Regex[][] KnownPatterns = Known
        .Select(skill => skill.Aliases
            .Select(alias => new Regex(
                $@"(?<![\p{{L}}\p{{N}}_]){Regex.Escape(alias)}(?![\p{{L}}\p{{N}}_]|\+|#)",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(200)))
            .ToArray())
        .ToArray();

    private static readonly string[] SectionHeaders =
    [
        "yetenekler", "teknik yetenekler", "yetenek", "beceriler", "teknik beceriler", "yetkinlikler", "teknik yetkinlikler",
        "teknolojiler", "kullandığı teknolojiler", "araçlar", "skills", "technical skills", "key skills", "core skills",
        "technologies", "tech stack", "tools", "competencies", "expertise",
    ];

    private static readonly string[] OtherHeaders =
    [
        "deneyim", "iş deneyimi", "iş deneyimleri", "çalışma geçmişi", "eğitim", "eğitim bilgileri", "projeler", "sertifikalar",
        "sertifikalar ve kurslar", "kurslar", "referanslar", "diller", "yabancı dil", "hobiler", "ilgi alanları", "kişisel bilgiler",
        "hakkımda", "özet", "profil", "iletişim", "experience", "work experience", "employment", "education", "projects",
        "certificates", "certifications", "references", "languages", "interests", "hobbies", "summary", "about me", "profile",
        "contact", "personal information",
    ];

    private static readonly Regex SeparatorPattern = new(@"[,;|•·●▪◦■□►➢✓✔•●▪]+|\s{3,}", RegexOptions.Compiled);

    public static IReadOnlyList<string> Extract(IReadOnlyList<string> lines)
    {
        var found = new List<(int Position, string Name)>();

        var text = string.Join('\n', lines);
        for (var i = 0; i < Known.Length; i++)
        {
            var position = KnownPatterns[i].Select(pattern => FirstIndex(pattern, text)).Where(p => p >= 0).DefaultIfEmpty(-1).Min();
            if (position >= 0)
                found.Add((position, Known[i].Name));
        }

        // Bölümdeki maddeler bilinen listede olmasa da alınır; metindeki sıralarını korumak için satır konumu kullanılır.
        var offset = 0;
        var lineOffsets = lines.Select(line => { var start = offset; offset += line.Length + 1; return start; }).ToList();
        for (var i = 0; i < lines.Count; i++)
        {
            if (!IsSectionStart(lines[i], out var inline))
                continue;

            foreach (var item in SplitItems(inline))
                found.Add((lineOffsets[i], Canonical(item)));

            for (var j = i + 1; j < lines.Count && j <= i + MaxSectionLines; j++)
            {
                if (IsHeader(lines[j]))
                    break;

                foreach (var item in SplitItems(lines[j]))
                    found.Add((lineOffsets[j], Canonical(item)));
            }
        }

        // "ASP.NET Core" metni "ASP.NET" olarak da eşleşir; aynı yerde daha uzun ad varsa kısası atılır.
        found.RemoveAll(entry => found.Any(other =>
            other.Position == entry.Position && other.Name.StartsWith(entry.Name + " ", StringComparison.Ordinal)));

        var skills = found
            .OrderBy(entry => entry.Position)
            .Select(entry => entry.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(MaxSkills)
            .ToList();

        // Boş sonuç, CvProfile.Empty ile karşılaştırılabilsin diye ortak boş liste olarak döner.
        return skills.Count == 0 ? Array.Empty<string>() : skills;
    }

    private static int FirstIndex(Regex pattern, string text)
    {
        try
        {
            var match = pattern.Match(text);
            return match.Success ? match.Index : -1;
        }
        catch (RegexMatchTimeoutException)
        {
            return -1;
        }
    }

    // "Yetenekler" tek başına satırda veya "Yetenekler: C#, SQL" biçiminde olabilir.
    private static bool IsSectionStart(string line, out string inline)
    {
        inline = string.Empty;
        var trimmed = line.Trim();
        var colon = trimmed.IndexOf(':');
        var head = Normalize(colon >= 0 ? trimmed[..colon] : trimmed);
        if (!SectionHeaders.Contains(head))
            return false;

        if (colon >= 0)
            inline = trimmed[(colon + 1)..];

        return true;
    }

    private static bool IsHeader(string line)
    {
        var trimmed = line.Trim().TrimEnd(':');
        return trimmed.Length <= 40 && (OtherHeaders.Contains(Normalize(trimmed)) || SectionHeaders.Contains(Normalize(trimmed)));
    }

    private static IEnumerable<string> SplitItems(string text) =>
        SeparatorPattern.Split(text)
            .Select(item => item.Trim().Trim('-', '–', '—', '*', '.', ':', ' '))
            .Where(IsPlausibleSkill);

    private static bool IsPlausibleSkill(string item) =>
        item.Length is >= 2 and <= MaxSkillLength
        && item.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length <= MaxSkillWords
        && item.Any(char.IsLetter)
        && !item.Contains('@') && !item.Contains("http", StringComparison.OrdinalIgnoreCase)
        && !item.Contains("www.", StringComparison.OrdinalIgnoreCase);

    // Bilinen bir yetenekse standart yazılışıyla ("postgres" → "PostgreSQL"), değilse olduğu gibi döner.
    private static string Canonical(string item)
    {
        for (var i = 0; i < Known.Length; i++)
        {
            if (Known[i].Aliases.Append(Known[i].Name).Any(alias => string.Equals(alias, item, StringComparison.OrdinalIgnoreCase)))
                return Known[i].Name;
        }

        return item;
    }

    private static string Normalize(string text) =>
        text.Trim().TrimEnd(':').Replace('I', 'ı').Replace('İ', 'i').ToLowerInvariant();
}
