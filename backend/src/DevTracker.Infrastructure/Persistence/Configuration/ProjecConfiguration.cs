using DevTracker.Domain.Entities;
using DevTracker.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DevTracker.Infrastructure.Persistance.Configuration
{
    public class ProjectConfiguration : IEntityTypeConfiguration<Project>
    {
        public void Configure(EntityTypeBuilder<Project> builder) 
        {
            builder.HasKey(p => p.Id);
            builder.HasIndex(p => new { p.OwnerId, p.Name }).IsUnique();
            builder.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(p => p.OwnerId)
                .OnDelete(DeleteBehavior.Restrict);
            builder.Property(p => p.Name)
                .IsRequired()
                .HasMaxLength(150);
            builder.Property(p => p.Status)
                .HasConversion<string>();


    
        }

    }

}
