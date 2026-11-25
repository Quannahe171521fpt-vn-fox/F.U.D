using System.Collections.Generic;
using System.Threading.Tasks;
using FUD.Models;

namespace FUD.Repositories.Interfaces.Requests
{
    public interface IRequestRepository
    {
        Task<List<Request>> GetAllRequestsAsync(string? requestType = null, string? status = null);
        Task<List<Request>> GetStudentRequestsAsync(int studentId);
        Task<Request?> GetRequestByIdAsync(int id);
        Task<Request> CreateRequestAsync(Request request);
        Task UpdateRequestAsync(Request request);
        Task<ComplaintRequest> AddComplaintRequestAsync(ComplaintRequest complaintRequest, IEnumerable<ComplaintEvidence> evidences);
        Task<Term?> GetCurrentActiveTermAsync();
        Task<User?> GetUserByIdAsync(int userId);
        Task UpdateUserAsync(User user);

        // Bed Transfer
        Task<Residency?> GetStudentActiveResidencyAsync(int studentId);
        Task<bool> HasOverdueInvoicesAsync(int studentId);
        Task<bool> HasPendingBedTransferAsync(int studentId, int termId);
        Task<List<Bed>> GetAvailableBedsForTransferAsync(string studentGender, int currentBedId, int termId);
        Task<Bed?> GetBedWithLocationByIdAsync(int bedId);
        Task<bool> IsBedAvailableForTransferAsync(int bedId, int termId);
        Task<Dictionary<(int DormId, int RoomTypeId), decimal>> GetPublishedRoomPricesAsync(int termId);
        Task<Request> CreateBedTransferRequestAsync(Request request, BedTransferRequest transferRequest, RequestLog log);
        Task<List<Guard>> GetGuardsAsync(int? dormId = null);
        Task<Guard?> GetGuardByUserIdAsync(int guardUserId);
        Task<decimal> GetStudentPaidRoomAmountAsync(int studentId, int termId);
        Task<Dictionary<string, decimal>> GetUtilityPricesByTermAsync(int termId);
        Task<Dictionary<string, decimal>> GetLatestRoomMeterReadingsAsync(int roomId);
        Task<int> GetActiveRoomResidentCountAsync(int roomId, int termId);
        Task<List<Equipment>> GetRoomAndBedEquipmentsAsync(int roomId, int bedId);
        Task ApproveBedTransferStep1Async(Request request, BedTransferRequest transferRequest, RequestLog log, List<Notification> notifications);
        Task RejectBedTransferAsync(Request request, RequestLog log, Notification notification);
        Task<List<Request>> GetTransferInspectionTasksForGuardAsync(int guardUserId);
        Task SubmitTransferInspectionAsync(Request request, Handover handover, List<HandoverItem> items, List<MeterReading> meterReadings, List<(int EquipmentId, string Condition, string? Note)> equipmentUpdates, RequestLog log, List<Notification> notifications);
        Task CompleteBedTransferAsync(Request request, BedTransferRequest transferRequest, int fromResidencyId, int fromBedId, int toBedId, DateOnly checkOutDate, Residency newResidency, List<Invoice> invoices, int? handoverId, int managerId, string? managerNote, RequestLog log, Notification studentNotification, List<Notification>? invoiceNotifications = null);

        // Checkout
        Task<bool> HasPendingCheckoutAsync(int studentId, int termId);
        Task<Request> CreateCheckoutRequestAsync(Request request, CheckoutRequest checkoutRequest, RequestLog log);
        Task ApproveCheckoutStep1Async(Request request, CheckoutRequest checkoutRequest, RequestLog log, List<Notification> notifications);
        Task RejectCheckoutAsync(Request request, RequestLog log, Notification notification);
        Task<List<Request>> GetCheckoutInspectionTasksForGuardAsync(int guardUserId);
        Task SubmitCheckoutInspectionAsync(Request request, Handover handover, List<HandoverItem> items, List<MeterReading> meterReadings, List<(int EquipmentId, string Condition, string? Note)> equipmentUpdates, RequestLog log, List<Notification> notifications);
        Task CompleteCheckoutAsync(Request request, CheckoutRequest checkoutRequest, int residencyId, int bedId, DateOnly actualCheckOutDate, List<Invoice> invoices, int? handoverId, int managerId, string? managerNote, RequestLog log, Notification studentNotification, List<Notification>? invoiceNotifications = null);
    }
}

