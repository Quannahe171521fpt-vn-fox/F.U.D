using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace FUD.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Campuses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Campuses__3214EC07E4AD92B4", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Equipment_Catalogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Category = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ActiveFlag = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Equipmen__3214EC073B406AD2", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Roles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RoleName = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Roles__3214EC0732451338", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Room_Types",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    BedCapacity = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Room_Typ__3214EC078D2E2D0C", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Terms",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    AcademicYear = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Terms__3214EC07C3DF6E9E", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    Username = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    FullName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Email = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: false),
                    Phone = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: true),
                    Gender = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: false),
                    PasswordHash = table.Column<string>(type: "varchar(255)", unicode: false, maxLength: 255, nullable: true),
                    GoogleId = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: true),
                    AccountStatus = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false, defaultValue: "Active"),
                    SuspensionReason = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    SuspendedAt = table.Column<DateTime>(type: "datetime", nullable: true),
                    SuspendedById = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime", nullable: false, defaultValueSql: "(getdate())"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime", nullable: false, defaultValueSql: "(getdate())"),
                    TokenVersion = table.Column<int>(type: "int", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Users__3214EC07E21F52F9", x => x.Id);
                    table.ForeignKey(
                        name: "FK__Users__Suspended__276EDEB3",
                        column: x => x.SuspendedById,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Violation_Types",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Violatio__3214EC079E6C9D57", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Dorms",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CampusId = table.Column<int>(type: "int", nullable: false),
                    Code = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Address = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Status = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false, defaultValue: "Active")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Dorms__3214EC07E1BA8CB5", x => x.Id);
                    table.ForeignKey(
                        name: "FK__Dorms__CampusId__5CD6CB2B",
                        column: x => x.CampusId,
                        principalTable: "Campuses",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Room_Type_Equipments",
                columns: table => new
                {
                    RoomTypeId = table.Column<int>(type: "int", nullable: false),
                    CatalogId = table.Column<int>(type: "int", nullable: false),
                    StandardQuantity = table.Column<int>(type: "int", nullable: false, defaultValue: 1)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Room_Typ__20ED858723264CAC", x => new { x.RoomTypeId, x.CatalogId });
                    table.ForeignKey(
                        name: "FK__Room_Type__Catal__7D439ABD",
                        column: x => x.CatalogId,
                        principalTable: "Equipment_Catalogs",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK__Room_Type__RoomT__7C4F7684",
                        column: x => x.RoomTypeId,
                        principalTable: "Room_Types",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Billing_Periods",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TermId = table.Column<int>(type: "int", nullable: false),
                    TargetMonth = table.Column<int>(type: "int", nullable: false),
                    TargetYear = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false, defaultValue: "Open"),
                    CalculatedTime = table.Column<DateTime>(type: "datetime", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Billing___3214EC07CA8A72CC", x => x.Id);
                    table.ForeignKey(
                        name: "FK__Billing_P__TermI__634EBE90",
                        column: x => x.TermId,
                        principalTable: "Terms",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Registration_Periods",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TermId = table.Column<int>(type: "int", nullable: false),
                    PeriodType = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ActiveFlag = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Registra__3214EC070E4AD3CA", x => x.Id);
                    table.ForeignKey(
                        name: "FK__Registrat__TermI__0D7A0286",
                        column: x => x.TermId,
                        principalTable: "Terms",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Utility_Prices",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TermId = table.Column<int>(type: "int", nullable: false),
                    MeterType = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Utility___3214EC07F5F03CA9", x => x.Id);
                    table.ForeignKey(
                        name: "FK__Utility_P__TermI__29221CFB",
                        column: x => x.TermId,
                        principalTable: "Terms",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Account_Status_Histories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    OldStatus = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    NewStatus = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ChangedById = table.Column<int>(type: "int", nullable: false),
                    ChangedAt = table.Column<DateTime>(type: "datetime", nullable: false, defaultValueSql: "(getdate())"),
                    NotificationStatus = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false, defaultValue: "None")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountStatusHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Account_Status_Histories_Users_ChangedById",
                        column: x => x.ChangedById,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Account_Status_Histories_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Admins",
                columns: table => new
                {
                    UserId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Admins__1788CC4CAAF73251", x => x.UserId);
                    table.ForeignKey(
                        name: "FK__Admins__UserId__36B12243",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Guards",
                columns: table => new
                {
                    UserId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Guards__1788CC4C91722B38", x => x.UserId);
                    table.ForeignKey(
                        name: "FK__Guards__UserId__3C69FB99",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Managers",
                columns: table => new
                {
                    UserId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Managers__1788CC4CF50E5151", x => x.UserId);
                    table.ForeignKey(
                        name: "FK__Managers__UserId__398D8EEE",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Notifications",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    Channel = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false, defaultValue: "InApp"),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Content = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ReferenceType = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true),
                    ReferenceId = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true),
                    ReadFlag = table.Column<bool>(type: "bit", nullable: false),
                    ReadTime = table.Column<DateTime>(type: "datetime", nullable: true),
                    SentTime = table.Column<DateTime>(type: "datetime", nullable: false, defaultValueSql: "(getdate())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Notifica__3214EC07A4672EEA", x => x.Id);
                    table.ForeignKey(
                        name: "FK__Notificat__UserI__46E78A0C",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Refresh_Tokens",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    TokenHash = table.Column<string>(type: "varchar(128)", unicode: false, maxLength: 128, nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime", nullable: false, defaultValueSql: "(getdate())"),
                    RevokedAt = table.Column<DateTime>(type: "datetime", nullable: true),
                    RevokedById = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RefreshTokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Refresh_Tokens_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Role_Change_Histories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    Action = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    OldRoles = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    NewRoles = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ChangedById = table.Column<int>(type: "int", nullable: false),
                    ChangedAt = table.Column<DateTime>(type: "datetime", nullable: false, defaultValueSql: "(getdate())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoleChangeHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Role_Change_Histories_Users_ChangedById",
                        column: x => x.ChangedById,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Role_Change_Histories_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Students",
                columns: table => new
                {
                    UserId = table.Column<int>(type: "int", nullable: false),
                    Major = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: true),
                    Cohort = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Students__1788CC4CF48551C9", x => x.UserId);
                    table.ForeignKey(
                        name: "FK__Students__UserId__3F466844",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "User_Roles",
                columns: table => new
                {
                    UserId = table.Column<int>(type: "int", nullable: false),
                    RoleId = table.Column<int>(type: "int", nullable: false),
                    AssignedAt = table.Column<DateTime>(type: "datetime", nullable: false, defaultValueSql: "(getdate())"),
                    AssignedById = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__User_Rol__AF2760AD092DD977", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK__User_Role__Assig__33D4B598",
                        column: x => x.AssignedById,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK__User_Role__RoleI__31EC6D26",
                        column: x => x.RoleId,
                        principalTable: "Roles",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK__User_Role__UserI__30F848ED",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Blocks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DormId = table.Column<int>(type: "int", nullable: false),
                    Code = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    NumberOfFloors = table.Column<int>(type: "int", nullable: true),
                    GenderType = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: false),
                    Status = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false, defaultValue: "Active")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Blocks__3214EC07F117F1D5", x => x.Id);
                    table.ForeignKey(
                        name: "FK__Blocks__DormId__628FA481",
                        column: x => x.DormId,
                        principalTable: "Dorms",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "News",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AuthorId = table.Column<int>(type: "int", nullable: false),
                    ApprovedById = table.Column<int>(type: "int", nullable: true),
                    Title = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Content = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Category = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false, defaultValue: "News"),
                    Status = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false, defaultValue: "Draft"),
                    PublishedTime = table.Column<DateTime>(type: "datetime", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__News__3214EC07553F91F9", x => x.Id);
                    table.ForeignKey(
                        name: "FK__News__ApprovedBy__4E88ABD4",
                        column: x => x.ApprovedById,
                        principalTable: "Managers",
                        principalColumn: "UserId");
                    table.ForeignKey(
                        name: "FK__News__AuthorId__4D94879B",
                        column: x => x.AuthorId,
                        principalTable: "Managers",
                        principalColumn: "UserId");
                });

            migrationBuilder.CreateTable(
                name: "Price_Lists",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TermId = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false, defaultValue: "Draft"),
                    CreationDate = table.Column<DateTime>(type: "datetime", nullable: false, defaultValueSql: "(getdate())"),
                    CreatedById = table.Column<int>(type: "int", nullable: false),
                    PublishedTime = table.Column<DateTime>(type: "datetime", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Price_Li__3214EC072B053555", x => x.Id);
                    table.ForeignKey(
                        name: "FK__Price_Lis__Creat__17036CC0",
                        column: x => x.CreatedById,
                        principalTable: "Managers",
                        principalColumn: "UserId");
                    table.ForeignKey(
                        name: "FK__Price_Lis__TermI__14270015",
                        column: x => x.TermId,
                        principalTable: "Terms",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Audit_Logs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: true),
                    ImpersonatedStudentId = table.Column<int>(type: "int", nullable: true),
                    ActionType = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: false),
                    TargetEntity = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: true),
                    TargetId = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: true),
                    OldValue = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NewValue = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IpAddress = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true),
                    UserAgent = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime", nullable: false, defaultValueSql: "(getdate())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Audit_Lo__3214EC0731DD2909", x => x.Id);
                    table.ForeignKey(
                        name: "FK__Audit_Log__Imper__4316F928",
                        column: x => x.ImpersonatedStudentId,
                        principalTable: "Students",
                        principalColumn: "UserId");
                    table.ForeignKey(
                        name: "FK__Audit_Log__UserI__4222D4EF",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Payment_Transactions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StudentId = table.Column<int>(type: "int", nullable: false),
                    Method = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false, defaultValue: "VietQR"),
                    GatewayReference = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: false),
                    QrLink = table.Column<string>(type: "varchar(500)", unicode: false, maxLength: 500, nullable: true),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Status = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false, defaultValue: "Processing"),
                    ReceiptNumber = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: true),
                    CreatedTime = table.Column<DateTime>(type: "datetime", nullable: false, defaultValueSql: "(getdate())"),
                    ExpiryTime = table.Column<DateTime>(type: "datetime", nullable: false),
                    PaidTime = table.Column<DateTime>(type: "datetime", nullable: true),
                    CallbackTime = table.Column<DateTime>(type: "datetime", nullable: true),
                    GatewayPayload = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Payment___3214EC075A4C7E8F", x => x.Id);
                    table.ForeignKey(
                        name: "FK__Payment_T__Stude__4F12BBB9",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "UserId");
                });

            migrationBuilder.CreateTable(
                name: "Requests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StudentId = table.Column<int>(type: "int", nullable: false),
                    TermId = table.Column<int>(type: "int", nullable: false),
                    RequestType = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false, defaultValue: "Pending"),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RejectReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CancelReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CancelledBySystem = table.Column<bool>(type: "bit", nullable: false),
                    ProcessedById = table.Column<int>(type: "int", nullable: true),
                    CreatedTime = table.Column<DateTime>(type: "datetime", nullable: false, defaultValueSql: "(getdate())"),
                    ProcessedTime = table.Column<DateTime>(type: "datetime", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Requests__3214EC07F0CFEE86", x => x.Id);
                    table.ForeignKey(
                        name: "FK__Requests__Proces__31B762FC",
                        column: x => x.ProcessedById,
                        principalTable: "Managers",
                        principalColumn: "UserId");
                    table.ForeignKey(
                        name: "FK__Requests__Studen__2DE6D218",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "UserId");
                    table.ForeignKey(
                        name: "FK__Requests__TermId__2EDAF651",
                        column: x => x.TermId,
                        principalTable: "Terms",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Import_Batches",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BillingPeriodId = table.Column<int>(type: "int", nullable: false),
                    BlockId = table.Column<int>(type: "int", nullable: false),
                    GuardId = table.Column<int>(type: "int", nullable: false),
                    Method = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    FileName = table.Column<string>(type: "varchar(255)", unicode: false, maxLength: 255, nullable: true),
                    TotalRows = table.Column<int>(type: "int", nullable: false),
                    ErrorRows = table.Column<int>(type: "int", nullable: false),
                    ImportTime = table.Column<DateTime>(type: "datetime", nullable: false, defaultValueSql: "(getdate())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Import_B__3214EC07229C722B", x => x.Id);
                    table.ForeignKey(
                        name: "FK__Import_Ba__Billi__690797E6",
                        column: x => x.BillingPeriodId,
                        principalTable: "Billing_Periods",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK__Import_Ba__Block__69FBBC1F",
                        column: x => x.BlockId,
                        principalTable: "Blocks",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK__Import_Ba__Guard__6AEFE058",
                        column: x => x.GuardId,
                        principalTable: "Guards",
                        principalColumn: "UserId");
                });

            migrationBuilder.CreateTable(
                name: "Rooms",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BlockId = table.Column<int>(type: "int", nullable: false),
                    RoomTypeId = table.Column<int>(type: "int", nullable: false),
                    RoomNumber = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    FloorLevel = table.Column<int>(type: "int", nullable: true),
                    Status = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false, defaultValue: "Active")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Rooms__3214EC07F31E1756", x => x.Id);
                    table.ForeignKey(
                        name: "FK__Rooms__BlockId__6D0D32F4",
                        column: x => x.BlockId,
                        principalTable: "Blocks",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK__Rooms__RoomTypeI__6E01572D",
                        column: x => x.RoomTypeId,
                        principalTable: "Room_Types",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "News_Attachments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    NewsId = table.Column<int>(type: "int", nullable: false),
                    FileURL = table.Column<string>(type: "varchar(500)", unicode: false, maxLength: 500, nullable: false),
                    FileType = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__News_Att__3214EC0763AE8A27", x => x.Id);
                    table.ForeignKey(
                        name: "FK__News_Atta__NewsI__5535A963",
                        column: x => x.NewsId,
                        principalTable: "News",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Price_Items",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PriceListId = table.Column<int>(type: "int", nullable: false),
                    DormId = table.Column<int>(type: "int", nullable: false),
                    RoomTypeId = table.Column<int>(type: "int", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Price_It__3214EC07EF37D042", x => x.Id);
                    table.ForeignKey(
                        name: "FK__Price_Ite__DormI__1CBC4616",
                        column: x => x.DormId,
                        principalTable: "Dorms",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK__Price_Ite__Price__1BC821DD",
                        column: x => x.PriceListId,
                        principalTable: "Price_Lists",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK__Price_Ite__RoomT__1DB06A4F",
                        column: x => x.RoomTypeId,
                        principalTable: "Room_Types",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Request_Logs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RequestId = table.Column<int>(type: "int", nullable: false),
                    FromStatus = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: true),
                    ToStatus = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ChangeTime = table.Column<DateTime>(type: "datetime", nullable: false, defaultValueSql: "(getdate())"),
                    ChangedById = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Request___3214EC072E852CEE", x => x.Id);
                    table.ForeignKey(
                        name: "FK__Request_L__Chang__5F7E2DAC",
                        column: x => x.ChangedById,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK__Request_L__Reque__5D95E53A",
                        column: x => x.RequestId,
                        principalTable: "Requests",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Beds",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RoomId = table.Column<int>(type: "int", nullable: false),
                    BedCode = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    PositionInRoom = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true),
                    Status = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false, defaultValue: "Free")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Beds__3214EC07F56B2B51", x => x.Id);
                    table.ForeignKey(
                        name: "FK__Beds__RoomId__73BA3083",
                        column: x => x.RoomId,
                        principalTable: "Rooms",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Utility_Bills",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RoomId = table.Column<int>(type: "int", nullable: false),
                    BillingPeriodId = table.Column<int>(type: "int", nullable: false),
                    TotalKwh = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    TotalM3 = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    ElectricityUnitPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    WaterUnitPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    ElectricityCost = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    WaterCost = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    TotalCost = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    NumberOfResidents = table.Column<int>(type: "int", nullable: true),
                    SharePerStudent = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    PublishedTime = table.Column<DateTime>(type: "datetime", nullable: true),
                    Status = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false, defaultValue: "Calculated"),
                    CorrectedFromId = table.Column<int>(type: "int", nullable: true),
                    CorrectionReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Utility___3214EC07B60F15DB", x => x.Id);
                    table.ForeignKey(
                        name: "FK__Utility_B__Billi__13F1F5EB",
                        column: x => x.BillingPeriodId,
                        principalTable: "Billing_Periods",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK__Utility_B__Corre__15DA3E5D",
                        column: x => x.CorrectedFromId,
                        principalTable: "Utility_Bills",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK__Utility_B__RoomI__12FDD1B2",
                        column: x => x.RoomId,
                        principalTable: "Rooms",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Bed_Prices",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BedId = table.Column<int>(type: "int", nullable: false),
                    TermId = table.Column<int>(type: "int", nullable: false),
                    TargetMonth = table.Column<int>(type: "int", nullable: true),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    EffectiveTo = table.Column<DateOnly>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Bed_Pric__3214EC078BE3DEF6", x => x.Id);
                    table.ForeignKey(
                        name: "FK__Bed_Price__BedId__2180FB33",
                        column: x => x.BedId,
                        principalTable: "Beds",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK__Bed_Price__TermI__22751F6C",
                        column: x => x.TermId,
                        principalTable: "Terms",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Equipments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RoomId = table.Column<int>(type: "int", nullable: false),
                    BedId = table.Column<int>(type: "int", nullable: true),
                    CatalogId = table.Column<int>(type: "int", nullable: false),
                    AssetCode = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: false),
                    Condition = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false, defaultValue: "Good"),
                    PurchaseDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Equipmen__3214EC070F0F354E", x => x.Id);
                    table.ForeignKey(
                        name: "FK__Equipment__BedId__03F0984C",
                        column: x => x.BedId,
                        principalTable: "Beds",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK__Equipment__Catal__04E4BC85",
                        column: x => x.CatalogId,
                        principalTable: "Equipment_Catalogs",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK__Equipment__RoomI__02FC7413",
                        column: x => x.RoomId,
                        principalTable: "Rooms",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Residencies",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StudentId = table.Column<int>(type: "int", nullable: false),
                    BedId = table.Column<int>(type: "int", nullable: false),
                    TermId = table.Column<int>(type: "int", nullable: false),
                    SourceRequestId = table.Column<int>(type: "int", nullable: true),
                    RegistrationType = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: true),
                    CheckInDate = table.Column<DateOnly>(type: "date", nullable: true),
                    PlannedCheckOutDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ActualCheckOutDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Status = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false, defaultValue: "Reserved")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Residenc__3214EC07374334CD", x => x.Id);
                    table.ForeignKey(
                        name: "FK__Residenci__BedId__41EDCAC5",
                        column: x => x.BedId,
                        principalTable: "Beds",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK__Residenci__Sourc__43D61337",
                        column: x => x.SourceRequestId,
                        principalTable: "Requests",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK__Residenci__Stude__40F9A68C",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "UserId");
                    table.ForeignKey(
                        name: "FK__Residenci__TermI__42E1EEFE",
                        column: x => x.TermId,
                        principalTable: "Terms",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Incident_Tickets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TicketCode = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    StudentId = table.Column<int>(type: "int", nullable: false),
                    RoomId = table.Column<int>(type: "int", nullable: false),
                    EquipmentId = table.Column<int>(type: "int", nullable: true),
                    AssignedById = table.Column<int>(type: "int", nullable: true),
                    AssignedGuardId = table.Column<int>(type: "int", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Status = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false, defaultValue: "New"),
                    ManagerNote = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    HandlingResult = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ReopenReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    EscalationReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedTime = table.Column<DateTime>(type: "datetime", nullable: false, defaultValueSql: "(getdate())"),
                    DoneTime = table.Column<DateTime>(type: "datetime", nullable: true),
                    ClosedTime = table.Column<DateTime>(type: "datetime", nullable: true),
                    AutoClosed = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Incident__3214EC07FC656E97", x => x.Id);
                    table.ForeignKey(
                        name: "FK__Incident___Assig__68D28DBC",
                        column: x => x.AssignedById,
                        principalTable: "Managers",
                        principalColumn: "UserId");
                    table.ForeignKey(
                        name: "FK__Incident___Assig__69C6B1F5",
                        column: x => x.AssignedGuardId,
                        principalTable: "Guards",
                        principalColumn: "UserId");
                    table.ForeignKey(
                        name: "FK__Incident___Equip__67DE6983",
                        column: x => x.EquipmentId,
                        principalTable: "Equipments",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK__Incident___RoomI__66EA454A",
                        column: x => x.RoomId,
                        principalTable: "Rooms",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK__Incident___Stude__65F62111",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "UserId");
                });

            migrationBuilder.CreateTable(
                name: "Bed_Transfer_Requests",
                columns: table => new
                {
                    RequestId = table.Column<int>(type: "int", nullable: false),
                    FromResidencyId = table.Column<int>(type: "int", nullable: false),
                    FromBedId = table.Column<int>(type: "int", nullable: false),
                    ToBedId = table.Column<int>(type: "int", nullable: false),
                    TransferReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ReviewedById = table.Column<int>(type: "int", nullable: true),
                    ReviewedTime = table.Column<DateTime>(type: "datetime", nullable: true),
                    AssignedGuardId = table.Column<int>(type: "int", nullable: true),
                    TransferDate = table.Column<DateOnly>(type: "date", nullable: true),
                    OldBedDailyRate = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    NewBedDailyRate = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    DaysStayed = table.Column<int>(type: "int", nullable: true),
                    DaysRemaining = table.Column<int>(type: "int", nullable: true),
                    AmountAlreadyPaid = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    OldBedAmount = table.Column<decimal>(type: "decimal(29,2)", nullable: true, computedColumnSql: "([OldBedDailyRate]*[DaysStayed])", stored: true),
                    NewBedAmount = table.Column<decimal>(type: "decimal(29,2)", nullable: true, computedColumnSql: "([NewBedDailyRate]*[DaysRemaining])", stored: true),
                    TotalAmount = table.Column<decimal>(type: "decimal(30,2)", nullable: true, computedColumnSql: "([OldBedDailyRate]*[DaysStayed]+[NewBedDailyRate]*[DaysRemaining])", stored: true),
                    PriceDifference = table.Column<decimal>(type: "decimal(31,2)", nullable: true, computedColumnSql: "(([OldBedDailyRate]*[DaysStayed]+[NewBedDailyRate]*[DaysRemaining])-[AmountAlreadyPaid])", stored: true),
                    NewResidencyId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Bed_Tran__33A8517AFC55419F", x => x.RequestId);
                    table.ForeignKey(
                        name: "FK__Bed_Trans__Assig__57DD0BE4",
                        column: x => x.AssignedGuardId,
                        principalTable: "Guards",
                        principalColumn: "UserId");
                    table.ForeignKey(
                        name: "FK__Bed_Trans__FromB__55009F39",
                        column: x => x.FromBedId,
                        principalTable: "Beds",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK__Bed_Trans__FromR__540C7B00",
                        column: x => x.FromResidencyId,
                        principalTable: "Residencies",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK__Bed_Trans__NewRe__58D1301D",
                        column: x => x.NewResidencyId,
                        principalTable: "Residencies",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK__Bed_Trans__Reque__531856C7",
                        column: x => x.RequestId,
                        principalTable: "Requests",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK__Bed_Trans__Revie__56E8E7AB",
                        column: x => x.ReviewedById,
                        principalTable: "Managers",
                        principalColumn: "UserId");
                    table.ForeignKey(
                        name: "FK__Bed_Trans__ToBed__55F4C372",
                        column: x => x.ToBedId,
                        principalTable: "Beds",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Booking_Requests",
                columns: table => new
                {
                    RequestId = table.Column<int>(type: "int", nullable: false),
                    BedId = table.Column<int>(type: "int", nullable: false),
                    BookingKind = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false, defaultValue: "Booking"),
                    KeepFromResidencyId = table.Column<int>(type: "int", nullable: true),
                    AgreementToRegulations = table.Column<bool>(type: "bit", nullable: false),
                    PaymentDeadline = table.Column<DateTime>(type: "datetime", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Booking___33A8517A6A60A479", x => x.RequestId);
                    table.ForeignKey(
                        name: "FK_Booking_KeepResidency",
                        column: x => x.KeepFromResidencyId,
                        principalTable: "Residencies",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK__Booking_R__BedId__395884C4",
                        column: x => x.BedId,
                        principalTable: "Beds",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK__Booking_R__Reque__3864608B",
                        column: x => x.RequestId,
                        principalTable: "Requests",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Checkout_Requests",
                columns: table => new
                {
                    RequestId = table.Column<int>(type: "int", nullable: false),
                    ResidencyId = table.Column<int>(type: "int", nullable: false),
                    ExpectedCheckOutDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ReviewedById = table.Column<int>(type: "int", nullable: true),
                    ReviewedTime = table.Column<DateTime>(type: "datetime", nullable: true),
                    AssignedGuardId = table.Column<int>(type: "int", nullable: true),
                    IsExceptionalCase = table.Column<bool>(type: "bit", nullable: false),
                    ExceptionNote = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Checkout__33A8517A96FE33D3", x => x.RequestId);
                    table.ForeignKey(
                        name: "FK__Checkout___Assig__4E53A1AA",
                        column: x => x.AssignedGuardId,
                        principalTable: "Guards",
                        principalColumn: "UserId");
                    table.ForeignKey(
                        name: "FK__Checkout___Reque__4B7734FF",
                        column: x => x.RequestId,
                        principalTable: "Requests",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK__Checkout___Resid__4C6B5938",
                        column: x => x.ResidencyId,
                        principalTable: "Residencies",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK__Checkout___Revie__4D5F7D71",
                        column: x => x.ReviewedById,
                        principalTable: "Managers",
                        principalColumn: "UserId");
                });

            migrationBuilder.CreateTable(
                name: "Handovers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    HandoverType = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    RequestId = table.Column<int>(type: "int", nullable: true),
                    ResidencyId = table.Column<int>(type: "int", nullable: true),
                    RoomId = table.Column<int>(type: "int", nullable: false),
                    GuardId = table.Column<int>(type: "int", nullable: false),
                    BelongingsRemainingFlag = table.Column<bool>(type: "bit", nullable: false),
                    BelongingsNote = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    HasDamageOrMissingFlag = table.Column<bool>(type: "bit", nullable: false),
                    TotalCompensation = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Status = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false, defaultValue: "Draft"),
                    StudentAcceptedFlag = table.Column<bool>(type: "bit", nullable: false),
                    StudentAcceptedTime = table.Column<DateTime>(type: "datetime", nullable: true),
                    ManagerReviewedById = table.Column<int>(type: "int", nullable: true),
                    ManagerReviewedTime = table.Column<DateTime>(type: "datetime", nullable: true),
                    ManagerNote = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedTime = table.Column<DateTime>(type: "datetime", nullable: false, defaultValueSql: "(getdate())"),
                    CompletedTime = table.Column<DateTime>(type: "datetime", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Handover__3214EC073BD8BDEF", x => x.Id);
                    table.ForeignKey(
                        name: "FK__Handovers__Guard__1D7B6025",
                        column: x => x.GuardId,
                        principalTable: "Guards",
                        principalColumn: "UserId");
                    table.ForeignKey(
                        name: "FK__Handovers__Manag__2334397B",
                        column: x => x.ManagerReviewedById,
                        principalTable: "Managers",
                        principalColumn: "UserId");
                    table.ForeignKey(
                        name: "FK__Handovers__Reque__1A9EF37A",
                        column: x => x.RequestId,
                        principalTable: "Requests",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK__Handovers__Resid__1B9317B3",
                        column: x => x.ResidencyId,
                        principalTable: "Residencies",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK__Handovers__RoomI__1C873BEC",
                        column: x => x.RoomId,
                        principalTable: "Rooms",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Violations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    StudentId = table.Column<int>(type: "int", nullable: false),
                    RoomId = table.Column<int>(type: "int", nullable: false),
                    ResidencyId = table.Column<int>(type: "int", nullable: true),
                    ViolationTypeId = table.Column<int>(type: "int", nullable: false),
                    RecordedById = table.Column<int>(type: "int", nullable: false),
                    EvaluatedById = table.Column<int>(type: "int", nullable: true),
                    OccurrenceTime = table.Column<DateTime>(type: "datetime", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    InitialLevel = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    FinalLevel = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ManagerNote = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ExplanationDeadline = table.Column<DateTime>(type: "datetime", nullable: true),
                    Status = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false, defaultValue: "Recorded"),
                    FinalizedTime = table.Column<DateTime>(type: "datetime", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Violatio__3214EC0755D9AA4F", x => x.Id);
                    table.ForeignKey(
                        name: "FK__Violation__Evalu__0B27A5C0",
                        column: x => x.EvaluatedById,
                        principalTable: "Managers",
                        principalColumn: "UserId");
                    table.ForeignKey(
                        name: "FK__Violation__Recor__0A338187",
                        column: x => x.RecordedById,
                        principalTable: "Guards",
                        principalColumn: "UserId");
                    table.ForeignKey(
                        name: "FK__Violation__Resid__084B3915",
                        column: x => x.ResidencyId,
                        principalTable: "Residencies",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK__Violation__RoomI__075714DC",
                        column: x => x.RoomId,
                        principalTable: "Rooms",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK__Violation__Stude__0662F0A3",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "UserId");
                    table.ForeignKey(
                        name: "FK__Violation__Viola__093F5D4E",
                        column: x => x.ViolationTypeId,
                        principalTable: "Violation_Types",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Repair_Feedbacks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TicketId = table.Column<int>(type: "int", nullable: false),
                    StudentId = table.Column<int>(type: "int", nullable: false),
                    Rating = table.Column<int>(type: "int", nullable: false),
                    Comment = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    SubmittedTime = table.Column<DateTime>(type: "datetime", nullable: false, defaultValueSql: "(getdate())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Repair_F__3214EC07E2B58C2C", x => x.Id);
                    table.ForeignKey(
                        name: "FK__Repair_Fe__Stude__7DCDAAA2",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "UserId");
                    table.ForeignKey(
                        name: "FK__Repair_Fe__Ticke__7CD98669",
                        column: x => x.TicketId,
                        principalTable: "Incident_Tickets",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Ticket_Photos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TicketId = table.Column<int>(type: "int", nullable: false),
                    PhotoType = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: false),
                    FileURL = table.Column<string>(type: "varchar(500)", unicode: false, maxLength: 500, nullable: false),
                    UploadTime = table.Column<DateTime>(type: "datetime", nullable: false, defaultValueSql: "(getdate())"),
                    UploaderId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Ticket_P__3214EC073369F21F", x => x.Id);
                    table.ForeignKey(
                        name: "FK__Ticket_Ph__Ticke__7167D3BD",
                        column: x => x.TicketId,
                        principalTable: "Incident_Tickets",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK__Ticket_Ph__Uploa__73501C2F",
                        column: x => x.UploaderId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Ticket_Status_Histories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TicketId = table.Column<int>(type: "int", nullable: false),
                    FromStatus = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: true),
                    ToStatus = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ChangedById = table.Column<int>(type: "int", nullable: true),
                    ChangeTime = table.Column<DateTime>(type: "datetime", nullable: false, defaultValueSql: "(getdate())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Ticket_S__3214EC075558CA1D", x => x.Id);
                    table.ForeignKey(
                        name: "FK__Ticket_St__Chang__7814D14C",
                        column: x => x.ChangedById,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK__Ticket_St__Ticke__7720AD13",
                        column: x => x.TicketId,
                        principalTable: "Incident_Tickets",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Meter_Readings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BillingPeriodId = table.Column<int>(type: "int", nullable: true),
                    RoomId = table.Column<int>(type: "int", nullable: false),
                    BatchId = table.Column<int>(type: "int", nullable: true),
                    CheckoutRequestId = table.Column<int>(type: "int", nullable: true),
                    TransferRequestId = table.Column<int>(type: "int", nullable: true),
                    MeterType = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    PreviousValue = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CurrentValue = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Consumption = table.Column<decimal>(type: "decimal(19,2)", nullable: true, computedColumnSql: "([CurrentValue]-[PreviousValue])", stored: true),
                    Source = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false, defaultValue: "Import"),
                    Status = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false, defaultValue: "Recorded"),
                    RecordedById = table.Column<int>(type: "int", nullable: false),
                    RecordedTime = table.Column<DateTime>(type: "datetime", nullable: false, defaultValueSql: "(getdate())"),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Meter_Re__3214EC075B50FC16", x => x.Id);
                    table.ForeignKey(
                        name: "FK__Meter_Rea__Batch__73852659",
                        column: x => x.BatchId,
                        principalTable: "Import_Batches",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK__Meter_Rea__Billi__719CDDE7",
                        column: x => x.BillingPeriodId,
                        principalTable: "Billing_Periods",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK__Meter_Rea__Check__74794A92",
                        column: x => x.CheckoutRequestId,
                        principalTable: "Checkout_Requests",
                        principalColumn: "RequestId");
                    table.ForeignKey(
                        name: "FK__Meter_Rea__Recor__7849DB76",
                        column: x => x.RecordedById,
                        principalTable: "Guards",
                        principalColumn: "UserId");
                    table.ForeignKey(
                        name: "FK__Meter_Rea__RoomI__72910220",
                        column: x => x.RoomId,
                        principalTable: "Rooms",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK__Meter_Rea__Trans__756D6ECB",
                        column: x => x.TransferRequestId,
                        principalTable: "Bed_Transfer_Requests",
                        principalColumn: "RequestId");
                });

            migrationBuilder.CreateTable(
                name: "Handover_Items",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    HandoverId = table.Column<int>(type: "int", nullable: false),
                    EquipmentId = table.Column<int>(type: "int", nullable: false),
                    ItemCondition = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false, defaultValue: "Good"),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CompensationAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Handover__3214EC0744AEA44C", x => x.Id);
                    table.ForeignKey(
                        name: "FK__Handover___Equip__2DB1C7EE",
                        column: x => x.EquipmentId,
                        principalTable: "Equipments",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK__Handover___Hando__2CBDA3B5",
                        column: x => x.HandoverId,
                        principalTable: "Handovers",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Invoices",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StudentId = table.Column<int>(type: "int", nullable: false),
                    TermId = table.Column<int>(type: "int", nullable: false),
                    InvoiceNumber = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: false),
                    InvoiceType = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    BookingRequestId = table.Column<int>(type: "int", nullable: true),
                    TransferRequestId = table.Column<int>(type: "int", nullable: true),
                    UtilityBillId = table.Column<int>(type: "int", nullable: true),
                    HandoverId = table.Column<int>(type: "int", nullable: true),
                    ParentInvoiceId = table.Column<int>(type: "int", nullable: true),
                    AdjustmentKind = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: true),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    IssuedTime = table.Column<DateTime>(type: "datetime", nullable: false, defaultValueSql: "(getdate())"),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: false),
                    PaidTime = table.Column<DateTime>(type: "datetime", nullable: true),
                    Status = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false, defaultValue: "Unpaid"),
                    CancelReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Invoices__3214EC07F33F57B0", x => x.Id);
                    table.ForeignKey(
                        name: "FK__Invoices__Bookin__3A179ED3",
                        column: x => x.BookingRequestId,
                        principalTable: "Booking_Requests",
                        principalColumn: "RequestId");
                    table.ForeignKey(
                        name: "FK__Invoices__Handov__3CF40B7E",
                        column: x => x.HandoverId,
                        principalTable: "Handovers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK__Invoices__Parent__3DE82FB7",
                        column: x => x.ParentInvoiceId,
                        principalTable: "Invoices",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK__Invoices__Studen__382F5661",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "UserId");
                    table.ForeignKey(
                        name: "FK__Invoices__TermId__39237A9A",
                        column: x => x.TermId,
                        principalTable: "Terms",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK__Invoices__Transf__3B0BC30C",
                        column: x => x.TransferRequestId,
                        principalTable: "Bed_Transfer_Requests",
                        principalColumn: "RequestId");
                    table.ForeignKey(
                        name: "FK__Invoices__Utilit__3BFFE745",
                        column: x => x.UtilityBillId,
                        principalTable: "Utility_Bills",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Violation_Explanations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ViolationId = table.Column<int>(type: "int", nullable: false),
                    StudentId = table.Column<int>(type: "int", nullable: false),
                    Content = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SubmissionTime = table.Column<DateTime>(type: "datetime", nullable: false, defaultValueSql: "(getdate())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Violatio__3214EC07CE1DED5F", x => x.Id);
                    table.ForeignKey(
                        name: "FK__Violation__Stude__13BCEBC1",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "UserId");
                    table.ForeignKey(
                        name: "FK__Violation__Viola__12C8C788",
                        column: x => x.ViolationId,
                        principalTable: "Violations",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Usage_Anomalies",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ReadingId = table.Column<int>(type: "int", nullable: false),
                    FlagReason = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Status = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false, defaultValue: "Flagged"),
                    Resolution = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: true),
                    ManagerNote = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ResolvedById = table.Column<int>(type: "int", nullable: true),
                    ResolvedTime = table.Column<DateTime>(type: "datetime", nullable: true),
                    CreatedTime = table.Column<DateTime>(type: "datetime", nullable: false, defaultValueSql: "(getdate())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Usage_An__3214EC074BD7CC63", x => x.Id);
                    table.ForeignKey(
                        name: "FK__Usage_Ano__Readi__00DF2177",
                        column: x => x.ReadingId,
                        principalTable: "Meter_Readings",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK__Usage_Ano__Resol__02C769E9",
                        column: x => x.ResolvedById,
                        principalTable: "Managers",
                        principalColumn: "UserId");
                });

            migrationBuilder.CreateTable(
                name: "Handover_Photos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    HandoverItemId = table.Column<int>(type: "int", nullable: false),
                    FileURL = table.Column<string>(type: "varchar(500)", unicode: false, maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Handover__3214EC077065DABD", x => x.Id);
                    table.ForeignKey(
                        name: "FK__Handover___Hando__345EC57D",
                        column: x => x.HandoverItemId,
                        principalTable: "Handover_Items",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Complaint_Requests",
                columns: table => new
                {
                    RequestId = table.Column<int>(type: "int", nullable: false),
                    TargetType = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    HandoverId = table.Column<int>(type: "int", nullable: true),
                    InvoiceId = table.Column<int>(type: "int", nullable: true),
                    ViolationId = table.Column<int>(type: "int", nullable: true),
                    Content = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    ResolutionNote = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Complain__33A8517AAC6B7058", x => x.RequestId);
                    table.ForeignKey(
                        name: "FK__Complaint__Hando__1D4655FB",
                        column: x => x.HandoverId,
                        principalTable: "Handovers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK__Complaint__Invoi__1E3A7A34",
                        column: x => x.InvoiceId,
                        principalTable: "Invoices",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK__Complaint__Reque__1C5231C2",
                        column: x => x.RequestId,
                        principalTable: "Requests",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK__Complaint__Viola__1F2E9E6D",
                        column: x => x.ViolationId,
                        principalTable: "Violations",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Invoice_Items",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    InvoiceId = table.Column<int>(type: "int", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,2)", nullable: false, defaultValue: 1m),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Invoice___3214EC0703D6358B", x => x.Id);
                    table.ForeignKey(
                        name: "FK__Invoice_I__Invoi__4959E263",
                        column: x => x.InvoiceId,
                        principalTable: "Invoices",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Refunds",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TransactionId = table.Column<int>(type: "int", nullable: false),
                    RelatedRequestId = table.Column<int>(type: "int", nullable: true),
                    RelatedInvoiceId = table.Column<int>(type: "int", nullable: true),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Status = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false, defaultValue: "Pending"),
                    CreatedTime = table.Column<DateTime>(type: "datetime", nullable: false, defaultValueSql: "(getdate())"),
                    RefundTime = table.Column<DateTime>(type: "datetime", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Refunds__3214EC07078E3F5D", x => x.Id);
                    table.ForeignKey(
                        name: "FK__Refunds__Related__5C6CB6D7",
                        column: x => x.RelatedRequestId,
                        principalTable: "Requests",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK__Refunds__Related__5D60DB10",
                        column: x => x.RelatedInvoiceId,
                        principalTable: "Invoices",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK__Refunds__Transac__5B78929E",
                        column: x => x.TransactionId,
                        principalTable: "Payment_Transactions",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Transaction_Invoices",
                columns: table => new
                {
                    TransactionId = table.Column<int>(type: "int", nullable: false),
                    InvoiceId = table.Column<int>(type: "int", nullable: false),
                    AllocatedAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Transact__183A50C0CC9DFD06", x => new { x.TransactionId, x.InvoiceId });
                    table.ForeignKey(
                        name: "FK__Transacti__Invoi__57A801BA",
                        column: x => x.InvoiceId,
                        principalTable: "Invoices",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK__Transacti__Trans__56B3DD81",
                        column: x => x.TransactionId,
                        principalTable: "Payment_Transactions",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Violation_Evidences",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ViolationId = table.Column<int>(type: "int", nullable: false),
                    ExplanationId = table.Column<int>(type: "int", nullable: true),
                    Source = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    FileURL = table.Column<string>(type: "varchar(500)", unicode: false, maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Violatio__3214EC07B8A08AE8", x => x.Id);
                    table.ForeignKey(
                        name: "FK__Violation__Expla__1881A0DE",
                        column: x => x.ExplanationId,
                        principalTable: "Violation_Explanations",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK__Violation__Viola__178D7CA5",
                        column: x => x.ViolationId,
                        principalTable: "Violations",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Inspection_Requests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AnomalyId = table.Column<int>(type: "int", nullable: false),
                    CreatedById = table.Column<int>(type: "int", nullable: false),
                    AssignedGuardId = table.Column<int>(type: "int", nullable: false),
                    ManagerNote = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    GuardNote = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    VerifiedValue = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Status = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false, defaultValue: "Requested"),
                    CreatedTime = table.Column<DateTime>(type: "datetime", nullable: false, defaultValueSql: "(getdate())"),
                    CompletedTime = table.Column<DateTime>(type: "datetime", nullable: true),
                    ApprovedTime = table.Column<DateTime>(type: "datetime", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Inspecti__3214EC07FCA1BD6D", x => x.Id);
                    table.ForeignKey(
                        name: "FK__Inspectio__Anoma__0880433F",
                        column: x => x.AnomalyId,
                        principalTable: "Usage_Anomalies",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK__Inspectio__Assig__0A688BB1",
                        column: x => x.AssignedGuardId,
                        principalTable: "Guards",
                        principalColumn: "UserId");
                    table.ForeignKey(
                        name: "FK__Inspectio__Creat__09746778",
                        column: x => x.CreatedById,
                        principalTable: "Managers",
                        principalColumn: "UserId");
                });

            migrationBuilder.CreateTable(
                name: "Complaint_Evidences",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ComplaintRequestId = table.Column<int>(type: "int", nullable: false),
                    FileURL = table.Column<string>(type: "varchar(500)", unicode: false, maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Complain__3214EC07B16AD9A8", x => x.Id);
                    table.ForeignKey(
                        name: "FK__Complaint__Compl__23F3538A",
                        column: x => x.ComplaintRequestId,
                        principalTable: "Complaint_Requests",
                        principalColumn: "RequestId");
                });

            migrationBuilder.CreateTable(
                name: "Inspection_Photos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    InspectionRequestId = table.Column<int>(type: "int", nullable: false),
                    FileURL = table.Column<string>(type: "varchar(500)", unicode: false, maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Inspecti__3214EC07EFAC3D4B", x => x.Id);
                    table.ForeignKey(
                        name: "FK__Inspectio__Inspe__10216507",
                        column: x => x.InspectionRequestId,
                        principalTable: "Inspection_Requests",
                        principalColumn: "Id");
                });

            migrationBuilder.InsertData(
                table: "Roles",
                columns: new[] { "Id", "Description", "RoleName" },
                values: new object[,]
                {
                    { 1, "Sinh viên", "Student" },
                    { 2, "Quản lý ký túc xá", "Manager" },
                    { 3, "Bảo vệ / nhân viên trực", "Guard" },
                    { 4, "Quản trị hệ thống", "Admin" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Account_Status_Histories_ChangedById",
                table: "Account_Status_Histories",
                column: "ChangedById");

            migrationBuilder.CreateIndex(
                name: "IX_AccountStatusHistories_User_Time",
                table: "Account_Status_Histories",
                columns: new[] { "UserId", "ChangedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Audit_Logs_ImpersonatedStudentId",
                table: "Audit_Logs",
                column: "ImpersonatedStudentId");

            migrationBuilder.CreateIndex(
                name: "IX_Audit_Logs_User_Time",
                table: "Audit_Logs",
                columns: new[] { "UserId", "CreatedAt" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_Bed_Prices_BedId",
                table: "Bed_Prices",
                column: "BedId");

            migrationBuilder.CreateIndex(
                name: "IX_Bed_Prices_TermId",
                table: "Bed_Prices",
                column: "TermId");

            migrationBuilder.CreateIndex(
                name: "IX_Bed_Transfer_Requests_AssignedGuardId",
                table: "Bed_Transfer_Requests",
                column: "AssignedGuardId");

            migrationBuilder.CreateIndex(
                name: "IX_Bed_Transfer_Requests_FromBedId",
                table: "Bed_Transfer_Requests",
                column: "FromBedId");

            migrationBuilder.CreateIndex(
                name: "IX_Bed_Transfer_Requests_FromResidencyId",
                table: "Bed_Transfer_Requests",
                column: "FromResidencyId");

            migrationBuilder.CreateIndex(
                name: "IX_Bed_Transfer_Requests_NewResidencyId",
                table: "Bed_Transfer_Requests",
                column: "NewResidencyId");

            migrationBuilder.CreateIndex(
                name: "IX_Bed_Transfer_Requests_ReviewedById",
                table: "Bed_Transfer_Requests",
                column: "ReviewedById");

            migrationBuilder.CreateIndex(
                name: "IX_Transfer_ToBed",
                table: "Bed_Transfer_Requests",
                column: "ToBedId");

            migrationBuilder.CreateIndex(
                name: "IX_Beds_Room_Status",
                table: "Beds",
                columns: new[] { "RoomId", "Status" });

            migrationBuilder.CreateIndex(
                name: "UQ_Bed_Code",
                table: "Beds",
                columns: new[] { "RoomId", "BedCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Billing_Periods_TermId",
                table: "Billing_Periods",
                column: "TermId");

            migrationBuilder.CreateIndex(
                name: "UQ_BillingPeriod",
                table: "Billing_Periods",
                columns: new[] { "TargetMonth", "TargetYear" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_Block_Code",
                table: "Blocks",
                columns: new[] { "DormId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Booking_Requests_BedId",
                table: "Booking_Requests",
                column: "BedId");

            migrationBuilder.CreateIndex(
                name: "IX_Booking_Requests_KeepFromResidencyId",
                table: "Booking_Requests",
                column: "KeepFromResidencyId");

            migrationBuilder.CreateIndex(
                name: "UQ__Campuses__A25C5AA7B74A7DF3",
                table: "Campuses",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Checkout_Requests_AssignedGuardId",
                table: "Checkout_Requests",
                column: "AssignedGuardId");

            migrationBuilder.CreateIndex(
                name: "IX_Checkout_Requests_ResidencyId",
                table: "Checkout_Requests",
                column: "ResidencyId");

            migrationBuilder.CreateIndex(
                name: "IX_Checkout_Requests_ReviewedById",
                table: "Checkout_Requests",
                column: "ReviewedById");

            migrationBuilder.CreateIndex(
                name: "IX_Complaint_Evidences_ComplaintRequestId",
                table: "Complaint_Evidences",
                column: "ComplaintRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_Complaint_Requests_HandoverId",
                table: "Complaint_Requests",
                column: "HandoverId");

            migrationBuilder.CreateIndex(
                name: "IX_Complaint_Requests_InvoiceId",
                table: "Complaint_Requests",
                column: "InvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_Complaint_Requests_ViolationId",
                table: "Complaint_Requests",
                column: "ViolationId");

            migrationBuilder.CreateIndex(
                name: "IX_Dorms_CampusId",
                table: "Dorms",
                column: "CampusId");

            migrationBuilder.CreateIndex(
                name: "UQ__Dorms__737584F6382767DE",
                table: "Dorms",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ__Dorms__A25C5AA703D05168",
                table: "Dorms",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_EquipCatalog",
                table: "Equipment_Catalogs",
                columns: new[] { "Category", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Equipments_BedId",
                table: "Equipments",
                column: "BedId");

            migrationBuilder.CreateIndex(
                name: "IX_Equipments_CatalogId",
                table: "Equipments",
                column: "CatalogId");

            migrationBuilder.CreateIndex(
                name: "IX_Equipments_Room",
                table: "Equipments",
                column: "RoomId");

            migrationBuilder.CreateIndex(
                name: "UQ__Equipmen__2DDE5240F364D659",
                table: "Equipments",
                column: "AssetCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Handover_Items_EquipmentId",
                table: "Handover_Items",
                column: "EquipmentId");

            migrationBuilder.CreateIndex(
                name: "UQ_HandoverItem",
                table: "Handover_Items",
                columns: new[] { "HandoverId", "EquipmentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Handover_Photos_HandoverItemId",
                table: "Handover_Photos",
                column: "HandoverItemId");

            migrationBuilder.CreateIndex(
                name: "IX_Handovers_GuardId",
                table: "Handovers",
                column: "GuardId");

            migrationBuilder.CreateIndex(
                name: "IX_Handovers_ManagerReviewedById",
                table: "Handovers",
                column: "ManagerReviewedById");

            migrationBuilder.CreateIndex(
                name: "IX_Handovers_RoomId",
                table: "Handovers",
                column: "RoomId");

            migrationBuilder.CreateIndex(
                name: "UX_Handover_Request",
                table: "Handovers",
                column: "RequestId",
                unique: true,
                filter: "([RequestId] IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "UX_Handover_Residency",
                table: "Handovers",
                column: "ResidencyId",
                unique: true,
                filter: "([ResidencyId] IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_Import_Batches_BillingPeriodId",
                table: "Import_Batches",
                column: "BillingPeriodId");

            migrationBuilder.CreateIndex(
                name: "IX_Import_Batches_BlockId",
                table: "Import_Batches",
                column: "BlockId");

            migrationBuilder.CreateIndex(
                name: "IX_Import_Batches_GuardId",
                table: "Import_Batches",
                column: "GuardId");

            migrationBuilder.CreateIndex(
                name: "IX_Incident_Tickets_AssignedById",
                table: "Incident_Tickets",
                column: "AssignedById");

            migrationBuilder.CreateIndex(
                name: "IX_Incident_Tickets_AssignedGuardId",
                table: "Incident_Tickets",
                column: "AssignedGuardId");

            migrationBuilder.CreateIndex(
                name: "IX_Incident_Tickets_EquipmentId",
                table: "Incident_Tickets",
                column: "EquipmentId");

            migrationBuilder.CreateIndex(
                name: "IX_Incident_Tickets_StudentId",
                table: "Incident_Tickets",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_Room",
                table: "Incident_Tickets",
                column: "RoomId");

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_Status_DoneTime",
                table: "Incident_Tickets",
                columns: new[] { "Status", "DoneTime" });

            migrationBuilder.CreateIndex(
                name: "UQ__Incident__598CF7A39E2F8F3D",
                table: "Incident_Tickets",
                column: "TicketCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Inspection_Photos_InspectionRequestId",
                table: "Inspection_Photos",
                column: "InspectionRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_Inspection_Requests_AnomalyId",
                table: "Inspection_Requests",
                column: "AnomalyId");

            migrationBuilder.CreateIndex(
                name: "IX_Inspection_Requests_AssignedGuardId",
                table: "Inspection_Requests",
                column: "AssignedGuardId");

            migrationBuilder.CreateIndex(
                name: "IX_Inspection_Requests_CreatedById",
                table: "Inspection_Requests",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_Invoice_Items_InvoiceId",
                table: "Invoice_Items",
                column: "InvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_BookingRequestId",
                table: "Invoices",
                column: "BookingRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_HandoverId",
                table: "Invoices",
                column: "HandoverId");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_ParentInvoiceId",
                table: "Invoices",
                column: "ParentInvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_Status_DueDate",
                table: "Invoices",
                columns: new[] { "Status", "DueDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_Student_Status",
                table: "Invoices",
                columns: new[] { "StudentId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_TermId",
                table: "Invoices",
                column: "TermId");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_TransferRequestId",
                table: "Invoices",
                column: "TransferRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_UtilityBillId",
                table: "Invoices",
                column: "UtilityBillId");

            migrationBuilder.CreateIndex(
                name: "UQ__Invoices__D776E981277FBAC2",
                table: "Invoices",
                column: "InvoiceNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Meter_Readings_BatchId",
                table: "Meter_Readings",
                column: "BatchId");

            migrationBuilder.CreateIndex(
                name: "IX_Meter_Readings_BillingPeriodId",
                table: "Meter_Readings",
                column: "BillingPeriodId");

            migrationBuilder.CreateIndex(
                name: "IX_Meter_Readings_RecordedById",
                table: "Meter_Readings",
                column: "RecordedById");

            migrationBuilder.CreateIndex(
                name: "IX_Meter_Room",
                table: "Meter_Readings",
                columns: new[] { "RoomId", "BillingPeriodId" });

            migrationBuilder.CreateIndex(
                name: "UX_Meter_Checkout",
                table: "Meter_Readings",
                columns: new[] { "CheckoutRequestId", "RoomId", "MeterType" },
                unique: true,
                filter: "([CheckoutRequestId] IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "UX_Meter_Monthly",
                table: "Meter_Readings",
                columns: new[] { "RoomId", "MeterType", "BillingPeriodId" },
                unique: true,
                filter: "([BillingPeriodId] IS NOT NULL AND [CheckoutRequestId] IS NULL AND [TransferRequestId] IS NULL)");

            migrationBuilder.CreateIndex(
                name: "UX_Meter_Transfer",
                table: "Meter_Readings",
                columns: new[] { "TransferRequestId", "RoomId", "MeterType" },
                unique: true,
                filter: "([TransferRequestId] IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_News_ApprovedById",
                table: "News",
                column: "ApprovedById");

            migrationBuilder.CreateIndex(
                name: "IX_News_AuthorId",
                table: "News",
                column: "AuthorId");

            migrationBuilder.CreateIndex(
                name: "IX_News_Attachments_NewsId",
                table: "News_Attachments",
                column: "NewsId");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_User",
                table: "Notifications",
                columns: new[] { "UserId", "ReadFlag", "SentTime" },
                descending: new[] { false, false, true });

            migrationBuilder.CreateIndex(
                name: "IX_Payment_Status_Expiry",
                table: "Payment_Transactions",
                columns: new[] { "Status", "ExpiryTime" });

            migrationBuilder.CreateIndex(
                name: "IX_Payment_Transactions_StudentId",
                table: "Payment_Transactions",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "UQ__Payment___0D7A2497667E1EAF",
                table: "Payment_Transactions",
                column: "GatewayReference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Price_Items_DormId",
                table: "Price_Items",
                column: "DormId");

            migrationBuilder.CreateIndex(
                name: "IX_Price_Items_RoomTypeId",
                table: "Price_Items",
                column: "RoomTypeId");

            migrationBuilder.CreateIndex(
                name: "UQ_PriceItem",
                table: "Price_Items",
                columns: new[] { "PriceListId", "DormId", "RoomTypeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Price_Lists_CreatedById",
                table: "Price_Lists",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "UQ__Price_Li__410A21A46DDADDE6",
                table: "Price_Lists",
                column: "TermId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Refresh_Tokens_TokenHash",
                table: "Refresh_Tokens",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_User_Expires",
                table: "Refresh_Tokens",
                columns: new[] { "UserId", "ExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Refunds_RelatedInvoiceId",
                table: "Refunds",
                column: "RelatedInvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_Refunds_RelatedRequestId",
                table: "Refunds",
                column: "RelatedRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_Refunds_TransactionId",
                table: "Refunds",
                column: "TransactionId");

            migrationBuilder.CreateIndex(
                name: "UX_RegPeriod_ActivePerType",
                table: "Registration_Periods",
                columns: new[] { "TermId", "PeriodType" },
                unique: true,
                filter: "([ActiveFlag]=(1))");

            migrationBuilder.CreateIndex(
                name: "IX_Repair_Feedbacks_StudentId",
                table: "Repair_Feedbacks",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "UQ__Repair_F__712CC606C38D45B4",
                table: "Repair_Feedbacks",
                column: "TicketId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Request_Logs_ChangedById",
                table: "Request_Logs",
                column: "ChangedById");

            migrationBuilder.CreateIndex(
                name: "IX_Request_Logs_Request",
                table: "Request_Logs",
                column: "RequestId");

            migrationBuilder.CreateIndex(
                name: "IX_Requests_ProcessedById",
                table: "Requests",
                column: "ProcessedById");

            migrationBuilder.CreateIndex(
                name: "IX_Requests_Status_Type",
                table: "Requests",
                columns: new[] { "Status", "RequestType" });

            migrationBuilder.CreateIndex(
                name: "IX_Requests_Student",
                table: "Requests",
                columns: new[] { "StudentId", "RequestType" });

            migrationBuilder.CreateIndex(
                name: "IX_Requests_TermId",
                table: "Requests",
                column: "TermId");

            migrationBuilder.CreateIndex(
                name: "IX_Residencies_SourceRequestId",
                table: "Residencies",
                column: "SourceRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_Residencies_TermId",
                table: "Residencies",
                column: "TermId");

            migrationBuilder.CreateIndex(
                name: "UX_Residency_Bed_Term",
                table: "Residencies",
                columns: new[] { "BedId", "TermId" },
                unique: true,
                filter: "([Status] IN ('Reserved', 'Active'))");

            migrationBuilder.CreateIndex(
                name: "UX_Residency_Student_Term",
                table: "Residencies",
                columns: new[] { "StudentId", "TermId" },
                unique: true,
                filter: "([Status] IN ('Reserved', 'Active'))");

            migrationBuilder.CreateIndex(
                name: "IX_Role_Change_Histories_ChangedById",
                table: "Role_Change_Histories",
                column: "ChangedById");

            migrationBuilder.CreateIndex(
                name: "IX_RoleChangeHistories_User_Time",
                table: "Role_Change_Histories",
                columns: new[] { "UserId", "ChangedAt" });

            migrationBuilder.CreateIndex(
                name: "UQ__Roles__8A2B6160873AEB23",
                table: "Roles",
                column: "RoleName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Room_Type_Equipments_CatalogId",
                table: "Room_Type_Equipments",
                column: "CatalogId");

            migrationBuilder.CreateIndex(
                name: "UQ__Room_Typ__737584F6E07B0AF2",
                table: "Room_Types",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Rooms_RoomTypeId",
                table: "Rooms",
                column: "RoomTypeId");

            migrationBuilder.CreateIndex(
                name: "UQ_Room_Number",
                table: "Rooms",
                columns: new[] { "BlockId", "RoomNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_Term",
                table: "Terms",
                columns: new[] { "Name", "AcademicYear" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Ticket_Photos_TicketId",
                table: "Ticket_Photos",
                column: "TicketId");

            migrationBuilder.CreateIndex(
                name: "IX_Ticket_Photos_UploaderId",
                table: "Ticket_Photos",
                column: "UploaderId");

            migrationBuilder.CreateIndex(
                name: "IX_Ticket_Status_Histories_ChangedById",
                table: "Ticket_Status_Histories",
                column: "ChangedById");

            migrationBuilder.CreateIndex(
                name: "IX_Ticket_Status_Histories_TicketId",
                table: "Ticket_Status_Histories",
                column: "TicketId");

            migrationBuilder.CreateIndex(
                name: "IX_Transaction_Invoices_InvoiceId",
                table: "Transaction_Invoices",
                column: "InvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_Usage_Anomalies_ReadingId",
                table: "Usage_Anomalies",
                column: "ReadingId");

            migrationBuilder.CreateIndex(
                name: "IX_Usage_Anomalies_ResolvedById",
                table: "Usage_Anomalies",
                column: "ResolvedById");

            migrationBuilder.CreateIndex(
                name: "IX_User_Roles_AssignedById",
                table: "User_Roles",
                column: "AssignedById");

            migrationBuilder.CreateIndex(
                name: "IX_User_Roles_RoleId",
                table: "User_Roles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_SuspendedById",
                table: "Users",
                column: "SuspendedById");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Username",
                table: "Users",
                column: "Username",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ__Users__A25C5AA7FE2750B3",
                table: "Users",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ__Users__A9D105342A00E01E",
                table: "Users",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_Users_GoogleId",
                table: "Users",
                column: "GoogleId",
                unique: true,
                filter: "([GoogleId] IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_Utility_Bills_BillingPeriodId",
                table: "Utility_Bills",
                column: "BillingPeriodId");

            migrationBuilder.CreateIndex(
                name: "IX_Utility_Bills_CorrectedFromId",
                table: "Utility_Bills",
                column: "CorrectedFromId");

            migrationBuilder.CreateIndex(
                name: "UX_UtilityBill_Active",
                table: "Utility_Bills",
                columns: new[] { "RoomId", "BillingPeriodId" },
                unique: true,
                filter: "([Status]<>'Corrected')");

            migrationBuilder.CreateIndex(
                name: "UQ_UtilityPrice",
                table: "Utility_Prices",
                columns: new[] { "TermId", "MeterType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Violation_Evidences_ExplanationId",
                table: "Violation_Evidences",
                column: "ExplanationId");

            migrationBuilder.CreateIndex(
                name: "IX_Violation_Evidences_ViolationId",
                table: "Violation_Evidences",
                column: "ViolationId");

            migrationBuilder.CreateIndex(
                name: "IX_Violation_Explanations_StudentId",
                table: "Violation_Explanations",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "UQ__Violatio__18B6DC09BE22DF20",
                table: "Violation_Explanations",
                column: "ViolationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ__Violatio__737584F69526588D",
                table: "Violation_Types",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Violations_EvaluatedById",
                table: "Violations",
                column: "EvaluatedById");

            migrationBuilder.CreateIndex(
                name: "IX_Violations_RecordedById",
                table: "Violations",
                column: "RecordedById");

            migrationBuilder.CreateIndex(
                name: "IX_Violations_ResidencyId",
                table: "Violations",
                column: "ResidencyId");

            migrationBuilder.CreateIndex(
                name: "IX_Violations_RoomId",
                table: "Violations",
                column: "RoomId");

            migrationBuilder.CreateIndex(
                name: "IX_Violations_Student",
                table: "Violations",
                columns: new[] { "StudentId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Violations_ViolationTypeId",
                table: "Violations",
                column: "ViolationTypeId");

            migrationBuilder.CreateIndex(
                name: "UQ__Violatio__A25C5AA794BDC264",
                table: "Violations",
                column: "Code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Account_Status_Histories");

            migrationBuilder.DropTable(
                name: "Admins");

            migrationBuilder.DropTable(
                name: "Audit_Logs");

            migrationBuilder.DropTable(
                name: "Bed_Prices");

            migrationBuilder.DropTable(
                name: "Complaint_Evidences");

            migrationBuilder.DropTable(
                name: "Handover_Photos");

            migrationBuilder.DropTable(
                name: "Inspection_Photos");

            migrationBuilder.DropTable(
                name: "Invoice_Items");

            migrationBuilder.DropTable(
                name: "News_Attachments");

            migrationBuilder.DropTable(
                name: "Notifications");

            migrationBuilder.DropTable(
                name: "Price_Items");

            migrationBuilder.DropTable(
                name: "Refresh_Tokens");

            migrationBuilder.DropTable(
                name: "Refunds");

            migrationBuilder.DropTable(
                name: "Registration_Periods");

            migrationBuilder.DropTable(
                name: "Repair_Feedbacks");

            migrationBuilder.DropTable(
                name: "Request_Logs");

            migrationBuilder.DropTable(
                name: "Role_Change_Histories");

            migrationBuilder.DropTable(
                name: "Room_Type_Equipments");

            migrationBuilder.DropTable(
                name: "Ticket_Photos");

            migrationBuilder.DropTable(
                name: "Ticket_Status_Histories");

            migrationBuilder.DropTable(
                name: "Transaction_Invoices");

            migrationBuilder.DropTable(
                name: "User_Roles");

            migrationBuilder.DropTable(
                name: "Utility_Prices");

            migrationBuilder.DropTable(
                name: "Violation_Evidences");

            migrationBuilder.DropTable(
                name: "Complaint_Requests");

            migrationBuilder.DropTable(
                name: "Handover_Items");

            migrationBuilder.DropTable(
                name: "Inspection_Requests");

            migrationBuilder.DropTable(
                name: "News");

            migrationBuilder.DropTable(
                name: "Price_Lists");

            migrationBuilder.DropTable(
                name: "Incident_Tickets");

            migrationBuilder.DropTable(
                name: "Payment_Transactions");

            migrationBuilder.DropTable(
                name: "Roles");

            migrationBuilder.DropTable(
                name: "Violation_Explanations");

            migrationBuilder.DropTable(
                name: "Invoices");

            migrationBuilder.DropTable(
                name: "Usage_Anomalies");

            migrationBuilder.DropTable(
                name: "Equipments");

            migrationBuilder.DropTable(
                name: "Violations");

            migrationBuilder.DropTable(
                name: "Booking_Requests");

            migrationBuilder.DropTable(
                name: "Handovers");

            migrationBuilder.DropTable(
                name: "Utility_Bills");

            migrationBuilder.DropTable(
                name: "Meter_Readings");

            migrationBuilder.DropTable(
                name: "Equipment_Catalogs");

            migrationBuilder.DropTable(
                name: "Violation_Types");

            migrationBuilder.DropTable(
                name: "Import_Batches");

            migrationBuilder.DropTable(
                name: "Checkout_Requests");

            migrationBuilder.DropTable(
                name: "Bed_Transfer_Requests");

            migrationBuilder.DropTable(
                name: "Billing_Periods");

            migrationBuilder.DropTable(
                name: "Guards");

            migrationBuilder.DropTable(
                name: "Residencies");

            migrationBuilder.DropTable(
                name: "Beds");

            migrationBuilder.DropTable(
                name: "Requests");

            migrationBuilder.DropTable(
                name: "Rooms");

            migrationBuilder.DropTable(
                name: "Managers");

            migrationBuilder.DropTable(
                name: "Students");

            migrationBuilder.DropTable(
                name: "Terms");

            migrationBuilder.DropTable(
                name: "Blocks");

            migrationBuilder.DropTable(
                name: "Room_Types");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "Dorms");

            migrationBuilder.DropTable(
                name: "Campuses");
        }
    }
}
