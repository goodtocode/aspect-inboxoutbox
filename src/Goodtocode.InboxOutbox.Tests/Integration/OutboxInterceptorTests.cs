using Goodtocode.InboxOutbox.Entities;
using Goodtocode.InboxOutbox.Extensions;
using Goodtocode.InboxOutbox.Interceptors;
using Goodtocode.InboxOutbox.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Goodtocode.InboxOutbox.Tests.Integration;

[TestClass]
public class OutboxInterceptorTests
{
    [TestMethod]
    public async Task OutboxInterceptorCapturesDomainEventsSuccess()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .AddInterceptors(new OutboxSaveChangesInterceptor())
            .Options;

        using var context = new TestDbContext(options);

        var entity = new TestDomainEntity(Guid.NewGuid(), "Test Entity");
        entity.RaiseTestEvent();

        context.TestEntities.Add(entity);

        // Act
        await context.SaveChangesAsync(TestContext.CancellationToken);

        // Assert
        var outboxMessages = await context.OutboxMessages.ToListAsync(TestContext.CancellationToken);
        Assert.HasCount(1, outboxMessages);
        Assert.AreEqual(nameof(TestDomainEvent), outboxMessages[0].Type);
        Assert.AreEqual(0, outboxMessages[0].Status);
        Assert.IsEmpty(entity.DomainEvents); // Events should be cleared
    }

    public TestContext TestContext { get; set; }
}

public class TestDbContext(DbContextOptions<TestDbContext> options) : DbContext(options)
{
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();
    public DbSet<TestDomainEntity> TestEntities => Set<TestDomainEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyInboxOutbox();

        modelBuilder.Entity<TestDomainEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired();
        });

        base.OnModelCreating(modelBuilder);
    }
}

public class TestDomainEntity(Guid id, string name) : IDomainEntity
{
    private readonly List<IDomainEvent> _domainEvents = new();

    public Guid Id { get; private set; } = id;
    public string Name { get; private set; } = name;

    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    public void RaiseTestEvent()
    {
        _domainEvents.Add(new TestDomainEvent(Id, Name));
    }

    public void ClearDomainEvents()
    {
        _domainEvents.Clear();
    }
}

public class TestDomainEvent(Guid entityId, string entityName) : IDomainEvent
{
    public Guid EntityId { get; } = entityId;
    public string EntityName { get; } = entityName;
    public DateTime OccurredOnUtc { get; } = DateTime.UtcNow;
}
