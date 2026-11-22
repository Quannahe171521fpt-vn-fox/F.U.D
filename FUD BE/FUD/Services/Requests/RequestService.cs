using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FUD.DTOs.Requests;
using FUD.Infrastructure.Email;
using FUD.Infrastructure.Storage;
using FUD.Models;
using FUD.Repositories.Interfaces.Requests;
using FUD.Repositories.Interfaces.Violations;
using FUD.Services.Interfaces;
using FUD.Services.Interfaces.Requests;

namespace FUD.Services.Requests
{
    public class RequestService : IRequestService
    {
        private readonly IRequestRepository _requestRepo;
        private readonly IViolationRepository _violationRepo;
        private readonly IFileStorage _fileStorage;
        private readonly IEmailSender _emailSender;
        private readonly IInAppNotificationService? _inAppNotificationService;
        private readonly Microsoft.Extensions.Configuration.IConfiguration? _configuration;

        public RequestService(
            IRequestRepository requestRepo,
            IViolationRepository violationRepo,
            IFileStorage fileStorage,
            IEmailSender emailSender,
            IInAppNotificationService? inAppNotificationService = null,
            Microsoft.Extensions.Configuration.IConfiguration? configuration = null)
        {
            _requestRepo = requestRepo;
            _violationRepo = violationRepo;
            _fileStorage = fileStorage;
            _emailSender = emailSender;
            _inAppNotificationService = inAppNotificationService;
            _configuration = configuration;
        }

        public async Task<List<RequestItemDto>> GetAllRequestsAsync(string? requestType = null, string? status = null)
        {
            var requests = await _requestRepo.GetAllRequestsAsync(requestType, status);
            return requests.Select(MapToDto).ToList();
        }

        public async Task<List<RequestItemDto>> GetStudentRequestsAsync(int studentId)
        {
            var requests = await _requestRepo.GetStudentRequestsAsync(studentId);
            return requests.Select(MapToDto).ToList();
        }

        public async Task<BedTransferContextDto> GetBedTransferContextAsync(int studentId)
        {
            var studentUser = await _requestRepo.GetUserByIdAsync(studentId);
            if (studentUser == null)
            {
                return new BedTransferContextDto
                {
                    Eligible = false,
                    IneligibleReason = "Không tìm thấy thông tin tài khoản sinh viên."
                };
            }

            if (studentUser.AccountStatus == "Suspended" || !string.IsNullOrEmpty(studentUser.SuspensionReason))
            {
                return new BedTransferContextDto
                {
                    Eligible = false,
                    IneligibleReason = $"Tài khoản của bạn đang bị đình chỉ nội trú ({studentUser.SuspensionReason ?? "Vi phạm quy chế"})."
                };
            }

            var activeResidency = await _requestRepo.GetStudentActiveResidencyAsync(studentId);
            if (activeResidency == null)
            {
                return new BedTransferContextDto
                {
                    Eligible = false,
                    IneligibleReason = "Bạn chưa có đợt lưu trú nào đang hoạt động (Active) trong ký túc xá nên không thể yêu cầu chuyển giường."
                };
            }

            var prices = await _requestRepo.GetPublishedRoomPricesAsync(activeResidency.TermId);
            var currentDormId = activeResidency.Bed.Room.Block.DormId;
            var currentRoomTypeId = activeResidency.Bed.Room.RoomTypeId;
            prices.TryGetValue((currentDormId, currentRoomTypeId), out var currentPrice);

            var currentStayDto = new CurrentResidencyTransferDto
            {
                ResidencyId = activeResidency.Id,
                TermId = activeResidency.TermId,
                TermName = activeResidency.Term.Name,
                AcademicYear = activeResidency.Term.AcademicYear,
                TermStartDate = activeResidency.Term.StartDate,
                TermEndDate = activeResidency.Term.EndDate,
                DormName = activeResidency.Bed.Room.Block.Dorm.Name,
                BlockCode = activeResidency.Bed.Room.Block.Code,
                BlockName = activeResidency.Bed.Room.Block.Name,
                RoomId = activeResidency.Bed.RoomId,
                RoomNumber = activeResidency.Bed.Room.RoomNumber,
                RoomTypeName = activeResidency.Bed.Room.RoomType.Name,
                BedId = activeResidency.BedId,
                BedCode = activeResidency.Bed.BedCode,
                PositionInRoom = activeResidency.Bed.PositionInRoom,
                CheckInDate = activeResidency.CheckInDate,
                CurrentTermPrice = currentPrice > 0 ? currentPrice : null
            };

            if (await _requestRepo.HasOverdueInvoicesAsync(studentId))
            {
                return new BedTransferContextDto
                {
                    Eligible = false,
                    IneligibleReason = "Bạn đang có hóa đơn quá hạn thanh toán. Vui lòng thanh toán trước khi gửi yêu cầu chuyển giường.",
                    CurrentResidency = currentStayDto
                };
            }

            if (await _requestRepo.HasPendingBedTransferAsync(studentId, activeResidency.TermId))
            {
                return new BedTransferContextDto
                {
                    Eligible = false,
                    IneligibleReason = "Bạn đã có một đơn yêu cầu chuyển giường đang chờ xét duyệt trong học kỳ này.",
                    CurrentResidency = currentStayDto
                };
            }

            var availableBeds = await _requestRepo.GetAvailableBedsForTransferAsync(
                studentUser.Gender,
                activeResidency.BedId,
                activeResidency.TermId);

            var bedDtos = availableBeds.Select(b =>
            {
                prices.TryGetValue((b.Room.Block.DormId, b.Room.RoomTypeId), out var bedPrice);
                return new AvailableTransferBedDto
                {
                    BedId = b.Id,
                    BedCode = b.BedCode,
                    PositionInRoom = b.PositionInRoom,
                    RoomId = b.RoomId,
                    RoomNumber = b.Room.RoomNumber,
                    FloorLevel = b.Room.FloorLevel,
                    RoomTypeId = b.Room.RoomTypeId,
                    RoomTypeName = b.Room.RoomType.Name,
                    BedCapacity = b.Room.RoomType.BedCapacity,
                    BlockId = b.Room.BlockId,
                    BlockCode = b.Room.Block.Code,
                    BlockName = b.Room.Block.Name,
                    DormId = b.Room.Block.DormId,
                    DormCode = b.Room.Block.Dorm.Code,
                    DormName = b.Room.Block.Dorm.Name,
                    TermPrice = bedPrice > 0 ? bedPrice : null
                };
            }).ToList();

            return new BedTransferContextDto
            {
                Eligible = true,
                CurrentResidency = currentStayDto,
                AvailableBeds = bedDtos
            };
        }

        public async Task<RequestItemDto> CreateRequestAsync(int studentId, CreateRequestDto dto)
        {
            // Kiểm tra trạng thái tài khoản sinh viên: Nếu bị đình chỉ thì khóa toàn bộ quyền gửi đơn (Request), bao gồm Booking, BedTransfer, v.v.
            var studentUser = await _requestRepo.GetUserByIdAsync(studentId);
            if (studentUser != null && (studentUser.AccountStatus == "Suspended" || !string.IsNullOrEmpty(studentUser.SuspensionReason)))
            {
                throw new InvalidOperationException(
                    $"Tài khoản của bạn đã bị ĐÌNH CHỈ NỘI TRÚ ({studentUser.SuspensionReason ?? "Đình chỉ nội trú do vi phạm quy chế"}). " +
                    "Hệ thống đã khóa toàn bộ quyền gửi đơn từ & booking giữ chỗ. Chỉ khi Ban Quản lý mở lại tài khoản bạn mới có thể thực hiện thao tác tiếp.");
            }

            if (dto.RequestType == "BedTransfer")
            {
                return await CreateBedTransferInternalAsync(studentId, studentUser, dto);
            }

            int termId = dto.TermId ?? 0;
            if (termId == 0)
            {
                var activeTerm = await _requestRepo.GetCurrentActiveTermAsync();
                if (activeTerm == null)
                    throw new InvalidOperationException("Không tìm thấy học kỳ hoạt động.");
                termId = activeTerm.Id;
            }

            var request = new Request
            {
                StudentId = studentId,
                TermId = termId,
                RequestType = dto.RequestType,
                Status = "Pending",
                Reason = dto.Reason ?? dto.Content,
                CreatedTime = DateTime.UtcNow,
            };

            await _requestRepo.CreateRequestAsync(request);

            if (dto.RequestType == "Complaint")
            {
                var complaint = new ComplaintRequest
                {
                    RequestId = request.Id,
                    TargetType = dto.TargetType ?? "Violation",
                    ViolationId = dto.TargetViolationId,
                    Content = dto.Content ?? dto.Reason ?? string.Empty
                };

                var evidences = new List<ComplaintEvidence>();
                if (dto.Evidences != null && dto.Evidences.Any())
                {
                    var uploadedUrls = await _fileStorage.UploadImagesAsync(dto.Evidences, "complaint-evidences");
                    foreach (var url in uploadedUrls)
                    {
                        evidences.Add(new ComplaintEvidence
                        {
                            ComplaintRequestId = request.Id,
                            FileUrl = url
                        });
                    }
                }

                await _requestRepo.AddComplaintRequestAsync(complaint, evidences);
            }

            var reloaded = await _requestRepo.GetRequestByIdAsync(request.Id);

            if (_inAppNotificationService != null)
            {
                var studentLabel = studentUser != null
                    ? $"{studentUser.FullName} ({studentUser.Code})"
                    : (reloaded?.Student?.User != null
                        ? $"{reloaded.Student.User.FullName} ({reloaded.Student.User.Code})"
                        : $"SV #{studentId}");
                var reqCode = $"REQ-{request.Id:D6}";
                var title = dto.RequestType switch
                {
                    "Complaint" => $"Đơn khiếu nại mới ({reqCode})",
                    "Checkout" => $"Yêu cầu trả phòng mới ({reqCode})",
                    _ => $"Đơn yêu cầu mới ({reqCode})"
                };
                var kindLabel = dto.RequestType == "Complaint"
                    ? "khiếu nại"
                    : dto.RequestType == "Checkout"
                        ? "trả phòng"
                        : "yêu cầu";
                var content = $"Sinh viên {studentLabel} vừa gửi đơn {kindLabel}. Lý do: {request.Reason ?? "Không có"}.";

                await _inAppNotificationService.NotifyRolesAsync(
                    new[] { AccountConstants.Roles.Manager },
                    title,
                    content,
                    "Request",
                    request.Id.ToString());
            }

            return MapToDto(reloaded!);
        }

        private async Task<RequestItemDto> CreateBedTransferInternalAsync(int studentId, User? studentUser, CreateRequestDto dto)
        {
            if (!dto.ToBedId.HasValue || dto.ToBedId.Value <= 0)
            {
                throw new InvalidOperationException("Vui lòng chọn giường đích muốn chuyển đến.");
            }

            var reason = (dto.Reason ?? dto.Content)?.Trim();
            if (string.IsNullOrWhiteSpace(reason))
            {
                throw new InvalidOperationException("Vui lòng nhập lý do chuyển giường.");
            }

            var activeResidency = await _requestRepo.GetStudentActiveResidencyAsync(studentId);
            if (activeResidency == null)
            {
                throw new InvalidOperationException("Bạn chưa có đợt lưu trú nào đang hoạt động (Active) để thực hiện chuyển giường.");
            }

            if (await _requestRepo.HasOverdueInvoicesAsync(studentId))
            {
                throw new InvalidOperationException("Sinh viên đang có hóa đơn quá hạn, không thể gửi yêu cầu chuyển giường.");
            }

            if (await _requestRepo.HasPendingBedTransferAsync(studentId, activeResidency.TermId))
            {
                throw new InvalidOperationException("Bạn đã có một đơn yêu cầu chuyển giường đang chờ xét duyệt trong học kỳ này.");
            }

            var toBed = await _requestRepo.GetBedWithLocationByIdAsync(dto.ToBedId.Value);
            if (toBed == null)
            {
                throw new InvalidOperationException("Không tìm thấy giường đích được chọn.");
            }

            if (toBed.Id == activeResidency.BedId)
            {
                throw new InvalidOperationException("Giường đích phải khác với giường bạn đang ở hiện tại.");
            }

            if (toBed.Status != "Free"
                || toBed.Room.Status != "Active"
                || toBed.Room.Block.Status != "Active"
                || toBed.Room.Block.Dorm.Status != "Active")
            {
                throw new InvalidOperationException("Giường đích hiện không ở trạng thái trống (Free) hoặc khu/phòng không hoạt động.");
            }

            if (studentUser != null && !string.Equals(studentUser.Gender, toBed.Room.Block.GenderType, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Giới tính sinh viên không khớp với khu (Block) của giường đích.");
            }

            if (!await _requestRepo.IsBedAvailableForTransferAsync(toBed.Id, activeResidency.TermId))
            {
                throw new InvalidOperationException("Giường đích đã có sinh viên khác giữ chỗ hoặc đang chờ chuyển đến.");
            }

            if (dto.TransferDate.HasValue)
            {
                var todayVn = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
                if (dto.TransferDate.Value < todayVn)
                {
                    throw new InvalidOperationException("Ngày dự kiến chuyển không được nhỏ hơn ngày hiện tại.");
                }
                if (dto.TransferDate.Value > activeResidency.Term.EndDate)
                {
                    throw new InvalidOperationException("Ngày dự kiến chuyển phải nằm trong thời gian của học kỳ hiện tại.");
                }
            }

            var now = DateTime.UtcNow;
            var request = new Request
            {
                StudentId = studentId,
                TermId = activeResidency.TermId,
                RequestType = "BedTransfer",
                Status = "Pending",
                Reason = reason,
                CreatedTime = now
            };

            var transferRequest = new BedTransferRequest
            {
                FromResidencyId = activeResidency.Id,
                FromBedId = activeResidency.BedId,
                ToBedId = toBed.Id,
                TransferReason = reason,
                TransferDate = dto.TransferDate
            };

            var log = new RequestLog
            {
                FromStatus = null,
                ToStatus = "Pending",
                Note = $"Sinh viên gửi yêu cầu chuyển từ giường {activeResidency.Bed.BedCode} sang giường {toBed.BedCode}.",
                ChangeTime = now,
                ChangedById = studentId
            };

            toBed.Status = "Pending Transfer";
            await _requestRepo.CreateBedTransferRequestAsync(request, transferRequest, log);

            if (_inAppNotificationService != null)
            {
                var reqCode = $"REQ-{request.Id:D6}";
                var studentLabel = studentUser != null
                    ? $"{studentUser.FullName} ({studentUser.Code})"
                    : $"SV #{studentId}";

                await _inAppNotificationService.NotifyRolesAsync(
                    new[] { AccountConstants.Roles.Manager },
                    $"Yêu cầu chuyển giường mới ({reqCode})",
                    $"Sinh viên {studentLabel} yêu cầu chuyển từ giường {activeResidency.Bed.BedCode} sang giường {toBed.BedCode}.",
                    "Request",
                    request.Id.ToString());
            }

            var reloaded = await _requestRepo.GetRequestByIdAsync(request.Id);
            return MapToDto(reloaded!);
        }

        public async Task<List<GuardLookupDto>> GetGuardsAsync(int? dormId = null)
        {
            var guards = await _requestRepo.GetGuardsAsync(dormId);
            return guards.Select(g => new GuardLookupDto
            {
                UserId = g.UserId,
                FullName = g.User?.FullName ?? string.Empty,
                GuardCode = g.User?.Code ?? string.Empty,
                PhoneNumber = g.User?.Phone,
                AssignedDormId = g.DormId,
                AssignedDormName = g.Dorm?.Name ?? string.Empty
            }).ToList();
        }

        public async Task<List<TransferInspectionTaskDto>> GetGuardTransferInspectionsAsync(int guardUserId)
        {
            var tasks = await _requestRepo.GetTransferInspectionTasksForGuardAsync(guardUserId);
            var roomIds = tasks
                .Where(t => t.BedTransferRequest?.FromBed != null)
                .Select(t => t.BedTransferRequest!.FromBed.RoomId)
                .Distinct()
                .ToList();

            var prevReadingsByRoom = new Dictionary<int, Dictionary<string, decimal>>();
            foreach (var roomId in roomIds)
            {
                prevReadingsByRoom[roomId] = await _requestRepo.GetLatestRoomMeterReadingsAsync(roomId);
            }

            return tasks.Select(t =>
            {
                var roomId = t.BedTransferRequest!.FromBed.RoomId;
                prevReadingsByRoom.TryGetValue(roomId, out var prev);
                return MapToGuardTaskDto(t, prev);
            }).ToList();
        }

        public async Task<TransferInspectionTaskDto> SubmitTransferInspectionAsync(
            int requestId,
            int guardUserId,
            SubmitTransferInspectionDto dto)
        {
            var req = await _requestRepo.GetRequestByIdAsync(requestId);
            if (req == null)
                throw new KeyNotFoundException("Không tìm thấy đơn chuyển giường.");

            if (req.RequestType != "BedTransfer" || req.BedTransferRequest == null)
                throw new InvalidOperationException("Đơn yêu cầu không phải loại chuyển giường.");

            if (req.Status != "Approved")
                throw new InvalidOperationException("Chỉ có thể lập biên bản kiểm tra khi đơn chuyển giường đã được Quản lý duyệt Bước 1 (Approved).");

            var bt = req.BedTransferRequest;
            if (bt.AssignedGuardId != guardUserId)
                throw new InvalidOperationException("Bạn không phải Bảo vệ được phân công kiểm tra đơn chuyển giường này.");

            if (req.Handover != null || (bt.MeterReadings != null && bt.MeterReadings.Count > 0))
                throw new InvalidOperationException("Đơn chuyển giường này đã được kiểm tra và lập biên bản bàn giao trước đó.");

            var fromRoomId = bt.FromBed.RoomId;
            var prevReadings = await _requestRepo.GetLatestRoomMeterReadingsAsync(fromRoomId);
            var prevElec = prevReadings.GetValueOrDefault("Electricity", 0m);
            var prevWater = prevReadings.GetValueOrDefault("Water", 0m);

            if (dto.CurrentElectricityReading < 0 || dto.CurrentWaterReading < 0)
                throw new InvalidOperationException("Chỉ số điện, nước không được nhỏ hơn 0.");

            if (dto.CurrentElectricityReading < prevElec)
                throw new InvalidOperationException($"Chỉ số điện hiện tại ({dto.CurrentElectricityReading}) không được nhỏ hơn chỉ số cũ ({prevElec}).");

            if (dto.CurrentWaterReading < prevWater)
                throw new InvalidOperationException($"Chỉ số nước hiện tại ({dto.CurrentWaterReading}) không được nhỏ hơn chỉ số cũ ({prevWater}).");

            string? validElecUrl;
            string? validWaterUrl;
            try
            {
                validElecUrl = FUD.Services.Utilities.MeterEvidenceUrlValidator.ValidateOrNull(dto.ElectricityEvidenceImageUrl, _configuration);
                validWaterUrl = FUD.Services.Utilities.MeterEvidenceUrlValidator.ValidateOrNull(dto.WaterEvidenceImageUrl, _configuration);
            }
            catch (ArgumentException ex)
            {
                throw new InvalidOperationException(ex.Message);
            }

            var roomEquipments = await _requestRepo.GetRoomAndBedEquipmentsAsync(fromRoomId, bt.FromBedId);
            var validEquipMap = roomEquipments.ToDictionary(e => e.Id);

            var allowedConditions = new HashSet<string>(StringComparer.Ordinal) { "Good", "Damaged", "Missing" };
            var handoverItems = new List<HandoverItem>();
            var equipmentUpdates = new List<(int EquipmentId, string Condition, string? Note)>();
            var seenEquipIds = new HashSet<int>();

            foreach (var itemInput in dto.Items ?? new List<TransferInspectionItemInputDto>())
            {
                if (!seenEquipIds.Add(itemInput.EquipmentId))
                    continue;

                if (!validEquipMap.ContainsKey(itemInput.EquipmentId))
                    throw new InvalidOperationException($"Thiết bị #{itemInput.EquipmentId} không thuộc phòng/giường cũ của sinh viên.");

                if (!allowedConditions.Contains(itemInput.ItemCondition))
                    throw new InvalidOperationException($"Tình trạng thiết bị '{itemInput.ItemCondition}' không hợp lệ (chỉ chấp nhận Good, Damaged, Missing).");

                if (itemInput.CompensationAmount < 0)
                    throw new InvalidOperationException("Số tiền bồi thường thiết bị không được âm.");

                var compensation = itemInput.ItemCondition == "Good" ? 0m : Math.Round(itemInput.CompensationAmount, 2);
                var note = string.IsNullOrWhiteSpace(itemInput.Description) ? null : itemInput.Description.Trim();

                handoverItems.Add(new HandoverItem
                {
                    EquipmentId = itemInput.EquipmentId,
                    ItemCondition = itemInput.ItemCondition,
                    Note = note,
                    CompensationAmount = compensation
                });

                if (itemInput.ItemCondition == "Damaged")
                {
                    equipmentUpdates.Add((itemInput.EquipmentId, "Broken", note));
                }
                else if (itemInput.ItemCondition == "Missing")
                {
                    equipmentUpdates.Add((itemInput.EquipmentId, "Broken", note ?? "Thất lạc khi bàn giao chuyển giường"));
                }
            }

            var now = DateTime.UtcNow;
            var totalCompensation = handoverItems.Sum(i => i.CompensationAmount);
            var hasDamageOrMissing = handoverItems.Any(i => i.ItemCondition != "Good");
            var trimmedNote = string.IsNullOrWhiteSpace(dto.Note) ? null : dto.Note.Trim();

            var handover = new Handover
            {
                HandoverType = "BedTransfer",
                RequestId = req.Id,
                ResidencyId = null,
                RoomId = fromRoomId,
                GuardId = guardUserId,
                BelongingsRemainingFlag = false,
                BelongingsNote = trimmedNote,
                HasDamageOrMissingFlag = hasDamageOrMissing,
                TotalCompensation = totalCompensation,
                Status = "Completed",
                StudentAcceptedFlag = true,
                StudentAcceptedTime = now,
                CreatedTime = now,
                CompletedTime = now
            };

            var meterReadings = new List<MeterReading>
            {
                new MeterReading
                {
                    RoomId = fromRoomId,
                    TransferRequestId = req.Id,
                    MeterType = "Electricity",
                    PreviousValue = prevElec,
                    CurrentValue = dto.CurrentElectricityReading,
                    Source = "Handover",
                    Status = "Recorded",
                    RecordedById = guardUserId,
                    RecordedTime = now,
                    Note = trimmedNote,
                    EvidenceImageUrl = validElecUrl
                },
                new MeterReading
                {
                    RoomId = fromRoomId,
                    TransferRequestId = req.Id,
                    MeterType = "Water",
                    PreviousValue = prevWater,
                    CurrentValue = dto.CurrentWaterReading,
                    Source = "Handover",
                    Status = "Recorded",
                    RecordedById = guardUserId,
                    RecordedTime = now,
                    Note = trimmedNote,
                    EvidenceImageUrl = validWaterUrl
                }
            };

            var utilityPrices = await _requestRepo.GetUtilityPricesByTermAsync(req.TermId);
            var residentCount = await _requestRepo.GetActiveRoomResidentCountAsync(fromRoomId, req.TermId);
            var elecShare = Math.Round((dto.CurrentElectricityReading - prevElec) * utilityPrices.GetValueOrDefault("Electricity", 0m) / residentCount, 2);
            var waterShare = Math.Round((dto.CurrentWaterReading - prevWater) * utilityPrices.GetValueOrDefault("Water", 0m) / residentCount, 2);
            var requestCode = $"REQ-{req.Id:D6}";

            var log = new RequestLog
            {
                RequestId = req.Id,
                FromStatus = req.Status,
                ToStatus = req.Status,
                Note = $"Bảo vệ hoàn tất kiểm tra 3 bên (SV-Bảo vệ-Bạn cùng phòng). Điện: {prevElec:0.##}→{dto.CurrentElectricityReading:0.##}, Nước: {prevWater:0.##}→{dto.CurrentWaterReading:0.##}. Tiền điện/nước chia đầu người ({residentCount} SV): {(elecShare + waterShare):N0}đ. Bồi thường thiết bị: {totalCompensation:N0}đ.",
                ChangeTime = now,
                ChangedById = guardUserId
            };

            var notifications = new List<Notification>
            {
                new Notification
                {
                    UserId = req.StudentId,
                    Channel = "InApp",
                    Title = $"Đã lập biên bản kiểm tra chuyển giường ({requestCode})",
                    Content = $"Bảo vệ đã hoàn tất kiểm tra phòng {bt.FromBed.Room?.RoomNumber} dưới sự chứng kiến 3 bên. Điện nước chốt: {(elecShare + waterShare):N0}đ, bồi thường thiết bị: {totalCompensation:N0}đ. Đơn đang chờ Quản lý phát hành hóa đơn và chuyển giường.",
                    ReferenceType = "Request",
                    ReferenceId = req.Id.ToString(),
                    ReadFlag = false,
                    SentTime = now
                }
            };

            if (req.ProcessedById.HasValue)
            {
                notifications.Add(new Notification
                {
                    UserId = req.ProcessedById.Value,
                    Channel = "InApp",
                    Title = $"Biên bản kiểm tra chuyển giường đã sẵn sàng ({requestCode})",
                    Content = $"Bảo vệ đã gửi biên bản kiểm tra & chỉ số điện nước cuối cho đơn {requestCode}. Tổng điện/nước + bồi thường: {(elecShare + waterShare + totalCompensation):N0}đ. Vui lòng xem xét và hoàn tất chuyển giường.",
                    ReferenceType = "Request",
                    ReferenceId = req.Id.ToString(),
                    ReadFlag = false,
                    SentTime = now
                });
            }

            await _requestRepo.SubmitTransferInspectionAsync(req, handover, handoverItems, meterReadings, equipmentUpdates, log, notifications);

            var reloaded = await _requestRepo.GetRequestByIdAsync(requestId) ?? req;
            if (reloaded.Handover == null)
            {
                handover.HandoverItems = handoverItems;
                reloaded.Handover = handover;
            }
            if (reloaded.BedTransferRequest != null && reloaded.BedTransferRequest.MeterReadings.Count == 0)
            {
                reloaded.BedTransferRequest.MeterReadings = meterReadings;
            }

            return MapToGuardTaskDto(reloaded, prevReadings);
        }

        public async Task<RequestItemDto> ProcessRequestAsync(int requestId, int managerId, ProcessRequestDto dto)
        {
            var req = await _requestRepo.GetRequestByIdAsync(requestId);
            if (req == null)
                throw new KeyNotFoundException("Không tìm thấy đơn yêu cầu.");

            if (req.RequestType == "BedTransfer")
            {
                return await ProcessBedTransferRequestAsync(req, managerId, dto);
            }

            req.Status = dto.Decision;
            req.ProcessedById = managerId;
            req.ProcessedTime = DateTime.UtcNow;

            if (dto.Decision == "Rejected")
            {
                req.RejectReason = dto.Note;
            }

            if (req.ComplaintRequest != null)
            {
                req.ComplaintRequest.ResolutionNote = dto.Note;
            }

            if (req.RequestType == "Complaint" && req.ComplaintRequest?.ViolationId != null)
            {
                var violation = await _violationRepo.GetViolationByIdAsync(req.ComplaintRequest.ViolationId.Value);
                if (violation != null)
                {
                    if (dto.Decision == "Approved")
                    {
                        if (!string.IsNullOrWhiteSpace(dto.AdjustedLevel))
                        {
                            violation.FinalLevel = dto.AdjustedLevel;
                            violation.Status = "Finalized";
                        }
                        else
                        {
                            violation.Status = "Dismissed";
                        }
                    }
                    else
                    {
                        violation.FinalLevel = violation.InitialLevel;
                        violation.Status = "Finalized";
                    }

                    violation.ManagerNote = dto.Note;
                    violation.FinalizedTime = DateTime.UtcNow;
                    violation.EvaluatedById = managerId;
                    await _violationRepo.UpdateViolationAsync(violation);

                    // Xử lý khóa / mở đình chỉ dựa trên kết quả giải quyết khiếu nại của Quản lý
                    var studentUser = await _requestRepo.GetUserByIdAsync(req.StudentId);
                    if (studentUser != null)
                    {
                        if (violation.Status == "Finalized" && !string.IsNullOrEmpty(violation.FinalLevel) &&
                            (violation.FinalLevel.Contains("Đình chỉ") || violation.FinalLevel.Contains("Buộc rời")))
                        {
                            studentUser.AccountStatus = "Suspended";
                            studentUser.SuspendedAt = DateTime.UtcNow;
                            studentUser.SuspendedById = managerId;
                            studentUser.SuspensionReason = $"Bị đình chỉ nội trú theo quyết định kỷ luật biên bản {violation.Code} ({violation.FinalLevel})";
                            await _requestRepo.UpdateUserAsync(studentUser);
                        }
                        else if (violation.Status == "Dismissed" || (violation.Status == "Finalized" && !string.IsNullOrEmpty(violation.FinalLevel) && !violation.FinalLevel.Contains("Đình chỉ")))
                        {
                            if (studentUser.AccountStatus == "Suspended")
                            {
                                studentUser.AccountStatus = "Active";
                                studentUser.SuspendedAt = null;
                                studentUser.SuspendedById = null;
                                studentUser.SuspensionReason = null;
                                await _requestRepo.UpdateUserAsync(studentUser);
                            }
                        }
                    }

                    if (req.Student?.User != null && !string.IsNullOrWhiteSpace(req.Student.User.Email))
                    {
                        var studentEmail = req.Student.User.Email;
                        var studentName = req.Student.User.FullName;
                        var statusVi = violation.Status == "Dismissed" ? "Đã bác bỏ biên bản (khiếu nại được duyệt)" :
                                       violation.Status == "Finalized" ? $"Đã có kết luận ({violation.FinalLevel})" : violation.Status;

                        var subject = $"[KTX FUD] Thông báo kết quả xử lý đơn khiếu nại: {violation.Code}";
                        var body = $@"
<div style='font-family: Arial, sans-serif; line-height: 1.6; color: #333;'>
    <h2 style='color: #ea580c;'>Kết quả xử lý Đơn khiếu nại</h2>
    <p>Xin chào <strong>{studentName}</strong>,</p>
    <p>Ban Quản lý Ký túc xá đã hoàn tất xem xét và phản hồi đơn khiếu nại của bạn đối với biên bản vi phạm <strong>{violation.Code}</strong>.</p>
    <div style='background: #f8fafc; border-left: 4px solid #ea580c; padding: 12px; margin: 16px 0;'>
        <p><strong>Biên bản:</strong> {violation.Code}</p>
        <p><strong>Lỗi vi phạm:</strong> {violation.ViolationType?.Name}</p>
        <p><strong>Trạng thái kết luận:</strong> {statusVi}</p>
        <p><strong>Mức độ chính thức:</strong> {violation.FinalLevel ?? "Đã hủy bỏ"}</p>
        <p><strong>Phản hồi từ Ban Quản lý:</strong> {dto.Note ?? "Không có"}</p>
    </div>
    <p>Vui lòng đăng nhập hệ thống F.U.D để xem chi tiết đầy đủ hồ sơ của bạn.</p>
    <hr style='border: none; border-top: 1px solid #e2e8f0; margin: 20px 0;'/>
    <p style='font-size: 12px; color: #64748b;'>Ban Quản lý Ký túc xá Đại học FPT</p>
</div>";
                        try
                        {
                            await _emailSender.SendEmailAsync(studentEmail, subject, body);
                        }
                        catch
                        {
                            // Ignore email sending error
                        }
                    }
                }
            }

            await _requestRepo.UpdateRequestAsync(req);

            if (_inAppNotificationService != null)
            {
                var reqCode = $"REQ-{req.Id:D6}";
                var decisionText = dto.Decision == "Approved" ? "được chấp thuận" : "bị từ chối";
                var noteText = string.IsNullOrWhiteSpace(dto.Note)
                    ? string.Empty
                    : $" Ghi chú từ Ban Quản lý: {dto.Note.Trim()}.";

                await _inAppNotificationService.NotifyUserAsync(
                    req.StudentId,
                    $"Đơn yêu cầu {reqCode} đã {decisionText}",
                    $"Đơn {req.RequestType} ({reqCode}) của bạn đã {decisionText}.{noteText}",
                    "Request",
                    req.Id.ToString());
            }

            return MapToDto(req);
        }

        private static RequestItemDto MapToDto(Request req)
        {
            var res = req.Student?.Residencies?.FirstOrDefault();
            var roomInfo = res?.Bed?.Room != null ? $"P.{res.Bed.Room.RoomNumber} ({res.Bed.Room.Block?.Name})" : "Chưa xếp phòng";

            var dto = new RequestItemDto
            {
                Id = req.Id,
                RequestCode = $"REQ-{req.Id:D6}",
                StudentId = req.StudentId,
                StudentName = req.Student?.User?.FullName ?? string.Empty,
                StudentCode = req.Student?.User?.Code ?? string.Empty,
                RoomInfo = roomInfo,
                RequestType = req.RequestType,
                Status = req.Status,
                Reason = req.Reason,
                RejectReason = req.RejectReason,
                CreatedTime = req.CreatedTime,
                ProcessedTime = req.ProcessedTime,
                ProcessedByName = req.ProcessedBy?.User?.FullName,
                StudentAccountStatus = req.Student?.User?.AccountStatus ?? "Active",
                SuspensionReason = req.Student?.User?.SuspensionReason,
            };

            if (req.ComplaintRequest != null)
            {
                dto.Complaint = new ComplaintDetailDto
                {
                    TargetType = req.ComplaintRequest.TargetType,
                    ViolationId = req.ComplaintRequest.ViolationId,
                    ViolationCode = req.ComplaintRequest.Violation?.Code,
                    ViolationTypeName = req.ComplaintRequest.Violation?.ViolationType?.Name,
                    InitialLevel = req.ComplaintRequest.Violation?.InitialLevel,
                    FinalLevel = req.ComplaintRequest.Violation?.FinalLevel,
                    ViolationStatus = req.ComplaintRequest.Violation?.Status,
                    Content = req.ComplaintRequest.Content,
                    ResolutionNote = req.ComplaintRequest.ResolutionNote,
                    Evidences = req.ComplaintRequest.ComplaintEvidences.Select(e => e.FileUrl).ToList()
                };
            }

            if (req.BedTransferRequest != null)
            {
                dto.BedTransfer = BuildBedTransferDetailDto(req);
            }

            return dto;
        }

        private async Task<RequestItemDto> ProcessBedTransferRequestAsync(Request req, int managerId, ProcessRequestDto dto)
        {
            var bt = req.BedTransferRequest
                ?? throw new InvalidOperationException("Không tìm thấy thông tin chi tiết đơn chuyển giường.");

            if (dto.Decision != "Approved" && dto.Decision != "Rejected" && dto.Decision != "Completed")
            {
                throw new InvalidOperationException("Quyết định xử lý không hợp lệ cho đơn chuyển giường (chỉ chấp nhận Approved, Rejected, Completed).");
            }

            var requestCode = $"REQ-{req.Id:D6}";
            var now = DateTime.UtcNow;

            if (dto.Decision == "Rejected")
            {
                if (req.Status != "Pending" && req.Status != "Approved")
                {
                    throw new InvalidOperationException("Chỉ có thể từ chối đơn chuyển giường đang chờ xử lý hoặc đang trong quá trình kiểm tra.");
                }

                if (string.IsNullOrWhiteSpace(dto.Note))
                {
                    throw new InvalidOperationException("Vui lòng nhập lý do từ chối đơn chuyển giường.");
                }

                var previousStatus = req.Status;
                var rejectReason = dto.Note.Trim();
                req.Status = "Rejected";
                req.RejectReason = rejectReason;
                req.ProcessedById = managerId;
                req.ProcessedTime = now;

                var log = new RequestLog
                {
                    RequestId = req.Id,
                    FromStatus = previousStatus,
                    ToStatus = "Rejected",
                    Note = $"Quản lý từ chối đơn chuyển giường: {rejectReason}. Giường đích đã được giải phóng về trạng thái trống.",
                    ChangeTime = now,
                    ChangedById = managerId
                };

                var notification = new Notification
                {
                    UserId = req.StudentId,
                    Channel = "InApp",
                    Title = $"Đơn chuyển giường {requestCode} bị từ chối",
                    Content = $"Yêu cầu chuyển giường từ {bt.FromBed?.BedCode} sang {bt.ToBed?.BedCode} đã bị từ chối. Lý do: {rejectReason}.",
                    ReferenceType = "Request",
                    ReferenceId = req.Id.ToString(),
                    ReadFlag = false,
                    SentTime = now
                };

                await _requestRepo.RejectBedTransferAsync(req, log, notification);

                await TrySendEmailAsync(
                    req.Student?.User?.Email,
                    $"[KTX FUD] Thông báo từ chối đơn chuyển giường {requestCode}",
                    $@"<div style='font-family: Arial, sans-serif; line-height: 1.6; color: #333;'>
                        <h2 style='color: #dc2626;'>Đơn chuyển giường bị từ chối</h2>
                        <p>Xin chào <strong>{req.Student?.User?.FullName}</strong>,</p>
                        <p>Đơn yêu cầu chuyển giường <strong>{requestCode}</strong> của bạn đã bị Ban Quản lý từ chối.</p>
                        <div style='background: #fef2f2; border-left: 4px solid #dc2626; padding: 12px; margin: 16px 0;'>
                            <p><strong>Giường hiện tại:</strong> {bt.FromBed?.BedCode} - P.{bt.FromBed?.Room?.RoomNumber}</p>
                            <p><strong>Giường đăng ký chuyển:</strong> {bt.ToBed?.BedCode} - P.{bt.ToBed?.Room?.RoomNumber}</p>
                            <p><strong>Lý do từ chối:</strong> {rejectReason}</p>
                        </div>
                        <p>Bạn vẫn tiếp tục lưu trú tại giường hiện tại.</p>
                    </div>");

                var reloadedReject = await _requestRepo.GetRequestByIdAsync(req.Id) ?? req;
                return MapToDto(reloadedReject);
            }

            if (dto.Decision == "Approved")
            {
                if (req.Status != "Pending")
                {
                    throw new InvalidOperationException("Đơn chuyển giường không ở trạng thái Chờ xử lý (Pending) để duyệt Bước 1.");
                }

                if (!dto.AssignedGuardId.HasValue || dto.AssignedGuardId.Value <= 0)
                {
                    throw new InvalidOperationException("Vui lòng chọn Bảo vệ phụ trách kiểm tra phòng cũ trước khi duyệt đơn.");
                }

                var guard = await _requestRepo.GetGuardByUserIdAsync(dto.AssignedGuardId.Value);
                if (guard == null)
                {
                    throw new InvalidOperationException("Không tìm thấy Bảo vệ được phân công hoặc tài khoản Bảo vệ không hoạt động.");
                }

                var term = bt.FromResidency?.Term ?? req.Term
                    ?? throw new InvalidOperationException("Không xác định được học kỳ của đợt lưu trú hiện tại.");

                var todayVn = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
                var transferDate = dto.TransferDate ?? bt.TransferDate ?? todayVn;
                if (transferDate > term.EndDate)
                {
                    throw new InvalidOperationException("Ngày chuyển giường không được vượt quá ngày kết thúc học kỳ.");
                }

                var roomPrices = await _requestRepo.GetPublishedRoomPricesAsync(req.TermId);
                var oldDormId = bt.FromBed?.Room?.Block?.DormId ?? 0;
                var oldRoomTypeId = bt.FromBed?.Room?.RoomTypeId ?? 0;
                var newDormId = bt.ToBed?.Room?.Block?.DormId ?? 0;
                var newRoomTypeId = bt.ToBed?.Room?.RoomTypeId ?? 0;

                roomPrices.TryGetValue((oldDormId, oldRoomTypeId), out var oldRoomPrice);
                roomPrices.TryGetValue((newDormId, newRoomTypeId), out var newRoomPrice);

                var totalTermDays = Math.Max(1, term.EndDate.DayNumber - term.StartDate.DayNumber);
                var stayStart = bt.FromResidency?.CheckInDate is DateOnly ci && ci > term.StartDate
                    ? ci
                    : term.StartDate;
                var clampedTransferDate = transferDate < stayStart ? stayStart : transferDate;

                var daysStayed = Math.Clamp(clampedTransferDate.DayNumber - stayStart.DayNumber, 0, totalTermDays);
                var daysRemaining = Math.Max(0, term.EndDate.DayNumber - clampedTransferDate.DayNumber);
                var oldDailyRate = Math.Round(oldRoomPrice / totalTermDays, 2);
                var newDailyRate = Math.Round(newRoomPrice / totalTermDays, 2);

                var paidFromInvoices = await _requestRepo.GetStudentPaidRoomAmountAsync(req.StudentId, req.TermId);
                var amountAlreadyPaid = paidFromInvoices > 0m ? paidFromInvoices : oldRoomPrice;

                bt.TransferDate = transferDate;
                bt.AssignedGuardId = guard.UserId;
                bt.ReviewedById = managerId;
                bt.ReviewedTime = now;
                bt.OldBedDailyRate = oldDailyRate;
                bt.NewBedDailyRate = newDailyRate;
                bt.DaysStayed = daysStayed;
                bt.DaysRemaining = daysRemaining;
                bt.AmountAlreadyPaid = amountAlreadyPaid;

                req.Status = "Approved";
                req.ProcessedById = managerId;
                req.ProcessedTime = now;

                var log = new RequestLog
                {
                    RequestId = req.Id,
                    FromStatus = "Pending",
                    ToStatus = "Approved",
                    Note = $"Quản lý duyệt Bước 1, chốt ngày chuyển {transferDate:dd/MM/yyyy} và phân công Bảo vệ {guard.User?.FullName} kiểm tra phòng cũ."
                           + (string.IsNullOrWhiteSpace(dto.Note) ? string.Empty : $" Ghi chú: {dto.Note.Trim()}"),
                    ChangeTime = now,
                    ChangedById = managerId
                };

                var guardDisplayName = guard.User?.FullName ?? $"#{guard.UserId}";
                var notifications = new List<Notification>
                {
                    new Notification
                    {
                        UserId = req.StudentId,
                        Channel = "InApp",
                        Title = $"Đơn chuyển giường {requestCode} đã được chấp thuận Bước 1",
                        Content = $"Ban Quản lý đã duyệt đơn chuyển sang giường {bt.ToBed?.BedCode} (ngày {transferDate:dd/MM/yyyy}). Bảo vệ {guardDisplayName} sẽ kiểm tra thiết bị phòng cũ và chốt chỉ số điện nước cùng bạn.",
                        ReferenceType = "Request",
                        ReferenceId = req.Id.ToString(),
                        ReadFlag = false,
                        SentTime = now
                    },
                    new Notification
                    {
                        UserId = guard.UserId,
                        Channel = "InApp",
                        Title = $"Nhiệm vụ kiểm tra chuyển giường mới ({requestCode})",
                        Content = $"Bạn được phân công kiểm tra phòng {bt.FromBed?.Room?.RoomNumber} - Giường {bt.FromBed?.BedCode} cho sinh viên {req.Student?.User?.FullName} (ngày dự kiến {transferDate:dd/MM/yyyy}).",
                        ReferenceType = "Request",
                        ReferenceId = req.Id.ToString(),
                        ReadFlag = false,
                        SentTime = now
                    }
                };

                await _requestRepo.ApproveBedTransferStep1Async(req, bt, log, notifications);

                await TrySendEmailAsync(
                    req.Student?.User?.Email,
                    $"[KTX FUD] Chấp thuận Bước 1 đơn chuyển giường {requestCode}",
                    $@"<div style='font-family: Arial, sans-serif; line-height: 1.6; color: #333;'>
                        <h2 style='color: #2563eb;'>Đơn chuyển giường đã được chấp thuận Bước 1</h2>
                        <p>Xin chào <strong>{req.Student?.User?.FullName}</strong>,</p>
                        <p>Ban Quản lý KTX đã chấp thuận yêu cầu chuyển giường <strong>{requestCode}</strong> và phân công cán bộ Bảo vệ phối hợp kiểm tra bàn giao phòng cũ.</p>
                        <div style='background: #eff6ff; border-left: 4px solid #2563eb; padding: 12px; margin: 16px 0;'>
                            <p><strong>Giường cũ:</strong> {bt.FromBed?.BedCode} - P.{bt.FromBed?.Room?.RoomNumber}</p>
                            <p><strong>Giường mới:</strong> {bt.ToBed?.BedCode} - P.{bt.ToBed?.Room?.RoomNumber}</p>
                            <p><strong>Ngày chuyển dự kiến:</strong> {transferDate:dd/MM/yyyy}</p>
                            <p><strong>Bảo vệ phụ trách:</strong> {guardDisplayName} ({guard.User?.Phone ?? "N/A"})</p>
                        </div>
                        <p>Vui lòng phối hợp cùng Bảo vệ và bạn cùng phòng để kiểm tra thiết bị, chốt điện nước phòng cũ trước khi Quản lý phát hành hóa đơn và cập nhật giường mới.</p>
                    </div>");

                var reloadedApprove = await _requestRepo.GetRequestByIdAsync(req.Id);
                if (reloadedApprove == null)
                {
                    bt.AssignedGuard = guard;
                    reloadedApprove = req;
                }
                return MapToDto(reloadedApprove);
            }

            // Decision == "Completed" (Manager Bước 2: Phát hành hóa đơn & cập nhật Residency/Bed)
            if (req.Status != "Approved")
            {
                throw new InvalidOperationException("Đơn chuyển giường phải ở trạng thái Đã duyệt Bước 1 (Approved) trước khi hoàn tất chuyển giường.");
            }

            if (req.Handover == null)
            {
                throw new InvalidOperationException("Chưa có biên bản kiểm tra phòng cũ & chốt điện nước từ Bảo vệ. Không thể hoàn tất chuyển giường.");
            }

            var detail = BuildBedTransferDetailDto(req);
            var positiveProrate = detail.PriceDifference.HasValue && detail.PriceDifference.Value > 0m
                ? Math.Round(detail.PriceDifference.Value, 2)
                : 0m;
            var refundDue = detail.PriceDifference.HasValue && detail.PriceDifference.Value < 0m
                ? Math.Round(Math.Abs(detail.PriceDifference.Value), 2)
                : 0m;
            var utilityShareTotal = Math.Round(detail.UtilityShareAmount ?? 0m, 2);
            var compensationTotal = Math.Round(req.Handover.TotalCompensation, 2);

            var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
            var dueDate = today.AddDays(7);
            var suffix = now.Ticks.ToString()[^6..];
            var invoices = new List<Invoice>();

            if (positiveProrate > 0m || utilityShareTotal > 0m)
            {
                var transferInvoice = new Invoice
                {
                    InvoiceNumber = $"INV-TRF-{req.Id}-{suffix}",
                    StudentId = req.StudentId,
                    TermId = req.TermId,
                    InvoiceType = positiveProrate > 0m ? "BedTransfer" : "Utility",
                    TransferRequestId = req.Id,
                    Amount = Math.Round(positiveProrate + utilityShareTotal, 2),
                    Status = "Unpaid",
                    IssuedTime = now,
                    DueDate = dueDate
                };

                if (positiveProrate > 0m)
                {
                    transferInvoice.InvoiceItems.Add(new InvoiceItem
                    {
                        Description = $"Chênh lệch phí giường chuyển giữa kỳ ({bt.FromBed?.BedCode} → {bt.ToBed?.BedCode}, {detail.DaysRemaining ?? 0} ngày còn lại)",
                        Quantity = 1m,
                        UnitPrice = positiveProrate,
                        Amount = positiveProrate
                    });
                }

                foreach (var mr in detail.MeterReadings.Where(m => m.StudentShareCost > 0m))
                {
                    var label = mr.MeterType == "Electricity" ? "Điện" : "Nước";
                    var unit = mr.MeterType == "Electricity" ? "kWh" : "m³";
                    var share = Math.Round(mr.StudentShareCost, 2);
                    transferInvoice.InvoiceItems.Add(new InvoiceItem
                    {
                        Description = $"Tiền {label} chốt khi rời P.{bt.FromBed?.Room?.RoomNumber} ({mr.Consumption:0.##} {unit} / {mr.ResidentCount} SV)",
                        Quantity = 1m,
                        UnitPrice = share,
                        Amount = share
                    });
                }

                if (transferInvoice.InvoiceItems.Count > 0)
                {
                    transferInvoice.Amount = transferInvoice.InvoiceItems.Sum(i => i.Amount);
                    invoices.Add(transferInvoice);
                }
            }

            if (compensationTotal > 0m)
            {
                var compInvoice = new Invoice
                {
                    InvoiceNumber = $"INV-CMP-{req.Id}-{suffix}",
                    StudentId = req.StudentId,
                    TermId = req.TermId,
                    InvoiceType = "Compensation",
                    HandoverId = req.Handover.Id,
                    Amount = compensationTotal,
                    Status = "Unpaid",
                    IssuedTime = now,
                    DueDate = dueDate
                };

                foreach (var item in req.Handover.HandoverItems.Where(i => i.CompensationAmount > 0m))
                {
                    var equipName = item.Equipment?.Catalog?.Name ?? item.Equipment?.AssetCode ?? $"Thiết bị #{item.EquipmentId}";
                    var itemAmount = Math.Round(item.CompensationAmount, 2);
                    compInvoice.InvoiceItems.Add(new InvoiceItem
                    {
                        Description = $"Bồi thường hư hỏng/mất thiết bị: {equipName} ({item.ItemCondition})",
                        Quantity = 1m,
                        UnitPrice = itemAmount,
                        Amount = itemAmount
                    });
                }

                if (compInvoice.InvoiceItems.Count > 0)
                {
                    compInvoice.Amount = compInvoice.InvoiceItems.Sum(i => i.Amount);
                    invoices.Add(compInvoice);
                }
            }

            var transferTerm = bt.FromResidency?.Term ?? req.Term;
            var actualTransferDate = bt.TransferDate ?? today;
            var termEnd = transferTerm?.EndDate ?? actualTransferDate;
            var newCheckInDate = actualTransferDate > termEnd ? termEnd : actualTransferDate;

            var newResidency = new Residency
            {
                StudentId = req.StudentId,
                BedId = bt.ToBedId,
                TermId = req.TermId,
                RegistrationType = "Transfer",
                Status = "Active",
                CheckInDate = newCheckInDate,
                PlannedCheckOutDate = termEnd
            };

            req.Status = "Completed";
            req.ProcessedById = managerId;
            req.ProcessedTime = now;

            var totalBilled = invoices.Sum(i => i.Amount);
            var invoiceCodes = invoices.Count > 0
                ? string.Join(", ", invoices.Select(i => $"{i.InvoiceNumber} ({i.Amount:N0}đ)"))
                : "Không phát sinh công nợ";

            var completeLog = new RequestLog
            {
                RequestId = req.Id,
                FromStatus = "Approved",
                ToStatus = "Completed",
                Note = $"Quản lý hoàn tất chuyển giường ({bt.FromBed?.BedCode} → {bt.ToBed?.BedCode}). Hóa đơn phát hành: {invoiceCodes}."
                       + (refundDue > 0m ? $" Ghi nhận chênh lệch hoàn trả dự kiến: {refundDue:N0}đ." : string.Empty)
                       + (string.IsNullOrWhiteSpace(dto.Note) ? string.Empty : $" Ghi chú: {dto.Note.Trim()}"),
                ChangeTime = now,
                ChangedById = managerId
            };

            var completeNotification = new Notification
            {
                UserId = req.StudentId,
                Channel = "InApp",
                Title = $"Hoàn tất chuyển giường ({requestCode}) - Giường mới {bt.ToBed?.BedCode}",
                Content = $"Yêu cầu chuyển giường sang P.{bt.ToBed?.Room?.RoomNumber} - Giường {bt.ToBed?.BedCode} đã hoàn tất."
                          + (totalBilled > 0m ? $" Hóa đơn công nợ đã phát hành: {invoiceCodes} (hạn thanh toán {dueDate:dd/MM/yyyy})." : " Không có khoản công nợ phát sinh.")
                          + (refundDue > 0m ? $" Số tiền chênh lệch giảm phòng ({refundDue:N0}đ) sẽ được Ban Quản lý đối soát hoàn/cấn trừ." : string.Empty),
                ReferenceType = "Request",
                ReferenceId = req.Id.ToString(),
                ReadFlag = false,
                SentTime = now
            };

            await _requestRepo.CompleteBedTransferAsync(
                req,
                bt,
                bt.FromResidencyId,
                bt.FromBedId,
                bt.ToBedId,
                actualTransferDate,
                newResidency,
                invoices,
                req.Handover.Id,
                managerId,
                string.IsNullOrWhiteSpace(dto.Note) ? null : dto.Note.Trim(),
                completeLog,
                completeNotification);

            await TrySendEmailAsync(
                req.Student?.User?.Email,
                $"[KTX FUD] Hoàn tất chuyển giường {requestCode} - Nhận giường mới {bt.ToBed?.BedCode}",
                $@"<div style='font-family: Arial, sans-serif; line-height: 1.6; color: #333;'>
                    <h2 style='color: #16a34a;'>Hoàn tất chuyển giường giữa kỳ</h2>
                    <p>Xin chào <strong>{req.Student?.User?.FullName}</strong>,</p>
                    <p>Ban Quản lý Ký túc xá xác nhận đã hoàn tất thủ tục chuyển giường <strong>{requestCode}</strong> cho bạn.</p>
                    <div style='background: #f0fdf4; border-left: 4px solid #16a34a; padding: 12px; margin: 16px 0;'>
                        <p><strong>Giường cũ (đã trả):</strong> {bt.FromBed?.BedCode} - P.{bt.FromBed?.Room?.RoomNumber} ({bt.FromBed?.Room?.Block?.Dorm?.Name})</p>
                        <p><strong>Giường mới (đang lưu trú):</strong> {bt.ToBed?.BedCode} - P.{bt.ToBed?.Room?.RoomNumber} ({bt.ToBed?.Room?.Block?.Dorm?.Name})</p>
                        <p><strong>Ngày hiệu lực:</strong> {newCheckInDate:dd/MM/yyyy}</p>
                        <p><strong>Hóa đơn công nợ:</strong> {invoiceCodes}</p>
                    </div>
                    <p>Vui lòng kiểm tra mục Hóa đơn / Yêu cầu trên hệ thống F.U.D để hoàn tất thanh toán đúng hạn.</p>
                </div>");

            var reloadedComplete = await _requestRepo.GetRequestByIdAsync(req.Id) ?? req;
            return MapToDto(reloadedComplete);
        }

        private static BedTransferDetailDto BuildBedTransferDetailDto(Request req)
        {
            var bt = req.BedTransferRequest!;
            var oldBedAmount = bt.OldBedAmount ?? (bt.OldBedDailyRate.HasValue && bt.DaysStayed.HasValue
                ? Math.Round(bt.OldBedDailyRate.Value * bt.DaysStayed.Value, 2)
                : (decimal?)null);
            var newBedAmount = bt.NewBedAmount ?? (bt.NewBedDailyRate.HasValue && bt.DaysRemaining.HasValue
                ? Math.Round(bt.NewBedDailyRate.Value * bt.DaysRemaining.Value, 2)
                : (decimal?)null);
            var totalAmount = bt.TotalAmount ?? (oldBedAmount.HasValue && newBedAmount.HasValue
                ? Math.Round(oldBedAmount.Value + newBedAmount.Value, 2)
                : (decimal?)null);
            var priceDifference = bt.PriceDifference ?? (totalAmount.HasValue && bt.AmountAlreadyPaid.HasValue
                ? Math.Round(totalAmount.Value - bt.AmountAlreadyPaid.Value, 2)
                : (decimal?)null);

            var utilityPrices = req.Term?.UtilityPrices?
                .GroupBy(u => u.MeterType)
                .ToDictionary(g => g.Key, g => g.First().UnitPrice)
                ?? new Dictionary<string, decimal>();

            var activeResidentCount = CountRoomTransferResidents(req);
            var meterReadings = BuildMeterReadingDtos(bt.MeterReadings, utilityPrices, activeResidentCount);
            var handoverDto = BuildHandoverInfoDto(req.Handover);

            var utilityShare = meterReadings.Count > 0 ? Math.Round(meterReadings.Sum(m => m.StudentShareCost), 2) : (decimal?)null;
            var equipmentComp = handoverDto != null ? Math.Round(handoverDto.TotalCompensation, 2) : (decimal?)null;
            var positiveProrate = priceDifference.HasValue && priceDifference.Value > 0m ? priceDifference.Value : 0m;
            var refundDue = priceDifference.HasValue && priceDifference.Value < 0m ? Math.Round(Math.Abs(priceDifference.Value), 2) : 0m;
            var totalOutstanding = priceDifference.HasValue || utilityShare.HasValue || equipmentComp.HasValue
                ? Math.Round(positiveProrate + (utilityShare ?? 0m) + (equipmentComp ?? 0m), 2)
                : (decimal?)null;

            var combinedInvoices = (bt.Invoices ?? Enumerable.Empty<Invoice>())
                .Concat(req.Handover?.Invoices ?? Enumerable.Empty<Invoice>())
                .GroupBy(i => i.Id)
                .Select(g => g.First())
                .OrderBy(i => i.Id)
                .Select(i => new BedTransferInvoiceSummaryDto
                {
                    Id = i.Id,
                    InvoiceNumber = i.InvoiceNumber,
                    InvoiceType = i.InvoiceType,
                    Status = i.Status,
                    TotalAmount = i.Amount,
                    IssueDate = DateOnly.FromDateTime(i.IssuedTime),
                    DueDate = i.DueDate,
                    Description = i.InvoiceType == "Compensation"
                        ? "Hóa đơn bồi thường thiết bị chuyển giường"
                        : i.InvoiceType == "Utility"
                            ? "Hóa đơn điện nước chốt chuyển giường"
                            : "Hóa đơn công nợ chuyển giường giữa kỳ"
                })
                .ToList();

            return new BedTransferDetailDto
            {
                FromResidencyId = bt.FromResidencyId,
                FromBedId = bt.FromBedId,
                FromRoomId = bt.FromBed?.RoomId ?? 0,
                FromDormId = bt.FromBed?.Room?.Block?.DormId ?? 0,
                FromBedCode = bt.FromBed?.BedCode ?? string.Empty,
                FromRoomNumber = bt.FromBed?.Room?.RoomNumber ?? string.Empty,
                FromBlockCode = bt.FromBed?.Room?.Block?.Code ?? string.Empty,
                FromBlockName = bt.FromBed?.Room?.Block?.Name ?? string.Empty,
                FromDormName = bt.FromBed?.Room?.Block?.Dorm?.Name ?? string.Empty,
                ToBedId = bt.ToBedId,
                ToBedCode = bt.ToBed?.BedCode ?? string.Empty,
                ToRoomNumber = bt.ToBed?.Room?.RoomNumber ?? string.Empty,
                ToBlockCode = bt.ToBed?.Room?.Block?.Code ?? string.Empty,
                ToBlockName = bt.ToBed?.Room?.Block?.Name ?? string.Empty,
                ToDormName = bt.ToBed?.Room?.Block?.Dorm?.Name ?? string.Empty,
                ToRoomTypeName = bt.ToBed?.Room?.RoomType?.Name ?? string.Empty,
                TransferReason = bt.TransferReason,
                TransferDate = bt.TransferDate,
                OldBedDailyRate = bt.OldBedDailyRate,
                NewBedDailyRate = bt.NewBedDailyRate,
                DaysStayed = bt.DaysStayed,
                DaysRemaining = bt.DaysRemaining,
                OldBedAmount = oldBedAmount,
                NewBedAmount = newBedAmount,
                AmountAlreadyPaid = bt.AmountAlreadyPaid,
                TotalAmount = totalAmount,
                PriceDifference = priceDifference,
                AssignedGuardId = bt.AssignedGuardId,
                AssignedGuardName = bt.AssignedGuard?.User?.FullName,
                AssignedGuardCode = bt.AssignedGuard?.User?.Code,
                ReviewedById = bt.ReviewedById,
                ReviewedTime = bt.ReviewedTime,
                NewResidencyId = bt.NewResidencyId,
                Handover = handoverDto,
                MeterReadings = meterReadings,
                UtilityShareAmount = utilityShare,
                EquipmentCompensationAmount = equipmentComp,
                TotalOutstandingAmount = totalOutstanding,
                RefundDueAmount = refundDue > 0m ? refundDue : null,
                Invoices = combinedInvoices
            };
        }

        private static TransferInspectionTaskDto MapToGuardTaskDto(Request req, Dictionary<string, decimal>? prevReadings)
        {
            var bt = req.BedTransferRequest!;
            var utilityPrices = req.Term?.UtilityPrices?
                .GroupBy(u => u.MeterType)
                .ToDictionary(g => g.Key, g => g.First().UnitPrice)
                ?? new Dictionary<string, decimal>();
            var residentCount = CountRoomTransferResidents(req);
            var meterReadings = BuildMeterReadingDtos(bt.MeterReadings, utilityPrices, residentCount);
            var elecMr = meterReadings.FirstOrDefault(m => m.MeterType == "Electricity");
            var waterMr = meterReadings.FirstOrDefault(m => m.MeterType == "Water");

            return new TransferInspectionTaskDto
            {
                RequestId = req.Id,
                RequestCode = $"REQ-{req.Id:D6}",
                Status = req.Status,
                StudentId = req.StudentId,
                StudentName = req.Student?.User?.FullName ?? string.Empty,
                StudentCode = req.Student?.User?.Code ?? string.Empty,
                StudentPhone = req.Student?.User?.Phone,
                FromResidencyId = bt.FromResidencyId,
                FromDormName = bt.FromBed?.Room?.Block?.Dorm?.Name ?? string.Empty,
                FromBlockName = bt.FromBed?.Room?.Block?.Name ?? string.Empty,
                FromRoomId = bt.FromBed?.RoomId ?? 0,
                FromRoomNumber = bt.FromBed?.Room?.RoomNumber ?? string.Empty,
                FromBedId = bt.FromBedId,
                FromBedCode = bt.FromBed?.BedCode ?? string.Empty,
                ToDormName = bt.ToBed?.Room?.Block?.Dorm?.Name ?? string.Empty,
                ToBlockName = bt.ToBed?.Room?.Block?.Name ?? string.Empty,
                ToRoomNumber = bt.ToBed?.Room?.RoomNumber ?? string.Empty,
                ToBedCode = bt.ToBed?.BedCode ?? string.Empty,
                TransferDate = bt.TransferDate,
                TransferReason = bt.TransferReason,
                AssignedGuardId = bt.AssignedGuardId,
                AssignedGuardName = bt.AssignedGuard?.User?.FullName,
                PreviousElectricityReading = elecMr?.PreviousValue ?? (prevReadings?.GetValueOrDefault("Electricity", 0m) ?? 0m),
                PreviousWaterReading = waterMr?.PreviousValue ?? (prevReadings?.GetValueOrDefault("Water", 0m) ?? 0m),
                ElectricityUnitPrice = utilityPrices.GetValueOrDefault("Electricity", 0m),
                WaterUnitPrice = utilityPrices.GetValueOrDefault("Water", 0m),
                ActiveResidentCount = residentCount,
                HandoverCompleted = req.Handover != null,
                Handover = BuildHandoverInfoDto(req.Handover),
                MeterReadings = meterReadings
            };
        }

        private static int CountRoomTransferResidents(Request req)
        {
            var bt = req.BedTransferRequest;
            if (bt?.FromBed?.Room?.Beds == null || bt.FromBed.Room.Beds.Count == 0)
                return 1;

            var count = bt.FromBed.Room.Beds
                .SelectMany(b => b.Residencies)
                .Count(r => r.TermId == req.TermId && (r.Status == "Active" || r.Id == bt.FromResidencyId));

            return count > 0 ? count : 1;
        }

        private static List<BedTransferMeterReadingDto> BuildMeterReadingDtos(
            IEnumerable<MeterReading>? readings,
            Dictionary<string, decimal> utilityPrices,
            int residentCount)
        {
            if (readings == null)
                return new List<BedTransferMeterReadingDto>();

            return readings
                .OrderBy(m => m.MeterType)
                .Select(m =>
                {
                    var consumption = m.Consumption ?? Math.Max(0m, m.CurrentValue - m.PreviousValue);
                    var unitPrice = utilityPrices.GetValueOrDefault(m.MeterType, 0m);
                    var totalCost = Math.Round(consumption * unitPrice, 2);
                    var shareCost = Math.Round(totalCost / Math.Max(1, residentCount), 2);
                    return new BedTransferMeterReadingDto
                    {
                        Id = m.Id,
                        MeterType = m.MeterType,
                        PreviousValue = m.PreviousValue,
                        CurrentValue = m.CurrentValue,
                        Consumption = consumption,
                        UnitPrice = unitPrice,
                        TotalCost = totalCost,
                        StudentShareCost = shareCost,
                        ResidentCount = Math.Max(1, residentCount),
                        RecordedTime = m.RecordedTime,
                        Note = m.Note,
                        EvidenceImageUrl = m.EvidenceImageUrl
                    };
                })
                .ToList();
        }

        private static BedTransferHandoverInfoDto? BuildHandoverInfoDto(Handover? h)
        {
            if (h == null)
                return null;

            return new BedTransferHandoverInfoDto
            {
                Id = h.Id,
                HandoverCode = $"BBN-{h.Id:D5}",
                Status = h.Status,
                InspectorName = h.Guard?.User?.FullName ?? string.Empty,
                InspectionNote = h.BelongingsNote ?? h.ManagerNote,
                TotalCompensation = h.TotalCompensation,
                StudentAcceptedFlag = h.StudentAcceptedFlag,
                StudentAcceptedTime = h.StudentAcceptedTime,
                CompletedTime = h.CompletedTime,
                Items = (h.HandoverItems ?? Enumerable.Empty<HandoverItem>())
                    .OrderBy(i => i.Id)
                    .Select(i => new BedTransferHandoverItemDto
                    {
                        Id = i.Id,
                        EquipmentId = i.EquipmentId,
                        EquipmentCode = i.Equipment?.AssetCode ?? string.Empty,
                        EquipmentName = i.Equipment?.Catalog?.Name ?? string.Empty,
                        Category = i.Equipment?.Catalog?.Category ?? string.Empty,
                        ItemCondition = i.ItemCondition,
                        Description = i.Note,
                        CompensationAmount = i.CompensationAmount
                    })
                    .ToList()
            };
        }

        private async Task TrySendEmailAsync(string? email, string subject, string htmlBody)
        {
            if (string.IsNullOrWhiteSpace(email))
                return;

            try
            {
                await _emailSender.SendEmailAsync(email, subject, htmlBody);
            }
            catch
            {
                // Ignore email sending error so completed DB workflow is not rolled back
            }
        }
    }
}
