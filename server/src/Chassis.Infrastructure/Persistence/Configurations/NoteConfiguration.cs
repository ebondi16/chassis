using Chassis.Domain.Common;
using Chassis.Domain.Notes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Chassis.Infrastructure.Persistence.Configurations;

internal sealed class NoteConfiguration : IEntityTypeConfiguration<Note>
{
    public void Configure(EntityTypeBuilder<Note> builder)
    {
        builder.ToTable("Notes");

        builder.HasKey(note => note.Id);

        builder.Property(note => note.Id)
            .HasConversion(id => id.Value, value => new NoteId(value))
            .ValueGeneratedNever();

        builder.Property(note => note.TenantId)
            .HasConversion(id => id.Value, value => new TenantId(value))
            .IsRequired();

        builder.HasIndex(note => note.TenantId);

        builder.Property(note => note.Title)
            .HasMaxLength(Note.MaxTitleLength)
            .IsRequired();

        builder.Property(note => note.Body)
            .IsRequired();

        builder.Property(note => note.CreatedOnUtc)
            .IsRequired();

        builder.Property(note => note.UpdatedOnUtc);

        builder.Ignore(note => note.DomainEvents);
    }
}
