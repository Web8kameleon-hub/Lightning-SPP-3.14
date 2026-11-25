namespace wwwmmm.Crm;

/// <summary>
/// A customer account. <paramref name="Id"/> is the durable key; the email is the natural
/// identity and is stored normalised so two records can never describe the same mailbox.
/// Nothing here is seeded or invented: a record exists only because someone created it.
/// </summary>
public sealed record CrmCustomer(
    string Id,
    string Name,
    string Email,
    string? Company,
    string Status,
    long CreatedUnix,
    long UpdatedUnix);

/// <summary>A named person at a customer, with an optional role and direct contact detail.</summary>
public sealed record CrmContact(
    string Id,
    string CustomerId,
    string Name,
    string? Email,
    string? Phone,
    string? Role,
    long CreatedUnix);

/// <summary>
/// A document attached to a customer. <paramref name="Sha256"/> pins the exact bytes that were
/// linked, so a later re-scan of the same path cannot silently change what the record refers to.
/// <paramref name="Category"/> is kept only when a classification actually produced it.
/// </summary>
public sealed record CrmDocument(
    string Id,
    string CustomerId,
    string RelativePath,
    string Sha256,
    string? Category,
    string TextSource,
    long LinkedUnix);

/// <summary>Append-only audit entry. Activities are never edited or deleted.</summary>
public sealed record CrmActivity(
    string Id,
    string CustomerId,
    string Kind,
    string Detail,
    long OccurredUnix);

/// <summary>Counts derived from stored records, never estimated.</summary>
public sealed record CrmSummary(
    int Customers,
    int ActiveCustomers,
    int Contacts,
    int Documents,
    int UnclassifiedDocuments,
    int Activities);

/// <summary>A customer plus everything recorded against it, for one coherent read.</summary>
public sealed record CrmCustomerDetail(
    CrmCustomer Customer,
    IReadOnlyList<CrmContact> Contacts,
    IReadOnlyList<CrmDocument> Documents,
    IReadOnlyList<CrmActivity> Activities);

/// <summary>Outcome of a link operation, stating whether the record already existed.</summary>
public sealed record CrmLinkResult(CrmDocument Document, bool Created);
