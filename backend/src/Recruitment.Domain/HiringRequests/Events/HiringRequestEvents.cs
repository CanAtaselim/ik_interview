using Recruitment.Domain.Common;

namespace Recruitment.Domain.HiringRequests.Events;

public sealed record HiringRequestSubmitted(Guid HiringRequestId) : IDomainEvent;

/// <summary>Talep bir onaycının kararını bekliyor (gönderimde ilk adım, her onaydan sonra sıradaki).</summary>
public sealed record HiringRequestAwaitingApproval(Guid HiringRequestId, Users.Role Role, int Step) : IDomainEvent;

/// <summary>Son onay verildi; talep için pozisyon açılır.</summary>
public sealed record HiringRequestApproved(Guid HiringRequestId) : IDomainEvent;

public sealed record HiringRequestRejected(Guid HiringRequestId) : IDomainEvent;

public sealed record HiringRequestCancelled(Guid HiringRequestId) : IDomainEvent;
