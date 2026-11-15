using FluentAssertions;
using FUD.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Xunit;

namespace FUD.Tests.Models;

public class FudContextTriggerTests
{
    [Theory]
    [InlineData(typeof(Residency), "Residencies", "trg_Residency_GenderMatch")]
    [InlineData(typeof(Block), "Blocks", "trg_Blocks_PreventGenderChangeWhenOccupied")]
    public void TriggerBackedEntities_DisableSqlOutputClauseAndDeclareTrigger(
        Type entityClrType,
        string tableName,
        string expectedTriggerName)
    {
        var options = new DbContextOptionsBuilder<FudContext>()
            .UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=FUD_Dummy;Trusted_Connection=True;")
            .Options;

        using var context = new FudContext(options);
        var entityType = context.Model.FindEntityType(entityClrType);

        entityType.Should().NotBeNull();
        entityType!.GetTableName().Should().Be(tableName);
        entityType.GetDeclaredTriggers().Select(t => t.ModelName).Should().Contain(expectedTriggerName);
        entityType.IsSqlOutputClauseUsed(StoreObjectIdentifier.Table(tableName, null)).Should().BeFalse();
    }
}
