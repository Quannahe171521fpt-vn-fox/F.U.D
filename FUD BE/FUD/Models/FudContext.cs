using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace FUD.Models;

public partial class FudContext : DbContext
{
    public FudContext()
    {
    }

    public FudContext(DbContextOptions<FudContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Admin> Admins { get; set; }

    public virtual DbSet<AuditLog> AuditLogs { get; set; }

    public virtual DbSet<Bed> Beds { get; set; }

    public virtual DbSet<BedPrice> BedPrices { get; set; }

    public virtual DbSet<BedTransferRequest> BedTransferRequests { get; set; }

    public virtual DbSet<BillingPeriod> BillingPeriods { get; set; }

    public virtual DbSet<Block> Blocks { get; set; }

    public virtual DbSet<BookingRequest> BookingRequests { get; set; }

    public virtual DbSet<Campus> Campuses { get; set; }

    public virtual DbSet<CheckoutRequest> CheckoutRequests { get; set; }

    public virtual DbSet<ComplaintEvidence> ComplaintEvidences { get; set; }

    public virtual DbSet<ComplaintRequest> ComplaintRequests { get; set; }

    public virtual DbSet<Dorm> Dorms { get; set; }

    public virtual DbSet<Equipment> Equipments { get; set; }

    public virtual DbSet<EquipmentCatalog> EquipmentCatalogs { get; set; }

    public virtual DbSet<Guard> Guards { get; set; }

    public virtual DbSet<Handover> Handovers { get; set; }

    public virtual DbSet<HandoverItem> HandoverItems { get; set; }

    public virtual DbSet<HandoverPhoto> HandoverPhotos { get; set; }

    public virtual DbSet<ImportBatch> ImportBatches { get; set; }

    public virtual DbSet<IncidentTicket> IncidentTickets { get; set; }

    public virtual DbSet<InspectionPhoto> InspectionPhotos { get; set; }

    public virtual DbSet<InspectionRequest> InspectionRequests { get; set; }

    public virtual DbSet<Invoice> Invoices { get; set; }

    public virtual DbSet<InvoiceItem> InvoiceItems { get; set; }

    public virtual DbSet<Manager> Managers { get; set; }

    public virtual DbSet<MeterReading> MeterReadings { get; set; }

    public virtual DbSet<News> News { get; set; }

    public virtual DbSet<NewsAttachment> NewsAttachments { get; set; }

    public virtual DbSet<Notification> Notifications { get; set; }

    public virtual DbSet<PaymentTransaction> PaymentTransactions { get; set; }

    public virtual DbSet<PriceItem> PriceItems { get; set; }

    public virtual DbSet<PriceList> PriceLists { get; set; }

    public virtual DbSet<Refund> Refunds { get; set; }

    public virtual DbSet<RegistrationPeriod> RegistrationPeriods { get; set; }

    public virtual DbSet<RepairFeedback> RepairFeedbacks { get; set; }

    public virtual DbSet<Request> Requests { get; set; }

    public virtual DbSet<RequestLog> RequestLogs { get; set; }

    public virtual DbSet<Residency> Residencies { get; set; }

    public virtual DbSet<Role> Roles { get; set; }

    public virtual DbSet<Room> Rooms { get; set; }

    public virtual DbSet<RoomType> RoomTypes { get; set; }

    public virtual DbSet<RoomTypeEquipment> RoomTypeEquipments { get; set; }

    public virtual DbSet<Student> Students { get; set; }

    public virtual DbSet<Term> Terms { get; set; }

    public virtual DbSet<TicketPhoto> TicketPhotos { get; set; }

    public virtual DbSet<TicketStatusHistory> TicketStatusHistories { get; set; }

    public virtual DbSet<TransactionInvoice> TransactionInvoices { get; set; }

    public virtual DbSet<UsageAnomaly> UsageAnomalies { get; set; }

    public virtual DbSet<User> Users { get; set; }

    public virtual DbSet<UserRole> UserRoles { get; set; }

    public virtual DbSet<UtilityBill> UtilityBills { get; set; }

    public virtual DbSet<UtilityPrice> UtilityPrices { get; set; }

    public virtual DbSet<Violation> Violations { get; set; }

    public virtual DbSet<ViolationEvidence> ViolationEvidences { get; set; }

    public virtual DbSet<ViolationExplanation> ViolationExplanations { get; set; }

    public virtual DbSet<ViolationType> ViolationTypes { get; set; }

    public virtual DbSet<VwStudentOutstanding> VwStudentOutstandings { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Admin>(entity =>
        {
            entity.HasKey(e => e.UserId).HasName("PK__Admins__1788CC4CAAF73251");

            entity.Property(e => e.UserId).ValueGeneratedNever();

            entity.HasOne(d => d.User).WithOne(p => p.Admin)
                .HasForeignKey<Admin>(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Admins__UserId__36B12243");
        });

        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Audit_Lo__3214EC0731DD2909");

            entity.ToTable("Audit_Logs", tb => tb.HasTrigger("trg_Audit_Logs_Immutable"));

            entity.HasIndex(e => new { e.UserId, e.CreatedAt }, "IX_Audit_Logs_User_Time").IsDescending(false, true);

            entity.Property(e => e.ActionType)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.IpAddress)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.TargetEntity)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.TargetId)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.UserAgent).HasMaxLength(255);

            entity.HasOne(d => d.ImpersonatedStudent).WithMany(p => p.AuditLogs)
                .HasForeignKey(d => d.ImpersonatedStudentId)
                .HasConstraintName("FK__Audit_Log__Imper__4316F928");

            entity.HasOne(d => d.User).WithMany(p => p.AuditLogs)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("FK__Audit_Log__UserI__4222D4EF");
        });

        modelBuilder.Entity<Bed>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Beds__3214EC07F56B2B51");

            entity.HasIndex(e => new { e.RoomId, e.Status }, "IX_Beds_Room_Status");

            entity.HasIndex(e => new { e.RoomId, e.BedCode }, "UQ_Bed_Code").IsUnique();

            entity.Property(e => e.BedCode)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.PositionInRoom)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Status)
                .HasMaxLength(30)
                .IsUnicode(false)
                .HasDefaultValue("Free");

            entity.HasOne(d => d.Room).WithMany(p => p.Beds)
                .HasForeignKey(d => d.RoomId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Beds__RoomId__73BA3083");
        });

        modelBuilder.Entity<BedPrice>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Bed_Pric__3214EC078BE3DEF6");

            entity.ToTable("Bed_Prices", tb => tb.HasTrigger("trg_BedPrices_NoOverlap"));

            entity.Property(e => e.UnitPrice).HasColumnType("decimal(18, 2)");

            entity.HasOne(d => d.Bed).WithMany(p => p.BedPrices)
                .HasForeignKey(d => d.BedId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Bed_Price__BedId__2180FB33");

            entity.HasOne(d => d.Term).WithMany(p => p.BedPrices)
                .HasForeignKey(d => d.TermId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Bed_Price__TermI__22751F6C");
        });

        modelBuilder.Entity<BedTransferRequest>(entity =>
        {
            entity.HasKey(e => e.RequestId).HasName("PK__Bed_Tran__33A8517AFC55419F");

            entity.ToTable("Bed_Transfer_Requests", tb => tb.HasTrigger("trg_Transfer_GenderMatch"));

            entity.HasIndex(e => e.ToBedId, "IX_Transfer_ToBed");

            entity.Property(e => e.RequestId).ValueGeneratedNever();
            entity.Property(e => e.AmountAlreadyPaid).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.NewBedAmount)
                .HasComputedColumnSql("([NewBedDailyRate]*[DaysRemaining])", true)
                .HasColumnType("decimal(29, 2)");
            entity.Property(e => e.NewBedDailyRate).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.OldBedAmount)
                .HasComputedColumnSql("([OldBedDailyRate]*[DaysStayed])", true)
                .HasColumnType("decimal(29, 2)");
            entity.Property(e => e.OldBedDailyRate).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.PriceDifference)
                .HasComputedColumnSql("(([OldBedDailyRate]*[DaysStayed]+[NewBedDailyRate]*[DaysRemaining])-[AmountAlreadyPaid])", true)
                .HasColumnType("decimal(31, 2)");
            entity.Property(e => e.ReviewedTime).HasColumnType("datetime");
            entity.Property(e => e.TotalAmount)
                .HasComputedColumnSql("([OldBedDailyRate]*[DaysStayed]+[NewBedDailyRate]*[DaysRemaining])", true)
                .HasColumnType("decimal(30, 2)");
            entity.Property(e => e.TransferReason).HasMaxLength(500);

            entity.HasOne(d => d.AssignedGuard).WithMany(p => p.BedTransferRequests)
                .HasForeignKey(d => d.AssignedGuardId)
                .HasConstraintName("FK__Bed_Trans__Assig__57DD0BE4");

            entity.HasOne(d => d.FromBed).WithMany(p => p.BedTransferRequestFromBeds)
                .HasForeignKey(d => d.FromBedId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Bed_Trans__FromB__55009F39");

            entity.HasOne(d => d.FromResidency).WithMany(p => p.BedTransferRequestFromResidencies)
                .HasForeignKey(d => d.FromResidencyId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Bed_Trans__FromR__540C7B00");

            entity.HasOne(d => d.NewResidency).WithMany(p => p.BedTransferRequestNewResidencies)
                .HasForeignKey(d => d.NewResidencyId)
                .HasConstraintName("FK__Bed_Trans__NewRe__58D1301D");

            entity.HasOne(d => d.Request).WithOne(p => p.BedTransferRequest)
                .HasForeignKey<BedTransferRequest>(d => d.RequestId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Bed_Trans__Reque__531856C7");

            entity.HasOne(d => d.ReviewedBy).WithMany(p => p.BedTransferRequests)
                .HasForeignKey(d => d.ReviewedById)
                .HasConstraintName("FK__Bed_Trans__Revie__56E8E7AB");

            entity.HasOne(d => d.ToBed).WithMany(p => p.BedTransferRequestToBeds)
                .HasForeignKey(d => d.ToBedId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Bed_Trans__ToBed__55F4C372");
        });

        modelBuilder.Entity<BillingPeriod>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Billing___3214EC07CA8A72CC");

            entity.ToTable("Billing_Periods");

            entity.HasIndex(e => new { e.TargetMonth, e.TargetYear }, "UQ_BillingPeriod").IsUnique();

            entity.Property(e => e.CalculatedTime).HasColumnType("datetime");
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("Open");

            entity.HasOne(d => d.Term).WithMany(p => p.BillingPeriods)
                .HasForeignKey(d => d.TermId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Billing_P__TermI__634EBE90");
        });

        modelBuilder.Entity<Block>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Blocks__3214EC07F117F1D5");

            entity.HasIndex(e => new { e.DormId, e.Code }, "UQ_Block_Code").IsUnique();

            entity.Property(e => e.Code)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.GenderType)
                .HasMaxLength(10)
                .IsUnicode(false);
            entity.Property(e => e.Name).HasMaxLength(100);
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("Active");

            entity.HasOne(d => d.Dorm).WithMany(p => p.Blocks)
                .HasForeignKey(d => d.DormId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Blocks__DormId__628FA481");
        });

        modelBuilder.Entity<BookingRequest>(entity =>
        {
            entity.HasKey(e => e.RequestId).HasName("PK__Booking___33A8517A6A60A479");

            entity.ToTable("Booking_Requests", tb => tb.HasTrigger("trg_Booking_GenderMatch"));

            entity.Property(e => e.RequestId).ValueGeneratedNever();
            entity.Property(e => e.BookingKind)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("Booking");
            entity.Property(e => e.PaymentDeadline).HasColumnType("datetime");

            entity.HasOne(d => d.Bed).WithMany(p => p.BookingRequests)
                .HasForeignKey(d => d.BedId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Booking_R__BedId__395884C4");

            entity.HasOne(d => d.KeepFromResidency).WithMany(p => p.BookingRequests)
                .HasForeignKey(d => d.KeepFromResidencyId)
                .HasConstraintName("FK_Booking_KeepResidency");

            entity.HasOne(d => d.Request).WithOne(p => p.BookingRequest)
                .HasForeignKey<BookingRequest>(d => d.RequestId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Booking_R__Reque__3864608B");
        });

        modelBuilder.Entity<Campus>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Campuses__3214EC07E4AD92B4");

            entity.HasIndex(e => e.Code, "UQ__Campuses__A25C5AA7B74A7DF3").IsUnique();

            entity.Property(e => e.Code)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Name).HasMaxLength(100);
        });

        modelBuilder.Entity<CheckoutRequest>(entity =>
        {
            entity.HasKey(e => e.RequestId).HasName("PK__Checkout__33A8517A96FE33D3");

            entity.ToTable("Checkout_Requests");

            entity.Property(e => e.RequestId).ValueGeneratedNever();
            entity.Property(e => e.ExceptionNote).HasMaxLength(500);
            entity.Property(e => e.ReviewedTime).HasColumnType("datetime");

            entity.HasOne(d => d.AssignedGuard).WithMany(p => p.CheckoutRequests)
                .HasForeignKey(d => d.AssignedGuardId)
                .HasConstraintName("FK__Checkout___Assig__4E53A1AA");

            entity.HasOne(d => d.Request).WithOne(p => p.CheckoutRequest)
                .HasForeignKey<CheckoutRequest>(d => d.RequestId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Checkout___Reque__4B7734FF");

            entity.HasOne(d => d.Residency).WithMany(p => p.CheckoutRequests)
                .HasForeignKey(d => d.ResidencyId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Checkout___Resid__4C6B5938");

            entity.HasOne(d => d.ReviewedBy).WithMany(p => p.CheckoutRequests)
                .HasForeignKey(d => d.ReviewedById)
                .HasConstraintName("FK__Checkout___Revie__4D5F7D71");
        });

        modelBuilder.Entity<ComplaintEvidence>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Complain__3214EC07B16AD9A8");

            entity.ToTable("Complaint_Evidences");

            entity.Property(e => e.FileUrl)
                .HasMaxLength(500)
                .IsUnicode(false)
                .HasColumnName("FileURL");

            entity.HasOne(d => d.ComplaintRequest).WithMany(p => p.ComplaintEvidences)
                .HasForeignKey(d => d.ComplaintRequestId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Complaint__Compl__23F3538A");
        });

        modelBuilder.Entity<ComplaintRequest>(entity =>
        {
            entity.HasKey(e => e.RequestId).HasName("PK__Complain__33A8517AAC6B7058");

            entity.ToTable("Complaint_Requests", tb => tb.HasTrigger("trg_Complaint_OnePending"));

            entity.Property(e => e.RequestId).ValueGeneratedNever();
            entity.Property(e => e.Content).HasMaxLength(1000);
            entity.Property(e => e.ResolutionNote).HasMaxLength(500);
            entity.Property(e => e.TargetType)
                .HasMaxLength(20)
                .IsUnicode(false);

            entity.HasOne(d => d.Handover).WithMany(p => p.ComplaintRequests)
                .HasForeignKey(d => d.HandoverId)
                .HasConstraintName("FK__Complaint__Hando__1D4655FB");

            entity.HasOne(d => d.Invoice).WithMany(p => p.ComplaintRequests)
                .HasForeignKey(d => d.InvoiceId)
                .HasConstraintName("FK__Complaint__Invoi__1E3A7A34");

            entity.HasOne(d => d.Request).WithOne(p => p.ComplaintRequest)
                .HasForeignKey<ComplaintRequest>(d => d.RequestId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Complaint__Reque__1C5231C2");

            entity.HasOne(d => d.Violation).WithMany(p => p.ComplaintRequests)
                .HasForeignKey(d => d.ViolationId)
                .HasConstraintName("FK__Complaint__Viola__1F2E9E6D");
        });

        modelBuilder.Entity<Dorm>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Dorms__3214EC07E1BA8CB5");

            entity.HasIndex(e => e.Name, "UQ__Dorms__737584F6382767DE").IsUnique();

            entity.HasIndex(e => e.Code, "UQ__Dorms__A25C5AA703D05168").IsUnique();

            entity.Property(e => e.Address).HasMaxLength(255);
            entity.Property(e => e.Code)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Name).HasMaxLength(100);
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("Active");

            entity.HasOne(d => d.Campus).WithMany(p => p.Dorms)
                .HasForeignKey(d => d.CampusId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Dorms__CampusId__5CD6CB2B");
        });

        modelBuilder.Entity<Equipment>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Equipmen__3214EC070F0F354E");

            entity.HasIndex(e => e.RoomId, "IX_Equipments_Room");

            entity.HasIndex(e => e.AssetCode, "UQ__Equipmen__2DDE5240F364D659").IsUnique();

            entity.Property(e => e.AssetCode)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.Condition)
                .HasMaxLength(30)
                .HasDefaultValue("Good");
            entity.Property(e => e.Note).HasMaxLength(500);

            entity.HasOne(d => d.Bed).WithMany(p => p.Equipment)
                .HasForeignKey(d => d.BedId)
                .HasConstraintName("FK__Equipment__BedId__03F0984C");

            entity.HasOne(d => d.Catalog).WithMany(p => p.Equipment)
                .HasForeignKey(d => d.CatalogId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Equipment__Catal__04E4BC85");

            entity.HasOne(d => d.Room).WithMany(p => p.Equipment)
                .HasForeignKey(d => d.RoomId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Equipment__RoomI__02FC7413");
        });

        modelBuilder.Entity<EquipmentCatalog>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Equipmen__3214EC073B406AD2");

            entity.ToTable("Equipment_Catalogs");

            entity.HasIndex(e => new { e.Category, e.Name }, "UQ_EquipCatalog").IsUnique();

            entity.Property(e => e.ActiveFlag).HasDefaultValue(true);
            entity.Property(e => e.Category).HasMaxLength(100);
            entity.Property(e => e.Name).HasMaxLength(100);
        });

        modelBuilder.Entity<Guard>(entity =>
        {
            entity.HasKey(e => e.UserId).HasName("PK__Guards__1788CC4C91722B38");

            entity.Property(e => e.UserId).ValueGeneratedNever();

            entity.HasOne(d => d.User).WithOne(p => p.Guard)
                .HasForeignKey<Guard>(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Guards__UserId__3C69FB99");
        });

        modelBuilder.Entity<Handover>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Handover__3214EC073BD8BDEF");

            entity.HasIndex(e => e.RequestId, "UX_Handover_Request")
                .IsUnique()
                .HasFilter("([RequestId] IS NOT NULL)");

            entity.HasIndex(e => e.ResidencyId, "UX_Handover_Residency")
                .IsUnique()
                .HasFilter("([ResidencyId] IS NOT NULL)");

            entity.Property(e => e.BelongingsNote).HasMaxLength(500);
            entity.Property(e => e.CompletedTime).HasColumnType("datetime");
            entity.Property(e => e.CreatedTime)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.HandoverType)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.ManagerNote).HasMaxLength(500);
            entity.Property(e => e.ManagerReviewedTime).HasColumnType("datetime");
            entity.Property(e => e.Status)
                .HasMaxLength(30)
                .IsUnicode(false)
                .HasDefaultValue("Draft");
            entity.Property(e => e.StudentAcceptedTime).HasColumnType("datetime");
            entity.Property(e => e.TotalCompensation).HasColumnType("decimal(18, 2)");

            entity.HasOne(d => d.Guard).WithMany(p => p.Handovers)
                .HasForeignKey(d => d.GuardId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Handovers__Guard__1D7B6025");

            entity.HasOne(d => d.ManagerReviewedBy).WithMany(p => p.Handovers)
                .HasForeignKey(d => d.ManagerReviewedById)
                .HasConstraintName("FK__Handovers__Manag__2334397B");

            entity.HasOne(d => d.Request).WithOne(p => p.Handover)
                .HasForeignKey<Handover>(d => d.RequestId)
                .HasConstraintName("FK__Handovers__Reque__1A9EF37A");

            entity.HasOne(d => d.Residency).WithOne(p => p.Handover)
                .HasForeignKey<Handover>(d => d.ResidencyId)
                .HasConstraintName("FK__Handovers__Resid__1B9317B3");

            entity.HasOne(d => d.Room).WithMany(p => p.Handovers)
                .HasForeignKey(d => d.RoomId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Handovers__RoomI__1C873BEC");
        });

        modelBuilder.Entity<HandoverItem>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Handover__3214EC0744AEA44C");

            entity.ToTable("Handover_Items");

            entity.HasIndex(e => new { e.HandoverId, e.EquipmentId }, "UQ_HandoverItem").IsUnique();

            entity.Property(e => e.CompensationAmount).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.ItemCondition)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("Good");
            entity.Property(e => e.Note).HasMaxLength(500);

            entity.HasOne(d => d.Equipment).WithMany(p => p.HandoverItems)
                .HasForeignKey(d => d.EquipmentId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Handover___Equip__2DB1C7EE");

            entity.HasOne(d => d.Handover).WithMany(p => p.HandoverItems)
                .HasForeignKey(d => d.HandoverId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Handover___Hando__2CBDA3B5");
        });

        modelBuilder.Entity<HandoverPhoto>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Handover__3214EC077065DABD");

            entity.ToTable("Handover_Photos");

            entity.Property(e => e.FileUrl)
                .HasMaxLength(500)
                .IsUnicode(false)
                .HasColumnName("FileURL");

            entity.HasOne(d => d.HandoverItem).WithMany(p => p.HandoverPhotos)
                .HasForeignKey(d => d.HandoverItemId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Handover___Hando__345EC57D");
        });

        modelBuilder.Entity<ImportBatch>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Import_B__3214EC07229C722B");

            entity.ToTable("Import_Batches");

            entity.Property(e => e.FileName)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.ImportTime)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.Method)
                .HasMaxLength(20)
                .IsUnicode(false);

            entity.HasOne(d => d.BillingPeriod).WithMany(p => p.ImportBatches)
                .HasForeignKey(d => d.BillingPeriodId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Import_Ba__Billi__690797E6");

            entity.HasOne(d => d.Block).WithMany(p => p.ImportBatches)
                .HasForeignKey(d => d.BlockId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Import_Ba__Block__69FBBC1F");

            entity.HasOne(d => d.Guard).WithMany(p => p.ImportBatches)
                .HasForeignKey(d => d.GuardId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Import_Ba__Guard__6AEFE058");
        });

        modelBuilder.Entity<IncidentTicket>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Incident__3214EC07FC656E97");

            entity.ToTable("Incident_Tickets");

            entity.HasIndex(e => e.RoomId, "IX_Tickets_Room");

            entity.HasIndex(e => new { e.Status, e.DoneTime }, "IX_Tickets_Status_DoneTime");

            entity.HasIndex(e => e.TicketCode, "UQ__Incident__598CF7A39E2F8F3D").IsUnique();

            entity.Property(e => e.ClosedTime).HasColumnType("datetime");
            entity.Property(e => e.CreatedTime)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.Description).HasMaxLength(1000);
            entity.Property(e => e.DoneTime).HasColumnType("datetime");
            entity.Property(e => e.EscalationReason).HasMaxLength(500);
            entity.Property(e => e.HandlingResult).HasMaxLength(1000);
            entity.Property(e => e.ManagerNote).HasMaxLength(500);
            entity.Property(e => e.ReopenReason).HasMaxLength(500);
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("New");
            entity.Property(e => e.TicketCode)
                .HasMaxLength(50)
                .IsUnicode(false);

            entity.HasOne(d => d.AssignedBy).WithMany(p => p.IncidentTickets)
                .HasForeignKey(d => d.AssignedById)
                .HasConstraintName("FK__Incident___Assig__68D28DBC");

            entity.HasOne(d => d.AssignedGuard).WithMany(p => p.IncidentTickets)
                .HasForeignKey(d => d.AssignedGuardId)
                .HasConstraintName("FK__Incident___Assig__69C6B1F5");

            entity.HasOne(d => d.Equipment).WithMany(p => p.IncidentTickets)
                .HasForeignKey(d => d.EquipmentId)
                .HasConstraintName("FK__Incident___Equip__67DE6983");

            entity.HasOne(d => d.Room).WithMany(p => p.IncidentTickets)
                .HasForeignKey(d => d.RoomId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Incident___RoomI__66EA454A");

            entity.HasOne(d => d.Student).WithMany(p => p.IncidentTickets)
                .HasForeignKey(d => d.StudentId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Incident___Stude__65F62111");
        });

        modelBuilder.Entity<InspectionPhoto>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Inspecti__3214EC07EFAC3D4B");

            entity.ToTable("Inspection_Photos");

            entity.Property(e => e.FileUrl)
                .HasMaxLength(500)
                .IsUnicode(false)
                .HasColumnName("FileURL");

            entity.HasOne(d => d.InspectionRequest).WithMany(p => p.InspectionPhotos)
                .HasForeignKey(d => d.InspectionRequestId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Inspectio__Inspe__10216507");
        });

        modelBuilder.Entity<InspectionRequest>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Inspecti__3214EC07FCA1BD6D");

            entity.ToTable("Inspection_Requests");

            entity.Property(e => e.ApprovedTime).HasColumnType("datetime");
            entity.Property(e => e.CompletedTime).HasColumnType("datetime");
            entity.Property(e => e.CreatedTime)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.GuardNote).HasMaxLength(500);
            entity.Property(e => e.ManagerNote).HasMaxLength(500);
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("Requested");
            entity.Property(e => e.VerifiedValue).HasColumnType("decimal(18, 2)");

            entity.HasOne(d => d.Anomaly).WithMany(p => p.InspectionRequests)
                .HasForeignKey(d => d.AnomalyId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Inspectio__Anoma__0880433F");

            entity.HasOne(d => d.AssignedGuard).WithMany(p => p.InspectionRequests)
                .HasForeignKey(d => d.AssignedGuardId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Inspectio__Assig__0A688BB1");

            entity.HasOne(d => d.CreatedBy).WithMany(p => p.InspectionRequests)
                .HasForeignKey(d => d.CreatedById)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Inspectio__Creat__09746778");
        });

        modelBuilder.Entity<Invoice>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Invoices__3214EC07F33F57B0");

            entity.ToTable(tb => tb.HasTrigger("trg_Invoices_PaidImmutable"));

            entity.HasIndex(e => new { e.Status, e.DueDate }, "IX_Invoices_Status_DueDate");

            entity.HasIndex(e => new { e.StudentId, e.Status }, "IX_Invoices_Student_Status");

            entity.HasIndex(e => e.InvoiceNumber, "UQ__Invoices__D776E981277FBAC2").IsUnique();

            entity.Property(e => e.AdjustmentKind)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Amount).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.CancelReason).HasMaxLength(500);
            entity.Property(e => e.InvoiceNumber)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.InvoiceType)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.IssuedTime)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.PaidTime).HasColumnType("datetime");
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("Unpaid");

            entity.HasOne(d => d.BookingRequest).WithMany(p => p.Invoices)
                .HasForeignKey(d => d.BookingRequestId)
                .HasConstraintName("FK__Invoices__Bookin__3A179ED3");

            entity.HasOne(d => d.Handover).WithMany(p => p.Invoices)
                .HasForeignKey(d => d.HandoverId)
                .HasConstraintName("FK__Invoices__Handov__3CF40B7E");

            entity.HasOne(d => d.ParentInvoice).WithMany(p => p.InverseParentInvoice)
                .HasForeignKey(d => d.ParentInvoiceId)
                .HasConstraintName("FK__Invoices__Parent__3DE82FB7");

            entity.HasOne(d => d.Student).WithMany(p => p.Invoices)
                .HasForeignKey(d => d.StudentId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Invoices__Studen__382F5661");

            entity.HasOne(d => d.Term).WithMany(p => p.Invoices)
                .HasForeignKey(d => d.TermId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Invoices__TermId__39237A9A");

            entity.HasOne(d => d.TransferRequest).WithMany(p => p.Invoices)
                .HasForeignKey(d => d.TransferRequestId)
                .HasConstraintName("FK__Invoices__Transf__3B0BC30C");

            entity.HasOne(d => d.UtilityBill).WithMany(p => p.Invoices)
                .HasForeignKey(d => d.UtilityBillId)
                .HasConstraintName("FK__Invoices__Utilit__3BFFE745");
        });

        modelBuilder.Entity<InvoiceItem>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Invoice___3214EC0703D6358B");

            entity.ToTable("Invoice_Items", tb => tb.HasTrigger("trg_InvoiceItems_LockedAfterUnpaid"));

            entity.Property(e => e.Amount).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.Description).HasMaxLength(255);
            entity.Property(e => e.Quantity)
                .HasDefaultValue(1m)
                .HasColumnType("decimal(18, 2)");
            entity.Property(e => e.UnitPrice).HasColumnType("decimal(18, 2)");

            entity.HasOne(d => d.Invoice).WithMany(p => p.InvoiceItems)
                .HasForeignKey(d => d.InvoiceId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Invoice_I__Invoi__4959E263");
        });

        modelBuilder.Entity<Manager>(entity =>
        {
            entity.HasKey(e => e.UserId).HasName("PK__Managers__1788CC4CF50E5151");

            entity.Property(e => e.UserId).ValueGeneratedNever();

            entity.HasOne(d => d.User).WithOne(p => p.Manager)
                .HasForeignKey<Manager>(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Managers__UserId__398D8EEE");
        });

        modelBuilder.Entity<MeterReading>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Meter_Re__3214EC075B50FC16");

            entity.ToTable("Meter_Readings");

            entity.HasIndex(e => new { e.RoomId, e.BillingPeriodId }, "IX_Meter_Room");

            entity.HasIndex(e => new { e.CheckoutRequestId, e.RoomId, e.MeterType }, "UX_Meter_Checkout")
                .IsUnique()
                .HasFilter("([CheckoutRequestId] IS NOT NULL)");

            entity.HasIndex(e => new { e.RoomId, e.MeterType, e.BillingPeriodId }, "UX_Meter_Monthly")
                .IsUnique()
                .HasFilter("([BillingPeriodId] IS NOT NULL AND [CheckoutRequestId] IS NULL AND [TransferRequestId] IS NULL)");

            entity.HasIndex(e => new { e.TransferRequestId, e.RoomId, e.MeterType }, "UX_Meter_Transfer")
                .IsUnique()
                .HasFilter("([TransferRequestId] IS NOT NULL)");

            entity.Property(e => e.Consumption)
                .HasComputedColumnSql("([CurrentValue]-[PreviousValue])", true)
                .HasColumnType("decimal(19, 2)");
            entity.Property(e => e.CurrentValue).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.MeterType)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Note).HasMaxLength(500);
            entity.Property(e => e.PreviousValue).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.RecordedTime)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.Source)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("Import");
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("Recorded");

            entity.HasOne(d => d.Batch).WithMany(p => p.MeterReadings)
                .HasForeignKey(d => d.BatchId)
                .HasConstraintName("FK__Meter_Rea__Batch__73852659");

            entity.HasOne(d => d.BillingPeriod).WithMany(p => p.MeterReadings)
                .HasForeignKey(d => d.BillingPeriodId)
                .HasConstraintName("FK__Meter_Rea__Billi__719CDDE7");

            entity.HasOne(d => d.CheckoutRequest).WithMany(p => p.MeterReadings)
                .HasForeignKey(d => d.CheckoutRequestId)
                .HasConstraintName("FK__Meter_Rea__Check__74794A92");

            entity.HasOne(d => d.RecordedBy).WithMany(p => p.MeterReadings)
                .HasForeignKey(d => d.RecordedById)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Meter_Rea__Recor__7849DB76");

            entity.HasOne(d => d.Room).WithMany(p => p.MeterReadings)
                .HasForeignKey(d => d.RoomId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Meter_Rea__RoomI__72910220");

            entity.HasOne(d => d.TransferRequest).WithMany(p => p.MeterReadings)
                .HasForeignKey(d => d.TransferRequestId)
                .HasConstraintName("FK__Meter_Rea__Trans__756D6ECB");
        });

        modelBuilder.Entity<News>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__News__3214EC07553F91F9");

            entity.Property(e => e.Category)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasDefaultValue("News");
            entity.Property(e => e.PublishedTime).HasColumnType("datetime");
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasDefaultValue("Draft");
            entity.Property(e => e.Title).HasMaxLength(255);

            entity.HasOne(d => d.ApprovedBy).WithMany(p => p.NewsApprovedBies)
                .HasForeignKey(d => d.ApprovedById)
                .HasConstraintName("FK__News__ApprovedBy__4E88ABD4");

            entity.HasOne(d => d.Author).WithMany(p => p.NewsAuthors)
                .HasForeignKey(d => d.AuthorId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__News__AuthorId__4D94879B");
        });

        modelBuilder.Entity<NewsAttachment>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__News_Att__3214EC0763AE8A27");

            entity.ToTable("News_Attachments");

            entity.Property(e => e.FileType)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.FileUrl)
                .HasMaxLength(500)
                .IsUnicode(false)
                .HasColumnName("FileURL");

            entity.HasOne(d => d.News).WithMany(p => p.NewsAttachments)
                .HasForeignKey(d => d.NewsId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__News_Atta__NewsI__5535A963");
        });

        modelBuilder.Entity<Notification>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Notifica__3214EC07A4672EEA");

            entity.HasIndex(e => new { e.UserId, e.ReadFlag, e.SentTime }, "IX_Notifications_User").IsDescending(false, false, true);

            entity.Property(e => e.Channel)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("InApp");
            entity.Property(e => e.ReadTime).HasColumnType("datetime");
            entity.Property(e => e.ReferenceId)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.ReferenceType)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.SentTime)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.Title).HasMaxLength(200);

            entity.HasOne(d => d.User).WithMany(p => p.Notifications)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Notificat__UserI__46E78A0C");
        });

        modelBuilder.Entity<PaymentTransaction>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Payment___3214EC075A4C7E8F");

            entity.ToTable("Payment_Transactions");

            entity.HasIndex(e => new { e.Status, e.ExpiryTime }, "IX_Payment_Status_Expiry");

            entity.HasIndex(e => e.GatewayReference, "UQ__Payment___0D7A2497667E1EAF").IsUnique();

            entity.Property(e => e.CallbackTime).HasColumnType("datetime");
            entity.Property(e => e.CreatedTime)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.ExpiryTime).HasColumnType("datetime");
            entity.Property(e => e.GatewayReference)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.Method)
                .HasMaxLength(30)
                .IsUnicode(false)
                .HasDefaultValue("VietQR");
            entity.Property(e => e.PaidTime).HasColumnType("datetime");
            entity.Property(e => e.QrLink)
                .HasMaxLength(500)
                .IsUnicode(false);
            entity.Property(e => e.ReceiptNumber)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("Processing");
            entity.Property(e => e.TotalAmount).HasColumnType("decimal(18, 2)");

            entity.HasOne(d => d.Student).WithMany(p => p.PaymentTransactions)
                .HasForeignKey(d => d.StudentId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Payment_T__Stude__4F12BBB9");
        });

        modelBuilder.Entity<PriceItem>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Price_It__3214EC07EF37D042");

            entity.ToTable("Price_Items");

            entity.HasIndex(e => new { e.PriceListId, e.DormId, e.RoomTypeId }, "UQ_PriceItem").IsUnique();

            entity.Property(e => e.UnitPrice).HasColumnType("decimal(18, 2)");

            entity.HasOne(d => d.Dorm).WithMany(p => p.PriceItems)
                .HasForeignKey(d => d.DormId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Price_Ite__DormI__1CBC4616");

            entity.HasOne(d => d.PriceList).WithMany(p => p.PriceItems)
                .HasForeignKey(d => d.PriceListId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Price_Ite__Price__1BC821DD");

            entity.HasOne(d => d.RoomType).WithMany(p => p.PriceItems)
                .HasForeignKey(d => d.RoomTypeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Price_Ite__RoomT__1DB06A4F");
        });

        modelBuilder.Entity<PriceList>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Price_Li__3214EC072B053555");

            entity.ToTable("Price_Lists");

            entity.HasIndex(e => e.TermId, "UQ__Price_Li__410A21A46DDADDE6").IsUnique();

            entity.Property(e => e.CreationDate)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.PublishedTime).HasColumnType("datetime");
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("Draft");

            entity.HasOne(d => d.CreatedBy).WithMany(p => p.PriceLists)
                .HasForeignKey(d => d.CreatedById)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Price_Lis__Creat__17036CC0");

            entity.HasOne(d => d.Term).WithOne(p => p.PriceList)
                .HasForeignKey<PriceList>(d => d.TermId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Price_Lis__TermI__14270015");
        });

        modelBuilder.Entity<Refund>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Refunds__3214EC07078E3F5D");

            entity.Property(e => e.Amount).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.CreatedTime)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.Reason).HasMaxLength(255);
            entity.Property(e => e.RefundTime).HasColumnType("datetime");
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("Pending");

            entity.HasOne(d => d.RelatedInvoice).WithMany(p => p.Refunds)
                .HasForeignKey(d => d.RelatedInvoiceId)
                .HasConstraintName("FK__Refunds__Related__5D60DB10");

            entity.HasOne(d => d.RelatedRequest).WithMany(p => p.Refunds)
                .HasForeignKey(d => d.RelatedRequestId)
                .HasConstraintName("FK__Refunds__Related__5C6CB6D7");

            entity.HasOne(d => d.Transaction).WithMany(p => p.Refunds)
                .HasForeignKey(d => d.TransactionId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Refunds__Transac__5B78929E");
        });

        modelBuilder.Entity<RegistrationPeriod>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Registra__3214EC070E4AD3CA");

            entity.ToTable("Registration_Periods", tb => tb.HasTrigger("trg_RegPeriod_RequirePublishedPrice"));

            entity.HasIndex(e => new { e.TermId, e.PeriodType }, "UX_RegPeriod_ActivePerType")
                .IsUnique()
                .HasFilter("([ActiveFlag]=(1))");

            entity.Property(e => e.ActiveFlag).HasDefaultValue(true);
            entity.Property(e => e.PeriodType)
                .HasMaxLength(20)
                .IsUnicode(false);

            entity.HasOne(d => d.Term).WithMany(p => p.RegistrationPeriods)
                .HasForeignKey(d => d.TermId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Registrat__TermI__0D7A0286");
        });

        modelBuilder.Entity<RepairFeedback>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Repair_F__3214EC07E2B58C2C");

            entity.ToTable("Repair_Feedbacks");

            entity.HasIndex(e => e.TicketId, "UQ__Repair_F__712CC606C38D45B4").IsUnique();

            entity.Property(e => e.Comment).HasMaxLength(500);
            entity.Property(e => e.SubmittedTime)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");

            entity.HasOne(d => d.Student).WithMany(p => p.RepairFeedbacks)
                .HasForeignKey(d => d.StudentId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Repair_Fe__Stude__7DCDAAA2");

            entity.HasOne(d => d.Ticket).WithOne(p => p.RepairFeedback)
                .HasForeignKey<RepairFeedback>(d => d.TicketId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Repair_Fe__Ticke__7CD98669");
        });

        modelBuilder.Entity<Request>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Requests__3214EC07F0CFEE86");

            entity.ToTable(tb => tb.HasTrigger("trg_Requests_BlockWhenOverdue"));

            entity.HasIndex(e => new { e.Status, e.RequestType }, "IX_Requests_Status_Type");

            entity.HasIndex(e => new { e.StudentId, e.RequestType }, "IX_Requests_Student");

            entity.Property(e => e.CancelReason).HasMaxLength(500);
            entity.Property(e => e.CreatedTime)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.ProcessedTime).HasColumnType("datetime");
            entity.Property(e => e.Reason).HasMaxLength(500);
            entity.Property(e => e.RejectReason).HasMaxLength(500);
            entity.Property(e => e.RequestType)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Status)
                .HasMaxLength(30)
                .IsUnicode(false)
                .HasDefaultValue("Pending");

            entity.HasOne(d => d.ProcessedBy).WithMany(p => p.Requests)
                .HasForeignKey(d => d.ProcessedById)
                .HasConstraintName("FK__Requests__Proces__31B762FC");

            entity.HasOne(d => d.Student).WithMany(p => p.Requests)
                .HasForeignKey(d => d.StudentId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Requests__Studen__2DE6D218");

            entity.HasOne(d => d.Term).WithMany(p => p.Requests)
                .HasForeignKey(d => d.TermId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Requests__TermId__2EDAF651");
        });

        modelBuilder.Entity<RequestLog>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Request___3214EC072E852CEE");

            entity.ToTable("Request_Logs");

            entity.HasIndex(e => e.RequestId, "IX_Request_Logs_Request");

            entity.Property(e => e.ChangeTime)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.FromStatus)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.Note).HasMaxLength(500);
            entity.Property(e => e.ToStatus)
                .HasMaxLength(30)
                .IsUnicode(false);

            entity.HasOne(d => d.ChangedBy).WithMany(p => p.RequestLogs)
                .HasForeignKey(d => d.ChangedById)
                .HasConstraintName("FK__Request_L__Chang__5F7E2DAC");

            entity.HasOne(d => d.Request).WithMany(p => p.RequestLogs)
                .HasForeignKey(d => d.RequestId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Request_L__Reque__5D95E53A");
        });

        modelBuilder.Entity<Residency>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Residenc__3214EC07374334CD");

            entity.HasIndex(e => new { e.BedId, e.TermId }, "UX_Residency_Bed_Term")
                .IsUnique()
                .HasFilter("([Status] IN ('Reserved', 'Active'))");

            entity.HasIndex(e => new { e.StudentId, e.TermId }, "UX_Residency_Student_Term")
                .IsUnique()
                .HasFilter("([Status] IN ('Reserved', 'Active'))");

            entity.Property(e => e.RegistrationType)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("Reserved");

            entity.HasOne(d => d.Bed).WithMany(p => p.Residencies)
                .HasForeignKey(d => d.BedId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Residenci__BedId__41EDCAC5");

            entity.HasOne(d => d.SourceRequest).WithMany(p => p.Residencies)
                .HasForeignKey(d => d.SourceRequestId)
                .HasConstraintName("FK__Residenci__Sourc__43D61337");

            entity.HasOne(d => d.Student).WithMany(p => p.Residencies)
                .HasForeignKey(d => d.StudentId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Residenci__Stude__40F9A68C");

            entity.HasOne(d => d.Term).WithMany(p => p.Residencies)
                .HasForeignKey(d => d.TermId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Residenci__TermI__42E1EEFE");
        });

        modelBuilder.Entity<Role>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Roles__3214EC0732451338");

            entity.HasIndex(e => e.RoleName, "UQ__Roles__8A2B6160873AEB23").IsUnique();

            entity.Property(e => e.Description).HasMaxLength(255);
            entity.Property(e => e.RoleName)
                .HasMaxLength(50)
                .IsUnicode(false);
        });

        modelBuilder.Entity<Room>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Rooms__3214EC07F31E1756");

            entity.HasIndex(e => new { e.BlockId, e.RoomNumber }, "UQ_Room_Number").IsUnique();

            entity.Property(e => e.RoomNumber)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("Active");

            entity.HasOne(d => d.Block).WithMany(p => p.Rooms)
                .HasForeignKey(d => d.BlockId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Rooms__BlockId__6D0D32F4");

            entity.HasOne(d => d.RoomType).WithMany(p => p.Rooms)
                .HasForeignKey(d => d.RoomTypeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Rooms__RoomTypeI__6E01572D");
        });

        modelBuilder.Entity<RoomType>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Room_Typ__3214EC078D2E2D0C");

            entity.ToTable("Room_Types");

            entity.HasIndex(e => e.Name, "UQ__Room_Typ__737584F6E07B0AF2").IsUnique();

            entity.Property(e => e.Name).HasMaxLength(100);
        });

        modelBuilder.Entity<RoomTypeEquipment>(entity =>
        {
            entity.HasKey(e => new { e.RoomTypeId, e.CatalogId }).HasName("PK__Room_Typ__20ED858723264CAC");

            entity.ToTable("Room_Type_Equipments");

            entity.Property(e => e.StandardQuantity).HasDefaultValue(1);

            entity.HasOne(d => d.Catalog).WithMany(p => p.RoomTypeEquipments)
                .HasForeignKey(d => d.CatalogId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Room_Type__Catal__7D439ABD");

            entity.HasOne(d => d.RoomType).WithMany(p => p.RoomTypeEquipments)
                .HasForeignKey(d => d.RoomTypeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Room_Type__RoomT__7C4F7684");
        });

        modelBuilder.Entity<Student>(entity =>
        {
            entity.HasKey(e => e.UserId).HasName("PK__Students__1788CC4CF48551C9");

            entity.Property(e => e.UserId).ValueGeneratedNever();
            entity.Property(e => e.Cohort)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Major)
                .HasMaxLength(100)
                .IsUnicode(false);

            entity.HasOne(d => d.User).WithOne(p => p.Student)
                .HasForeignKey<Student>(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Students__UserId__3F466844");
        });

        modelBuilder.Entity<Term>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Terms__3214EC07C3DF6E9E");

            entity.HasIndex(e => new { e.Name, e.AcademicYear }, "UQ_Term").IsUnique();

            entity.Property(e => e.AcademicYear)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Name).HasMaxLength(100);
        });

        modelBuilder.Entity<TicketPhoto>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Ticket_P__3214EC073369F21F");

            entity.ToTable("Ticket_Photos");

            entity.Property(e => e.FileUrl)
                .HasMaxLength(500)
                .IsUnicode(false)
                .HasColumnName("FileURL");
            entity.Property(e => e.PhotoType)
                .HasMaxLength(10)
                .IsUnicode(false);
            entity.Property(e => e.UploadTime)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");

            entity.HasOne(d => d.Ticket).WithMany(p => p.TicketPhotos)
                .HasForeignKey(d => d.TicketId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Ticket_Ph__Ticke__7167D3BD");

            entity.HasOne(d => d.Uploader).WithMany(p => p.TicketPhotos)
                .HasForeignKey(d => d.UploaderId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Ticket_Ph__Uploa__73501C2F");
        });

        modelBuilder.Entity<TicketStatusHistory>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Ticket_S__3214EC075558CA1D");

            entity.ToTable("Ticket_Status_Histories");

            entity.Property(e => e.ChangeTime)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.FromStatus)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Note).HasMaxLength(500);
            entity.Property(e => e.ToStatus)
                .HasMaxLength(20)
                .IsUnicode(false);

            entity.HasOne(d => d.ChangedBy).WithMany(p => p.TicketStatusHistories)
                .HasForeignKey(d => d.ChangedById)
                .HasConstraintName("FK__Ticket_St__Chang__7814D14C");

            entity.HasOne(d => d.Ticket).WithMany(p => p.TicketStatusHistories)
                .HasForeignKey(d => d.TicketId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Ticket_St__Ticke__7720AD13");
        });

        modelBuilder.Entity<TransactionInvoice>(entity =>
        {
            entity.HasKey(e => new { e.TransactionId, e.InvoiceId }).HasName("PK__Transact__183A50C0CC9DFD06");

            entity.ToTable("Transaction_Invoices");

            entity.Property(e => e.AllocatedAmount).HasColumnType("decimal(18, 2)");

            entity.HasOne(d => d.Invoice).WithMany(p => p.TransactionInvoices)
                .HasForeignKey(d => d.InvoiceId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Transacti__Invoi__57A801BA");

            entity.HasOne(d => d.Transaction).WithMany(p => p.TransactionInvoices)
                .HasForeignKey(d => d.TransactionId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Transacti__Trans__56B3DD81");
        });

        modelBuilder.Entity<UsageAnomaly>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Usage_An__3214EC074BD7CC63");

            entity.ToTable("Usage_Anomalies");

            entity.Property(e => e.CreatedTime)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.FlagReason).HasMaxLength(255);
            entity.Property(e => e.ManagerNote).HasMaxLength(500);
            entity.Property(e => e.Resolution)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.ResolvedTime).HasColumnType("datetime");
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("Flagged");

            entity.HasOne(d => d.Reading).WithMany(p => p.UsageAnomalies)
                .HasForeignKey(d => d.ReadingId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Usage_Ano__Readi__00DF2177");

            entity.HasOne(d => d.ResolvedBy).WithMany(p => p.UsageAnomalies)
                .HasForeignKey(d => d.ResolvedById)
                .HasConstraintName("FK__Usage_Ano__Resol__02C769E9");
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Users__3214EC07E21F52F9");

            entity.HasIndex(e => e.Code, "UQ__Users__A25C5AA7FE2750B3").IsUnique();

            entity.HasIndex(e => e.Email, "UQ__Users__A9D105342A00E01E").IsUnique();

            entity.HasIndex(e => e.GoogleId, "UX_Users_GoogleId")
                .IsUnique()
                .HasFilter("([GoogleId] IS NOT NULL)");

            entity.Property(e => e.AccountStatus)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("Active");
            entity.Property(e => e.Code)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.Email)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.FullName).HasMaxLength(100);
            entity.Property(e => e.Gender)
                .HasMaxLength(10)
                .IsUnicode(false);
            entity.Property(e => e.GoogleId)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.PasswordHash)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.Phone)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.SuspendedAt).HasColumnType("datetime");
            entity.Property(e => e.SuspensionReason).HasMaxLength(255);

            entity.HasOne(d => d.SuspendedBy).WithMany(p => p.InverseSuspendedBy)
                .HasForeignKey(d => d.SuspendedById)
                .HasConstraintName("FK__Users__Suspended__276EDEB3");
        });

        modelBuilder.Entity<UserRole>(entity =>
        {
            entity.HasKey(e => new { e.UserId, e.RoleId }).HasName("PK__User_Rol__AF2760AD092DD977");

            entity.ToTable("User_Roles");

            entity.Property(e => e.AssignedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");

            entity.HasOne(d => d.AssignedBy).WithMany(p => p.UserRoleAssignedBies)
                .HasForeignKey(d => d.AssignedById)
                .HasConstraintName("FK__User_Role__Assig__33D4B598");

            entity.HasOne(d => d.Role).WithMany(p => p.UserRoles)
                .HasForeignKey(d => d.RoleId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__User_Role__RoleI__31EC6D26");

            entity.HasOne(d => d.User).WithMany(p => p.UserRoleUsers)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__User_Role__UserI__30F848ED");
        });

        modelBuilder.Entity<UtilityBill>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Utility___3214EC07B60F15DB");

            entity.ToTable("Utility_Bills");

            entity.HasIndex(e => new { e.RoomId, e.BillingPeriodId }, "UX_UtilityBill_Active")
                .IsUnique()
                .HasFilter("([Status]<>'Corrected')");

            entity.Property(e => e.CorrectionReason).HasMaxLength(500);
            entity.Property(e => e.ElectricityCost).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.ElectricityUnitPrice).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.PublishedTime).HasColumnType("datetime");
            entity.Property(e => e.SharePerStudent).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("Calculated");
            entity.Property(e => e.TotalCost).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.TotalKwh).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.TotalM3).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.WaterCost).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.WaterUnitPrice).HasColumnType("decimal(18, 2)");

            entity.HasOne(d => d.BillingPeriod).WithMany(p => p.UtilityBills)
                .HasForeignKey(d => d.BillingPeriodId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Utility_B__Billi__13F1F5EB");

            entity.HasOne(d => d.CorrectedFrom).WithMany(p => p.InverseCorrectedFrom)
                .HasForeignKey(d => d.CorrectedFromId)
                .HasConstraintName("FK__Utility_B__Corre__15DA3E5D");

            entity.HasOne(d => d.Room).WithMany(p => p.UtilityBills)
                .HasForeignKey(d => d.RoomId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Utility_B__RoomI__12FDD1B2");
        });

        modelBuilder.Entity<UtilityPrice>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Utility___3214EC07F5F03CA9");

            entity.ToTable("Utility_Prices");

            entity.HasIndex(e => new { e.TermId, e.MeterType }, "UQ_UtilityPrice").IsUnique();

            entity.Property(e => e.MeterType)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.UnitPrice).HasColumnType("decimal(18, 2)");

            entity.HasOne(d => d.Term).WithMany(p => p.UtilityPrices)
                .HasForeignKey(d => d.TermId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Utility_P__TermI__29221CFB");
        });

        modelBuilder.Entity<Violation>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Violatio__3214EC0755D9AA4F");

            entity.HasIndex(e => new { e.StudentId, e.Status }, "IX_Violations_Student");

            entity.HasIndex(e => e.Code, "UQ__Violatio__A25C5AA794BDC264").IsUnique();

            entity.Property(e => e.Code)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.ExplanationDeadline).HasColumnType("datetime");
            entity.Property(e => e.FinalLevel).HasMaxLength(100);
            entity.Property(e => e.FinalizedTime).HasColumnType("datetime");
            entity.Property(e => e.InitialLevel).HasMaxLength(100);
            entity.Property(e => e.ManagerNote).HasMaxLength(500);
            entity.Property(e => e.OccurrenceTime).HasColumnType("datetime");
            entity.Property(e => e.Status)
                .HasMaxLength(30)
                .IsUnicode(false)
                .HasDefaultValue("Recorded");

            entity.HasOne(d => d.EvaluatedBy).WithMany(p => p.Violations)
                .HasForeignKey(d => d.EvaluatedById)
                .HasConstraintName("FK__Violation__Evalu__0B27A5C0");

            entity.HasOne(d => d.RecordedBy).WithMany(p => p.Violations)
                .HasForeignKey(d => d.RecordedById)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Violation__Recor__0A338187");

            entity.HasOne(d => d.Residency).WithMany(p => p.Violations)
                .HasForeignKey(d => d.ResidencyId)
                .HasConstraintName("FK__Violation__Resid__084B3915");

            entity.HasOne(d => d.Room).WithMany(p => p.Violations)
                .HasForeignKey(d => d.RoomId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Violation__RoomI__075714DC");

            entity.HasOne(d => d.Student).WithMany(p => p.Violations)
                .HasForeignKey(d => d.StudentId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Violation__Stude__0662F0A3");

            entity.HasOne(d => d.ViolationType).WithMany(p => p.Violations)
                .HasForeignKey(d => d.ViolationTypeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Violation__Viola__093F5D4E");
        });

        modelBuilder.Entity<ViolationEvidence>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Violatio__3214EC07B8A08AE8");

            entity.ToTable("Violation_Evidences");

            entity.Property(e => e.FileUrl)
                .HasMaxLength(500)
                .IsUnicode(false)
                .HasColumnName("FileURL");
            entity.Property(e => e.Source)
                .HasMaxLength(20)
                .IsUnicode(false);

            entity.HasOne(d => d.Explanation).WithMany(p => p.ViolationEvidences)
                .HasForeignKey(d => d.ExplanationId)
                .HasConstraintName("FK__Violation__Expla__1881A0DE");

            entity.HasOne(d => d.Violation).WithMany(p => p.ViolationEvidences)
                .HasForeignKey(d => d.ViolationId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Violation__Viola__178D7CA5");
        });

        modelBuilder.Entity<ViolationExplanation>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Violatio__3214EC07CE1DED5F");

            entity.ToTable("Violation_Explanations");

            entity.HasIndex(e => e.ViolationId, "UQ__Violatio__18B6DC09BE22DF20").IsUnique();

            entity.Property(e => e.SubmissionTime)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");

            entity.HasOne(d => d.Student).WithMany(p => p.ViolationExplanations)
                .HasForeignKey(d => d.StudentId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Violation__Stude__13BCEBC1");

            entity.HasOne(d => d.Violation).WithOne(p => p.ViolationExplanation)
                .HasForeignKey<ViolationExplanation>(d => d.ViolationId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Violation__Viola__12C8C788");
        });

        modelBuilder.Entity<ViolationType>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Violatio__3214EC079E6C9D57");

            entity.ToTable("Violation_Types");

            entity.HasIndex(e => e.Name, "UQ__Violatio__737584F69526588D").IsUnique();

            entity.Property(e => e.Name).HasMaxLength(255);
        });

        modelBuilder.Entity<VwStudentOutstanding>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("vw_Student_Outstanding");

            entity.Property(e => e.OutstandingAmount).HasColumnType("decimal(38, 2)");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
