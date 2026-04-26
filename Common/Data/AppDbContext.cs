using Microsoft.EntityFrameworkCore;
using CradleSoft.DMS.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;

namespace CradleSoft.DMS.Data
{
  public class AppDbContext : IdentityDbContext<ApplicationUser>
  {
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
      
    }

    public DbSet<RefreshToken> RefreshTokens { get; set; }

    public DbSet<AccessToken> RevokedAccessTokens { get; set; }

    public DbSet<ContractTemplate> ContractTemplates { get; set; }

    public DbSet<ContractPlaceholder> ContractPlaceholders { get; set; }

    public DbSet<ContractInstance> ContractInstances { get; set; }

    public DbSet<ContractSigner> ContractSigners { get; set; }

    public DbSet<ContractFieldValue> ContractFieldValues { get; set; }

    public DbSet<StorageBucket> StorageBuckets { get; set; }

    public DbSet<StorageObject> StorageObjects { get; set; }

    public DbSet<BucketShare> BucketShares { get; set; }

    public DbSet<ObjectShare> ObjectShares { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
      base.OnModelCreating(builder);

      builder.Entity<ContractTemplate>(entity =>
      {
        entity.HasKey(x => x.TemplateId);
        entity.Property(x => x.TemplateId).HasMaxLength(64);
        entity.Property(x => x.Bucket).HasMaxLength(255).IsRequired();
        entity.Property(x => x.ObjectKey).HasMaxLength(1024).IsRequired();
        entity.Property(x => x.FileName).HasMaxLength(512).IsRequired();
        entity.Property(x => x.ContentType).HasMaxLength(128);
        entity.Property(x => x.Title).HasMaxLength(512);
        entity.Property(x => x.AuthorId).HasMaxLength(128);
        entity.Property(x => x.Status).HasMaxLength(64).IsRequired();
      });

      builder.Entity<ContractPlaceholder>(entity =>
      {
        entity.HasKey(x => x.PlaceholderId);
        entity.Property(x => x.PlaceholderId).HasMaxLength(64);
        entity.Property(x => x.TemplateId).HasMaxLength(64).IsRequired();
        entity.Property(x => x.FieldType).HasMaxLength(64).IsRequired();
        entity.Property(x => x.Role).HasMaxLength(128).IsRequired();
        entity.Property(x => x.Label).HasMaxLength(256);
        entity.Property(x => x.X).HasPrecision(18, 4);
        entity.Property(x => x.Y).HasPrecision(18, 4);
        entity.Property(x => x.Width).HasPrecision(18, 4);
        entity.Property(x => x.Height).HasPrecision(18, 4);

        entity.HasOne(x => x.Template)
          .WithMany(x => x.Placeholders)
          .HasForeignKey(x => x.TemplateId)
          .OnDelete(DeleteBehavior.Cascade);

        entity.HasIndex(x => new { x.TemplateId, x.Order });
      });

      builder.Entity<ContractInstance>(entity =>
      {
        entity.HasKey(x => x.InstanceId);
        entity.Property(x => x.InstanceId).HasMaxLength(64);
        entity.Property(x => x.TemplateId).HasMaxLength(64).IsRequired();
        entity.Property(x => x.Name).HasMaxLength(512);
        entity.Property(x => x.Status).HasMaxLength(64).IsRequired();
        entity.Property(x => x.FinalArtifactHash).HasMaxLength(256);

        entity.HasOne(x => x.Template)
          .WithMany(x => x.Instances)
          .HasForeignKey(x => x.TemplateId)
          .OnDelete(DeleteBehavior.Restrict);
      });

      builder.Entity<ContractSigner>(entity =>
      {
        entity.HasKey(x => x.Id);
        entity.Property(x => x.InstanceId).HasMaxLength(64).IsRequired();
        entity.Property(x => x.SignerId).HasMaxLength(128).IsRequired();
        entity.Property(x => x.Role).HasMaxLength(128).IsRequired();
        entity.Property(x => x.DisplayName).HasMaxLength(256).IsRequired();
        entity.Property(x => x.Email).HasMaxLength(256);

        entity.HasOne(x => x.Instance)
          .WithMany(x => x.Signers)
          .HasForeignKey(x => x.InstanceId)
          .OnDelete(DeleteBehavior.Cascade);

        entity.HasIndex(x => new { x.InstanceId, x.SignerId }).IsUnique();
      });

      builder.Entity<ContractFieldValue>(entity =>
      {
        entity.HasKey(x => x.Id);
        entity.Property(x => x.InstanceId).HasMaxLength(64).IsRequired();
        entity.Property(x => x.PlaceholderId).HasMaxLength(64).IsRequired();
        entity.Property(x => x.SignerId).HasMaxLength(128).IsRequired();
        entity.Property(x => x.SourceIp).HasMaxLength(64);

        entity.HasOne(x => x.Instance)
          .WithMany(x => x.FieldValues)
          .HasForeignKey(x => x.InstanceId)
          .OnDelete(DeleteBehavior.Cascade);

        entity.HasIndex(x => new { x.InstanceId, x.PlaceholderId, x.SignerId }).IsUnique();
      });

      builder.Entity<StorageBucket>(entity =>
      {
        entity.HasKey(x => x.Id);
        entity.HasAlternateKey(x => x.BucketName);
        entity.Property(x => x.BucketName).HasMaxLength(255);
        entity.Property(x => x.PolicyJson);
        entity.Property(x => x.ObjectCount).HasDefaultValue(0L);
        entity.Property(x => x.TotalSizeBytes).HasDefaultValue(0L);

        entity.HasOne(x => x.Owner)
          .WithMany()
          .HasForeignKey(x => x.OwnerId)
          .OnDelete(DeleteBehavior.SetNull);

        entity.HasIndex(x => x.OwnerId);
      });

      builder.Entity<StorageObject>(entity =>
      {
        entity.HasKey(x => x.Id);
        entity.Property(x => x.RouteId).IsRequired();
        entity.Property(x => x.BucketId).IsRequired();
        entity.Property(x => x.BucketName).HasMaxLength(255).IsRequired();
        entity.Property(x => x.ObjectKey).HasMaxLength(1024).IsRequired();
        entity.Property(x => x.ContentType).HasMaxLength(255).IsRequired();

        entity.HasIndex(x => x.RouteId).IsUnique();
        entity.HasIndex(x => new { x.BucketName, x.ObjectKey }).IsUnique();

        entity.HasOne(x => x.Bucket)
          .WithMany(x => x.Objects)
          .HasForeignKey(x => x.BucketId)
          .OnDelete(DeleteBehavior.Cascade);
      });

      builder.Entity<BucketShare>(entity =>
      {
        entity.HasKey(x => x.Id);
        entity.Property(x => x.BucketId).IsRequired();
        entity.Property(x => x.BucketName).HasMaxLength(255).IsRequired();
        entity.Property(x => x.SharedByUserId).HasMaxLength(450).IsRequired();
        entity.Property(x => x.SharedWithUserId).HasMaxLength(450).IsRequired();
        entity.Property(x => x.AcknowledgedAt);
        entity.Property(x => x.ExpiresAt);
        entity.Property(x => x.Permission)
          .HasMaxLength(32)
          .HasDefaultValue(BucketSharePermissions.ViewOnly)
          .IsRequired();

        entity.HasIndex(x => new { x.BucketName, x.SharedWithUserId }).IsUnique();
        entity.HasIndex(x => x.SharedWithUserId);

        entity.HasOne(x => x.Bucket)
          .WithMany(x => x.Shares)
          .HasForeignKey(x => x.BucketId)
          .OnDelete(DeleteBehavior.Cascade);

        entity.HasOne(x => x.SharedByUser)
          .WithMany()
          .HasForeignKey(x => x.SharedByUserId)
          .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(x => x.SharedWithUser)
          .WithMany()
          .HasForeignKey(x => x.SharedWithUserId)
          .OnDelete(DeleteBehavior.Restrict);
      });

      builder.Entity<ObjectShare>(entity =>
      {
        entity.HasKey(x => x.Id);
        entity.Property(x => x.StorageObjectId).IsRequired();
        entity.Property(x => x.BucketName).HasMaxLength(255).IsRequired();
        entity.Property(x => x.ObjectKey).HasMaxLength(1024).IsRequired();
        entity.Property(x => x.SharedByUserId).HasMaxLength(450).IsRequired();
        entity.Property(x => x.SharedWithUserId).HasMaxLength(450).IsRequired();
        entity.Property(x => x.ExpiresAt);

        entity.HasIndex(x => new { x.StorageObjectId, x.SharedWithUserId }).IsUnique();
        entity.HasIndex(x => x.SharedWithUserId);

        entity.HasOne(x => x.StorageObject)
          .WithMany(x => x.Shares)
          .HasForeignKey(x => x.StorageObjectId)
          .OnDelete(DeleteBehavior.Cascade);

        entity.HasOne(x => x.SharedByUser)
          .WithMany()
          .HasForeignKey(x => x.SharedByUserId)
          .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(x => x.SharedWithUser)
          .WithMany()
          .HasForeignKey(x => x.SharedWithUserId)
          .OnDelete(DeleteBehavior.Restrict);
      });
    }
  }
}
