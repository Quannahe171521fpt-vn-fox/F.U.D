using FUD.Common;
using FUD.DTOs.Facilities.Room;
using FUD.Models;
using FUD.Repositories.Interfaces.Facilities;
using FUD.Services.Interfaces.Facilities;

namespace FUD.Services.Facilities
{
    public class RoomService : IRoomService
    {
        private readonly IRoomRepository _roomRepo;

        public RoomService(IRoomRepository roomRepo)
        {
            _roomRepo = roomRepo;
        }

        public async Task<PagedResult<RoomDto>> GetAllAsync(RoomQueryDto query)
        {
            var (items, total) = await _roomRepo.GetAllAsync(query);
            return new PagedResult<RoomDto>
            {
                Items = items.Select(MapToDto),
                TotalCount = total,
                Page = query.Page,
                PageSize = query.PageSize
            };
        }

        public async Task<PagedResult<RoomDto>> GetAllForGuardAsync(RoomQueryDto query, int guardUserId)
        {
            var guardDormId = await ResolveGuardDormIdOrThrowAsync(guardUserId);
            var scopedQuery = new RoomQueryDto
            {
                DormId = guardDormId,
                BlockId = query.BlockId,
                BlockName = query.BlockName,
                RoomNumber = query.RoomNumber,
                ExactRoomNumber = query.ExactRoomNumber,
                FloorLevel = query.FloorLevel,
                RoomTypeId = query.RoomTypeId,
                GenderType = query.GenderType,
                Status = query.Status,
                Page = query.Page,
                PageSize = query.PageSize
            };

            return await GetAllAsync(scopedQuery);
        }

        public async Task<RoomDto?> GetByIdAsync(int id)
        {
            var room = await _roomRepo.GetByIdAsync(id);
            return room == null ? null : MapToDto(room);
        }

        public async Task<RoomDto?> GetByIdForGuardAsync(int id, int guardUserId)
        {
            var guardDormId = await ResolveGuardDormIdOrThrowAsync(guardUserId);
            var room = await _roomRepo.GetByIdAsync(id);
            if (room == null)
                return null;

            if (room.Block?.DormId != guardDormId)
                throw new UnauthorizedAccessException("Bạn chỉ được xem thông tin phòng thuộc Block đang được phân công quản lý.");

            return MapToDto(room);
        }

        public async Task<RoomDetailDto?> GetDetailByIdAsync(int id, int? guardUserId = null)
        {
            int? guardDormId = null;
            if (guardUserId.HasValue)
            {
                guardDormId = await ResolveGuardDormIdOrThrowAsync(guardUserId.Value);
            }

            var room = await _roomRepo.GetRoomDetailWithResidentsAsync(id);
            if (room == null)
                return null;

            if (guardDormId.HasValue && room.Block?.DormId != guardDormId.Value)
            {
                throw new UnauthorizedAccessException("Bạn chỉ được xem chi tiết phòng thuộc Block đang được phân công quản lý.");
            }

            var orderedBeds = (room.Beds ?? Enumerable.Empty<Bed>())
                .OrderBy(b => b.BedCode)
                .ToList();

            var checkedInStudents = new List<RoomCheckedInStudentDto>();
            var bedDtos = new List<RoomBedOccupancyDto>();

            foreach (var bed in orderedBeds)
            {
                var checkedInResidency = (bed.Residencies ?? Enumerable.Empty<Residency>())
                    .Where(res => IsCheckedInResidency(res, bed))
                    .OrderByDescending(res => res.CheckInDate ?? DateOnly.MinValue)
                    .ThenByDescending(res => res.Id)
                    .FirstOrDefault();

                if (checkedInResidency != null && checkedInResidency.Student?.User != null)
                {
                    var studentUser = checkedInResidency.Student.User;
                    var term = checkedInResidency.Term;
                    checkedInStudents.Add(new RoomCheckedInStudentDto
                    {
                        ResidencyId = checkedInResidency.Id,
                        StudentId = checkedInResidency.StudentId,
                        StudentCode = studentUser.Code ?? string.Empty,
                        FullName = studentUser.FullName ?? string.Empty,
                        Gender = studentUser.Gender,
                        Email = studentUser.Email,
                        Phone = studentUser.Phone,
                        Major = checkedInResidency.Student.Major,
                        Cohort = checkedInResidency.Student.Cohort,
                        BedId = bed.Id,
                        BedCode = bed.BedCode,
                        PositionInRoom = bed.PositionInRoom,
                        TermId = checkedInResidency.TermId,
                        TermName = term != null ? $"{term.Name} ({term.AcademicYear})" : string.Empty,
                        RegistrationType = checkedInResidency.RegistrationType,
                        CheckInDate = checkedInResidency.CheckInDate,
                        PlannedCheckOutDate = checkedInResidency.PlannedCheckOutDate,
                        HandoverId = checkedInResidency.Handover?.Id,
                        HandoverCompletedTime = checkedInResidency.Handover?.CompletedTime ?? checkedInResidency.Handover?.CreatedTime
                    });
                }

                var hasCheckedInResident = checkedInResidency != null;
                var effectiveBedStatus = GetEffectiveCurrentBedStatus(bed, hasCheckedInResident);

                bedDtos.Add(new RoomBedOccupancyDto
                {
                    BedId = bed.Id,
                    BedCode = bed.BedCode,
                    PositionInRoom = bed.PositionInRoom,
                    Status = effectiveBedStatus,
                    CheckedInStudentCode = checkedInResidency?.Student?.User?.Code,
                    CheckedInStudentName = checkedInResidency?.Student?.User?.FullName
                });
            }

            return new RoomDetailDto
            {
                Id = room.Id,
                DormId = room.Block?.DormId ?? 0,
                DormName = room.Block?.Dorm?.Name ?? string.Empty,
                BlockId = room.BlockId,
                BlockCode = room.Block?.Code ?? string.Empty,
                BlockName = room.Block?.Name ?? string.Empty,
                BlockGenderType = room.Block?.GenderType ?? string.Empty,
                RoomTypeId = room.RoomTypeId,
                RoomTypeName = room.RoomType?.Name ?? string.Empty,
                BedCapacity = room.RoomType?.BedCapacity ?? 0,
                RoomNumber = room.RoomNumber,
                FloorLevel = room.FloorLevel,
                Status = room.Status,
                TotalBeds = bedDtos.Count,
                FreeBeds = bedDtos.Count(b => b.Status == "Free"),
                OccupiedBeds = bedDtos.Count(b => b.Status == "Occupied" || b.Status == "Holding" || b.Status == "Pending Transfer"),
                CheckedInCount = checkedInStudents.Count,
                CheckedInStudents = checkedInStudents,
                Beds = bedDtos
            };
        }

        public async Task<IEnumerable<GuardAssignedBlockDto>> GetAssignedBlocksLookupAsync(int? guardUserId = null)
        {
            int? dormId = null;
            if (guardUserId.HasValue)
            {
                dormId = await ResolveGuardDormIdOrThrowAsync(guardUserId.Value);
            }

            var blocks = await _roomRepo.GetBlocksWithRoomsAsync(dormId);
            return blocks.Select(b => new GuardAssignedBlockDto
            {
                Id = b.Id,
                DormId = b.DormId,
                DormName = b.Dorm?.Name ?? string.Empty,
                Code = b.Code,
                Name = b.Name,
                GenderType = b.GenderType,
                NumberOfFloors = b.FloorLevel,
                Status = b.Status,
                Rooms = (b.Rooms ?? Enumerable.Empty<Room>())
                    .OrderBy(r => r.FloorLevel)
                    .ThenBy(r => r.RoomNumber)
                    .Select(r => new GuardRoomLookupItemDto
                    {
                        Id = r.Id,
                        RoomNumber = r.RoomNumber,
                        FloorLevel = r.FloorLevel
                    })
                    .ToList()
            }).ToList();
        }

        private async Task<int> ResolveGuardDormIdOrThrowAsync(int guardUserId)
        {
            var dormId = await _roomRepo.GetGuardDormIdAsync(guardUserId);
            if (!dormId.HasValue || dormId.Value <= 0)
            {
                throw new UnauthorizedAccessException("Tài khoản bảo vệ chưa được phân công khu KTX / Block quản lý.");
            }
            return dormId.Value;
        }

        private static string GetEffectiveCurrentBedStatus(Bed bed, bool hasCheckedInResident)
        {
            if (hasCheckedInResident)
            {
                return string.Equals(bed.Status, "Pending Transfer", StringComparison.OrdinalIgnoreCase)
                    ? "Pending Transfer"
                    : "Occupied";
            }

            return bed.Status;
        }

        private static bool IsCheckedInResidency(Residency res, Bed? bed = null)
        {
            if (!string.Equals(res.Status, "Active", StringComparison.OrdinalIgnoreCase))
                return false;

            if (res.Handover != null)
            {
                return string.Equals(res.Handover.HandoverType, "Checkin", StringComparison.OrdinalIgnoreCase)
                    && string.Equals(res.Handover.Status, "Completed", StringComparison.OrdinalIgnoreCase);
            }

            var effectiveBed = bed ?? res.Bed;
            var bedStatus = effectiveBed?.Status;
            return res.CheckInDate.HasValue
                && !string.Equals(bedStatus, "Holding", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(bedStatus, "Maintenance", StringComparison.OrdinalIgnoreCase);
        }

        public async Task<RoomDto> CreateAsync(CreateRoomDto dto)
        {
            if (!await _roomRepo.IsRoomNumberUniqueAsync(dto.BlockId, dto.RoomNumber))
                throw new InvalidOperationException($"Phòng '{dto.RoomNumber}' đã tồn tại trong block này");

            if (dto.FloorLevel.HasValue)
            {
                var blockFloors = await _roomRepo.GetBlockNumberOfFloorsAsync(dto.BlockId);
                if (blockFloors.HasValue && (dto.FloorLevel.Value < 1 || dto.FloorLevel.Value > blockFloors.Value))
                    throw new InvalidOperationException($"Số tầng ({dto.FloorLevel}) không hợp lệ. Block chỉ có {blockFloors.Value} tầng");

                // Kiểm tra trùng tên phòng trong cùng tầng của block
                if (!await _roomRepo.IsRoomNumberUniqueOnFloorAsync(dto.BlockId, dto.FloorLevel.Value, dto.RoomNumber))
                    throw new InvalidOperationException($"Phòng '{dto.RoomNumber}' đã tồn tại ở tầng {dto.FloorLevel} trong block này");
            }

            var room = new Room
            {
                BlockId = dto.BlockId,
                RoomTypeId = dto.RoomTypeId,
                RoomNumber = dto.RoomNumber,
                FloorLevel = dto.FloorLevel,
                Status = "Active"
            };

            var created = await _roomRepo.CreateAsync(room);
            var result = await _roomRepo.GetByIdAsync(created.Id);
            return MapToDto(result!);
        }

        public async Task<RoomDto> UpdateAsync(int id, UpdateRoomDto dto)
        {
            var room = await _roomRepo.GetByIdAsync(id)
                ?? throw new KeyNotFoundException($"Phòng {id} không tồn tại");

            // Không cho sửa khi phòng đang có sinh viên ở (Occupied / Holding / Pending Transfer)
            if (await _roomRepo.HasActiveBedsAsync(id))
                throw new InvalidOperationException("Không thể chỉnh sửa phòng khi đang có sinh viên cư trú. Vui lòng chuyển sinh viên trước.");

            if (!await _roomRepo.IsRoomNumberUniqueAsync(room.BlockId, dto.RoomNumber, id))
                throw new InvalidOperationException($"Phòng '{dto.RoomNumber}' đã tồn tại trong block này");

            if (dto.FloorLevel.HasValue)
            {
                var blockFloors = room.Block?.NumberOfFloors ?? await _roomRepo.GetBlockNumberOfFloorsAsync(room.BlockId);
                if (blockFloors.HasValue && (dto.FloorLevel.Value < 1 || dto.FloorLevel.Value > blockFloors.Value))
                    throw new InvalidOperationException($"Số tầng ({dto.FloorLevel}) không hợp lệ. Block chỉ có {blockFloors.Value} tầng");

                // Kiểm tra trùng tên phòng trong cùng tầng của block
                if (!await _roomRepo.IsRoomNumberUniqueOnFloorAsync(room.BlockId, dto.FloorLevel.Value, dto.RoomNumber, id))
                    throw new InvalidOperationException($"Phòng '{dto.RoomNumber}' đã tồn tại ở tầng {dto.FloorLevel} trong block này");
            }

            room.RoomTypeId = dto.RoomTypeId;
            room.RoomNumber = dto.RoomNumber;
            room.FloorLevel = dto.FloorLevel;

            await _roomRepo.UpdateAsync(room);
            var result = await _roomRepo.GetByIdAsync(id);
            return MapToDto(result!);
        }

        public async Task<RoomDto> UpdateStatusAsync(int id, UpdateRoomStatusDto dto)
        {
            var room = await _roomRepo.GetByIdAsync(id)
                ?? throw new KeyNotFoundException($"Phòng {id} không tồn tại");

            room.Status = dto.Status;

            if (dto.Status == "Maintenance")
            {
                var beds = await _roomRepo.GetBedsByRoomIdAsync(id);
                foreach (var bed in beds.Where(b => b.Status == "Free"))
                    bed.Status = "Maintenance";
            }

            await _roomRepo.UpdateAsync(room);
            var result = await _roomRepo.GetByIdAsync(id);
            return MapToDto(result!);
        }

        public async Task<IEnumerable<RoomTypeLookupDto>> GetRoomTypesAsync()
        {
            var types = await _roomRepo.GetRoomTypesAsync();
            return types.Select(t => new RoomTypeLookupDto
            {
                Id = t.Id,
                Name = t.Name,
                BedCapacity = t.BedCapacity
            });
        }

        private static RoomDto MapToDto(Room r)
        {
            var beds = r.Beds ?? Enumerable.Empty<Bed>();
            int totalBeds = 0;
            int freeBeds = 0;
            int occupiedBeds = 0;
            int checkedInCount = 0;

            foreach (var b in beds)
            {
                totalBeds++;
                var checkedInOnBed = (b.Residencies ?? Enumerable.Empty<Residency>())
                    .Count(res => IsCheckedInResidency(res, b));
                checkedInCount += checkedInOnBed;

                var effectiveStatus = GetEffectiveCurrentBedStatus(b, checkedInOnBed > 0);
                if (effectiveStatus == "Free")
                {
                    freeBeds++;
                }
                else if (effectiveStatus == "Occupied" || effectiveStatus == "Holding" || effectiveStatus == "Pending Transfer")
                {
                    occupiedBeds++;
                }
            }

            return new RoomDto
            {
                Id = r.Id,
                DormId = r.Block?.DormId ?? 0,
                DormName = r.Block?.Dorm?.Name ?? string.Empty,
                BlockId = r.BlockId,
                BlockName = r.Block?.Name ?? string.Empty,
                BlockGenderType = r.Block?.GenderType ?? string.Empty,
                RoomTypeId = r.RoomTypeId,
                RoomTypeName = r.RoomType?.Name ?? string.Empty,
                BedCapacity = r.RoomType?.BedCapacity ?? 0,
                RoomNumber = r.RoomNumber,
                FloorLevel = r.FloorLevel,
                Status = r.Status,
                TotalBeds = totalBeds,
                FreeBeds = freeBeds,
                OccupiedBeds = occupiedBeds,
                CheckedInStudentsCount = checkedInCount
            };
        }
    }
}
