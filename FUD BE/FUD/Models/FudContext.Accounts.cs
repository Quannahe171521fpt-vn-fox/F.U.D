using Microsoft.EntityFrameworkCore;

namespace FUD.Models;

public partial class FudContext
{
    /// <summary>
    /// Cấu hình riêng cho module Account Management (Users / Roles / UserRoles / lịch sử / refresh token).
    /// </summary>
    partial void OnModelCreatingPartial(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasIndex(e => e.Username).IsUnique();

            entity.Property(e => e.Username)
                .HasMaxLength(50)
                .IsUnicode(false);

            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");

            entity.Property(e => e.TokenVersion).HasDefaultValue(0);
        });

        modelBuilder.Entity<AccountStatusHistory>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK_AccountStatusHistories");

            entity.ToTable("Account_Status_Histories");

            entity.HasIndex(e => new { e.UserId, e.ChangedAt }, "IX_AccountStatusHistories_User_Time");

            entity.Property(e => e.OldStatus)
                .HasMaxLength(20)
                .IsUnicode(false);

            entity.Property(e => e.NewStatus)
                .HasMaxLength(20)
                .IsUnicode(false);

            entity.Property(e => e.Reason).HasMaxLength(255);

            entity.Property(e => e.Notes).HasMaxLength(1000);

            entity.Property(e => e.NotificationStatus)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("None");

            entity.Property(e => e.ChangedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");

            entity.HasOne(d => d.ChangedBy).WithMany(p => p.AccountStatusChangedBies)
                .HasForeignKey(d => d.ChangedById)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasOne(d => d.User).WithMany(p => p.AccountStatusHistories)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull);
        });

        modelBuilder.Entity<RoleChangeHistory>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK_RoleChangeHistories");

            entity.ToTable("Role_Change_Histories");

            entity.HasIndex(e => new { e.UserId, e.ChangedAt }, "IX_RoleChangeHistories_User_Time");

            entity.Property(e => e.Action)
                .HasMaxLength(20)
                .IsUnicode(false);

            entity.Property(e => e.OldRoles).HasMaxLength(500);

            entity.Property(e => e.NewRoles).HasMaxLength(500);

            entity.Property(e => e.ChangedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");

            entity.HasOne(d => d.ChangedBy).WithMany(p => p.RoleChangeChangedBies)
                .HasForeignKey(d => d.ChangedById)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasOne(d => d.User).WithMany(p => p.RoleChangeHistories)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull);
        });

        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK_RefreshTokens");

            entity.ToTable("Refresh_Tokens");

            entity.HasIndex(e => e.TokenHash).IsUnique();

            entity.HasIndex(e => new { e.UserId, e.ExpiresAt }, "IX_RefreshTokens_User_Expires");

            entity.Property(e => e.TokenHash)
                .HasMaxLength(128)
                .IsUnicode(false);

            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");

            entity.Property(e => e.ExpiresAt).HasColumnType("datetime");

            entity.Property(e => e.RevokedAt).HasColumnType("datetime");

            entity.HasOne(d => d.User).WithMany(p => p.RefreshTokens)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ── Seed 4 vai trò hệ thống ────────────────────────────────
        modelBuilder.Entity<Role>().HasData(
            new Role { Id = 1, RoleName = "Student", Description = "Sinh viên" },
            new Role { Id = 2, RoleName = "Manager", Description = "Quản lý ký túc xá" },
            new Role { Id = 3, RoleName = "Guard", Description = "Bảo vệ / nhân viên trực" },
            new Role { Id = 4, RoleName = "Admin", Description = "Quản trị hệ thống" }
        );
    }
}
