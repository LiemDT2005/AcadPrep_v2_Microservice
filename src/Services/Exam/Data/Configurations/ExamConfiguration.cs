using Exam.Entities;
using Exam.Entities.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Exam.Data.Configurations;

public class ExamConfiguration : IEntityTypeConfiguration<global::Exam.Entities.Exam>
{
    public void Configure(EntityTypeBuilder<global::Exam.Entities.Exam> builder)
    {
        builder.ToTable("EXAMS");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("ExamId")
            .ValueGeneratedOnAdd();

        builder.Property(x => x.Title)
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(x => x.Description)
            .IsRequired(false);

        builder.Property(x => x.Duration)
            .IsRequired();

        builder.Property(x => x.AudioUrl)
            .HasMaxLength(500)
            .IsUnicode(false)
            .IsRequired(false);

        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .HasDefaultValue(ExamStatus.Draft)
            .IsRequired();

        builder.Property(x => x.IsDeleted)
            .HasDefaultValue(false);

        builder.Property(x => x.CreatedAt)
            .HasDefaultValueSql("GETDATE()");

        builder.Property(x => x.LastModifiedAt)
            .IsRequired(false);

        builder.HasOne(x => x.ExamSeries)
            .WithMany(s => s.Exams)
            .HasForeignKey(x => x.ExamSeriesId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}
