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
    public async Task OutboxInterceptor_CapturesDomainEvents_Success()
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
        await context.SaveChangesAsync();

        // Assert
        var outboxMessages = await context.OutboxMessages.ToListAsync();
        Assert.AreEqual(1, outboxMessages.Count);
        Assert.AreEqual(nameof(TestDomainEvent), outboxMessages[0].Type);
        Assert.AreEqual(0, outboxMessages[0].Status);
        Assert.AreEqual(0, entity.DomainEvents.Count); // Events should be cleared
    }
}

public class TestDbContext : DbContext
{
    public TestDbContext(DbContextOptions<TestDbContext> options) : base(options)
    {
    }

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

public class TestDomainEntity : IDomainEntity
{
    private readonly List<IDomainEvent> _domainEvents = new();

    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;

    public TestDomainEntity(Guid id, string name)
    {
        Id = id;
        Name = name;
    }

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

public class TestDomainEvent : IDomainEvent
{
    public Guid EntityId { get; }
    public string EntityName { get; }
    public DateTime OccurredOnUtc { get; }

    public TestDomainEvent(Guid entityId, string entityName)
    {
        EntityId = entityId;
        EntityName = entityName;
        OccurredOnUtc = DateTime.UtcNow;
    }
}
