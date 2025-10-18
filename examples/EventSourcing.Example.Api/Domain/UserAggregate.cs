using EventSourcing.Core;
using EventSourcing.Example.Api.Domain.Events;

namespace EventSourcing.Example.Api.Domain;

public class UserAggregate : AggregateBase<Guid>
{
    public override Guid Id { get; protected set; }
    public string Email { get; protected set; } = string.Empty;
    public string FirstName { get; protected set; } = string.Empty;
    public string LastName { get; protected set; } = string.Empty;
    public bool IsActive { get; protected set; }
    public string? DeactivationReason { get; protected set; }

    // Commands - Business logic that raises events

    public void CreateUser(Guid userId, string email, string firstName, string lastName)
    {
        if (Id != Guid.Empty)
            throw new InvalidOperationException("User already exists");

        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("Email is required", nameof(email));

        if (string.IsNullOrWhiteSpace(firstName))
            throw new ArgumentException("First name is required", nameof(firstName));

        if (string.IsNullOrWhiteSpace(lastName))
            throw new ArgumentException("Last name is required", nameof(lastName));

        RaiseEvent(new UserCreatedEvent(userId, email, firstName, lastName));
    }

    public void ChangeName(string firstName, string lastName)
    {
        if (Id == Guid.Empty)
            throw new InvalidOperationException("User does not exist");

        if (string.IsNullOrWhiteSpace(firstName))
            throw new ArgumentException("First name is required", nameof(firstName));

        if (string.IsNullOrWhiteSpace(lastName))
            throw new ArgumentException("Last name is required", nameof(lastName));

        if (FirstName == firstName && LastName == lastName)
            return; // No change

        RaiseEvent(new UserNameChangedEvent(firstName, lastName));
    }

    public void ChangeEmail(string newEmail)
    {
        if (Id == Guid.Empty)
            throw new InvalidOperationException("User does not exist");

        if (string.IsNullOrWhiteSpace(newEmail))
            throw new ArgumentException("Email is required", nameof(newEmail));

        if (Email == newEmail)
            return; // No change

        RaiseEvent(new UserEmailChangedEvent(newEmail));
    }

    public void Activate()
    {
        if (Id == Guid.Empty)
            throw new InvalidOperationException("User does not exist");

        if (IsActive)
            return; // Already active

        RaiseEvent(new UserActivatedEvent());
    }

    public void Deactivate(string reason)
    {
        if (Id == Guid.Empty)
            throw new InvalidOperationException("User does not exist");

        if (!IsActive)
            return; // Already deactivated

        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Deactivation reason is required", nameof(reason));

        RaiseEvent(new UserDeactivatedEvent(reason));
    }

    // Event Handlers - Apply state changes

    private void Apply(UserCreatedEvent @event)
    {
        Id = @event.UserId;
        Email = @event.Email;
        FirstName = @event.FirstName;
        LastName = @event.LastName;
        IsActive = true;
    }

    private void Apply(UserNameChangedEvent @event)
    {
        FirstName = @event.FirstName;
        LastName = @event.LastName;
    }

    private void Apply(UserEmailChangedEvent @event)
    {
        Email = @event.NewEmail;
    }

    private void Apply(UserActivatedEvent @event)
    {
        IsActive = true;
        DeactivationReason = null;
    }

    private void Apply(UserDeactivatedEvent @event)
    {
        IsActive = false;
        DeactivationReason = @event.Reason;
    }
}
