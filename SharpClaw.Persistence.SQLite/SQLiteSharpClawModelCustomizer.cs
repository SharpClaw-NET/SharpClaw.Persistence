using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using SharpClaw.Runtime.INF.Persistence;

namespace SharpClaw.Persistence.SQLite;

internal sealed class SQLiteSharpClawModelCustomizer(
    ModelCustomizerDependencies dependencies) : ModelCustomizer(dependencies)
{
    public override void Customize(ModelBuilder modelBuilder, DbContext context)
    {
        base.Customize(modelBuilder, context);
        if (context is not SharpClawDbContext)
            return;

        foreach (var property in modelBuilder.Model.GetEntityTypes().SelectMany(type => type.GetProperties()))
        {
            if (property.ClrType == typeof(DateTimeOffset))
            {
                property.SetValueConverter(new ValueConverter<DateTimeOffset, long>(
                    value => value.ToUnixTimeMilliseconds(),
                    value => DateTimeOffset.FromUnixTimeMilliseconds(value)));
            }
            else if (property.ClrType == typeof(DateTimeOffset?))
            {
                property.SetValueConverter(new ValueConverter<DateTimeOffset?, long?>(
                    value => value.HasValue ? value.Value.ToUnixTimeMilliseconds() : null,
                    value => value.HasValue ? DateTimeOffset.FromUnixTimeMilliseconds(value.Value) : null));
            }
        }
    }
}
