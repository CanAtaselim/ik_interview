using System.DirectoryServices.Protocols;
using System.Globalization;
using System.Net;
using System.Text;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Recruitment.Application.Abstractions;
using Recruitment.Domain.Users;

namespace Recruitment.Infrastructure.Identity;

/// <summary>
/// Active Directory erişimi. Windows'a özgü System.DirectoryServices yerine her platformda
/// çalışan System.DirectoryServices.Protocols kullanır.
/// </summary>
internal sealed class LdapService(
    IOptions<LdapOptions> options,
    IHostEnvironment environment,
    ILogger<LdapService> logger) : ILdapService
{
    private const int InvalidCredentials = 49;
    private const int AccountDisabledFlag = 0x2;

    // LDAP_MATCHING_RULE_IN_CHAIN: iç içe grup üyeliklerini tek sorguda çözer.
    private const string MatchingRuleInChain = "1.2.840.113556.1.4.1941";

    private static readonly string[] UserAttributes =
        ["sAMAccountName", "displayName", "mail", "department", "title", "manager", "userAccountControl"];

    private readonly LdapOptions _options = options.Value;

    public Task<bool> Authenticate(string username, string password, CancellationToken ct)
    {
        // Boş şifreyle bind anonim sayılıp başarılı dönebilir.
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrEmpty(password))
            return Task.FromResult(false);

        return Task.Run(
            () =>
            {
                try
                {
                    using var connection = Connect(new NetworkCredential(ToBindName(username), password));
                    return true;
                }
                catch (LdapException ex) when (ex.ErrorCode == InvalidCredentials)
                {
                    return false;
                }
            },
            ct);
    }

    public Task<LdapUser?> FindUser(string username, CancellationToken ct) => Task.Run(() => Find(username), ct);

    private LdapUser? Find(string username)
    {
        using var connection = Connect(new NetworkCredential(_options.BindUser, _options.BindPassword));

        var userFilter = string.Format(
            CultureInfo.InvariantCulture, _options.UserSearchFilter, EscapeFilterValue(username));
        var entry = Search(connection, _options.BaseDn, userFilter, SearchScope.Subtree, UserAttributes)
            .FirstOrDefault();
        if (entry is null)
            return null;

        var groups = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var groupFilter = $"(member:{MatchingRuleInChain}:={EscapeFilterValue(entry.DistinguishedName)})";
        foreach (var group in Search(connection, _options.BaseDn, groupFilter, SearchScope.Subtree, ["cn"]))
        {
            groups.Add(group.DistinguishedName);
            if (GetString(group, "cn") is { } commonName)
                groups.Add(commonName);
        }

        var accountFlags = int.TryParse(
            GetString(entry, "userAccountControl"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var flags)
            ? flags
            : 0;

        return new LdapUser(
            username,
            GetString(entry, "displayName") ?? username,
            GetString(entry, "mail"),
            GetString(entry, "department"),
            GetString(entry, "title"),
            FindManagerUsername(connection, GetString(entry, "manager")),
            IsEnabled: (accountFlags & AccountDisabledFlag) == 0,
            groups);
    }

    // "manager" özniteliği DN tutar; kullanıcı adı için o kaydı ayrıca okumak gerekir.
    private static string? FindManagerUsername(LdapConnection connection, string? managerDn)
    {
        if (managerDn is null)
            return null;

        var manager = Search(connection, managerDn, "(objectClass=*)", SearchScope.Base, ["sAMAccountName"])
            .FirstOrDefault();
        var accountName = manager is null ? null : GetString(manager, "sAMAccountName");

        return accountName is null ? null : UsernameNormalizer.Normalize(accountName);
    }

    private LdapConnection Connect(NetworkCredential credential)
    {
        var connection = new LdapConnection(
            new LdapDirectoryIdentifier(_options.Host, _options.Port), credential, AuthType.Basic)
        {
            Timeout = TimeSpan.FromSeconds(_options.TimeoutSeconds),
        };

        try
        {
            connection.SessionOptions.ProtocolVersion = 3;
            connection.SessionOptions.ReferralChasing = ReferralChasingOptions.None;

            if (_options.UseSsl)
                connection.SessionOptions.SecureSocketLayer = true;
            else
                StartTls(connection);

            connection.Bind();
            return connection;
        }
        catch
        {
            connection.Dispose();
            throw;
        }
    }

    // Basic bind şifreyi düz metin taşır; bu yüzden şifresiz bağlantıya yalnızca geliştirmede izin verilir.
    private void StartTls(LdapConnection connection)
    {
        try
        {
            connection.SessionOptions.StartTransportLayerSecurity(null);
        }
        catch (Exception ex) when (environment.IsDevelopment() && ex is LdapException or DirectoryOperationException)
        {
            logger.LogWarning(ex, "LDAP StartTLS kurulamadı; geliştirme ortamında şifresiz bağlantıyla devam ediliyor");
        }
    }

    private string ToBindName(string username) =>
        string.IsNullOrWhiteSpace(_options.Domain) ? username : $"{username}@{_options.Domain}";

    private static List<SearchResultEntry> Search(
        LdapConnection connection, string baseDn, string filter, SearchScope scope, string[] attributes)
    {
        var response = (SearchResponse)connection.SendRequest(new SearchRequest(baseDn, filter, scope, attributes));
        return response.Entries.Cast<SearchResultEntry>().ToList();
    }

    private static string? GetString(SearchResultEntry entry, string attribute)
    {
        if (!entry.Attributes.Contains(attribute))
            return null;

        return entry.Attributes[attribute].GetValues(typeof(string)).OfType<string>().FirstOrDefault();
    }

    /// <summary>RFC 4515: filtre içindeki özel karakterleri kaçırır (LDAP injection'a karşı).</summary>
    internal static string EscapeFilterValue(string value)
    {
        var builder = new StringBuilder(value.Length);

        foreach (var c in value)
        {
            builder.Append(c switch
            {
                '\\' => @"\5c",
                '*' => @"\2a",
                '(' => @"\28",
                ')' => @"\29",
                '\0' => @"\00",
                _ => c.ToString(),
            });
        }

        return builder.ToString();
    }
}
