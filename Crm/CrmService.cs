using wwwmmm.Workflow;

namespace wwwmmm.Crm;

/// <summary>
/// CRM operations over a workspace. Every method either records something it actually did or
/// throws; none of them reports success for work it skipped. Reads always come from disk, so a
/// second process sees the same records and nothing is cached past its own write.
/// </summary>
public sealed class CrmService
{
    private readonly CrmRepository repository;
    private readonly TimeProvider clock;
    private readonly WorkspaceScope workspace;

    public CrmService(WorkspaceScope workspace, TimeProvider? clock = null)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        this.clock = clock ?? TimeProvider.System;
        this.workspace = workspace;
        repository = new CrmRepository(workspace);
    }

    private long Now => clock.GetUtcNow().ToUnixTimeSeconds();

    /// <summary>
    /// Creates the customer, or returns the existing one when the mailbox already exists. An
    /// existing mailbox with different details is a conflict, not an update: updating requires
    /// an explicit call, so a typo in a name can never silently rewrite a customer record.
    /// </summary>
    public async Task<CrmCustomer> CreateCustomerAsync(string name, string email, string? company,
        string? status = null, CancellationToken token = default)
    {
        var canonical = CrmValidation.Email(email);
        var recordStatus = CrmValidation.Status(status ?? CrmValidation.ActiveStatus);
        var existing = repository.ListCustomers()
            .FirstOrDefault(customer => string.Equals(customer.Email, canonical, StringComparison.Ordinal));
        if (existing is not null) return RejectConflictingDetails(existing, name, company, recordStatus);

        var now = Now;
        var customer = new CrmCustomer(
            CrmValidation.Identity("cus_", canonical),
            CrmValidation.Name(name, "customer name"),
            canonical,
            CrmValidation.OptionalText(company, "company"),
            recordStatus,
            now,
            now);
        await CommitAsync("customers", customer.Id, customer, customer.Id, "customer-created", $"Customer {customer.Id} created.", token);
        return customer;
    }

    private static CrmCustomer RejectConflictingDetails(CrmCustomer existing, string name, string? company, string status)
    {
        var sameName = string.Equals(existing.Name, name.Trim(), StringComparison.Ordinal);
        var sameCompany = string.Equals(existing.Company, CrmValidation.OptionalText(company, "company"), StringComparison.Ordinal);
        if (!sameName || !sameCompany || existing.Status != status)
            throw new InvalidOperationException(
                $"Customer {existing.Id} already exists with different details; use 'crm update' to change a record deliberately.");
        return existing;
    }

    public async Task<CrmCustomer> SetStatusAsync(string customerId, string status, CancellationToken token = default)
    {
        var existing = RequireCustomer(customerId);
        var updated = existing with { Status = CrmValidation.Status(status), UpdatedUnix = Now };
        if (updated.Status == existing.Status) return existing;
        var expected = WorkflowJson.Hash(System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(existing, CrmJson.Options));
        await CommitAsync("customers", updated.Id, updated, customerId, "customer-status", $"Status changed from {existing.Status} to {updated.Status}.", token, expected);
        return updated;
    }

    public async Task<CrmContact> AddContactAsync(string customerId, string name, string? email, string? phone,
        string? role, CancellationToken token = default)
    {
        RequireCustomer(customerId);
        var contact = new CrmContact(
            CrmValidation.Mint("con_"),
            customerId,
            CrmValidation.Name(name, "contact name"),
            string.IsNullOrWhiteSpace(email) ? null : CrmValidation.Email(email),
            CrmValidation.OptionalText(phone, "phone", 64),
            CrmValidation.OptionalText(role, "role", 120),
            Now);
        await CommitAsync("contacts", contact.Id, contact, customerId, "contact-added", $"Contact {contact.Id} added.", token);
        return contact;
    }

    /// <summary>
    /// Links a document by its exact content hash. The same customer and hash is idempotent; the
    /// same path with different bytes is rejected so a record never points at changed content.
    /// </summary>
    public async Task<CrmLinkResult> LinkDocumentAsync(string customerId, string relativePath, string sha256,
        string? category, string textSource, CancellationToken token = default)
    {
        RequireCustomer(customerId);
        var hash = CrmValidation.Sha256(sha256);
        var cleanCategory = CrmValidation.Category(category);
        var path = CleanRelativePath(relativePath);
        if (string.IsNullOrWhiteSpace(textSource) || textSource is not ("text-layer" or "ocr" or "utf8" or "none"))
            throw new ArgumentException("textSource must be one of: text-layer, ocr, utf8, none.");
        var input = await workspace.ReadAsync(path, token);
        if (input.Source.Sha256 != hash) throw new IOException("Document hash does not match the actual workspace file.");

        var documents = repository.ListDocuments(customerId);
        var sameContent = documents.FirstOrDefault(document => document.Sha256 == hash);
        if (sameContent is not null)
        {
            if (sameContent.Category != cleanCategory && cleanCategory is not null)
                throw new InvalidOperationException($"Document {sameContent.Id} is already linked with a different category.");
            return new CrmLinkResult(sameContent, Created: false);
        }
        if (documents.Any(document => string.Equals(document.RelativePath, path, StringComparison.Ordinal)))
            throw new InvalidOperationException("That path is already linked with different content; review it before relinking.");

        var document = new CrmDocument(CrmValidation.Identity("doc_", customerId + ":" + hash), customerId, path, hash, cleanCategory, textSource, Now);
        await CommitAsync("documents", document.Id, document, customerId, "document-linked", $"Document {document.Id} linked from {path}.", token);
        return new CrmLinkResult(document, Created: true);
    }

    public Task<CrmActivity> RecordActivityAsync(string customerId, string kind, string detail,
        CancellationToken token = default)
    {
        RequireCustomer(customerId);
        if (string.IsNullOrWhiteSpace(kind) || kind.Length > 64 || kind.Any(character => !char.IsAsciiLetterLower(character) && character != '-'))
            throw new ArgumentException("Activity kind must be lowercase ASCII letters and hyphens.");
        var activity = new CrmActivity(CrmValidation.Mint("act_"), customerId, kind, CrmValidation.Detail(detail), Now);
        return SaveActivityAsync(activity, token);
    }

    private async Task<CrmActivity> SaveActivityAsync(CrmActivity activity, CancellationToken token)
    {
        await repository.SaveActivityAsync(activity, token);
        return activity;
    }

    public CrmCustomer RequireCustomer(string customerId) =>
        repository.FindCustomer(customerId) ?? throw new ArgumentException($"Unknown customer: {customerId}");

    public CrmCustomerDetail GetDetail(string customerId) => repository.Detail(customerId);

    public IReadOnlyList<CrmCustomer> ListCustomers() => repository.ListCustomers();

    public CrmSummary Summary()
    {
        return repository.Summary();
    }

    private Task CommitAsync<T>(string category, string id, T record, string customerId, string kind,
        string detail, CancellationToken token, string? expected = null)
    {
        var activity = new CrmActivity(CrmValidation.Mint("act_"), customerId, kind, detail, Now);
        return repository.CommitAsync([CrmRepository.Record(category, id, record, expected),
            CrmRepository.Record("activities", activity.Id, activity)], token);
    }

    private static string CleanRelativePath(string relativePath)
    {
        var cleaned = (relativePath ?? string.Empty).Trim().Replace('\\', '/');
        if (cleaned.Length is 0 or > 1024 || cleaned.Contains(':') || cleaned.StartsWith('/') ||
            cleaned.Split('/').Any(part => part is "" or "." or ".." || part != part.TrimEnd(' ', '.')))
            throw new ArgumentException("Supply a relative workspace path without traversal or a drive prefix.");
        return cleaned;
    }
}
